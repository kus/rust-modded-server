#!/usr/bin/env bash

# Install
# As root (sudo su)
# cd / && curl --silent --output "gcp.sh" "https://raw.githubusercontent.com/kus/rust-modded-server/master/gcp.sh" && chmod +x gcp.sh && bash gcp.sh

METADATA_URL="${METADATA_URL:-http://metadata.google.internal/computeMetadata/v1/instance/attributes}"

get_metadata () {
    if [ -z "$1" ]
    then
        local result=""
    else
        local result=$(curl -s "$METADATA_URL/$1?alt=text" -H "Metadata-Flavor: Google")
		if [[ $result == *"<!DOCTYPE html>"* ]]; then
			result=""
		fi
    fi

    echo $result
}

# Get meta data from GCP and set environment variables
META_MOD_URL=$(get_metadata MOD_URL)
META_PORT=$(get_metadata SERVER_PORT)
META_RCON_PORT=$(get_metadata RCON_PORT)
META_RCON_PASSWORD=$(get_metadata RCON_PASSWORD)
META_RCON_WEB=$(get_metadata RCON_WEB)
META_APP_PORT=$(get_metadata APP_PORT)
META_MAP=$(get_metadata SERVER_MAP)
META_SEED=$(get_metadata SERVER_SEED)
META_SIZE=$(get_metadata SERVER_SIZE)
META_TICKRATE=$(get_metadata SERVER_TICKRATE)
META_MAX_PLAYERS=$(get_metadata SERVER_MAX_PLAYERS)
META_SERVER_IDENTITY=$(get_metadata SERVER_IDENTITY)
META_SERVER_NAME=$(get_metadata SERVER_NAME)
META_DESCRIPTION=$(get_metadata SERVER_DESCRIPTION)
META_URL=$(get_metadata SERVER_URL)
META_BANNER_URL=$(get_metadata SERVER_BANNER_URL)
META_SAVE_INTERVAL=$(get_metadata SERVER_SAVE_INTERVAL)
META_DECAY=$(get_metadata SERVER_DECAY)
META_CRAFT_INSTANT=$(get_metadata SERVER_CRAFT_INSTANT)
export MOD_URL="${META_MOD_URL:-https://github.com/kus/rust-modded-server/archive/master.zip}"
export SERVER_PORT="${META_PORT:-28015}"
export RCON_PORT="${META_RCON_PORT:-28016}"
export RCON_PASSWORD="${META_RCON_PASSWORD:-changeme}"
export RCON_WEB="${META_RCON_WEB:-1}"
export APP_PORT="${META_APP_PORT:-28082}"
export SERVER_MAP="${META_MAP:-Barren}"
export SERVER_SEED="${META_SEED:-123456}"
export SERVER_SIZE="${META_SIZE:-3000}"
export SERVER_TICKRATE="${META_TICKRATE:-30}"
export SERVER_MAX_PLAYERS="${META_MAX_PLAYERS:-50}"
export SERVER_IDENTITY="${META_SERVER_IDENTITY:-world}"
export SERVER_NAME="${META_SERVER_NAME:-Rust Server}"
export SERVER_DESCRIPTION="${META_DESCRIPTION:-Rust Server}"
export SERVER_URL="${META_URL:-https://github.com/kus/rust-modded-server}"
export SERVER_BANNER_URL="${META_BANNER_URL:-}"
export SERVER_SAVE_INTERVAL="${META_SAVE_INTERVAL:-300}"
export SERVER_DECAY="${META_DECAY:-1}"
export SERVER_CRAFT_INSTANT="${META_CRAFT_INSTANT:-0}"
export DUCK_DOMAIN="${DUCK_DOMAIN:-$(get_metadata DUCK_DOMAIN)}"
export DUCK_TOKEN="${DUCK_TOKEN:-$(get_metadata DUCK_TOKEN)}"

cd /

# Update DuckDNS with our current IP
if [ ! -z "$DUCK_TOKEN" ]; then
    echo url="http://www.duckdns.org/update?domains=$DUCK_DOMAIN&token=$DUCK_TOKEN&ip=$(dig +short myip.opendns.com @resolver1.opendns.com)" | curl -k -o /duck.log -K -
fi

# Download latest installer
curl --silent --output "install.sh" "https://raw.githubusercontent.com/kus/rust-modded-server/master/install.sh" && chmod +x install.sh

# Run
bash install.sh |& tee /install.log
