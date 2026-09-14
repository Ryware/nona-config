#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <version> <source-ref> <target-sha>" >&2
  exit 2
fi

version="$1"
source_ref="$2"
target_sha="$3"

if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Expected a stable version such as 4.0.0, without a prefix." >&2
  exit 1
fi

if [[ "$source_ref" != "refs/heads/master" ]]; then
  echo "Releases must be started from the master branch." >&2
  exit 1
fi

git fetch --no-tags origin refs/heads/master
master_sha="$(git rev-parse 'FETCH_HEAD^{commit}')"
target_sha="$(git rev-parse "${target_sha}^{commit}")"
if [[ "$target_sha" != "$master_sha" ]]; then
  echo "The selected commit is not the current origin/master commit." >&2
  exit 1
fi

if git ls-remote --exit-code --refs origin "refs/tags/$version" >/dev/null 2>&1; then
  git fetch --no-tags origin "refs/tags/$version"
  existing_sha="$(git rev-parse 'FETCH_HEAD^{commit}')"
  if [[ "$existing_sha" != "$target_sha" ]]; then
    echo "Tag $version already points to a different commit." >&2
    exit 1
  fi
  result="existing"
else
  git tag "$version" "$target_sha"
  git push origin "refs/tags/$version"
  result="created"
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "version=$version"
    echo "checkout_ref=$target_sha"
    echo "tag_result=$result"
  } >> "$GITHUB_OUTPUT"
fi

echo "Release tag $version ($result): $target_sha"
