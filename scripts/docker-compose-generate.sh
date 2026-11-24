#!/usr/bin/env bash
# Regenerates docker/docker-compose.yaml from the Aspire AppHost.
#
# The compose file is generated, never hand-written — the AppHost
# (BookmarkFeeder.AppHost/Program.cs) is the single source of truth for the topology, service
# wiring, image variables, ports, restart policies and healthchecks. Edit the AppHost, run this,
# commit the result.
#
# Also refreshes docker/.env, which only ever receives KEYS: `aspire publish` writes the env file
# with Save(includeValues: false). Any values you have already filled in are preserved
# (EnvFile.Add uses onlyIfMissing), so this is safe to re-run against a configured .env.
#
# docker/.env is gitignored; the compose and the .env.*.template files are committed.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# Paths stay RELATIVE and every command runs from the repo root: the aspire CLI is a Windows
# executable and cannot read Git Bash's /d/repos/... paths.
cd "$REPO_ROOT"
APPHOST="BookmarkFeeder.AppHost/BookmarkFeeder.AppHost.csproj"
OUTPUT_DIR="docker"
COMPOSE_FILE="$OUTPUT_DIR/docker-compose.yaml"

check=0

usage() {
  cat <<EOF
Usage: $(basename "$0") [--check]

Regenerate docker/docker-compose.yaml from the Aspire AppHost.

Options:
  --check     Fail if the committed compose is out of date with the AppHost; change nothing
  -h, --help  Show this message
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --check) check=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

# The aspire CLI is a .cmd shim on Windows, which Git Bash cannot exec directly.
aspire_cli() {
  if command -v aspire >/dev/null 2>&1; then
    aspire "$@"
  elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command "aspire $*"
  else
    echo "aspire CLI not found. Install with: dotnet tool install -g aspire.cli" >&2
    exit 1
  fi
}

# A running AppHost holds BookmarkFeeder.WebApi.exe open; publish builds the AppHost and fails on
# the file lock with an error that does not mention the AppHost at all.
if command -v tasklist >/dev/null 2>&1; then
  if tasklist //FI "IMAGENAME eq BookmarkFeeder.WebApi.exe" 2>/dev/null | grep -qi "BookmarkFeeder"; then
    echo "Stop the Aspire AppHost first — it locks the build output." >&2
    exit 1
  fi
fi

if [[ $check -eq 1 ]]; then
  # Relative, for the same Windows-path reason as above.
  target=".aspire-compose-check"
  rm -rf "$target"
  trap 'rm -rf "$REPO_ROOT/.aspire-compose-check"' EXIT
else
  target="$OUTPUT_DIR"
fi

echo "Generating compose from $(basename "$APPHOST") -> $target"
aspire_cli publish --apphost "$APPHOST" -o "$target" --non-interactive --nologo

if [[ $check -eq 1 ]]; then
  [[ -f "$COMPOSE_FILE" ]] || { echo "No committed compose at $COMPOSE_FILE" >&2; exit 1; }
  if ! diff -u "$COMPOSE_FILE" "$target/docker-compose.yaml"; then
    echo >&2
    echo "docker/docker-compose.yaml is out of date with the AppHost. Run $(basename "$0") and commit." >&2
    exit 1
  fi
  echo "compose is up to date with the AppHost"
  exit 0
fi

echo "Wrote $COMPOSE_FILE"
echo "docker/.env received any new KEYS; existing values were preserved."
echo
echo "Next:"
echo "  cp docker/.env.local.template docker/.env    # first time only"
echo "  docker compose -f docker/docker-compose.yaml up -d"
