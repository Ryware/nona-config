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

SPEC="$SPEC" python3 - <<'PY'
import json
import os

spec_path = os.environ["SPEC"]
with open(spec_path, encoding="utf-8") as spec_file:
    spec = json.load(spec_file)

schemas = spec.setdefault("components", {}).setdefault("schemas", {})

config_entry = schemas.get("ConfigEntryDto")
if config_entry is not None:
    config_entry.setdefault("properties", {}).setdefault(
        "activeVersion",
        {"type": "integer", "format": "int32"},
    )
    required = config_entry.setdefault("required", [])
    if "activeVersion" not in required:
        insert_at = required.index("scope") + 1 if "scope" in required else len(required)
        required.insert(insert_at, "activeVersion")

schemas.setdefault(
    "ConfigEntryVersionDto",
    {
        "required": [
            "project",
            "environment",
            "key",
            "version",
            "value",
            "contentType",
            "scope",
            "createdAt",
            "actor",
        ],
        "type": "object",
        "properties": {
            "project": {"type": "string"},
            "environment": {"type": "string"},
            "key": {"type": "string"},
            "version": {"type": "integer", "format": "int32"},
            "value": {"type": "string"},
            "contentType": {"type": "string"},
            "scope": {"type": "string"},
            "createdAt": {"type": "string", "format": "date-time"},
            "actor": {"type": "string", "nullable": True},
        },
    },
)

schemas.setdefault(
    "RollbackConfigEntryRequest",
    {
        "required": ["version"],
        "type": "object",
        "properties": {
            "version": {"type": "integer", "format": "int32"},
        },
    },
)

paths = spec.setdefault("paths", {})
config_entry_path = "/admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}"
history_path = f"{config_entry_path}/history"
rollback_path = f"{config_entry_path}/rollback"

base_parameters = [
    {"name": "projectId", "in": "path", "required": True, "schema": {"type": "string"}},
    {"name": "environmentName", "in": "path", "required": True, "schema": {"type": "string"}},
    {"name": "key", "in": "path", "required": True, "schema": {"type": "string"}},
]

def json_content(schema):
    return {
        media_type: {"schema": schema}
        for media_type in ("text/plain", "application/json", "text/json")
    }

def json_request_body(schema):
    return {
        "content": {
            "application/json": {"schema": schema},
            "text/json": {"schema": schema},
            "application/*+json": {"schema": schema},
        },
        "required": True,
    }

problem = {"$ref": "#/components/schemas/ProblemDetails"}
config_entry_ref = {"$ref": "#/components/schemas/ConfigEntryDto"}
version_ref = {"$ref": "#/components/schemas/ConfigEntryVersionDto"}
rollback_ref = {"$ref": "#/components/schemas/RollbackConfigEntryRequest"}

paths.setdefault(history_path, {})["get"] = {
    "tags": ["AdminConfigEntries"],
    "parameters": base_parameters,
    "responses": {
        "200": {
            "description": "OK",
            "content": json_content({"type": "array", "items": version_ref}),
        },
        "404": {
            "description": "Not Found",
            "content": json_content(problem),
        },
    },
    "security": [{"Bearer": []}],
}

paths.setdefault(rollback_path, {})["post"] = {
    "tags": ["AdminConfigEntries"],
    "parameters": base_parameters,
    "requestBody": json_request_body(rollback_ref),
    "responses": {
        "200": {
            "description": "OK",
            "content": json_content(config_entry_ref),
        },
        "400": {
            "description": "Bad Request",
            "content": json_content(problem),
        },
        "404": {
            "description": "Not Found",
            "content": json_content(problem),
        },
    },
    "security": [{"Bearer": []}],
}

with open(spec_path, "w", encoding="utf-8") as spec_file:
    json.dump(spec, spec_file, indent=2)
    spec_file.write("\n")
PY

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
