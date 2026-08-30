param(
    [string]$Version = '2.1.0',
    [string]$OutputRoot,
    [string]$IsccPath,
    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRevision = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve the source Git commit.'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $shortRevision = $sourceRevision.Substring(0, [Math]::Min(12, $sourceRevision.Length))
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
    ExpectedSourceRevision = $sourceRevision
    PayloadRoot = $payloadRoot
    OutputDirectory = $installerOutput
}
if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    $installerParameters.IsccPath = $IsccPath
}
& (Join-Path $PSScriptRoot 'Build-ReleaseInstaller.ps1') @installerParameters

$installerPath = Join-Path $installerOutput "UniDesk_Setup_$Version.exe"
if ($AllowDirtyWorktree) {
    Write-Warning 'Dirty-worktree output is for local prevalidation only; unsigned release readiness was not granted.'
} else {
    & (Join-Path $PSScriptRoot 'Test-UnsignedReleaseReadiness.ps1') `
        -InstallerPath $installerPath `
        -SourceManifestPath (Join-Path $payloadRoot 'release-source.json') `
        -ExpectedSourceRevision $sourceRevision `
        -ExpectedVersion $Version `
        -ManifestOutputPath (Join-Path $installerOutput 'release-manifest.json') `
        -ChecksumOutputPath (Join-Path $installerOutput 'SHA256SUMS.txt')
}

Write-Host "Unsigned release artifact created at $releaseRoot"
Write-Host 'Public release still requires the complete manual matrix and the project owner final approval.'
