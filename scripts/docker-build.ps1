#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the three BookmarkFeeder container images locally. No version bump, no push.

.DESCRIPTION
    webapi and gateway are built with the .NET SDK's container support (no Dockerfile needed);
    web is built from its own Dockerfile (Vite build -> static nginx). All target linux/amd64,
    which is what the NAS runs.

    Use docker-release.ps1 to bump the version and push. This script is for building and
    testing locally.

.EXAMPLE
    ./scripts/docker-build.ps1
    Builds every image at the version in ./VERSION.

.EXAMPLE
    ./scripts/docker-build.ps1 -Version 1.2.3-test
    Builds with an explicit tag, ignoring ./VERSION.
#>
[CmdletBinding()]
param(
    # Tag to build. Defaults to the contents of ./VERSION.
    [string]$Version,

    # Skip tagging :latest alongside the version.
    [switch]$NoLatest
)

$ErrorActionPreference = 'Stop'

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $repoRoot 'VERSION'
$namespace   = 'mgpeter'

if (-not $Version) {
    if (-not (Test-Path $versionFile)) { throw "VERSION file not found at $versionFile" }
    $Version = (Get-Content -Raw -Path $versionFile).Trim()
}
if (-not $Version) { throw 'No version to build' }

# A running AppHost holds BookmarkFeeder.WebApi.exe open and the build fails with a confusing
# file-lock error. Say so up front rather than letting MSBuild explain it badly.
$running = Get-Process -Name 'BookmarkFeeder*' -ErrorAction SilentlyContinue
if ($running) {
    throw "Stop the Aspire AppHost first — it locks the build output. Running: $($running.Name -join ', ')"
}

& docker version --format '{{.Server.Os}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker daemon is not reachable' }

Write-Host "Building BookmarkFeeder $Version (linux/amd64)" -ForegroundColor Cyan

# --- webapi + gateway: .NET SDK container publish -------------------------------------------
# NOTE: -t:PublishContainer, never /t:. Git Bash rewrites /t:... into a Windows path and MSBuild
# then reports the baffling "MSB1008: Only one project can be specified".
$dotnetImages = @(
    @{ Name = 'webapi';  Project = 'BookmarkFeeder.WebApi/BookmarkFeeder.WebApi.csproj' }
    @{ Name = 'gateway'; Project = 'BookmarkFeeder.Gateway/BookmarkFeeder.Gateway.csproj' }
)

foreach ($image in $dotnetImages) {
    $repository = "$namespace/bookmarkfeeder-$($image.Name)"
    Write-Host "  -> $repository`:$Version" -ForegroundColor DarkGray

    & dotnet publish (Join-Path $repoRoot $image.Project) `
        -c Release --os linux --arch x64 -t:PublishContainer `
        -p:ContainerRepository=$repository `
        -p:ContainerImageTag=$Version `
        --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($image.Name)" }
}

# --- web: its own Dockerfile (node build -> nginx) ------------------------------------------
$webRepository = "$namespace/bookmarkfeeder-web"
Write-Host "  -> $webRepository`:$Version" -ForegroundColor DarkGray
& docker buildx build --platform linux/amd64 `
    -t "$webRepository`:$Version" --load (Join-Path $repoRoot 'BookmarkFeeder.Web')
if ($LASTEXITCODE -ne 0) { throw 'docker buildx build failed for web' }

# --- :latest ---------------------------------------------------------------------------------
if (-not $NoLatest) {
    foreach ($name in @('webapi', 'gateway', 'web')) {
        & docker tag "$namespace/bookmarkfeeder-$name`:$Version" "$namespace/bookmarkfeeder-$name`:latest"
        if ($LASTEXITCODE -ne 0) { throw "docker tag failed for $name" }
    }
}

Write-Host "Built $Version" -ForegroundColor Green
& docker images --filter "reference=$namespace/bookmarkfeeder-*:$Version" --format '  {{.Repository}}:{{.Tag}}  {{.Size}}'
