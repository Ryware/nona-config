#!/bin/bash
# VM bootstrap. Re-runs on every boot, so everything here is idempotent.
set -euo pipefail

exec > >(tee -a /var/log/nona-startup.log) 2>&1
echo "=== nona startup $(date -Is) ==="

APP_DIR=/opt/nona
META="http://metadata.google.internal/computeMetadata/v1/instance/attributes"

meta() { curl -fsS -H "Metadata-Flavor: Google" "$META/$1" 2>/dev/null || true; }

NONA_DOMAIN="$(meta nona-domain)"
ACME_EMAIL="$(meta nona-acme-email)"
NONA_IMAGE="$(meta nona-image)"
: "${NONA_IMAGE:=rywaredev/nona:latest}"

# --- swap ---------------------------------------------------------------
# 1 GB of RAM. Fits, but an apt upgrade or image pull can push it over.
if [ ! -f /swapfile ]; then
  echo "--- creating swap"
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi
swapon /swapfile 2>/dev/null || true
sysctl -w vm.swappiness=10 >/dev/null

# --- docker -------------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
  echo "--- installing docker"
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq ca-certificates curl gnupg
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin
fi
systemctl enable --now docker

command -v jq >/dev/null 2>&1 || DEBIAN_FRONTEND=noninteractive apt-get install -y -qq jq

# The reset timer fires at local midnight.
timedatectl set-timezone Asia/Jerusalem

# Keep container logs from filling a 30 GB disk.
mkdir -p /etc/docker
cat > /etc/docker/daemon.json <<'JSON'
{
  "log-driver": "json-file",
  "log-opts": { "max-size": "10m", "max-file": "3" }
}
JSON
systemctl restart docker

# --- app files ----------------------------------------------------------
mkdir -p "$APP_DIR"
cd "$APP_DIR"

# Generated once. Rewriting the JWT key would invalidate every active session.
if [ ! -f "$APP_DIR/.env" ]; then
  echo "--- generating .env"
  {
    echo "NONA_IMAGE=$NONA_IMAGE"
    echo "NONA_DOMAIN=$NONA_DOMAIN"
    echo "ACME_EMAIL=$ACME_EMAIL"
    echo "NONA_JWT_KEY=$(openssl rand -base64 48 | tr -d '\n')"
  } > "$APP_DIR/.env"
  chmod 600 "$APP_DIR/.env"
else
  sed -i "s|^NONA_IMAGE=.*|NONA_IMAGE=$NONA_IMAGE|" "$APP_DIR/.env"
  sed -i "s|^NONA_DOMAIN=.*|NONA_DOMAIN=$NONA_DOMAIN|" "$APP_DIR/.env"
  sed -i "s|^ACME_EMAIL=.*|ACME_EMAIL=$ACME_EMAIL|" "$APP_DIR/.env"
fi

cat > "$APP_DIR/Caddyfile" <<'CADDY'
{
	email {$ACME_EMAIL}
}

{$NONA_DOMAIN} {
	encode zstd gzip

	# A path matcher without a wildcard is exact, so this catches only the bare
	# root - /projects, /login and the API all fall through to Nona.
	handle / {
		root * /srv/demo
		rewrite * /index.html
		# Runs once and redirects; without this browsers cache it heuristically.
		header Cache-Control "no-store"
		file_server
	}

	# Rewritten by every reset, so a cached copy would hand out a dead key.
	handle /demo-info.json {
		root * /srv/demo
		header Cache-Control "no-store"
		file_server
	}

	handle {
		reverse_proxy nona:8080
	}
}
CADDY

# Caddy mounts this, so it has to exist before the demo layer is installed.
mkdir -p "$APP_DIR/demo"

cat > "$APP_DIR/docker-compose.yml" <<'COMPOSE'
services:
  nona:
    image: ${NONA_IMAGE}
    restart: unless-stopped
    ports:
      # Loopback: reachable by seed.sh and reset.sh, never from the internet.
      - "127.0.0.1:18080:8080"
    environment:
      Jwt__Key: ${NONA_JWT_KEY}
      Jwt__Issuer: nona
      Jwt__Audience: nona
    volumes:
      - nona-data:/var/lib/nona

  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    environment:
      NONA_DOMAIN: ${NONA_DOMAIN}
      ACME_EMAIL: ${ACME_EMAIL}
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - ./demo:/srv/demo:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - nona

volumes:
  nona-data:
  caddy-data:
  caddy-config:
COMPOSE

echo "--- starting stack"
docker compose pull --quiet
docker compose up -d

# `up -d` only restarts a container whose compose definition changed, so a Caddyfile
# edit alone is otherwise ignored until the next reboot.
echo "--- reloading Caddy config"
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile \
  || docker compose restart caddy

echo "=== nona startup complete $(date -Is) ==="
