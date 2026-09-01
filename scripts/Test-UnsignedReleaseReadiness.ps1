param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,
    [Parameter(Mandatory)]
    [string]$SourceManifestPath,
    [Parameter(Mandatory)]
    [string]$ExpectedSourceRevision,
    [string]$ExpectedVersion = '2.2.1',
    [string]$PawnIoPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer-assets\PawnIO_setup.exe'),
    [string]$ExpectedPawnIoSignerSubject = 'E=admin@namazso.eu, CN=namazso.eu, O=namazso, L=Debrecen, C=HU',
    [string]$ManifestOutputPath,
    [string]$ChecksumOutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$approvedUnsignedStableVersions = @('2.1.0', '2.2.0', '2.2.1')
if ($approvedUnsignedStableVersions -cnotcontains $ExpectedVersion) {
    throw "Version '$ExpectedVersion' is not an approved unsigned stable-release exception."
}
if ($ExpectedSourceRevision -cnotmatch '^[0-9a-f]{40}$') {
    throw 'The expected source revision must be a lowercase 40-character Git commit.'
}

$resolvedInstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
$resolvedManifestPath = (Resolve-Path -LiteralPath $SourceManifestPath).Path
$payloadRoot = Split-Path -Parent $resolvedManifestPath
$sourceManifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json

if ($sourceManifest.schema -ne 3) {
    throw "Unsupported release source manifest schema '$($sourceManifest.schema)'."
}
if ($sourceManifest.version -ne $ExpectedVersion) {
    throw "Release source manifest version '$($sourceManifest.version)' does not match '$ExpectedVersion'."
}
$isDirtyProperty = $sourceManifest.PSObject.Properties['isDirty']
if ($null -eq $isDirtyProperty -or
    $isDirtyProperty.Value -isnot [bool] -or
    $isDirtyProperty.Value) {
    throw 'Release source manifest isDirty must exist and be Boolean false.'
}
if ($sourceManifest.sourceRevision -ne $ExpectedSourceRevision) {
    throw "Release source revision '$($sourceManifest.sourceRevision)' does not match '$ExpectedSourceRevision'."
}
if ($sourceManifest.runtime -ne 'win-x64') {
    throw "Release source runtime '$($sourceManifest.runtime)' is not win-x64."
}

$currentSourceRevision = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or
    -not $currentSourceRevision.Equals($ExpectedSourceRevision, [StringComparison]::Ordinal)) {
    throw "The current repository HEAD '$currentSourceRevision' does not match '$ExpectedSourceRevision'."
}
$worktreeStatus = @(& git -C $projectRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to verify the current repository worktree state.'
}
if ($worktreeStatus.Count -ne 0) {
    throw 'The current repository worktree contains tracked or untracked changes.'
}

& (Join-Path $PSScriptRoot 'Test-ReleasePayloadIntegrity.ps1') `
    -PayloadRoot $payloadRoot `
    -SourceManifestPath $resolvedManifestPath

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceManifest.sdkVersion -ne $sdkVersion) {
    throw "Release source SDK '$($sourceManifest.sdkVersion)' does not match installed SDK '$sdkVersion'."
}

$globalJsonPath = Join-Path $projectRoot 'global.json'
$globalJsonSha256 = (Get-FileHash -LiteralPath $globalJsonPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceManifest.globalJsonSha256 -ne $globalJsonSha256) {
    throw 'Release source globalJsonSha256 does not match the checked-out source.'
}

$expectedPackageLocks = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](?:artifacts|bin|obj|publish)[\\/]' } |
    ForEach-Object {
        $lockPath = Join-Path $_.DirectoryName 'packages.lock.json'
        if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
            throw "Package lock file is missing for '$($_.FullName)'."
        }
        [pscustomobject][ordered]@{
            path = [IO.Path]::GetRelativePath($projectRoot, $lockPath).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    } | Sort-Object path)
$manifestPackageLocks = @($sourceManifest.packageLocks | Sort-Object path)
if ($manifestPackageLocks.Count -ne $expectedPackageLocks.Count) {
    throw 'Release source packageLocks inventory is incomplete.'
}
for ($index = 0; $index -lt $expectedPackageLocks.Count; $index++) {
    if ($manifestPackageLocks[$index].path -ne $expectedPackageLocks[$index].path -or
        $manifestPackageLocks[$index].sha256 -ne $expectedPackageLocks[$index].sha256) {
        throw "Release source package lock '$($manifestPackageLocks[$index].path)' does not match the checked-out source."
    }
}

$pdbFiles = @(Get-ChildItem -LiteralPath $payloadRoot -Filter '*.pdb' -File -Recurse)
if ($pdbFiles.Count -ne 0) {
    throw "Release payload contains debug symbols: $($pdbFiles[0].FullName)"
}

