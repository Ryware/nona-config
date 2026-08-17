#!/usr/bin/env bash
# Creates the demo VM. e2-micro + 30 GB pd-standard in us-central1 are free tier;
# the static IPv4 is the only charge (~$3/mo). Re-runnable.
#
#   ./deploy.sh
#   ./deploy.sh --domain demo.example.com --zone us-east1-b
#   PROJECT_ID=other-proj ./deploy.sh

set -euo pipefail

PROJECT_ID="${PROJECT_ID:-ryware}"
ZONE="${ZONE:-us-central1-a}"
NAME="${NAME:-nona-demo}"
DOMAIN="${DOMAIN:-demo.nonaconfig.com}"
ACME_EMAIL="${ACME_EMAIL:-michael@ryware.dev}"
IMAGE="${IMAGE:-rywaredev/nona:latest}"

while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT_ID="$2"; shift 2 ;;
    --zone)    ZONE="$2";       shift 2 ;;
    --name)    NAME="$2";       shift 2 ;;
    --domain)  DOMAIN="$2";     shift 2 ;;
    --email)   ACME_EMAIL="$2"; shift 2 ;;
    --image)   IMAGE="$2";      shift 2 ;;
    -h|--help) sed -n '2,7p' "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

REGION="${ZONE%-*}"
IP_NAME="$NAME-ip"
FW_NAME="$NAME-web"
TAG="$NAME"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STARTUP="$SCRIPT_DIR/startup-script.sh"

step() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }
note() { printf '    %s\n' "$1"; }

[ -f "$STARTUP" ] || { echo "startup-script.sh not found next to this script" >&2; exit 1; }

step "Project $PROJECT_ID / zone $ZONE"
gcloud config set project "$PROJECT_ID" >/dev/null

step "Enabling the Compute Engine API (no-op if already on)"
gcloud services enable compute.googleapis.com --project "$PROJECT_ID"

# --- static external IP -------------------------------------------------
# Costs the same as ephemeral while attached, but survives a stop/start, so the
# DNS record never goes stale.
step "Reserving static IP $IP_NAME in $REGION"
if [ -z "$(gcloud compute addresses list --project "$PROJECT_ID" \
             --filter="name=$IP_NAME AND region:$REGION" --format='value(name)')" ]; then
  gcloud compute addresses create "$IP_NAME" \
    --project "$PROJECT_ID" --region "$REGION" --network-tier STANDARD
else
  note "already reserved"
fi
IP="$(gcloud compute addresses describe "$IP_NAME" \
        --project "$PROJECT_ID" --region "$REGION" --format='value(address)' | tr -d '[:space:]')"
printf '    \033[32mIP: %s\033[0m\n' "$IP"

# --- firewall -----------------------------------------------------------
# 80/443 only; Nona's 8080 is never exposed. SSH rides default-allow-ssh.
step "Firewall rule $FW_NAME (tcp:80,443 -> tag '$TAG')"
if [ -z "$(gcloud compute firewall-rules list --project "$PROJECT_ID" \
             --filter="name=$FW_NAME" --format='value(name)')" ]; then
  gcloud compute firewall-rules create "$FW_NAME" \
    --project "$PROJECT_ID" \
    --allow tcp:80,tcp:443 \
    --target-tags "$TAG" \
    --source-ranges 0.0.0.0/0 \
    --description 'Nona demo: HTTP/HTTPS to Caddy'
else
  note "already exists"
fi

# --- the VM -------------------------------------------------------------
step "Creating VM $NAME (e2-micro, Debian 12, 30 GB pd-standard)"
if [ -z "$(gcloud compute instances list --project "$PROJECT_ID" \
             --filter="name=$NAME AND zone:$ZONE" --format='value(name)')" ]; then
  gcloud compute instances create "$NAME" \
    --project "$PROJECT_ID" \
    --zone "$ZONE" \
    --machine-type e2-micro \
    --image-family debian-12 \
    --image-project debian-cloud \
    --boot-disk-size 30GB \
    --boot-disk-type pd-standard \
    --network-tier STANDARD \
    --address "$IP" \
    --tags "$TAG" \
    --metadata "nona-domain=$DOMAIN,nona-acme-email=$ACME_EMAIL,nona-image=$IMAGE" \
    --metadata-from-file "startup-script=$STARTUP" \
    --labels "app=nona,env=demo"
