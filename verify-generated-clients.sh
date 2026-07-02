#!/usr/bin/env bash
# Regenerates API clients and fails if checked-in generated files are stale.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADMIN_PATH="${ADMIN_PATH:-"$SCRIPT_DIR/nona-config-admin"}"
CHECK_ADMIN=1
GENERATOR_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --admin-path)
      ADMIN_PATH="$2"
      GENERATOR_ARGS+=("$1" "$2")
      shift 2
      ;;
    --skip-admin)
      CHECK_ADMIN=0
      GENERATOR_ARGS+=("$1")
      shift
      ;;
    *)
      GENERATOR_ARGS+=("$1")
      shift
      ;;
  esac
done

bash "$SCRIPT_DIR/generate-clients.sh" "${GENERATOR_ARGS[@]}"

if [[ -d "$ADMIN_PATH" ]]; then
  ADMIN_PATH="$(cd "$ADMIN_PATH" && pwd)"
fi

failed=0

check_clean() {
  local label=$1
  local repo=$2
  shift 2

  local status
  status="$(git -C "$repo" status --porcelain -- "$@")"
  if [[ -n "$status" ]]; then
    echo ""
    echo "::error::Generated $label files are out of date. Run the generator and commit the changes."
    git -C "$repo" status --short -- "$@"
    git -C "$repo" diff -- "$@" || true
    failed=1
  fi
}

check_clean "backend client" "$SCRIPT_DIR" \
  cli/src/Nona.Cli/Core/Generated \
  migrator/src/ConfigMigrator.Core/Generated

if [[ "$CHECK_ADMIN" == "1" ]]; then
  if [[ ! -d "$ADMIN_PATH/.git" ]]; then
    admin_output="$ADMIN_PATH/src/generated/api.ts"
    if [[ "$admin_output" == "$SCRIPT_DIR"/* ]]; then
      check_clean "admin client" "$SCRIPT_DIR" "${admin_output#"$SCRIPT_DIR"/}"
    else
      echo ""
      echo "::warning::Skipping admin generated file check because $ADMIN_PATH is not inside this git checkout."
    fi
  else
    check_clean "admin client" "$ADMIN_PATH" src/generated/api.ts
  fi
fi

if [[ "$failed" != "0" ]]; then
  exit 1
fi

echo ""
echo "Generated clients are up to date."
