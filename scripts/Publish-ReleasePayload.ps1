param(
    [string]$Version = '2.1.0',
    [Parameter(Mandatory)]
    [string]$OutputRoot,
    [switch]$AllowDirtyWorktree,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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
    Invoke-DotNet @('restore', (Join-Path $projectRoot 'UniDesk.sln'), '-r', 'win-x64')
}

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

$manifest = [ordered]@{
    schema = 1
    version = $Version
    sourceRevision = $sourceRevision
    isDirty = $isDirty
    runtime = 'win-x64'
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