else
  note "already exists - updating metadata and re-running the startup script"
  gcloud compute instances add-metadata "$NAME" --project "$PROJECT_ID" --zone "$ZONE" \
    --metadata "nona-domain=$DOMAIN,nona-acme-email=$ACME_EMAIL,nona-image=$IMAGE" \
    --metadata-from-file "startup-script=$STARTUP"
  gcloud compute ssh "$NAME" --project "$PROJECT_ID" --zone "$ZONE" \
    --command 'sudo google_metadata_script_runner startup'
fi

# --- demo layer ---------------------------------------------------------
# Uploaded rather than baked into metadata, so seed data and the landing page can
# be re-pushed on their own.
step "Installing the demo layer (seed data, auto-login page, nightly reset)"

# scp is pscp on Windows, which won't expand '~'.
REMOTE_HOME="$(gcloud compute ssh "$NAME" --project "$PROJECT_ID" --zone "$ZONE" --quiet \
  --command 'echo $HOME' | tr -d '\r' | tail -1)"
STAGING="$REMOTE_HOME/demo-src"
note "staging at $STAGING"

gcloud compute ssh "$NAME" --project "$PROJECT_ID" --zone "$ZONE" --quiet \
  --command "mkdir -p '$STAGING/systemd'"

gcloud compute scp \
  "$SCRIPT_DIR/seed.sh" "$SCRIPT_DIR/reset.sh" "$SCRIPT_DIR/demo.env.example" \
  "$SCRIPT_DIR/demo-login.html" "$SCRIPT_DIR/install-demo.sh" \
  "$NAME:$STAGING/" --project "$PROJECT_ID" --zone "$ZONE" --quiet

gcloud compute scp \
  "$SCRIPT_DIR/systemd/nona-demo-reset.service" "$SCRIPT_DIR/systemd/nona-demo-reset.timer" \
  "$NAME:$STAGING/systemd/" --project "$PROJECT_ID" --zone "$ZONE" --quiet

gcloud compute ssh "$NAME" --project "$PROJECT_ID" --zone "$ZONE" --quiet \
  --command "chmod +x '$STAGING'/*.sh && sudo '$STAGING/install-demo.sh'"

step "Seeding the demo workspace"
gcloud compute ssh "$NAME" --project "$PROJECT_ID" --zone "$ZONE" --quiet \
  --command 'sudo systemctl start nona-demo-reset.service && sudo journalctl -u nona-demo-reset.service -n 25 --no-pager'

cat <<EOF

$(printf '\033[32m')======================================================================
 VM is up. Point DNS at it, then wait for Caddy to get a certificate.
======================================================================$(printf '\033[0m')

  GoDaddy DNS for nonaconfig.com - add:
      Type: A     Name: ${DOMAIN%%.*}     Value: $IP     TTL: 600

  Then:  https://$DOMAIN

  Watch the bootstrap:
      gcloud compute ssh $NAME --zone $ZONE --command 'sudo tail -f /var/log/nona-startup.log'
  Check the containers:
      gcloud compute ssh $NAME --zone $ZONE --command 'cd /opt/nona && sudo docker compose ps'
  Reset now:
      gcloud compute ssh $NAME --zone $ZONE --command 'sudo systemctl start nona-demo-reset.service'

  Tear down - all three, or the released-but-unattached IP bills more than it does now:
      gcloud compute instances delete $NAME --zone $ZONE --quiet
      gcloud compute addresses delete $IP_NAME --region $REGION --quiet
      gcloud compute firewall-rules delete $FW_NAME --quiet

EOF
