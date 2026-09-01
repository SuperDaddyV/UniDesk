param(
    [string]$Version = '2.2.1',
    [Parameter(Mandatory)]
    [string]$ExpectedSourceRevision,
    [Parameter(Mandatory)]
    [string]$PayloadRoot,
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$UnsignedSourceManifestPath,
    [string]$ExpectedUnsignedSourceManifestSha256,
    [string]$IsccPath,
    [switch]$RequireSignedPayload
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payloadPath = [IO.Path]::GetFullPath($PayloadRoot, $projectRoot)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory, $projectRoot)

if ($RequireSignedPayload) {
    $repositoryRevision = (& git -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or
        -not $repositoryRevision.Equals($ExpectedSourceRevision, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Installer source HEAD '$repositoryRevision' does not match '$ExpectedSourceRevision'."
    }
    $repositoryStatus = @(& git -C $projectRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $repositoryStatus.Count -ne 0) {
        throw 'A public installer must be compiled from a clean installer-source worktree.'
    }
}

if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
    throw "Release payload directory was not found: $payloadPath"
}
if (Test-Path -LiteralPath $outputPath) {
    throw "Installer output already exists: $outputPath"
}

$appDirectory = Join-Path $payloadPath 'App'
$serviceDirectory = Join-Path $payloadPath 'HardwareService'
$repairDirectory = Join-Path $payloadPath 'HardwareRepair'
$sourceManifestPath = Join-Path $payloadPath 'release-source.json'
if ($RequireSignedPayload) {
    if ([string]::IsNullOrWhiteSpace($UnsignedSourceManifestPath)) {
        throw 'The independently preserved unsigned source manifest is required for a signed payload.'
    }
    $trustedManifestPath = [IO.Path]::GetFullPath($UnsignedSourceManifestPath, $projectRoot)
    if ($ExpectedUnsignedSourceManifestSha256 -cnotmatch '^[0-9a-fA-F]{64}$') {
        throw 'The expected unsigned source manifest SHA-256 is missing or invalid.'
    }
    if ([IO.Path]::GetFullPath($sourceManifestPath).Equals(
            $trustedManifestPath,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The trusted unsigned source manifest must be outside the signed payload.'
    }
    $trustedManifestHash = (Get-FileHash -LiteralPath $trustedManifestPath -Algorithm SHA256).Hash
    $returnedManifestHash = (Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash
    if (-not $trustedManifestHash.Equals($ExpectedUnsignedSourceManifestSha256, [StringComparison]::OrdinalIgnoreCase) -or
        -not $returnedManifestHash.Equals($ExpectedUnsignedSourceManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The source manifest returned with the signed payload differs from the independently preserved unsigned manifest.'
    }
}
$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json
$sourceManifestSha256 = (Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceManifestSha256Part1 = $sourceManifestSha256.Substring(0, 32)
$sourceManifestSha256Part2 = $sourceManifestSha256.Substring(32, 32)
if ($sourceManifest.schema -ne 3) {
    throw "Unsupported release source manifest schema '$($sourceManifest.schema)'."
}
if ($sourceManifest.version -ne $Version) {
    throw "Release source manifest version '$($sourceManifest.version)' does not match $Version."
}
if ($sourceManifest.sourceRevision -ne $ExpectedSourceRevision) {
    throw "Release source revision '$($sourceManifest.sourceRevision)' does not match '$ExpectedSourceRevision'."
}
if ($sourceManifest.isDirty -and $RequireSignedPayload) {
    throw 'A public installer cannot be built from a dirty source manifest.'
}

$integrityParameters = @{
    PayloadRoot = $payloadPath
    SourceManifestPath = $sourceManifestPath
}
if ($RequireSignedPayload) {
    $integrityParameters.AllowSigningChanges = $true
}
& (Join-Path $PSScriptRoot 'Test-ReleasePayloadIntegrity.ps1') @integrityParameters

$expectedProductVersion = "$Version+$ExpectedSourceRevision"
$firstPartyFiles = @(
    @{ Name = 'Application'; Path = (Join-Path $appDirectory 'UniDesk.exe') },
    @{ Name = 'Application managed code'; Path = (Join-Path $appDirectory 'UniDesk.dll') },
    @{ Name = 'Application hardware contracts'; Path = (Join-Path $appDirectory 'UniDesk.Hardware.Contracts.dll') },
    @{ Name = 'Hardware service'; Path = (Join-Path $serviceDirectory 'UniDesk.HardwareService.exe') },
    @{ Name = 'Hardware service managed code'; Path = (Join-Path $serviceDirectory 'UniDesk.HardwareService.dll') },
    @{ Name = 'Hardware service contracts'; Path = (Join-Path $serviceDirectory 'UniDesk.Hardware.Contracts.dll') },
    @{ Name = 'Hardware repair helper'; Path = (Join-Path $repairDirectory 'UniDesk.HardwareRepair.exe') },
    @{ Name = 'Hardware repair managed code'; Path = (Join-Path $repairDirectory 'UniDesk.HardwareRepair.dll') }
)

foreach ($firstPartyFile in $firstPartyFiles) {
    $item = Get-Item -LiteralPath $firstPartyFile.Path
    if (-not $item.VersionInfo.ProductVersion.Equals($expectedProductVersion, [StringComparison]::Ordinal)) {
        throw "$($firstPartyFile.Name) product version '$($item.VersionInfo.ProductVersion)' does not bind to source revision '$ExpectedSourceRevision'."
    }
    if ($RequireSignedPayload) {
        $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
        if ($signature.Status -ne 'Valid') {
            throw "$($firstPartyFile.Name) must be signed before building the public installer."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $isccCommand) {
        $IsccPath = $isccCommand.Source
    } else {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
        )
        $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Inno Setup 6 compiler ISCC.exe was not found.'
}

New-Item -ItemType Directory -Path $outputPath | Out-Null
$definitions = @(
    "/DMyAppSourceDir=$appDirectory",
    "/DMyHardwareServiceSourceDir=$serviceDirectory",
    "/DMyHardwareRepairSourceDir=$repairDirectory",
    "/DMyOutputDir=$outputPath",
    "/DMyPayloadManifestSha256Part1=$sourceManifestSha256Part1",
    "/DMyPayloadManifestSha256Part2=$sourceManifestSha256Part2",
    (Join-Path $projectRoot 'UniDesk.iss')
)
& $IsccPath @definitions
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $outputPath "UniDesk_Setup_$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Expected installer was not created: $installerPath"
}
$finalSourceManifestSha256 = (Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($finalSourceManifestSha256 -ne $sourceManifestSha256) {
    throw 'The release source manifest changed while the installer was being compiled.'
}

Write-Host "Installer created at $installerPath"
