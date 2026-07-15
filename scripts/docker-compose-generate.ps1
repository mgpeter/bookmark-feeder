#Requires -Version 7.0
<#
.SYNOPSIS
    Regenerates docker/docker-compose.yaml from the Aspire AppHost.

.DESCRIPTION
    The compose file is generated, never hand-written — the AppHost
    (BookmarkFeeder.AppHost/Program.cs) is the single source of truth for the topology, service
    wiring, image variables, ports, restart policies and healthchecks. Edit the AppHost, run this,
    commit the result.

    Also refreshes docker/.env, which only ever receives KEYS: `aspire publish` writes the env file
    with Save(includeValues: false). Any values you have already filled in are preserved
    (EnvFile.Add uses onlyIfMissing), so this is safe to re-run against a configured .env.

    docker/.env is gitignored; the compose and the .env.*.template files are committed.

.EXAMPLE
    ./scripts/docker-compose-generate.ps1
    Regenerate docker/docker-compose.yaml.

.EXAMPLE
    ./scripts/docker-compose-generate.ps1 -Check
    Fail if the committed compose is out of date with the AppHost. Changes nothing.
#>
[CmdletBinding()]
param(
    # Regenerate to a temp dir and diff against the committed compose instead of overwriting it.
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appHost     = Join-Path $repoRoot 'BookmarkFeeder.AppHost/BookmarkFeeder.AppHost.csproj'
$outputDir   = Join-Path $repoRoot 'docker'
$composeFile = Join-Path $outputDir 'docker-compose.yaml'

if (-not (Get-Command aspire -ErrorAction SilentlyContinue)) {
    throw 'aspire CLI not found. Install with: dotnet tool install -g aspire.cli'
}

# A running AppHost holds BookmarkFeeder.WebApi.exe open; publish builds the AppHost and fails on
# the file lock with an error that does not mention the AppHost at all.
$running = Get-Process -Name 'BookmarkFeeder*' -ErrorAction SilentlyContinue
if ($running) {
    throw "Stop the Aspire AppHost first — it locks the build output. Running: $($running.Name -join ', ')"
}

$target = if ($Check) { Join-Path ([System.IO.Path]::GetTempPath()) "bookmarkfeeder-compose-$(Get-Random)" } else { $outputDir }

Write-Host "Generating compose from $([System.IO.Path]::GetFileName($appHost)) -> $target" -ForegroundColor Cyan

& aspire publish --apphost $appHost -o $target --non-interactive --nologo
if ($LASTEXITCODE -ne 0) { throw 'aspire publish failed' }

if ($Check) {
    $generated = Join-Path $target 'docker-compose.yaml'
    if (-not (Test-Path $composeFile)) { throw "No committed compose at $composeFile" }

    $difference = Compare-Object (Get-Content $composeFile) (Get-Content $generated)
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue

    if ($difference) {
        $difference | Format-Table -AutoSize | Out-String | Write-Host
        throw 'docker/docker-compose.yaml is out of date with the AppHost. Run ./scripts/docker-compose-generate.ps1 and commit.'
    }

    Write-Host 'compose is up to date with the AppHost' -ForegroundColor Green
    return
}

Write-Host "Wrote $composeFile" -ForegroundColor Green
Write-Host 'docker/.env received any new KEYS; existing values were preserved.' -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Next:' -ForegroundColor Cyan
Write-Host '  cp docker/.env.local.template docker/.env    # first time only'
Write-Host '  docker compose -f docker/docker-compose.yaml up -d'
