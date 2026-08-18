#!/usr/bin/env bash
# Installs the demo layer on the VM. Run by deploy.sh over SSH; re-run to pick up edits.

set -euo pipefail

SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR=/opt/nona
DEMO_DIR="$APP_DIR/demo"

step() { printf '\n==> %s\n' "$1"; }

step "Installing demo files to $DEMO_DIR"
mkdir -p "$DEMO_DIR"
install -m 0700 "$SRC/seed.sh"  "$DEMO_DIR/seed.sh"
install -m 0700 "$SRC/reset.sh" "$DEMO_DIR/reset.sh"

# Generated here rather than committed. Kept once created: the account is registered
# with it, so changing it without a reseed locks the landing page out.
if [ ! -f "$DEMO_DIR/demo.env" ]; then
  step "Generating demo credentials"
  # Shape satisfies Nona's policy: length, uppercase, digit, symbol.
  GENERATED="Demo-$(openssl rand -hex 8)!A1"
  EMAIL_DEFAULT="$(grep -E '^NONA_DEMO_EMAIL=' "$SRC/demo.env.example" | cut -d= -f2-)"
  {
    echo "# Generated on first install. Public by design - see demo.env.example."
    echo "NONA_DEMO_EMAIL=${EMAIL_DEFAULT:-demo@nonaconfig.com}"
    echo "NONA_DEMO_PASSWORD=$GENERATED"
  } > "$DEMO_DIR/demo.env"
  chmod 600 "$DEMO_DIR/demo.env"
  echo "  generated for ${EMAIL_DEFAULT:-demo@nonaconfig.com}"
fi

# demo.env is the single source; the page gets its copy substituted in here.
. "$DEMO_DIR/demo.env"
sed -e "s|__DEMO_EMAIL__|${NONA_DEMO_EMAIL}|g" \
    -e "s|__DEMO_PASSWORD__|${NONA_DEMO_PASSWORD}|g" \
    "$SRC/demo-login.html" > "$DEMO_DIR/index.html"
chmod 0644 "$DEMO_DIR/index.html"

step "Installing the nightly reset timer"
install -m 0644 "$SRC/systemd/nona-demo-reset.service" /etc/systemd/system/
install -m 0644 "$SRC/systemd/nona-demo-reset.timer"   /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now nona-demo-reset.timer

step "Reloading Caddy so it serves the landing page"
cd "$APP_DIR"
docker compose up -d

printf '\n==> Installed. Next reset:\n'
systemctl list-timers nona-demo-reset.timer --no-pager | sed -n '1,2p'
