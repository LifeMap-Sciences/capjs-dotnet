# Builds + packs CapNet to ./packages/ as a local feed.
# Consume from Auth.Web (or any other .NET project) by adding ./packages as a NuGet source.
#
# Usage:
#   ./scripts/pack-local.ps1                 # version comes from the .csproj
#   ./scripts/pack-local.ps1 -Version 0.1.1-dev

param(
    [string]$Version,
    [string]$Output = "$PSScriptRoot/../packages",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot/.."

Push-Location $repoRoot
try {
    if ($Version) {
        Write-Host "Packing CapNet $Version → $Output"
        dotnet pack CapNet/CapNet.csproj --configuration $Configuration --nologo -o $Output /p:PackageVersion=$Version
    } else {
        Write-Host "Packing CapNet (version from csproj) → $Output"
        dotnet pack CapNet/CapNet.csproj --configuration $Configuration --nologo -o $Output
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed (exit $LASTEXITCODE)" }

    Write-Host ""
    Write-Host "Packages in $Output :"
    Get-ChildItem $Output -Filter *.nupkg | Sort-Object LastWriteTime -Descending | Select-Object -First 5 Name, Length, LastWriteTime | Format-Table
} finally {
    Pop-Location
}
