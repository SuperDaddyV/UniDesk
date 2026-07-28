$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $json = dotnet list UniDesk.sln package --vulnerable --include-transitive --no-restore --format json --output-version 1
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet vulnerability audit failed with exit code $LASTEXITCODE."
    }

    $null = $json | ConvertFrom-Json
    $serialized = $json -join [Environment]::NewLine
    if ($serialized -match '"vulnerabilities"\s*:') {
        Write-Error $serialized
        throw 'Known vulnerable NuGet packages were found.'
    }

    Write-Host 'NuGet vulnerability audit passed.'
}
finally {
    Pop-Location
}
