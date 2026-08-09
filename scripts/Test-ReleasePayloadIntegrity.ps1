param(
    [Parameter(Mandatory)]
    [string]$PayloadRoot,
    [Parameter(Mandatory)]
    [string]$SourceManifestPath,
    [switch]$AllowSigningChanges
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleasePayloadTools.ps1')

$payloadPath = [IO.Path]::GetFullPath($PayloadRoot)
$manifestPath = [IO.Path]::GetFullPath($SourceManifestPath)
if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
    throw "Release payload directory was not found: $payloadPath"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Release source manifest was not found: $manifestPath"
}

$sourceManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($sourceManifest.schema -ne 3) {
    throw "Unsupported release source manifest schema '$($sourceManifest.schema)'."
}

$signingTargets = @(
    'App/UniDesk.exe',
    'App/UniDesk.dll',
    'App/UniDesk.Hardware.Contracts.dll',
    'HardwareService/UniDesk.HardwareService.exe',
    'HardwareService/UniDesk.HardwareService.dll',
    'HardwareService/UniDesk.Hardware.Contracts.dll',
    'HardwareRepair/UniDesk.HardwareRepair.exe',
    'HardwareRepair/UniDesk.HardwareRepair.dll'
)
$allowedDirectories = @('App', 'HardwareService', 'HardwareRepair')
$expectedDirectories = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$expectedFiles = [Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::OrdinalIgnoreCase)

foreach ($directoryEntry in @($sourceManifest.payloadDirectoryEntries)) {
    $relativePath = [string]$directoryEntry
    if ([string]::IsNullOrWhiteSpace($relativePath) -or
        $relativePath.Contains('\') -or
        [IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Split('/').Contains('..') -or
        $relativePath.Split('/').Contains('.')) {
        throw "Release payload manifest contains an unsafe directory path '$relativePath'."
    }
    if ($allowedDirectories -notcontains $relativePath.Split('/')[0]) {
        throw "Release payload manifest contains an unexpected directory path '$relativePath'."
    }
    if (-not $expectedDirectories.Add($relativePath)) {
        throw "Release payload manifest contains duplicate directory path '$relativePath'."
    }
}
foreach ($directoryName in $allowedDirectories) {
    if (-not $expectedDirectories.Contains($directoryName)) {
        throw "Release payload manifest is missing root directory '$directoryName'."
    }
}

foreach ($entry in @($sourceManifest.payloadFiles)) {
    $relativePath = [string]$entry.path
    if ([string]::IsNullOrWhiteSpace($relativePath) -or
        $relativePath.Contains('\') -or
        [IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Split('/').Contains('..') -or
        $relativePath.Split('/').Contains('.')) {
        throw "Release payload manifest contains an unsafe path '$relativePath'."
    }

    $topLevelDirectory = $relativePath.Split('/')[0]
    if ($allowedDirectories -notcontains $topLevelDirectory) {
        throw "Release payload manifest contains an unexpected directory '$topLevelDirectory'."
    }
    if (-not $expectedFiles.TryAdd($relativePath, $entry)) {
        throw "Release payload manifest contains duplicate path '$relativePath'."
    }

    $expectedSigningTarget = $signingTargets -contains $relativePath
    if ([bool]$entry.signingRequired -ne $expectedSigningTarget) {
        throw "Release payload signing classification is invalid for '$relativePath'."
    }
    if ([string]$entry.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "Release payload manifest contains an invalid SHA-256 for '$relativePath'."
    }
    if ($expectedSigningTarget -and
        [string]$entry.authenticodeContentSha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "Release payload manifest contains an invalid Authenticode content hash for '$relativePath'."
    }
}

foreach ($signingTarget in $signingTargets) {
    if (-not $expectedFiles.ContainsKey($signingTarget)) {
        throw "Release payload manifest is missing signing target '$signingTarget'."
    }
}

$payloadInventory = @(Get-ReleasePayloadInventoryItems `
    -PayloadRoot $payloadPath `
    -DirectoryNames $allowedDirectories)
$actualDirectories = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$actualFiles = [Collections.Generic.Dictionary[string, string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($item in $payloadInventory) {
    if ($item.Kind -eq 'Directory') {
        if (-not $actualDirectories.Add($item.RelativePath)) {
            throw "Release payload contains duplicate directory path '$($item.RelativePath)'."
        }
    } elseif (-not $actualFiles.TryAdd($item.RelativePath, $item.FullName)) {
        throw "Release payload contains duplicate file path '$($item.RelativePath)'."
    }
}

if (-not $actualDirectories.SetEquals($expectedDirectories)) {
    throw 'Release payload directory inventory does not match the source manifest.'
}

if ($actualFiles.Count -ne $expectedFiles.Count) {
    throw "Release payload file count '$($actualFiles.Count)' does not match manifest count '$($expectedFiles.Count)'."
}

foreach ($relativePath in $expectedFiles.Keys) {
    if (-not $actualFiles.ContainsKey($relativePath)) {
        throw "Release payload is missing manifest file '$relativePath'."
    }

    $entry = $expectedFiles[$relativePath]
    $actualHash = (Get-FileHash -LiteralPath $actualFiles[$relativePath] -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne [string]$entry.sha256) {
        $isAllowedSigningChange = $AllowSigningChanges -and
            [bool]$entry.signingRequired -and
            ($signingTargets -contains $relativePath)
        if (-not $isAllowedSigningChange) {
            throw "Release payload hash does not match the unsigned source for '$relativePath'."
        }
    }
    if ([bool]$entry.signingRequired) {
        $authenticodeContentHash = Get-AuthenticodeContentSha256 -Path $actualFiles[$relativePath]
        if ($authenticodeContentHash -ne [string]$entry.authenticodeContentSha256) {
            throw "Release payload Authenticode content differs from the unsigned source for '$relativePath'."
        }
    }
}

Write-Host "Release payload inventory and hashes match the source manifest."
