param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,
    [Parameter(Mandatory)]
    [string]$AppExePath,
    [Parameter(Mandatory)]
    [string]$ServiceExePath,
    [Parameter(Mandatory)]
    [string]$RepairHelperExePath,
    [Parameter(Mandatory)]
    [string]$SourceManifestPath,
    [Parameter(Mandatory)]
    [string]$ExpectedSourceRevision,
    [string]$PawnIoPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer-assets\PawnIO_setup.exe'),
    [string]$ExpectedVersion = '2.1.0',
    [string]$ManifestOutputPath
)

$ErrorActionPreference = 'Stop'
$sourceManifest = Get-Content -LiteralPath (Resolve-Path -LiteralPath $SourceManifestPath) -Raw |
    ConvertFrom-Json
if ($sourceManifest.schema -ne 1) {
    throw "Unsupported release source manifest schema '$($sourceManifest.schema)'."
}
if ($sourceManifest.version -ne $ExpectedVersion) {
    throw "Release source manifest version '$($sourceManifest.version)' does not match $ExpectedVersion."
}
if ($sourceManifest.isDirty) {
    throw 'Release source manifest reports a dirty worktree.'
}
if ($sourceManifest.sourceRevision -ne $ExpectedSourceRevision) {
    throw "Release source revision '$($sourceManifest.sourceRevision)' does not match '$ExpectedSourceRevision'."
}

$appDirectory = Split-Path -Parent $AppExePath
$serviceDirectory = Split-Path -Parent $ServiceExePath
$repairDirectory = Split-Path -Parent $RepairHelperExePath
$artifacts = @(
    @{ Name = 'Installer'; Path = $InstallerPath },
    @{ Name = 'Application'; Path = $AppExePath },
    @{ Name = 'Application managed code'; Path = (Join-Path $appDirectory 'UniDesk.dll') },
    @{ Name = 'Application hardware contracts'; Path = (Join-Path $appDirectory 'UniDesk.Hardware.Contracts.dll') },
    @{ Name = 'Hardware service'; Path = $ServiceExePath },
    @{ Name = 'Hardware service managed code'; Path = (Join-Path $serviceDirectory 'UniDesk.HardwareService.dll') },
    @{ Name = 'Hardware service contracts'; Path = (Join-Path $serviceDirectory 'UniDesk.Hardware.Contracts.dll') },
    @{ Name = 'Hardware repair helper'; Path = $RepairHelperExePath },
    @{ Name = 'Hardware repair managed code'; Path = (Join-Path $repairDirectory 'UniDesk.HardwareRepair.dll') },
    @{ Name = 'PawnIO installer'; Path = $PawnIoPath }
)

$artifactResults = foreach ($artifact in $artifacts) {
    $resolvedPath = (Resolve-Path -LiteralPath $artifact.Path).Path
    $signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
    if ($signature.Status -ne 'Valid') {
        throw "$($artifact.Name) is not release-ready: Authenticode status is $($signature.Status)."
    }

    $hash = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "$($artifact.Name) SHA256 $hash"
    [ordered]@{
        name = $artifact.Name
        fileName = [IO.Path]::GetFileName($resolvedPath)
        sha256 = $hash
        signer = $signature.SignerCertificate.Subject
    }
}

$pawnIoHash = (Get-FileHash -LiteralPath (Resolve-Path -LiteralPath $PawnIoPath) -Algorithm SHA256).Hash.ToLowerInvariant()
if ($pawnIoHash -ne 'a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0') {
    throw 'PawnIO installer hash does not match the pinned release input.'
}

foreach ($artifact in $artifacts | Where-Object { $_.Name -ne 'PawnIO installer' }) {
    $productVersion = (Get-Item -LiteralPath (Resolve-Path -LiteralPath $artifact.Path)).VersionInfo.ProductVersion
    if (-not $productVersion.StartsWith($ExpectedVersion, [StringComparison]::Ordinal)) {
        throw "$($artifact.Name) version '$productVersion' does not match $ExpectedVersion."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ManifestOutputPath)) {
    $manifestFullPath = if ([IO.Path]::IsPathRooted($ManifestOutputPath)) {
        [IO.Path]::GetFullPath($ManifestOutputPath)
    } else {
        [IO.Path]::GetFullPath((Join-Path (Get-Location) $ManifestOutputPath))
    }
    $manifestDirectory = Split-Path -Parent $manifestFullPath
    if (-not (Test-Path -LiteralPath $manifestDirectory)) {
        New-Item -ItemType Directory -Path $manifestDirectory | Out-Null
    }
    [ordered]@{
        schema = 1
        version = $ExpectedVersion
        sourceRevision = $ExpectedSourceRevision
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        artifacts = $artifactResults
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestFullPath -Encoding utf8
    Write-Host "Release manifest written to $manifestFullPath"
}

Write-Host "Release readiness checks passed for $ExpectedVersion."
