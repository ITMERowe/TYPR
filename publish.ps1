$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'TYPR.csproj'
$output = Join-Path $PSScriptRoot 'publish\win-x64'

Write-Host 'Publishing TYPR...'

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    -o $output

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to: $output"
