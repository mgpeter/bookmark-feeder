#!/usr/bin/env bash
# Bumps VERSION, builds the three images, and pushes them to Docker Hub.
#
# VERSION holds the last released version; this bumps it, builds via docker-build.sh, and pushes
# :<version> and :latest for webapi, gateway and web.
#
# Requires `docker login -u mgpeter` first — this script will not handle credentials.
#
# Deploy the NAS by the version tag, never :latest, so "which build is running?" always has an
# answer. The .env lines to paste are printed at the end.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION_FILE="$REPO_ROOT/VERSION"
NAMESPACE="mgpeter"
SERVICES=(webapi gateway web)

bump=patch
no_push=0
dry_run=0
bump_set=0

usage() {
  cat <<EOF
Usage: $(basename "$0") [--patch | --minor | --major] [--no-push] [--dry-run]

Bump VERSION, build the images, tag :latest and :<version>, push to Docker Hub.

Options:
  --patch    Bump patch (default)
  --minor    Bump minor (resets patch)
  --major    Bump major (resets minor and patch)
  --no-push  Build and tag locally; skip 'docker push'
  --dry-run  Print the version bump and exit without writing/building/pushing
  -h, --help Show this message
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --patch) bump=patch; bump_set=$((bump_set + 1)) ;;
    --minor) bump=minor; bump_set=$((bump_set + 1)) ;;
    --major) bump=major; bump_set=$((bump_set + 1)) ;;
    --no-push) no_push=1 ;;
    --dry-run) dry_run=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if [[ $bump_set -gt 1 ]]; then
  echo "Pass at most one of --patch / --minor / --major" >&2
  exit 2
fi

[[ -f "$VERSION_FILE" ]] || { echo "VERSION file not found at $VERSION_FILE" >&2; exit 1; }

current="$(tr -d '[:space:]' < "$VERSION_FILE")"
if [[ ! "$current" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "VERSION file does not contain a valid semver: '$current'" >&2
  exit 1
fi

IFS='.' read -r major minor patch <<<"$current"

case "$bump" in
  patch) patch=$((patch + 1)) ;;
  minor) minor=$((minor + 1)); patch=0 ;;
  major) major=$((major + 1)); minor=0; patch=0 ;;
esac

new="${major}.${minor}.${patch}"

echo "bookmarkfeeder: ${current} -> ${new} (${bump})"

if [[ $dry_run -eq 1 ]]; then
  echo "(dry-run) no changes made"
  exit 0
fi

# Fail before building rather than after: a push that 401s having spent minutes building is a
# waste of everyone's time.
if [[ $no_push -eq 0 ]]; then
  if ! docker system info 2>/dev/null | grep -qE '^\s*Username:'; then
    echo "Not logged in to Docker Hub. Run: docker login -u $NAMESPACE" >&2
    exit 1
  fi
fi

printf '%s\n' "$new" > "$VERSION_FILE"

"$SCRIPT_DIR/docker-build.sh" --version "$new"

if [[ $no_push -eq 1 ]]; then
  echo "Built (push skipped): $new"
  exit 0
fi

for service in "${SERVICES[@]}"; do
  echo "Pushing $NAMESPACE/bookmarkfeeder-$service (all tags) ..."
  docker push --all-tags "$NAMESPACE/bookmarkfeeder-$service"
done

echo "Released $new"
echo
echo "On the NAS, set these in .env and run: docker compose up -d"
for service in "${SERVICES[@]}"; do
  case "$service" in
    webapi)  variable="WEBAPI_IMAGE" ;;
    gateway) variable="GATEWAY_IMAGE" ;;
    web)     variable="WEB_IMAGE" ;;
  esac
  echo "  $variable=docker.io/$NAMESPACE/bookmarkfeeder-$service:$new"
done
