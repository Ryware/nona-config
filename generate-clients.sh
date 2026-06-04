#!/usr/bin/env bash
# generate-clients.sh
# Fetches the OpenAPI spec from the running server, then regenerates all API clients.
#
# Prerequisites:
#   dotnet tool restore           (installs Kiota)
#   Server must be running:       docker-compose up  OR  dotnet run --project core/src/WebApi
#
# Usage:
#   ./generate-clients.sh
#   ./generate-clients.sh --server-url http://localhost:18080
#   ./generate-clients.sh --admin-path /other/location/nona-config-admin

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_URL="${SERVER_URL:-http://localhost:5027}"
ADMIN_PATH="${ADMIN_PATH:-"$SCRIPT_DIR/../nona-config-admin"}"
SPEC="$SCRIPT_DIR/openapi.json"
FETCH_SPEC=1
LOCK_DIR="$SCRIPT_DIR/.nona-generate-clients.lock"

cleanup_lock() {
  if [[ "${LOCK_ACQUIRED:-0}" == "1" ]]; then
    rmdir "$LOCK_DIR"
  fi
}

acquire_lock() {
  local attempts=0

  until mkdir "$LOCK_DIR" 2>/dev/null; do
    attempts=$((attempts + 1))

    if [[ "$attempts" -ge 120 ]]; then
      echo "Timed out waiting for client generation lock: $LOCK_DIR" >&2
      exit 1
    fi

    sleep 1
  done

  LOCK_ACQUIRED=1
  trap cleanup_lock EXIT HUP INT TERM
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-url) SERVER_URL="$2"; shift 2 ;;
    --spec) SPEC="$2"; FETCH_SPEC=0; shift 2 ;;
    --admin-path) ADMIN_PATH="$2"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

acquire_lock

if [[ "$FETCH_SPEC" == "1" ]]; then
  echo "Fetching OpenAPI spec from $SERVER_URL/openapi/v1.json..."
  curl -f -s "$SERVER_URL/openapi/v1.json" -o "$SPEC"
  echo "  -> $SPEC"
else
  echo "Using OpenAPI spec: $SPEC"
fi

echo ""
echo "Generating C# client for Migrator..."
dotnet kiota generate \
    -l CSharp \
    -d "$SPEC" \
    -c NonaMigrationApiClient \
    -n Nona.Migrator.Core.Generated \
    -o "$SCRIPT_DIR/migrator/src/ConfigMigrator.Core/Generated" \
    --co
echo "  -> migrator/src/ConfigMigrator.Core/Generated"

echo ""
echo "Generating C# client for CLI..."
dotnet kiota generate \
    -l CSharp \
    -d "$SPEC" \
    -c NonaApiClient \
    -n Nona.Cli.Generated \
    -o "$SCRIPT_DIR/cli/src/Nona.Cli/Core/Generated" \
    --co
echo "  -> cli/src/Nona.Cli/Core/Generated"

echo ""
echo "Generating TypeScript types for admin UI..."
npx --yes openapi-typescript "$SPEC" -o "$ADMIN_PATH/src/generated/api.ts"
echo "  -> $ADMIN_PATH/src/generated/api.ts"

echo ""
echo "Done. Review the changes and commit."
