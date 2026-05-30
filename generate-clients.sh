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

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-url) SERVER_URL="$2"; shift 2 ;;
    --admin-path) ADMIN_PATH="$2"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

echo "Fetching OpenAPI spec from $SERVER_URL/openapi/v1.json..."
curl -f -s "$SERVER_URL/openapi/v1.json" -o "$SPEC"
echo "  -> $SPEC"

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
