#!/usr/bin/env bash
# Builds the three BookmarkFeeder container images locally. No version bump, no push.
#
# webapi and gateway are built with the .NET SDK's container support (no Dockerfile needed);
# web is built from its own Dockerfile (Vite build -> static nginx). All target linux/amd64,
# which is what the NAS runs.
#
# Use docker-release.sh to bump the version and push. This script is for building and testing
# locally.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION_FILE="$REPO_ROOT/VERSION"
NAMESPACE="mgpeter"

version=""
no_latest=0

usage() {
  cat <<EOF
Usage: $(basename "$0") [--version <tag>] [--no-latest]

Build the webapi, gateway and web images for linux/amd64.

Options:
  --version <tag>  Tag to build (default: contents of ./VERSION)
  --no-latest      Do not also tag :latest
  -h, --help       Show this message
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) version="${2:-}"; shift ;;
    --no-latest) no_latest=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if [[ -z "$version" ]]; then
  [[ -f "$VERSION_FILE" ]] || { echo "VERSION file not found at $VERSION_FILE" >&2; exit 1; }
  version="$(tr -d '[:space:]' < "$VERSION_FILE")"
fi
[[ -n "$version" ]] || { echo "No version to build" >&2; exit 1; }

docker version --format '{{.Server.Os}}' >/dev/null 2>&1 || {
  echo "Docker daemon is not reachable" >&2; exit 1
}

# A running Aspire AppHost holds BookmarkFeeder.WebApi.exe open and the build fails with a
# confusing file-lock error. Say so up front (Windows only; harmless elsewhere).
if command -v tasklist >/dev/null 2>&1; then
  if tasklist //FI "IMAGENAME eq BookmarkFeeder.WebApi.exe" 2>/dev/null | grep -qi "BookmarkFeeder"; then
    echo "Stop the Aspire AppHost first — it locks the build output." >&2
    exit 1
  fi
fi

echo "Building BookmarkFeeder $version (linux/amd64)"

# --- webapi + gateway: .NET SDK container publish --------------------------------------------
# NOTE: -t:PublishContainer, never /t:. Git Bash rewrites /t:... into a Windows path and MSBuild
# then reports the baffling "MSB1008: Only one project can be specified".
build_dotnet_image() {
  local name="$1" project="$2"
  local repository="$NAMESPACE/bookmarkfeeder-$name"
  echo "  -> $repository:$version"
  dotnet publish "$REPO_ROOT/$project" \
    -c Release --os linux --arch x64 -t:PublishContainer \
    -p:ContainerRepository="$repository" \
    -p:ContainerImageTag="$version" \
    --nologo -v quiet
}

build_dotnet_image webapi  "BookmarkFeeder.WebApi/BookmarkFeeder.WebApi.csproj"
build_dotnet_image gateway "BookmarkFeeder.Gateway/BookmarkFeeder.Gateway.csproj"

# --- web: its own Dockerfile (node build -> nginx) -------------------------------------------
echo "  -> $NAMESPACE/bookmarkfeeder-web:$version"
docker buildx build --platform linux/amd64 \
  -t "$NAMESPACE/bookmarkfeeder-web:$version" --load "$REPO_ROOT/BookmarkFeeder.Web"

# --- :latest ---------------------------------------------------------------------------------
if [[ $no_latest -eq 0 ]]; then
  for name in webapi gateway web; do
    docker tag "$NAMESPACE/bookmarkfeeder-$name:$version" "$NAMESPACE/bookmarkfeeder-$name:latest"
  done
fi

echo "Built $version"
docker images --filter "reference=$NAMESPACE/bookmarkfeeder-*:$version" \
  --format '  {{.Repository}}:{{.Tag}}  {{.Size}}'
