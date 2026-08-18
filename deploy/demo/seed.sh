#!/usr/bin/env bash
# Seeds the demo workspace over Nona's HTTP API - no demo code in the product.
# Relies on /auth/register creating the first admin and refusing thereafter, which
# is the state a freshly wiped database is in.
#
#   ./seed.sh
#   BASE_URL=https://demo.nonaconfig.com ./seed.sh

set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:8080}"
ENV_FILE="${ENV_FILE:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/demo.env}"

[ -f "$ENV_FILE" ] && . "$ENV_FILE"
EMAIL="${NONA_DEMO_EMAIL:?set NONA_DEMO_EMAIL}"
PASSWORD="${NONA_DEMO_PASSWORD:?set NONA_DEMO_PASSWORD}"

TOKEN=""

log()  { printf '  %s\n' "$1"; }
step() { printf '\n==> %s\n' "$1"; }

# --- plumbing -----------------------------------------------------------

# api METHOD PATH [JSON_BODY]
api() {
  local method="$1" path="$2" body="${3:-}"
  local args=(-sS -X "$method" "$BASE_URL$path" -H 'Content-Type: application/json')
  [ -n "$TOKEN" ] && args+=(-H "Authorization: Bearer $TOKEN")
  [ -n "$body" ] && args+=(-d "$body")
  curl "${args[@]}"
}

wait_for_api() {
  step "Waiting for Nona at $BASE_URL"
  for _ in $(seq 1 60); do
    if curl -sS -o /dev/null --max-time 3 "$BASE_URL/auth/first-time" 2>/dev/null; then
      log "up"
      return 0
    fi
    sleep 2
  done
  echo "Nona did not become reachable at $BASE_URL" >&2
  exit 1
}

authenticate() {
  step "Authenticating as $EMAIL"
  local payload response
  payload="$(jq -nc --arg e "$EMAIL" --arg p "$PASSWORD" '{email:$e,password:$p}')"

  # Fresh database -> register. Already seeded -> log in.
  response="$(api POST /auth/register "$payload")"
  TOKEN="$(jq -r '.token // empty' <<<"$response")"
  if [ -n "$TOKEN" ]; then
    log "registered the first admin account"
    return
  fi

  response="$(api POST /auth/login "$payload")"
  TOKEN="$(jq -r '.token // empty' <<<"$response")"
  if [ -z "$TOKEN" ]; then
    echo "Could not register or log in. Last response: $response" >&2
    exit 1
  fi
  log "logged in to the existing instance"
}

# --- resource helpers ---------------------------------------------------

create_project() {
  local name="$1" response
  response="$(api POST /admin/projects "$(jq -nc --arg n "$name" '{name:$n}')")"
  if [ -z "$(jq -r '.name // empty' <<<"$response")" ]; then
    echo "Failed to create project '$name': $response" >&2
    exit 1
  fi
  log "project: $name"
}

# Creating a project auto-creates Defaults:Environment (Production), so hitting an
# existing one here is expected rather than an error.
create_environment() {
  local project="$1" name="$2" response
  response="$(api POST "/admin/projects/$project/environments" "$(jq -nc --arg n "$name" '{name:$n}')")"
  if [ -n "$(jq -r '.name // empty' <<<"$response")" ]; then
    log "environment: $project/$name"
  else
    log "environment: $project/$name (already present)"
  fi
}

# set_param PROJECT ENV KEY VALUE CONTENT_TYPE [SCOPE]
set_param() {
  local project="$1" env="$2" key="$3" value="$4" content_type="$5" scope="${6:-all}"
  local encoded_key payload response
  encoded_key="$(jq -rn --arg k "$key" '$k|@uri')"
  payload="$(jq -nc \
    --arg v "$value" --arg c "$content_type" --arg s "$scope" \
    '{value:$v,contentType:$c,scope:$s}')"

  response="$(api PUT "/admin/projects/$project/environments/$env/config-entries/$encoded_key" "$payload")"
  if [ -z "$(jq -r '.key // empty' <<<"$response")" ]; then
    echo "Failed to set $project/$env/$key: $response" >&2
    exit 1
  fi
}

# publish_release PROJECT ENV VERSION MAKE_ACTIVE
# Snapshots the environment's working parameters, so set the values you want first.
publish_release() {
  local project="$1" env="$2" version="$3" make_active="$4" payload response
  payload="$(jq -nc --arg v "$version" --argjson a "$make_active" '{version:$v,makeActive:$a}')"
  response="$(api POST "/admin/projects/$project/environments/$env/releases" "$payload")"
  if [ -z "$(jq -r '.version // empty' <<<"$response")" ]; then
    echo "Failed to publish $project/$env $version: $response" >&2
    exit 1
  fi
  log "release: $project/$env $version$([ "$make_active" = true ] && echo ' (active)')"
}

# create_api_key PROJECT NAME SCOPE -> prints the generated key
create_api_key() {
  local project="$1" name="$2" scope="$3" payload response key
  payload="$(jq -nc --arg n "$name" --arg s "$scope" '{name:$n,scope:$s}')"
  response="$(api POST "/admin/projects/$project/api-keys" "$payload")"
  key="$(jq -r '.key // empty' <<<"$response")"
  if [ -z "$key" ]; then
    echo "Failed to create API key for $project: $response" >&2
    exit 1
  fi
  # stdout is the key itself, so progress has to go to stderr.
  log "api key: $project/$name" >&2
  printf '%s' "$key"
}

# --- the demo workspace -------------------------------------------------

