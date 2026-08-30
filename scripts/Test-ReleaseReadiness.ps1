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
    [string]$UnsignedSourceManifestPath,
    [Parameter(Mandatory)]
    [string]$ExpectedUnsignedSourceManifestSha256,
    [Parameter(Mandatory)]
    [string]$ExpectedSourceRevision,
    [Parameter(Mandatory)]
    [string]$ExpectedSignerSubject,
    [Parameter(Mandatory)]
    [string]$ExpectedUnsignedInstallerAuthenticodeContentSha256,
    [string]$PawnIoPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer-assets\PawnIO_setup.exe'),
    [string]$ExpectedPawnIoSignerSubject = 'E=admin@namazso.eu, CN=namazso.eu, O=namazso, L=Debrecen, C=HU',
    [string]$ExpectedVersion = '2.1.0',
    [string]$ManifestOutputPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleasePayloadTools.ps1')
if ($ExpectedUnsignedInstallerAuthenticodeContentSha256 -cnotmatch '^[0-9a-fA-F]{64}$') {
    throw 'The expected unsigned installer Authenticode content SHA-256 is invalid.'
}
$installerAuthenticodeContentSha256 = Get-AuthenticodeContentSha256 -Path $InstallerPath
if (-not $installerAuthenticodeContentSha256.Equals(
        $ExpectedUnsignedInstallerAuthenticodeContentSha256,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The signed installer content differs from the unsigned installer submitted for signing.'
}
$returnedManifestPath = [IO.Path]::GetFullPath($SourceManifestPath)
$trustedManifestPath = [IO.Path]::GetFullPath($UnsignedSourceManifestPath)
if ($ExpectedUnsignedSourceManifestSha256 -cnotmatch '^[0-9a-fA-F]{64}$') {
    throw 'The expected unsigned source manifest SHA-256 is invalid.'
}
if ($returnedManifestPath.Equals($trustedManifestPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The trusted unsigned source manifest must be outside the signed payload.'
}
$trustedManifestHash = (Get-FileHash -LiteralPath $trustedManifestPath -Algorithm SHA256).Hash
$returnedManifestHash = (Get-FileHash -LiteralPath $returnedManifestPath -Algorithm SHA256).Hash
if (-not $trustedManifestHash.Equals($ExpectedUnsignedSourceManifestSha256, [StringComparison]::OrdinalIgnoreCase) -or
    -not $returnedManifestHash.Equals($ExpectedUnsignedSourceManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The signed payload source manifest differs from the independently preserved unsigned manifest.'
}
$sourceManifest = Get-Content -LiteralPath (Resolve-Path -LiteralPath $SourceManifestPath) -Raw |
    ConvertFrom-Json
if ($sourceManifest.schema -ne 3) {
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

$payloadRoot = Split-Path -Parent ([IO.Path]::GetFullPath($SourceManifestPath))
& (Join-Path $PSScriptRoot 'Test-ReleasePayloadIntegrity.ps1') `
    -PayloadRoot $payloadRoot `
    -SourceManifestPath $SourceManifestPath `
    -AllowSigningChanges

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceManifest.sdkVersion -ne $sdkVersion) {
    throw "Release source SDK '$($sourceManifest.sdkVersion)' does not match installed SDK '$sdkVersion'."
}

$globalJsonPath = Join-Path $projectRoot 'global.json'
$globalJsonSha256 = (Get-FileHash -LiteralPath $globalJsonPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceManifest.globalJsonSha256 -ne $globalJsonSha256) {
    throw 'Release source global.json hash does not match the checked-out source.'
}

$expectedPackageLocks = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](?:artifacts|bin|obj|publish)[\\/]' } |
    ForEach-Object {
        $lockPath = Join-Path $_.DirectoryName 'packages.lock.json'
        if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
            throw "Package lock file is missing for $($_.FullName)."
        }
        [pscustomobject][ordered]@{
            path = [IO.Path]::GetRelativePath($projectRoot, $lockPath).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    } | Sort-Object path)
$manifestPackageLocks = @($sourceManifest.packageLocks | Sort-Object path)
if ($manifestPackageLocks.Count -ne $expectedPackageLocks.Count) {
    throw 'Release source package-lock inventory is incomplete.'
}
for ($index = 0; $index -lt $expectedPackageLocks.Count; $index++) {
    if ($manifestPackageLocks[$index].path -ne $expectedPackageLocks[$index].path -or
        $manifestPackageLocks[$index].sha256 -ne $expectedPackageLocks[$index].sha256) {
        throw "Release source package-lock entry '$($manifestPackageLocks[$index].path)' does not match the checked-out source."
    }
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
$expectedProductVersion = "$ExpectedVersion+$ExpectedSourceRevision"

$artifactResults = foreach ($artifact in $artifacts) {
    $resolvedPath = (Resolve-Path -LiteralPath $artifact.Path).Path
    $signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
    if ($signature.Status -ne 'Valid') {
        throw "$($artifact.Name) is not release-ready: Authenticode status is $($signature.Status)."
    }
    if ($null -eq $signature.SignerCertificate) {
        throw "$($artifact.Name) has no signer certificate."
    }

    $expectedSubject = if ($artifact.Name -eq 'PawnIO installer') {
        $ExpectedPawnIoSignerSubject
    } else {
        $ExpectedSignerSubject
    }
    if (-not $signature.SignerCertificate.Subject.Equals(
            $expectedSubject,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$($artifact.Name) signer '$($signature.SignerCertificate.Subject)' does not match expected signer '$expectedSubject'."
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

foreach ($artifact in $artifacts | Where-Object {
        $_.Name -ne 'PawnIO installer' -and $_.Name -ne 'Installer'
    }) {
    $productVersion = (Get-Item -LiteralPath (Resolve-Path -LiteralPath $artifact.Path)).VersionInfo.ProductVersion
    if (-not $productVersion.Equals($expectedProductVersion, [StringComparison]::Ordinal)) {
        throw "$($artifact.Name) product version '$productVersion' does not bind to source revision '$ExpectedSourceRevision'."
    }
}

$installerProductVersion = (Get-Item -LiteralPath (Resolve-Path -LiteralPath $InstallerPath)).VersionInfo.ProductVersion
if (-not $installerProductVersion.StartsWith($ExpectedVersion, [StringComparison]::Ordinal)) {
    throw "Installer version '$installerProductVersion' does not match $ExpectedVersion."
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
