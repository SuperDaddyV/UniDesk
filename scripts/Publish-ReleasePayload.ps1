param(
    [string]$Version = '2.2.1',
    [Parameter(Mandatory)]
    [string]$OutputRoot,
    [switch]$AllowDirtyWorktree,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleasePayloadTools.ps1')

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputPath = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    [IO.Path]::GetFullPath($OutputRoot)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputRoot))
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $PSScriptRoot 'Test-VersionConsistency.ps1') -ExpectedVersion $Version

$sourceRevision = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceRevision)) {
    throw 'Unable to resolve the source Git commit.'
}

$worktreeStatus = @(& git -C $projectRoot status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git worktree.'
}

$isDirty = $worktreeStatus.Count -gt 0
if ($isDirty -and -not $AllowDirtyWorktree) {
    throw 'Release payloads must be built from a clean Git worktree.'
}

if (Test-Path -LiteralPath $outputPath) {
    throw "Release payload output already exists: $outputPath"
}

$appOutput = Join-Path $outputPath 'App'
$serviceOutput = Join-Path $outputPath 'HardwareService'
$repairOutput = Join-Path $outputPath 'HardwareRepair'
foreach ($directory in @($appOutput, $serviceOutput, $repairOutput)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

if (-not $NoRestore) {
    Invoke-DotNet @('restore', (Join-Path $projectRoot 'UniDesk.sln'), '-r', 'win-x64', '--locked-mode')
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw 'Unable to resolve the .NET SDK version.'
}

$globalJsonPath = Join-Path $projectRoot 'global.json'
$globalJsonSha256 = (Get-FileHash -LiteralPath $globalJsonPath -Algorithm SHA256).Hash.ToLowerInvariant()
$projectFiles = Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](?:artifacts|bin|obj|publish)[\\/]' }
$packageLocks = foreach ($projectFile in $projectFiles) {
    $lockPath = Join-Path $projectFile.DirectoryName 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        throw "Package lock file is missing for $($projectFile.FullName)."
    }

    [ordered]@{
        path = [IO.Path]::GetRelativePath($projectRoot, $lockPath).Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$packageLocks = @($packageLocks | Sort-Object path)

$commonArguments = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:ContinuousIntegrationBuild=true',
    "-p:SourceRevisionId=$sourceRevision",
    '--no-restore'
)

$projects = @(
    @{ Path = 'UniDesk\UniDesk.csproj'; Output = $appOutput },
    @{ Path = 'UniDesk.HardwareService\UniDesk.HardwareService.csproj'; Output = $serviceOutput },
    @{ Path = 'UniDesk.HardwareRepair\UniDesk.HardwareRepair.csproj'; Output = $repairOutput }
)

foreach ($project in $projects) {
    $projectPath = Join-Path $projectRoot $project.Path
    Invoke-DotNet (@('publish', $projectPath) + $commonArguments + @('-o', $project.Output))
}

$signingTargetPaths = @(
    'App/UniDesk.exe',
    'App/UniDesk.dll',
    'App/UniDesk.Hardware.Contracts.dll',
    'HardwareService/UniDesk.HardwareService.exe',
    'HardwareService/UniDesk.HardwareService.dll',
    'HardwareService/UniDesk.Hardware.Contracts.dll',
    'HardwareRepair/UniDesk.HardwareRepair.exe',
    'HardwareRepair/UniDesk.HardwareRepair.dll'
)
$payloadInventory = @(Get-ReleasePayloadInventoryItems `
    -PayloadRoot $outputPath `
    -DirectoryNames @('App', 'HardwareService', 'HardwareRepair'))
$payloadDirectoryEntries = @(
    $payloadInventory |
        Where-Object Kind -eq 'Directory' |
        Select-Object -ExpandProperty RelativePath |
        Sort-Object
)
$payloadFiles = @(
    $payloadInventory |
        Where-Object Kind -eq 'File' |
        ForEach-Object {
            $relativePath = $_.RelativePath
            $isSigningTarget = $signingTargetPaths -contains $relativePath
            $entry = [ordered]@{
                path = $relativePath
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                signingRequired = $isSigningTarget
            }
            if ($isSigningTarget) {
                $entry.authenticodeContentSha256 = Get-AuthenticodeContentSha256 -Path $_.FullName
            }
            $entry
        } |
        Sort-Object path
)

$manifest = [ordered]@{
    schema = 3
    version = $Version
    sourceRevision = $sourceRevision
    isDirty = $isDirty
    runtime = 'win-x64'
    sdkVersion = $sdkVersion
    globalJsonSha256 = $globalJsonSha256
    packageLocks = $packageLocks
    payloadDirectoryEntries = $payloadDirectoryEntries
    payloadFiles = $payloadFiles
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    payloadDirectories = [ordered]@{
        application = 'App'
        hardwareService = 'HardwareService'
        hardwareRepair = 'HardwareRepair'
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'release-source.json') -Encoding utf8

Write-Host "Release payload created at $outputPath"
Write-Host "Source revision $sourceRevision; dirty=$isDirty"
