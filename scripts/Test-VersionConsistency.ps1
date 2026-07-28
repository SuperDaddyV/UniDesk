param(
    [string]$ExpectedVersion = '2.1.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFiles = @(
    'UniDesk\UniDesk.csproj',
    'UniDesk.HardwareRepair\UniDesk.HardwareRepair.csproj',
    'UniDesk.HardwareService\UniDesk.HardwareService.csproj',
    'UniDesk.Hardware.Contracts\UniDesk.Hardware.Contracts.csproj'
)

foreach ($relativePath in $projectFiles) {
    $path = Join-Path $projectRoot $relativePath
    [xml]$project = Get-Content -LiteralPath $path -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -ne $ExpectedVersion) {
        throw "$relativePath version is '$version'; expected '$ExpectedVersion'."
    }
}

$installerScript = Get-Content -LiteralPath (Join-Path $projectRoot 'UniDesk.iss') -Raw
if ($installerScript -notmatch ('#define MyAppVersion "' + [regex]::Escape($ExpectedVersion) + '"')) {
    throw "UniDesk.iss does not declare version $ExpectedVersion."
}

foreach ($readme in @('README.md', 'README.zh-CN.md', 'README.en-US.md', 'README.ja-JP.md', 'README.es-ES.md')) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot $readme) -Raw
    if ($content -notmatch ('UniDesk_Setup_' + [regex]::Escape($ExpectedVersion) + '\.exe')) {
        throw "$readme does not reference the $ExpectedVersion installer."
    }
}

foreach ($releaseScript in @(
    'scripts\Build-Release.ps1',
    'scripts\Build-ReleaseInstaller.ps1',
    'scripts\Publish-ReleasePayload.ps1'
)) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot $releaseScript) -Raw
    $expectedDeclaration = [regex]::Escape("[string]`$Version = '$ExpectedVersion'")
    if ($content -notmatch $expectedDeclaration) {
        throw "$releaseScript does not default to release version $ExpectedVersion."
    }
}

$readinessScript = Get-Content -LiteralPath (Join-Path $projectRoot 'scripts\Test-ReleaseReadiness.ps1') -Raw
$expectedReadinessDeclaration = [regex]::Escape("[string]`$ExpectedVersion = '$ExpectedVersion'")
if ($readinessScript -notmatch $expectedReadinessDeclaration) {
    throw "Test-ReleaseReadiness.ps1 does not default to release version $ExpectedVersion."
}

$signingWorkflow = Get-Content -LiteralPath (Join-Path $projectRoot '.github\workflows\release-signing.yml') -Raw
if ($signingWorkflow -notmatch ("default: '" + [regex]::Escape($ExpectedVersion) + "'")) {
    throw "release-signing.yml does not default to release version $ExpectedVersion."
}

$releaseNotes = Get-Content -LiteralPath (Join-Path $projectRoot 'docs\release-unidesk.md') -Raw
if ($releaseNotes -notmatch ('## v' + [regex]::Escape($ExpectedVersion))) {
    throw "release-unidesk.md does not contain v$ExpectedVersion release notes."
}

Write-Host "Version consistency check passed: $ExpectedVersion"
