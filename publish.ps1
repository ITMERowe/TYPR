# Builds the release TYPR.exe into publish\win-x64 (settings: Properties\PublishProfiles\win-x64.pubxml).
#   .\publish.ps1                 version 0.0.0
#   .\publish.ps1 -Version 1.2.3  stamps that version into the exe
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'TYPR.csproj'
$arguments = @('publish', $project, '-p:PublishProfile=win-x64')
if ($Version) { $arguments += "-p:Version=$Version" }

Write-Host 'Publishing TYPR...'
dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to: $(Join-Path $PSScriptRoot 'publish\win-x64\TYPR.exe')"
