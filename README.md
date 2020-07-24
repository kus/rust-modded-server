# Rust Modded Server

## About

A ready to go Rust server which comes pre-modded with uMod (Oxide) and plugins.

Getting up and running:

- [Running on Google Cloud](#running-on-google-cloud)
- [Running on Linux](#running-on-linux)

## Mods installed

Mod | Version | Why
--- | --- | ---
[uMod](https://umod.org/games/rust) | `2.0.4677` | Oxide (legacy) modification for the game Rust
[Gather Manager](https://umod.org/plugins/gather-manager) | `2.2.74` | Increases the amount of items gained from gathering resources
[Rust Kits](https://umod.org/plugins/rust-kits) | `3.2.95` | Item kits, autokits, kit cooldowns, and more
[Remover Tool](https://umod.org/plugins/remover-tool) | `4.3.15` | Building and entity removal tool
[Welcomer](https://umod.org/plugins/welcomer) | `1.5.1` | Provides welcome and join/leave messages
[Death Notes](https://umod.org/plugins/death-notes) | `6.3.5` | Broadcasts deaths to chat along with detailed information
[Time Of Day](https://umod.org/plugins/time-of-day) | `2.3.4` | Alters the day and night duration
[Blueprint Manager](https://umod.org/plugins/blueprint-manager) | `1.1.4` | Manage blueprints on your server easily
[No Decay](https://umod.org/plugins/no-decay) | `1.0.40` | Scales or disables decay of items, and deployables
[Heli Control](https://umod.org/plugins/heli-control) | `1.4.0` | Manage CH47 and helicopter health, player damage, configure turrets/rockets, and more
[Vehicle License](https://umod.org/plugins/vehicle-license) | `1.6.0` | Allows players to buy, spawn and recall: Boat, RHIB, Sedan, HotAirBalloon, MiniCopter and CH47 etc...
[Car Commander Lite](https://umod.org/plugins/car-commander-lite) | `0.1.3` | Car spawner with added features
[Spawn Mini](https://umod.org/plugins/spawn-mini) | `2.6.3` | Spawns a mini helicopter on command
[Portable Vehicles](https://umod.org/plugins/portable-vehicles) | `1.0.9` | Gives vehicles as item to players
[Teleport Gun](https://umod.org/plugins/teleport-gun) | `0.1.2` | Allows teleporting by shooting guns
[NTeleportation](https://umod.org/plugins/nteleportation) | `1.3.7` | Multiple teleportation systems for admin and players
[Copy Paste](https://umod.org/plugins/copy-paste) | `4.1.22` | Copy and paste your buildings to save them or move them

## Acessing admin menu

Open the console (F1)

### Spawn items

Click `Items` tab up the top and click an item to spawn it

### Console commands

Command | Value | Description
--- | --- | ---
`noclip` |  | Noclip
`god` | `0-1` | Godmode
`env.time` | `0-23` | Change time (24 hour time)
`global.teleport` | `steamid 64 bit or steam name` | Teleports you to a player
`global.teleport2me` | `steamid 64 bit or steam name` | Teleports a player to you
`weather.clouds` | `0-1` | Change the amount of clouds in the sky
`weather.fog` | `0-1` | Change the amount of fog
`weather.wind` | `0-1` | Change the amount of wind
`weather.rain` | `0-1` | Cange the amount of rain
`inventory.giveall` | `item id or item shortname` | Gives an item to everyone on the server
`inventory.givebpall` | `item id or item shortname` | Gives a blueprint to everyone on the server
`inventory.give` | `item id or item shortname` | Gives you an item
`inventory.giveto` | `item id or item shortname` `steam name` | Gives an item to a person using steam username
`inventory.giveid` | `item id or item shortname` `steamid 64 bit` | Gives an item to a person using steam id
`inventory.givearm` | `item id or item shortname` `steam name` | Gives an item to a persons hotbar
`inventory.givebp` | `item id or item shortname` `steam name` | Gives a blueprint to a person

### Events

Command | Value | Description
--- | --- | ---
`event.run` |  | Airdrop event
`heli.calltome` |  | Forces a helicopter to spawn off map and fly to your position
`heli.call` |  | Calls in a helicopter to roam the map like normal
`heli.strafe` | `steamid 64 bit or steam name` | Forces helicopter to target a specific player
`spawn ch47scientists.entity` |  | Spawn and trigger the Chinook 47 helicopter event, crewed by 10 hostile scientist inside

### Vehicles

Command | Value | Description
--- | --- | ---
`entity.spawnhere` | `minicopter.entity` | Spawns a minicopter
`entity.spawnhere` | `scraptransporthelicopter` | Spawns a Scrap Transport Helicopter
`entity.spawnhere` | `ch47.entity` | Spawns a Chinook 47 helicopter
`entity.spawnhere` | `hotairballoon` | Spawns a Hot air balloon
`entity.spawnhere` | `rhib` | Spawns a Rigid-Hulled Inflatable Boat

## Running on Google Cloud

Set your [Google Cloud project](https://console.cloud.google.com/project)

`gcloud config set project my-project`

### Create firewall rule

```
gcloud compute firewall-rules create rust-server \
--allow tcp:28015,tcp:28016,tcp:28082,tcp:80,tcp:443,udp:28015,udp:28016 \
--target-tags rust-server
```

### Create instance

```
gcloud beta compute instances create <instance-name> \
--project=<project> \
--zone=australia-southeast1-b \
--machine-type=n1-standard-2 \
--subnet=default \
--network-tier=PREMIUM \
--metadata=^,@^MOD_URL=https://github.com/kus/rust-modded-server/archive/master.zip,@RCON_PASSWORD=changeme,@SERVER_NAME=Rust\ Server\|5X\|NoBP\|Kits\|Vehicles\|InstaCraft,@SERVER_DESCRIPTION=A\ Rust\ Server\!\\n\\n5X\ gather,\ No\ BPs,\ No\ Decay,\ Kits,\ Vehicles,\ Instant\ craft,@SERVER_TICKRATE=30,@SERVER_MAP=Barren,@SERVER_SEED=123456,@SERVER_SIZE=3000,@SERVER_DECAY=0,@SERVER_CRAFT_INSTANT=1,@startup-script=echo\ \"Delaying\ for\ 30\ seconds...\"\ \#\&\&\ sleep\ 30\ \&\&\ cd\ /\ \&\&\ /gcp.sh \
--no-restart-on-failure \
--maintenance-policy=TERMINATE \
--scopes=https://www.googleapis.com/auth/devstorage.read_only,https://www.googleapis.com/auth/logging.write,https://www.googleapis.com/auth/monitoring.write,https://www.googleapis.com/auth/servicecontrol,https://www.googleapis.com/auth/service.management.readonly,https://www.googleapis.com/auth/trace.append \
--tags=rust-server \
--image-family=ubuntu-1804-lts \
--image-project=ubuntu-os-cloud \
--boot-disk-size=40GB \
--boot-disk-type=pd-standard \
--boot-disk-device-name=<instance-name> \
--no-shielded-secure-boot \
--shielded-vtpm \
--shielded-integrity-monitoring \
--reservation-affinity=any
```

### SSH to server

```
gcloud compute ssh <instance-name> \
--zone=australia-southeast1-b
```

### Install

```
sudo su
cd / && curl --silent --output "gcp.sh" "https://raw.githubusercontent.com/kus/rust-modded-server/master/gcp.sh" && chmod +x gcp.sh && bash gcp.sh
```

### Stop server

```
gcloud compute instances stop <instance-name> \
--zone australia-southeast1-b
```

### Start server

```
gcloud compute instances start <instance-name> \
--zone australia-southeast1-b
```

### Delete server

```
gcloud compute instances delete <instance-name> \
--zone australia-southeast1-b
```

### Push file to server from local machine

For example:

```
On local:
gcloud config set project <project>
cd /path/to/folder
gcloud compute scp file.txt root@<instance-name>:/home/steam/rust --zone australia-southeast1-b

On server SSH:
cd /home/steam/rust
chown steam:steam file.txt
chmod 644 file.txt
```

### Turn VM off at 3:30AM every day

SSH into the VM

Switch to root `sudo su`

Check the timezone your server is running in `sudo hwclock --show`

Open crontab file `nano /etc/crontab`

Append to the end of the crontab file `30 3    * * *   root    shutdown -h now`

Save `CTRL + X`

## Running on Linux

Make sure you have **40GB free space**.

You can configure the server from the following environment variables (or `Custom metadata` in Google Cloud):

Variable | Default Value | Description
--- | --- | ---
`MOD_URL` | `https://github.com/kus/rust-modded-server/archive/master.zip` | 
`SERVER_PORT` | `28015` | Server port
`RCON_PORT` | `28016` | RCON port
`RCON_PASSWORD` | `changeme` | RCON password
`RCON_WEB` | `1` | 0: Legacy source engine RCON, 1: Web RCON usable via http://facepunch.github.io/webrcon/
`APP_PORT` | `28082` | Rust+ Companion App Port
`SERVER_MAP` | `Barren` | Level: Procedural Map, Barren, Hapis, Craggy Island, Savas Island
`SERVER_SEED` | `123456` | Level map generation seed
`SERVER_SIZE` | `3000` | Defines the size of the map generated (min 1000, max 6000)
`SERVER_TICKRATE` | `30` | Server refresh rate
`SERVER_MAX_PLAYERS` | `50` | Maximum amount of players allowed to connect to your server at a time
`SERVER_IDENTITY` | `world` | Changes path to your server data rust/server/my_server_identity. Useful for running multiple instances
`SERVER_NAME` | `Rust Server` | The displayed name of your server
`SERVER_DESCRIPTION` | `Rust Server` | Server description
`SERVER_URL` | `https://github.com/kus/rust-modded-server` | Server URL
`SERVER_BANNER_URL` |  | Server Banner Image
`SERVER_SAVE_INTERVAL` | `300` | Time in seconds for server save
`SERVER_DECAY` | `1` | If server decay is on
`SERVER_CRAFT_INSTANT` | `0` | If instant craft is on
`DUCK_DOMAIN` |  | Duck DNS domain name to update
`DUCK_TOKEN` |  | Duck DNS token to update domain name

## License

See `LICENSE` for more details.
