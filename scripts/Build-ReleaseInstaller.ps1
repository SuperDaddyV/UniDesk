param(
    [string]$Version = '2.1.0',
    [Parameter(Mandatory)]
    [string]$PayloadRoot,
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$IsccPath,
    [switch]$RequireSignedPayload
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payloadPath = [IO.Path]::GetFullPath($PayloadRoot, $projectRoot)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory, $projectRoot)

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
    if (-not $item.VersionInfo.ProductVersion.StartsWith($Version, [StringComparison]::Ordinal)) {
        throw "$($firstPartyFile.Name) version '$($item.VersionInfo.ProductVersion)' does not match $Version."
    }
    if ($RequireSignedPayload) {
        $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
        if ($signature.Status -ne 'Valid') {
            throw "$($firstPartyFile.Name) must be signed before building the public installer."
        }
    }
}

$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json
if ($sourceManifest.version -ne $Version) {
    throw "Release source manifest version '$($sourceManifest.version)' does not match $Version."
}
if ($sourceManifest.isDirty -and $RequireSignedPayload) {
    throw 'A public installer cannot be built from a dirty source manifest.'
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

Write-Host "Installer created at $installerPath"
