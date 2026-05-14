# Reads .env for NUGET_API_KEY, packs CapNet, and pushes to nuget.org.
# Prefer CI for releases (tag-driven) — this script is for emergency manual pushes.
#
# Usage:
#   ./scripts/push-nuget.ps1                # uses version from the .csproj
#   ./scripts/push-nuget.ps1 -Version 0.1.1 # explicit version

param(
    [string]$Version,
    [string]$Configuration = "Release",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot/.."
$envFile = Join-Path $repoRoot ".env"

if (-not (Test-Path $envFile)) {
    throw ".env not found at $envFile. Copy .env.example to .env and fill in NUGET_API_KEY."
}

# Load .env into the current process. Lines that aren't KEY=VALUE are ignored.
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith('#') -and $line.Contains('=')) {
        $kv = $line -split '=', 2
        $key = $kv[0].Trim()
        $value = $kv[1].Trim().Trim('"').Trim("'")
        Set-Item -Path "Env:$key" -Value $value
    }
}

if (-not $env:NUGET_API_KEY) {
    throw "NUGET_API_KEY is empty in .env"
}

$source = if ($env:NUGET_SOURCE) { $env:NUGET_SOURCE } else { "https://api.nuget.org/v3/index.json" }
$packages = Join-Path $repoRoot "packages"

# Pack
& "$PSScriptRoot/pack-local.ps1" -Version $Version -Output $packages -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "pack-local.ps1 failed" }

# Find the package we just built (most recent .nupkg)
$pkg = Get-ChildItem $packages -Filter *.nupkg | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $pkg) { throw "No .nupkg found in $packages" }

Write-Host ""
Write-Host "Will push $($pkg.Name) → $source"

if ($DryRun) {
    Write-Host "DryRun: skipping actual push."
    return
}

$confirm = Read-Host "Continue? [y/N]"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Aborted."
    return
}

dotnet nuget push $pkg.FullName --source $source --api-key $env:NUGET_API_KEY --skip-duplicate
if ($LASTEXITCODE -ne 0) { throw "dotnet nuget push failed (exit $LASTEXITCODE)" }
Write-Host "Pushed."
