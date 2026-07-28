param(
    [string]$Version = '2.1.0',
    [string]$OutputRoot,
    [string]$IsccPath,
    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $shortRevision = (& git -C $projectRoot rev-parse --short=12 HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the source Git commit.'
    }
    $buildId = "{0}-{1}-{2}" -f $Version, $shortRevision, (Get-Date -Format 'yyyyMMdd-HHmmss')
    $OutputRoot = Join-Path $projectRoot "artifacts\release\$buildId"
}

$releaseRoot = [IO.Path]::GetFullPath($OutputRoot, $projectRoot)
$payloadRoot = Join-Path $releaseRoot 'payload'
$installerOutput = Join-Path $releaseRoot 'installer'

$publishParameters = @{
    Version = $Version
    OutputRoot = $payloadRoot
    AllowDirtyWorktree = [bool]$AllowDirtyWorktree
}
& (Join-Path $PSScriptRoot 'Publish-ReleasePayload.ps1') @publishParameters

$installerParameters = @{
    Version = $Version
    PayloadRoot = $payloadRoot
    OutputDirectory = $installerOutput
}
if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    $installerParameters.IsccPath = $IsccPath
}
& (Join-Path $PSScriptRoot 'Build-ReleaseInstaller.ps1') @installerParameters

Write-Host "Unsigned release candidate created at $releaseRoot"
Write-Host 'Public release still requires SignPath signing and Test-ReleaseReadiness.ps1.'
