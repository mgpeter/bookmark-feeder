#!/usr/bin/env bash
# Bumps VERSION, builds the three images, and pushes them to Docker Hub.
#
# VERSION holds the last released version; this bumps it, builds via docker-build.sh, and pushes
# :<version> and :latest for webapi, gateway and web. VERSION is written only after the push
# succeeds, so a failure leaves it untouched and re-running retries the same number.
#
# Requires `docker login -u mgpeter` first — this script will not handle credentials.
# --no-push builds without releasing, and therefore does not bump VERSION.
# --current releases VERSION as it stands, for when the bump was already committed by hand.
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
Usage: $(basename "$0") [--patch | --minor | --major | --current] [--no-push] [--dry-run]

Bump VERSION, build the images, tag :latest and :<version>, push to Docker Hub.

Options:
  --patch    Bump patch (default)
  --minor    Bump minor (resets patch)
  --major    Bump major (resets minor and patch)
  --current  Release VERSION unchanged (it was already bumped and committed)
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
    --current) bump=current; bump_set=$((bump_set + 1)) ;;
    --no-push) no_push=1 ;;
    --dry-run) dry_run=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if [[ $bump_set -gt 1 ]]; then
  echo "Pass at most one of --patch / --minor / --major / --current" >&2
  exit 2
fi

[[ -f "$VERSION_FILE" ]] || { echo "VERSION file not found at $VERSION_FILE" >&2; exit 1; }

# Images are built from the working tree, so uncommitted changes would ship under a version
# number no commit describes. A --no-push or --dry-run build is fine either way.
if [[ $no_push -eq 0 && $dry_run -eq 0 ]] && [[ -n "$(git -C "$REPO_ROOT" status --porcelain)" ]]; then
  echo "Uncommitted changes in $REPO_ROOT - commit or stash them before releasing." >&2
  git -C "$REPO_ROOT" status --short >&2
  exit 1
fi

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
  current) ;;
esac

new="${major}.${minor}.${patch}"

echo "bookmarkfeeder: ${current} -> ${new} (${bump})"

if [[ $dry_run -eq 1 ]]; then
  echo "(dry-run) no changes made"
  exit 0
fi

# There is deliberately no "are you logged in?" pre-check. Docker Desktop stores credentials in
# the OS credential manager (credsStore), so `docker info` reports no Username and config.json's
# auths entries are empty — every cheap check gives false negatives and blocks real releases.
# `docker push` says "denied: requested access to the resource is denied" clearly enough, and
# VERSION is only written once the push succeeds, so a failed login costs a rebuild, not a
# corrupted version.

"$SCRIPT_DIR/docker-build.sh" --version "$new"

if [[ $no_push -eq 1 ]]; then
  # VERSION is not bumped: nothing was released, so the last released version has not changed.
  echo "Built (push skipped, VERSION left at ${current}): $new"
  exit 0
fi

# Exactly this release's two tags. --all-tags would also publish every other tag the local image
# happens to have - a `docker-build --version 1.2.3-test`, or a --no-push build of a version that
# was never released.
for service in "${SERVICES[@]}"; do
  for tag in "$new" latest; do
    echo "Pushing $NAMESPACE/bookmarkfeeder-$service:$tag ..."
    docker push "$NAMESPACE/bookmarkfeeder-$service:$tag"
  done
done

# Only now: VERSION records what was actually released. A failed push leaves it untouched, so
# re-running the same command retries the same number instead of skipping one.
printf '%s\n' "$new" > "$VERSION_FILE"

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
