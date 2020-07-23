#!/usr/bin/env bash

# Variables
user="steam"
folder="rust"
IP="0.0.0.0"
PUBLIC_IP=$(dig +short myip.opendns.com @resolver1.opendns.com)

# Download latest stop script
curl --silent --output "stop.sh" "https://raw.githubusercontent.com/kus/rust-modded-server/master/stop.sh" && chmod +x stop.sh

# Check distrib
if ! command -v apt-get &> /dev/null; then
	echo "ERROR: OS distribution not supported..."
	exit 1
fi

# Check root
if [ "$EUID" -ne 0 ]; then
	echo "ERROR: Please run this script as root..."
	exit 1
fi

if [ -z "$PUBLIC_IP" ]; then
	echo "ERROR: Cannot retrieve your public IP address..."
	exit 1
fi

echo "Updating Operating System..."
apt update -y -q && apt upgrade -y -q >/dev/null
if [ "$?" -ne "0" ]; then
	echo "ERROR: Updating Operating System..."
	exit 1
fi

echo "Installing required packages..."
apt-get update -y -q >/dev/null
apt-get install -y -q lib32z1 wget bsdtar screen tar unzip nano >/dev/null
if [ "$?" -ne "0" ]; then
	echo "ERROR: Cannot install required packages..."
	exit 1
fi

echo "Checking $user user exists..."
getent passwd ${user} >/dev/null 2&>1
if [ "$?" -ne "0" ]; then
	echo "Adding $user user..."
	addgroup ${user} && \
	adduser --system --home /home/${user} --shell /bin/false --ingroup ${user} ${user} && \
	usermod -a -G tty ${user} && \
	mkdir -m 777 /home/${user}/${folder} && \
	chown -R ${user}:${user} /home/${user}/${folder}
	if [ "$?" -ne "0" ]; then
		echo "ERROR: Cannot add user $user..."
		exit 1
	fi
fi

echo "Checking steamcmd exists..."
if [ ! -d "/steamcmd" ]; then
	mkdir /steamcmd && cd /steamcmd
	wget https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
	tar -xvzf steamcmd_linux.tar.gz
	mkdir -p /root/.steam/sdk32/
	ln -s /steamcmd/linux32/steamclient.so /root/.steam/sdk32/steamclient.so
fi

# Create the necessary folder structure
if [ ! -d "/home/${user}/${folder}" ]; then
	echo "Missing /home/${user}/${folder}, creating.."
	mkdir -p /home/${user}/${folder}
fi
if [ ! -d "/home/${user}/${folder}/server/${SERVER_IDENTITY}" ]; then
	echo "Missing /home/${user}/${folder}/server/${SERVER_IDENTITY}, creating.."
	mkdir -p "/home/${user}/${folder}/server/${SERVER_IDENTITY}"
fi
if [ ! -d "/home/${user}/${folder}/server/${SERVER_IDENTITY}/cfg" ]; then
	echo "Missing /home/${user}/${folder}/server/${SERVER_IDENTITY}/cfg, creating.."
	mkdir -p "/home/${user}/${folder}/server/${SERVER_IDENTITY}/cfg"
fi

echo "Downloading any updates for Rust..."
/steamcmd/steamcmd.sh +login anonymous \
  +force_install_dir /home/${user}/${folder} \
  +app_info_update 1 \
  +app_update 258550 \
  +quit

cd /home/${user}

echo "Downloading and installing latest Oxide.."
OXIDE_URL="https://umod.org/games/rust/download/develop"
curl -sL $OXIDE_URL | bsdtar -xvf- -C /home/${user}/${folder}/
chmod 755 /home/${user}/${folder}/CSharpCompiler.x86_x64 > /dev/null 2>&1 &

echo "Downloading mod files..."
wget --quiet https://github.com/kus/rust-modded-server/archive/master.zip
unzip -o -qq master.zip

# Copy plugins
cp -rlf rust-modded-server-master/oxide/ /home/${user}/${folder}/oxide/

# Update server.cfg
echo -e "server.hostname \"$SERVER_NAME\"\nserver.description \"$SERVER_DESCRIPTION\"\n$(cat rust-modded-server-master/server.cfg)" > /home/${user}/${folder}/server/${SERVER_IDENTITY}/cfg/server.cfg

# Update users.cfg
echo -e "$(cat rust-modded-server-master/users.cfg)" > /home/${user}/${folder}/server/${SERVER_IDENTITY}/cfg/users.cfg

# Remove
rm -r rust-modded-server-master master.zip

chown -R ${user}:${user} /home/${user}/${folder}

cd /home/${user}/${folder}

# Rust includes a 64-bit version of steamclient.so, so we need to tell the OS where it exists
export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:/home/${user}/${folder}/RustDedicated_Data/Plugins/x86_64

# Run the server
echo "Starting Rust.."

STARTUP_COMMAND="-batchmode \
-load \
-nographics \
+server.ip 0.0.0.0 \
+rcon.ip 0.0.0.0 \
+server.secure 1 \
+server.port $SERVER_PORT \
+server.seed $SERVER_SEED \
+server.level \"$SERVER_MAP\" \
+server.identity \"$SERVER_IDENTITY\" \
+server.worldsize $SERVER_SIZE \
+server.maxplayers $SERVER_MAX_PLAYERS \
+server.tickrate $SERVER_TICKRATE \
+server.saveinterval $SERVER_SAVE_INTERVAL \
+decay.scale $SERVER_DECAY \
+craft.instant $SERVER_CRAFT_INSTANT"

if [ "$SERVER_DECAY" = "0" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +decay.upkeep 0 +hotairballoon.outsidedecayminutes 0 +modularcar.outsidedecayminutes 0 +baseridableanimal.decayminutes 0 +minicopter.insidedecayminutes 0 +minicopter.outsidedecayminutes 0 +motorrowboat.outsidedecayminutes 0"
fi

if [ ! -z "$SERVER_URL" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +server.url \"$SERVER_URL\""
fi

if [ ! -z "$SERVER_BANNER_URL" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +server.headerimage \"$SERVER_BANNER_URL\""
fi

if [ ! -z "$APP_PORT" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +app.port $APP_PORT"
fi

if [ ! -z "$RCON_PORT" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +rcon.port $RCON_PORT"
fi

if [ ! -z "$RCON_PASSWORD" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +rcon.password \"$RCON_PASSWORD\""
fi

if [ ! -z "$RCON_WEB" ]; then
	STARTUP_COMMAND="$STARTUP_COMMAND +rcon.web $RCON_WEB"
fi

echo "Starting server on $PUBLIC_IP:$SERVER_PORT"
echo "Using command /home/${user}/${folder}/RustDedicated $STARTUP_COMMAND"

/home/${user}/${folder}/RustDedicated $STARTUP_COMMAND 2>&1 &
