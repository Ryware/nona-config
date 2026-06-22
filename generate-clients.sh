#!/usr/bin/env bash
# Regenerates checked-in API clients from the WebApi OpenAPI document.
#
# Default usage builds the OpenAPI document from source, then regenerates the
# CLI, migrator, and admin UI clients:
#   ./generate-clients.sh
#   ./generate-clients.sh --admin-path /other/location/nona-config-admin
#
# Alternate inputs:
#   ./generate-clients.sh --spec ./obj/openapi/WebApi.json
#   ./generate-clients.sh --server-url http://localhost:18080
#   ./generate-clients.sh --skip-admin

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEBAPI_PROJECT="$SCRIPT_DIR/core/src/WebApi/WebApi.csproj"
OPENAPI_DIR="${OPENAPI_DIR:-"$SCRIPT_DIR/obj/openapi"}"
SPEC="${SPEC:-"$OPENAPI_DIR/WebApi.json"}"
SERVER_URL="${SERVER_URL:-}"
ADMIN_PATH="${ADMIN_PATH:-"$SCRIPT_DIR/../nona-config-admin"}"
BUILD_SPEC=1
FETCH_SPEC=0
GENERATE_ADMIN=1
RESTORE_TOOLS=1
LOCK_DIR="$SCRIPT_DIR/.nona-generate-clients.lock"

usage() {
  cat <<'USAGE'
Usage:
  ./generate-clients.sh [options]

Options:
  --admin-path PATH        Path to nona-config-admin. Default: ../nona-config-admin
  --server-url URL         Fetch OpenAPI from URL/openapi/v1.json instead of building it
  --spec PATH              Use an existing OpenAPI document instead of building one
  --skip-admin             Generate only backend C# clients
  --skip-tool-restore      Do not run dotnet tool restore before generation
  --help                   Show this help
USAGE
}

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

require_file() {
  local file=$1
  local description=$2

  if [[ ! -f "$file" ]]; then
    echo "Expected $description at $file, but it does not exist." >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --admin-path)
      ADMIN_PATH="$2"
      shift 2
      ;;
    --server-url)
      SERVER_URL="$2"
      BUILD_SPEC=0
      FETCH_SPEC=1
      shift 2
      ;;
    --spec)
      SPEC="$2"
      BUILD_SPEC=0
      FETCH_SPEC=0
      shift 2
      ;;
    --skip-admin)
      GENERATE_ADMIN=0
      shift
      ;;
    --skip-tool-restore)
      RESTORE_TOOLS=0
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

acquire_lock

if [[ "$RESTORE_TOOLS" == "1" ]]; then
  echo "Restoring .NET tools..."
  dotnet tool restore
fi

if [[ "$BUILD_SPEC" == "1" ]]; then
  echo "Building OpenAPI spec from $WEBAPI_PROJECT..."
  mkdir -p "$OPENAPI_DIR"
  Storage__Type=InMemory dotnet build "$WEBAPI_PROJECT" \
    /p:OpenApiGenerateDocuments=true \
    /p:OpenApiDocumentsDirectory="$OPENAPI_DIR"
  require_file "$SPEC" "generated OpenAPI spec"
  echo "  -> $SPEC"
elif [[ "$FETCH_SPEC" == "1" ]]; then
  if [[ -z "$SERVER_URL" ]]; then
    SERVER_URL="http://localhost:5027"
  fi

  echo "Fetching OpenAPI spec from $SERVER_URL/openapi/v1.json..."
  mkdir -p "$(dirname "$SPEC")"
  curl -f -s "$SERVER_URL/openapi/v1.json" -o "$SPEC"
  echo "  -> $SPEC"
else
  require_file "$SPEC" "OpenAPI spec"
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

if [[ "$GENERATE_ADMIN" == "1" ]]; then
  if [[ ! -d "$ADMIN_PATH" ]]; then
    echo "Admin path not found: $ADMIN_PATH" >&2
    echo "Pass --admin-path PATH or --skip-admin." >&2
    exit 1
  fi

  ADMIN_OUTPUT="$ADMIN_PATH/src/generated/api.ts"
  mkdir -p "$(dirname "$ADMIN_OUTPUT")"

  echo ""
  echo "Generating TypeScript types for admin UI..."
  if [[ -f "$ADMIN_PATH/package.json" ]] && command -v npm >/dev/null 2>&1; then
    npm --prefix "$ADMIN_PATH" exec --yes -- openapi-typescript "$SPEC" -o "$ADMIN_OUTPUT"
  else
    npx --yes openapi-typescript "$SPEC" -o "$ADMIN_OUTPUT"
  fi
  echo "  -> $ADMIN_OUTPUT"
fi

echo ""
echo "Done. Review the changes and commit the generated files."