seed_storefront() {
  step "Seeding 'acme-storefront'"
  create_project "acme-storefront"
  create_environment "acme-storefront" "Development"
  create_environment "acme-storefront" "Staging"
  create_environment "acme-storefront" "Production"

  local palette='{"primary":"#5b6ef5","surface":"#0f1115","accent":"#f5a623","radius":12}'

  set_param "acme-storefront" Development "Features:Checkout"              true                          boolean
  set_param "acme-storefront" Development "Features:NewNavigation"         true                          boolean
  set_param "acme-storefront" Development "Features:LiveChat"              true                          boolean
  set_param "acme-storefront" Development "Killswitch:Payments"            false                         boolean
  set_param "acme-storefront" Development "Checkout:MaxCartItems"          50                            number
  set_param "acme-storefront" Development "Checkout:FreeShippingThreshold" 25                            number
  set_param "acme-storefront" Development "Support:Email"                  dev-support@acme.example      text
  set_param "acme-storefront" Development "Theme:Palette"                  "$palette"                    json
  # Server-scoped: withheld from client-scoped API keys.
  set_param "acme-storefront" Development "Payments:ProviderTimeoutMs"     30000                         number server

  set_param "acme-storefront" Staging     "Features:Checkout"              true                          boolean
  set_param "acme-storefront" Staging     "Features:NewNavigation"         true                          boolean
  set_param "acme-storefront" Staging     "Features:LiveChat"              false                         boolean
  set_param "acme-storefront" Staging     "Killswitch:Payments"            false                         boolean
  set_param "acme-storefront" Staging     "Checkout:MaxCartItems"          25                            number
  set_param "acme-storefront" Staging     "Checkout:FreeShippingThreshold" 50                            number
  set_param "acme-storefront" Staging     "Support:Email"                  staging-support@acme.example  text
  set_param "acme-storefront" Staging     "Theme:Palette"                  "$palette"                    json
  set_param "acme-storefront" Staging     "Payments:ProviderTimeoutMs"     15000                         number server

  # Release history: set values, publish, overwrite, publish again.
  set_param "acme-storefront" Production  "Features:Checkout"              true                          boolean
  set_param "acme-storefront" Production  "Features:NewNavigation"         false                         boolean
  set_param "acme-storefront" Production  "Features:LiveChat"              false                         boolean
  set_param "acme-storefront" Production  "Killswitch:Payments"            false                         boolean
  set_param "acme-storefront" Production  "Checkout:MaxCartItems"          10                            number
  set_param "acme-storefront" Production  "Checkout:FreeShippingThreshold" 100                           number
  set_param "acme-storefront" Production  "Support:Email"                  support@acme.example          text
  publish_release "acme-storefront" Production "1.0.0" false

  set_param "acme-storefront" Production  "Features:LiveChat"              true                          boolean
  set_param "acme-storefront" Production  "Checkout:MaxCartItems"          20                            number
  set_param "acme-storefront" Production  "Checkout:FreeShippingThreshold" 75                            number
  publish_release "acme-storefront" Production "1.1.0" false

  set_param "acme-storefront" Production  "Theme:Palette"                  "$palette"                    json
  set_param "acme-storefront" Production  "Payments:ProviderTimeoutMs"     10000                         number server
  publish_release "acme-storefront" Production "1.2.0" true

  # Published but not activated, so Staging keeps serving working parameters.
  publish_release "acme-storefront" Staging "1.2.0" false

  STOREFRONT_KEY="$(create_api_key "acme-storefront" "Web client key" client)"
}

seed_mobile() {
  step "Seeding 'mobile-app'"
  create_project "mobile-app"
  create_environment "mobile-app" "Development"
  create_environment "mobile-app" "Production"

  set_param "mobile-app" Development "Features:BiometricLogin"    true          boolean
  set_param "mobile-app" Development "Features:OfflineMode"       true          boolean
  set_param "mobile-app" Development "Onboarding:Variant"         carousel      text
  set_param "mobile-app" Development "App:MinimumSupportedVersion" 3.0.0        text
  set_param "mobile-app" Development "App:Announcement" \
    '{"visible":true,"title":"Dev build","body":"Pointing at the staging API.","severity":"info"}' json

  set_param "mobile-app" Production  "Features:BiometricLogin"    true          boolean
  set_param "mobile-app" Production  "Features:OfflineMode"       false         boolean
  set_param "mobile-app" Production  "Onboarding:Variant"         single-screen text
  set_param "mobile-app" Production  "App:MinimumSupportedVersion" 3.4.0        text
  set_param "mobile-app" Production  "App:Announcement" \
    '{"visible":false,"title":"","body":"","severity":"info"}' json
  publish_release "mobile-app" Production "2.3.0" false

  set_param "mobile-app" Production  "App:MinimumSupportedVersion" 3.4.1        text
  publish_release "mobile-app" Production "2.4.0" true

  MOBILE_KEY="$(create_api_key "mobile-app" "Mobile client key" client)"
}

# --- run ----------------------------------------------------------------

command -v jq >/dev/null 2>&1 || { echo "jq is required" >&2; exit 1; }

wait_for_api
authenticate
seed_storefront
seed_mobile

# Keys are server-generated and change on every reseed, so the landing page reads
# them from here rather than hardcoding one.
INFO_DIR="${DEMO_INFO_DIR:-}"
if [ -n "$INFO_DIR" ] && [ -d "$INFO_DIR" ]; then
  jq -nc \
    --arg storefront "$STOREFRONT_KEY" \
    --arg mobile "$MOBILE_KEY" \
    '{storefrontApiKey:$storefront,mobileApiKey:$mobile}' > "$INFO_DIR/demo-info.json"
  log "wrote $INFO_DIR/demo-info.json"
fi

cat <<EOF

==> Demo workspace ready

  Console : $BASE_URL
  Sign in : $EMAIL / $PASSWORD

  Try a read:
    curl "$BASE_URL/api/Production/Features%3ACheckout" -H "X-Api-Key: $STOREFRONT_KEY"

EOF
