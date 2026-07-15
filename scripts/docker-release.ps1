#Requires -Version 7.0
<#
.SYNOPSIS
    Bumps VERSION, builds the three images, and pushes them to Docker Hub.

.DESCRIPTION
    VERSION holds the last released version; this bumps it, builds via docker-build.ps1, and
    pushes :<version> and :latest for webapi, gateway and web.

    Requires `docker login -u mgpeter` first — this script will not handle credentials.

    Deploy the NAS by the version tag, never :latest, so "which build is running?" always has
    an answer. The .env lines to paste are printed at the end.

.EXAMPLE
    ./scripts/docker-release.ps1 -Minor
    0.0.0 -> 0.1.0, build, push.

.EXAMPLE
    ./scripts/docker-release.ps1 -DryRun
    Print the bump and stop.
#>
[CmdletBinding(DefaultParameterSetName = 'Patch')]
param(
    [Parameter(ParameterSetName = 'Patch')]
    [switch]$Patch,

    [Parameter(ParameterSetName = 'Minor')]
    [switch]$Minor,

    [Parameter(ParameterSetName = 'Major')]
    [switch]$Major,

    # Bump and build, but skip the push.
    [switch]$NoPush,

    # Print the version bump and exit without writing, building or pushing.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $repoRoot 'VERSION'
$namespace   = 'mgpeter'
$services    = @('webapi', 'gateway', 'web')

$bump = if ($Major) { 'major' } elseif ($Minor) { 'minor' } else { 'patch' }

if (-not (Test-Path $versionFile)) { throw "VERSION file not found at $versionFile" }

$current = (Get-Content -Raw -Path $versionFile).Trim()
if ($current -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION file does not contain a valid semver: '$current'"
}

$majorN, $minorN, $patchN = $current.Split('.') | ForEach-Object { [int]$_ }

switch ($bump) {
    'patch' { $patchN += 1 }
    'minor' { $minorN += 1; $patchN = 0 }
    'major' { $majorN += 1; $minorN = 0; $patchN = 0 }
}

$new = "$majorN.$minorN.$patchN"

Write-Host "bookmarkfeeder: $current -> $new ($bump)" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host '(dry-run) no changes made'
    return
}

# Fail before building rather than after: a push that 401s having spent minutes building is
# a waste of everyone's time.
if (-not $NoPush) {
    $loggedIn = & docker system info 2>$null | Select-String -Pattern '^\s*Username:'
    if (-not $loggedIn) {
        throw "Not logged in to Docker Hub. Run: docker login -u $namespace"
    }
}

Set-Content -Path $versionFile -Value $new

& (Join-Path $PSScriptRoot 'docker-build.ps1') -Version $new
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

if ($NoPush) {
    Write-Host "Built (push skipped): $new" -ForegroundColor Yellow
    return
}

foreach ($service in $services) {
    $image = "$namespace/bookmarkfeeder-$service"
    Write-Host "Pushing $image (all tags) ..." -ForegroundColor DarkGray
    & docker push --all-tags $image
    if ($LASTEXITCODE -ne 0) { throw "docker push failed for $service" }
}

Write-Host "Released $new" -ForegroundColor Green
Write-Host ''
Write-Host 'On the NAS, set these in .env and run: docker compose up -d' -ForegroundColor Cyan
foreach ($service in $services) {
    $variable = if ($service -eq 'webapi') { 'WEBAPI_IMAGE' } else { "$($service.ToUpper())_IMAGE" }
    Write-Host "  $variable=docker.io/$namespace/bookmarkfeeder-$service`:$new"
}
