#!/usr/bin/env bash
# Regenerates API clients and fails if checked-in generated files are stale.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADMIN_PATH="${ADMIN_PATH:-"$SCRIPT_DIR/admin"}"
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

if [[ -d "$ADMIN_PATH" ]]; then
  ADMIN_PATH="$(cd "$ADMIN_PATH" && pwd)"
fi

failed=0

status_for() {
  local repo=$1
  shift

  git -C "$repo" status --porcelain -- "$@"
}

check_unchanged() {
  local label=$1
  local repo=$2
  local before=$3
  shift 3

  local status
  status="$(status_for "$repo" "$@")"
  if [[ "$status" != "$before" ]]; then
    echo ""
    echo "::error::Generated $label files are out of date. Run the generator and commit the changes."
    git -C "$repo" status --short -- "$@"
    git -C "$repo" diff -- "$@" || true
    failed=1
  fi
}

backend_paths=(
  cli/src/Nona.Cli/Core/Generated
  migrator/src/ConfigMigrator.Core/Generated
)
backend_before="$(status_for "$SCRIPT_DIR" "${backend_paths[@]}")"

if [[ "$CHECK_ADMIN" == "1" ]]; then
  if [[ ! -d "$ADMIN_PATH/.git" ]]; then
    admin_output="$ADMIN_PATH/src/generated/api.ts"
    if [[ "$admin_output" == "$SCRIPT_DIR"/* ]]; then
      admin_repo="$SCRIPT_DIR"
      admin_paths=("${admin_output#"$SCRIPT_DIR"/}")
      admin_before="$(status_for "$admin_repo" "${admin_paths[@]}")"
    else
      admin_repo=""
      admin_paths=()
      admin_before=""
    fi
  else
    admin_repo="$ADMIN_PATH"
    admin_paths=(src/generated/api.ts)
    admin_before="$(status_for "$admin_repo" "${admin_paths[@]}")"
  fi
fi

bash "$SCRIPT_DIR/generate-clients.sh" "${GENERATOR_ARGS[@]}"

check_unchanged "backend client" "$SCRIPT_DIR" "$backend_before" "${backend_paths[@]}"

if [[ "$CHECK_ADMIN" == "1" ]]; then
  if [[ "${admin_repo:-}" != "" ]]; then
    check_unchanged "admin client" "$admin_repo" "$admin_before" "${admin_paths[@]}"
  else
    echo ""
    echo "::warning::Skipping admin generated file check because $ADMIN_PATH is not inside this git checkout."
  fi
fi

if [[ "$failed" != "0" ]]; then
  exit 1
fi

echo ""
echo "Generated clients are up to date."
