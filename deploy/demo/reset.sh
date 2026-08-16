#!/usr/bin/env bash
# Wipes and reseeds the demo. Destroys the volume rather than deleting rows, so
# nothing a visitor did survives. Fired nightly by nona-demo-reset.timer.

set -euo pipefail

APP_DIR="${APP_DIR:-/opt/nona}"
DEMO_DIR="${DEMO_DIR:-/opt/nona/demo}"
DATA_VOLUME="${DATA_VOLUME:-nona_nona-data}"

log() { printf '[%s] %s\n' "$(date -Is)" "$1"; }

cd "$APP_DIR"

log "Stopping the stack"
docker compose down

log "Removing the data volume ($DATA_VOLUME)"
docker volume rm "$DATA_VOLUME" >/dev/null 2>&1 || log "volume was already absent"

log "Starting the stack"
docker compose up -d

log "Seeding"
# Over loopback, so a reset never depends on DNS, TLS, or the box being reachable.
DEMO_INFO_DIR="$DEMO_DIR" \
BASE_URL="http://127.0.0.1:18080" \
ENV_FILE="$DEMO_DIR/demo.env" \
  "$DEMO_DIR/seed.sh"

log "Reset complete"