$expectedProductVersion = "$ExpectedVersion+$ExpectedSourceRevision"
$artifactResults = [Collections.Generic.List[object]]::new()
$installerSignature = Get-AuthenticodeSignature -LiteralPath $resolvedInstallerPath
if ($installerSignature.Status -ne 'NotSigned') {
    throw "Installer must be explicitly unsigned; Authenticode status is $($installerSignature.Status)."
}
$installerProductVersion = (Get-Item -LiteralPath $resolvedInstallerPath).VersionInfo.ProductVersion.Trim()
if (-not $installerProductVersion.Equals($ExpectedVersion, [StringComparison]::Ordinal)) {
    throw "Installer version '$installerProductVersion' does not match '$ExpectedVersion'."
}
$sourceManifestSha256 = (Get-FileHash -LiteralPath $resolvedManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedInstallerDescription = "UniDesk payload 1 $($sourceManifestSha256.Substring(0, 32))"
$expectedInstallerCopyright = "UniDesk payload 2 $($sourceManifestSha256.Substring(32, 32))"
$installerDescription = (Get-Item -LiteralPath $resolvedInstallerPath).VersionInfo.FileDescription.Trim()
$installerCopyright = (Get-Item -LiteralPath $resolvedInstallerPath).VersionInfo.LegalCopyright.Trim()
if (-not $expectedInstallerDescription.Equals($installerDescription, [StringComparison]::Ordinal) -or
    -not $expectedInstallerCopyright.Equals($installerCopyright, [StringComparison]::Ordinal)) {
    throw "Installer payload fingerprint does not match '$sourceManifestSha256'."
}
$installerHash = (Get-FileHash -LiteralPath $resolvedInstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$artifactResults.Add([ordered]@{
    name = 'Installer'
    fileName = [IO.Path]::GetFileName($resolvedInstallerPath)
    sha256 = $installerHash
    size = (Get-Item -LiteralPath $resolvedInstallerPath).Length
    authenticode = 'NotSigned'
})

$firstPartyEntries = @($sourceManifest.payloadFiles |
    Where-Object { [bool]$_.signingRequired } |
    Sort-Object path)
foreach ($entry in $firstPartyEntries) {
    $relativePath = [string]$entry.path
    $fullPath = Join-Path $payloadRoot $relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $signature = Get-AuthenticodeSignature -LiteralPath $fullPath
    if ($signature.Status -ne 'NotSigned') {
        throw "First-party PE '$relativePath' must be explicitly unsigned; Authenticode status is $($signature.Status)."
    }
    $productVersion = (Get-Item -LiteralPath $fullPath).VersionInfo.ProductVersion
    if (-not $productVersion.Equals($expectedProductVersion, [StringComparison]::Ordinal)) {
        throw "First-party PE '$relativePath' product version '$productVersion' does not bind to '$ExpectedSourceRevision'."
    }
    $artifactResults.Add([ordered]@{
        name = $relativePath
        fileName = [IO.Path]::GetFileName($fullPath)
        sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        size = (Get-Item -LiteralPath $fullPath).Length
        authenticode = 'NotSigned'
    })
}

$resolvedPawnIoPath = (Resolve-Path -LiteralPath $PawnIoPath).Path
$pawnIoHash = (Get-FileHash -LiteralPath $resolvedPawnIoPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($pawnIoHash -ne 'a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0') {
    throw 'PawnIO installer hash does not match the pinned release input.'
}
$pawnIoSignature = Get-AuthenticodeSignature -LiteralPath $resolvedPawnIoPath
if ($pawnIoSignature.Status -ne 'Valid' -or $null -eq $pawnIoSignature.SignerCertificate) {
    throw "PawnIO installer Authenticode status is $($pawnIoSignature.Status)."
}
if (-not $pawnIoSignature.SignerCertificate.Subject.Equals(
        $ExpectedPawnIoSignerSubject,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "PawnIO signer '$($pawnIoSignature.SignerCertificate.Subject)' does not match the pinned publisher."
}
$artifactResults.Add([ordered]@{
    name = 'PawnIO installer'
    fileName = [IO.Path]::GetFileName($resolvedPawnIoPath)
    sha256 = $pawnIoHash
    size = (Get-Item -LiteralPath $resolvedPawnIoPath).Length
    authenticode = 'Valid'
    signer = $pawnIoSignature.SignerCertificate.Subject
})

if (-not [string]::IsNullOrWhiteSpace($ManifestOutputPath)) {
    $manifestFullPath = [IO.Path]::GetFullPath($ManifestOutputPath, (Get-Location))
    if (Test-Path -LiteralPath $manifestFullPath) {
        throw "Unsigned release manifest already exists: $manifestFullPath"
    }
    $manifestDirectory = Split-Path -Parent $manifestFullPath
    if (-not (Test-Path -LiteralPath $manifestDirectory)) {
        New-Item -ItemType Directory -Path $manifestDirectory | Out-Null
    }
    [ordered]@{
        schema = 1
        releaseMode = "unsigned-v$ExpectedVersion-exception"
        version = $ExpectedVersion
        sourceRevision = $ExpectedSourceRevision
        sourceManifestSha256 = $sourceManifestSha256
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        artifacts = $artifactResults
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestFullPath -Encoding utf8
    Write-Host "Unsigned release manifest written to $manifestFullPath"
}

if (-not [string]::IsNullOrWhiteSpace($ChecksumOutputPath)) {
    $checksumFullPath = [IO.Path]::GetFullPath($ChecksumOutputPath, (Get-Location))
    if (Test-Path -LiteralPath $checksumFullPath) {
        throw "Checksum file already exists: $checksumFullPath"
    }
    $checksumDirectory = Split-Path -Parent $checksumFullPath
    if (-not (Test-Path -LiteralPath $checksumDirectory)) {
        New-Item -ItemType Directory -Path $checksumDirectory | Out-Null
    }
    "$($installerHash.ToUpperInvariant())  $([IO.Path]::GetFileName($resolvedInstallerPath))" |
        Set-Content -LiteralPath $checksumFullPath -Encoding ascii
    Write-Host "Installer checksum written to $checksumFullPath"
}

Write-Host "Unsigned release readiness checks passed for $ExpectedVersion. Authenticode: NotSigned."
