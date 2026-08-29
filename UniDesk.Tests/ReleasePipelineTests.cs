using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace UniDesk.Tests;

public class ReleasePipelineTests
{
    private const string CheckoutV6Commit = "d23441a48e516b6c34aea4fa41551a30e30af803";
    private const string SetupDotnetV5Commit = "26b0ec14cb23fa6904739307f278c14f94c95bf1";
    private const string UploadArtifactV7Commit = "043fb46d1a93c77aae656e7c1c64a875d1fc6a0a";
    private const string DownloadArtifactV8Commit = "3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c";
    private const string SignPathV2Commit = "b9d91eadd323de506c0c81cf0c7fe7438f3360fd";

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void Installer_ShouldAcceptExplicitReleaseOutputDirectory()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("#ifndef MyOutputDir", script, StringComparison.Ordinal);
        Assert.Contains("OutputDir={#MyOutputDir}", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseBuildScripts_ShouldPublishAllFirstPartyExecutablesToFreshPayload()
    {
        var publishScript = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "Publish-ReleasePayload.ps1"));
        var installerScript = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "Build-ReleaseInstaller.ps1"));

        Assert.Contains("UniDesk\\UniDesk.csproj", publishScript, StringComparison.Ordinal);
        Assert.Contains("UniDesk.HardwareService\\UniDesk.HardwareService.csproj", publishScript, StringComparison.Ordinal);
        Assert.Contains("UniDesk.HardwareRepair\\UniDesk.HardwareRepair.csproj", publishScript, StringComparison.Ordinal);
        Assert.Contains("status --porcelain", publishScript, StringComparison.Ordinal);
        Assert.Contains("'--locked-mode'", publishScript, StringComparison.Ordinal);
        Assert.Contains("sdkVersion", publishScript, StringComparison.Ordinal);
        Assert.Contains("globalJsonSha256", publishScript, StringComparison.Ordinal);
        Assert.Contains("packageLocks", publishScript, StringComparison.Ordinal);
        Assert.Contains("payloadFiles", publishScript, StringComparison.Ordinal);
        Assert.Contains("payloadDirectoryEntries", publishScript, StringComparison.Ordinal);
        Assert.Contains("authenticodeContentSha256", publishScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", publishScript, StringComparison.Ordinal);
        Assert.Contains("MyAppSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyHardwareServiceSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyHardwareRepairSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyOutputDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("[string]$ExpectedSourceRevision", installerScript, StringComparison.Ordinal);
        Assert.Contains("sourceRevision -ne $ExpectedSourceRevision", installerScript, StringComparison.Ordinal);
        Assert.Contains("$expectedProductVersion = \"$Version+$ExpectedSourceRevision\"", installerScript, StringComparison.Ordinal);
        Assert.Contains("ProductVersion.Equals", installerScript, StringComparison.Ordinal);
        Assert.Contains("Test-ReleasePayloadIntegrity.ps1", installerScript, StringComparison.Ordinal);
        Assert.Contains("[string]$UnsignedSourceManifestPath", installerScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $trustedManifestPath", installerScript, StringComparison.Ordinal);
        Assert.Contains("rev-parse HEAD", installerScript, StringComparison.Ordinal);
        Assert.Contains("status --porcelain", installerScript, StringComparison.Ordinal);
        var payloadTools = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "ReleasePayloadTools.ps1"));
        Assert.Contains("FileAttributes]::ReparsePoint", payloadTools, StringComparison.Ordinal);

        var buildScript = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "Build-Release.ps1"));
        Assert.Contains("ExpectedSourceRevision = $sourceRevision", buildScript, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseSigningWorkflow_ShouldBeManualAndSignPayloadBeforeInstaller()
    {
        var workflow = File.ReadAllText(Path.Combine(
            ProjectRoot,
            ".github",
            "workflows",
            "release-signing.yml"));

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("actions: read", workflow, StringComparison.Ordinal);
        Assert.Contains("github.ref == 'refs/heads/main'", workflow, StringComparison.Ordinal);
        Assert.Contains("SIGNPATH_API_TOKEN", workflow, StringComparison.Ordinal);
        Assert.Contains("SIGNPATH_PAYLOAD_ARTIFACT_CONFIGURATION_SLUG", workflow, StringComparison.Ordinal);
        Assert.Contains("SIGNPATH_INSTALLER_ARTIFACT_CONFIGURATION_SLUG", workflow, StringComparison.Ordinal);
        Assert.Contains("environment: release-signing", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_VERSION: ${{ inputs.version }}", workflow, StringComparison.Ordinal);
        Assert.Contains("SIGNPATH_EXPECTED_SIGNER_SUBJECT", workflow, StringComparison.Ordinal);
        Assert.Contains("-ExpectedSignerSubject $env:SIGNPATH_EXPECTED_SIGNER_SUBJECT", workflow, StringComparison.Ordinal);
        Assert.Contains("-UnsignedSourceManifestPath '${{ runner.temp }}\\unsigned-payload\\release-source.json'", workflow, StringComparison.Ordinal);
        Assert.Contains("id: bind-unsigned-manifest", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.bind-unsigned-manifest.outputs.sha256", workflow, StringComparison.Ordinal);
        Assert.Contains("name: Restore trusted installer source", workflow, StringComparison.Ordinal);
        Assert.Contains("-ExpectedSourceRevision '${{ github.sha }}'", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("'${{ inputs.version }}'", workflow, StringComparison.Ordinal);
        Assert.Equal(
            1,
            workflow.Split("${{ inputs.version }}", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "SIGNPATH_EXPECTED_SIGNER_SUBJECT: ${{ needs.sign-installer.outputs.expected-signer-subject }}",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "'^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)$'",
            workflow,
            StringComparison.Ordinal);
        var readinessScript = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "Test-ReleaseReadiness.ps1"));
        Assert.Contains("[string]$ExpectedSignerSubject", readinessScript, StringComparison.Ordinal);
        Assert.Contains("[string]$ExpectedUnsignedInstallerAuthenticodeContentSha256", readinessScript, StringComparison.Ordinal);
        Assert.Contains("$ExpectedPawnIoSignerSubject", readinessScript, StringComparison.Ordinal);
        Assert.Contains("SignerCertificate.Subject.Equals", readinessScript, StringComparison.Ordinal);
        Assert.Contains("$expectedProductVersion = \"$ExpectedVersion+$ExpectedSourceRevision\"", readinessScript, StringComparison.Ordinal);
        Assert.Contains("$productVersion.Equals", readinessScript, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeContentSha256 -Path $InstallerPath", readinessScript, StringComparison.Ordinal);
        Assert.Contains("[string]$UnsignedSourceManifestPath", readinessScript, StringComparison.Ordinal);
        var signPathSetup = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "docs",
            "signpath-foundation-setup.md"));
        Assert.Contains("App/UniDesk.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Contains("HardwareService/UniDesk.HardwareService.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Contains("HardwareRepair/UniDesk.HardwareRepair.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Equal(
            2,
            workflow.Split(
                $"signpath/github-action-submit-signing-request@{SignPathV2Commit} # v2",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            3,
            workflow.Split(
                $"actions/checkout@{CheckoutV6Commit} # v6",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("gh release create", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseSigningWorkflow_ShouldIsolateEachExternalSigningTrustBoundary()
    {
        var workflow = File.ReadAllText(Path.Combine(
            ProjectRoot,
            ".github",
            "workflows",
            "release-signing.yml"))
            .ReplaceLineEndings("\n");

        foreach (var jobName in new[]
                 {
                     "build-unsigned-payload:",
                     "sign-payload:",
                     "build-installer:",
                     "sign-installer:",
                     "verify-release-candidate:"
                 })
        {
            Assert.Contains(jobName, workflow, StringComparison.Ordinal);
        }

        Assert.Equal(
            5,
            workflow.Split("runs-on: windows-latest", StringSplitOptions.None).Length - 1);
        Assert.Contains("needs: build-unsigned-payload", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: sign-payload", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: build-installer", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: [build-installer, sign-installer]", workflow, StringComparison.Ordinal);
        Assert.Equal(
            2,
            workflow.Split("environment: release-signing", StringSplitOptions.None).Length - 1);
        Assert.Contains("unsigned-payload-artifact-id:", workflow, StringComparison.Ordinal);
        Assert.Contains("signed-payload-artifact-id:", workflow, StringComparison.Ordinal);
        Assert.Contains("unsigned-installer-artifact-id:", workflow, StringComparison.Ordinal);
        Assert.Contains("signed-installer-artifact-id:", workflow, StringComparison.Ordinal);
        Assert.Contains("installer-authenticode-content-sha256:", workflow, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeContentSha256 -Path $installerPath", workflow, StringComparison.Ordinal);
        Assert.Contains("artifact-ids: ${{ needs.sign-payload.outputs.unsigned-payload-artifact-id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("artifact-ids: ${{ needs.sign-payload.outputs.signed-payload-artifact-id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("github-artifact-id: ${{ needs.build-installer.outputs.unsigned-installer-artifact-id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("artifact-ids: ${{ needs.sign-installer.outputs.signed-installer-artifact-id }}", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedUnsignedInstallerAuthenticodeContentSha256 '${{ needs.build-installer.outputs.installer-authenticode-content-sha256 }}'",
            workflow,
            StringComparison.Ordinal);

        var payloadSigningJob = GetWorkflowJob(workflow, "sign-payload", "build-installer");
        var installerSigningJob = GetWorkflowJob(workflow, "sign-installer", "verify-release-candidate");
        var verificationJob = workflow[workflow.IndexOf("  verify-release-candidate:\n", StringComparison.Ordinal)..];
        Assert.DoesNotContain("dotnet build", payloadSigningJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test-ReleaseReadiness.ps1", payloadSigningJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet build", installerSigningJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test-ReleaseReadiness.ps1", installerSigningJob, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"actions/setup-dotnet@{SetupDotnetV5Commit} # v5",
            verificationJob,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubWorkflows_ShouldUseNode24BasedOfficialActions()
    {
        var ciWorkflow = File.ReadAllText(Path.Combine(
            ProjectRoot,
            ".github",
            "workflows",
            "ci.yml"));
        var signingWorkflow = File.ReadAllText(Path.Combine(
            ProjectRoot,
            ".github",
            "workflows",
            "release-signing.yml"));

        Assert.Contains($"actions/checkout@{CheckoutV6Commit} # v6", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains($"actions/setup-dotnet@{SetupDotnetV5Commit} # v5", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains($"actions/checkout@{CheckoutV6Commit} # v6", signingWorkflow, StringComparison.Ordinal);
        Assert.Contains($"actions/setup-dotnet@{SetupDotnetV5Commit} # v5", signingWorkflow, StringComparison.Ordinal);
        Assert.Contains($"actions/upload-artifact@{UploadArtifactV7Commit} # v7", signingWorkflow, StringComparison.Ordinal);
        Assert.Contains($"actions/download-artifact@{DownloadArtifactV8Commit} # v8", signingWorkflow, StringComparison.Ordinal);
        Assert.Equal(
            5,
            signingWorkflow.Split(
                $"actions/download-artifact@{DownloadArtifactV8Commit} # v8",
                StringSplitOptions.None).Length - 1);
        Assert.Contains($"signpath/github-action-submit-signing-request@{SignPathV2Commit} # v2", signingWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/checkout@v6", signingWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/setup-dotnet@v5", signingWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/upload-artifact@v7", signingWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/download-artifact@v8", signingWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: signpath/github-action-submit-signing-request@v2", signingWorkflow, StringComparison.Ordinal);
    }

    private static string GetWorkflowJob(string workflow, string jobName, string nextJobName)
    {
        var start = workflow.IndexOf($"  {jobName}:\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Unable to find workflow job '{jobName}'.");
        var end = workflow.IndexOf($"  {nextJobName}:\n", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Unable to isolate workflow job '{jobName}'.");
        return workflow[start..end];
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("remove", false)]
    [InlineData("replace", false)]
    [InlineData("add-empty-directory", false)]
    [InlineData("signing-code-change", false)]
    [InlineData("signing-only-change", true)]
    public void ReleasePayloadIntegrityGate_ShouldRejectUnsignedCompanionInventoryChanges(
        string mutation,
        bool shouldPass)
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk_payload_integrity_{mutation}_{Guid.NewGuid():N}");
        var appDirectory = Directory.CreateDirectory(Path.Combine(testRoot, "App")).FullName;
        Directory.CreateDirectory(Path.Combine(testRoot, "HardwareService"));
        Directory.CreateDirectory(Path.Combine(testRoot, "HardwareRepair"));
        var signingTargets = new[]
        {
            "App/UniDesk.exe",
            "App/UniDesk.dll",
            "App/UniDesk.Hardware.Contracts.dll",
            "HardwareService/UniDesk.HardwareService.exe",
            "HardwareService/UniDesk.HardwareService.dll",
            "HardwareService/UniDesk.Hardware.Contracts.dll",
            "HardwareRepair/UniDesk.HardwareRepair.exe",
            "HardwareRepair/UniDesk.HardwareRepair.dll"
        };
        var payloadFiles = new List<Dictionary<string, object>>();
        foreach (var relativePath in signingTargets)
        {
            var fullPath = Path.Combine(testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(fullPath, CreateMinimalUnsignedPe());
            var unsignedHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
            payloadFiles.Add(new()
            {
                ["path"] = relativePath,
                ["sha256"] = unsignedHash,
                ["signingRequired"] = true,
                ["authenticodeContentSha256"] = unsignedHash
            });
        }
        var companionPath = Path.Combine(appDirectory, "companion.dll");
        File.WriteAllText(companionPath, "trusted payload");
        var trustedHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(companionPath))).ToLowerInvariant();
        payloadFiles.Add(new()
        {
            ["path"] = "App/companion.dll",
            ["sha256"] = trustedHash,
            ["signingRequired"] = false
        });
        var manifestPath = Path.Combine(testRoot, "release-source.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schema = 3,
                payloadDirectoryEntries = new[] { "App", "HardwareService", "HardwareRepair" },
                payloadFiles
            }));

        var scriptPath = Path.Combine(
            ProjectRoot,
            "scripts",
            "Test-ReleasePayloadIntegrity.ps1");
        Assert.Equal(0, RunPayloadIntegrityGate(scriptPath, testRoot, manifestPath));

        switch (mutation)
        {
            case "add":
                File.WriteAllText(Path.Combine(appDirectory, "injected.dll"), "injected");
                break;
            case "remove":
                File.Delete(companionPath);
                break;
            case "replace":
                File.WriteAllText(companionPath, "replaced payload");
                break;
            case "add-empty-directory":
                Directory.CreateDirectory(Path.Combine(appDirectory, "injected-empty"));
                break;
            case "signing-code-change":
                var signingTarget = Path.Combine(appDirectory, "UniDesk.exe");
                File.WriteAllBytes(
                    signingTarget,
                    SimulateAuthenticodeSigning(File.ReadAllBytes(signingTarget), changeCode: true));
                break;
            case "signing-only-change":
                var validSigningTarget = Path.Combine(appDirectory, "UniDesk.exe");
                File.WriteAllBytes(
                    validSigningTarget,
                    SimulateAuthenticodeSigning(File.ReadAllBytes(validSigningTarget), changeCode: false));
                break;
        }

        var result = RunPayloadIntegrityGate(
            scriptPath,
            testRoot,
            manifestPath,
            allowSigningChanges: mutation.StartsWith("signing-", StringComparison.Ordinal));
        if (shouldPass)
        {
            Assert.Equal(0, result);
        }
        else
        {
            Assert.NotEqual(0, result);
        }
    }

    private static int RunPayloadIntegrityGate(
        string scriptPath,
        string payloadRoot,
        string manifestPath,
        bool allowSigningChanges = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-PayloadRoot");
        startInfo.ArgumentList.Add(payloadRoot);
        startInfo.ArgumentList.Add("-SourceManifestPath");
        startInfo.ArgumentList.Add(manifestPath);
        if (allowSigningChanges)
        {
            startInfo.ArgumentList.Add("-AllowSigningChanges");
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        process.WaitForExit(30_000);
        return process.ExitCode;
    }

    private static byte[] CreateMinimalUnsignedPe()
    {
        var image = new byte[512];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(image, 0x3c);
        image[0x80] = (byte)'P';
        image[0x81] = (byte)'E';
        const int optionalHeaderOffset = 0x80 + 4 + 20;
        BitConverter.GetBytes((ushort)0x20b).CopyTo(image, optionalHeaderOffset);
        return image;
    }

    private static byte[] SimulateAuthenticodeSigning(byte[] unsignedImage, bool changeCode)
    {
        var signedImage = new byte[unsignedImage.Length + 8];
        unsignedImage.CopyTo(signedImage, 0);
        if (changeCode)
        {
            signedImage[0x150] ^= 0x5a;
        }

        const int optionalHeaderOffset = 0x80 + 4 + 20;
        BitConverter.GetBytes(0x12345678).CopyTo(signedImage, optionalHeaderOffset + 64);
        var certificateDirectoryOffset = optionalHeaderOffset + 112 + (4 * 8);
        BitConverter.GetBytes(unsignedImage.Length).CopyTo(signedImage, certificateDirectoryOffset);
        BitConverter.GetBytes(8).CopyTo(signedImage, certificateDirectoryOffset + 4);
        for (var index = unsignedImage.Length; index < signedImage.Length; index++)
        {
            signedImage[index] = 0xa5;
        }
        return signedImage;
    }

    [Fact]
    public void BuildInputs_ShouldBeLockedForReproducibleRestore()
    {
        var globalJson = File.ReadAllText(Path.Combine(ProjectRoot, "global.json"));
        Assert.Contains("10.0.302", globalJson, StringComparison.Ordinal);
        Assert.Contains("\"rollForward\": \"disable\"", globalJson, StringComparison.Ordinal);

        foreach (var projectDirectory in new[]
                 {
                     "UniDesk",
                     "UniDesk.Tests",
                     "UniDesk.Hardware.Contracts",
                     "UniDesk.HardwareRepair",
                     "UniDesk.HardwareService"
                 })
        {
            Assert.True(
                File.Exists(Path.Combine(ProjectRoot, projectDirectory, "packages.lock.json")),
                $"{projectDirectory} is missing packages.lock.json.");
        }

        foreach (var workflowName in new[] { "ci.yml", "release-signing.yml" })
        {
            var workflow = File.ReadAllText(Path.Combine(ProjectRoot, ".github", "workflows", workflowName));
            Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
            Assert.Contains("dotnet restore UniDesk.sln", workflow, StringComparison.Ordinal);
            Assert.Contains("--locked-mode", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuildReleaseScript_ShouldCreateShortRevisionWithValidPowerShellSyntax()
    {
        var script = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "Build-Release.ps1"));

        Assert.Contains("$sourceRevision.Substring", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$sourceRevision[..", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicReleaseDocumentation_ShouldDiscloseCodeSigningAndPrivacyPolicies()
    {
        const string sponsorship =
            "Free code signing provided by SignPath.io, certificate by SignPath Foundation";
        var codeSigningPolicy = File.ReadAllText(Path.Combine(ProjectRoot, "CODE_SIGNING_POLICY.md"));
        var privacyPolicy = File.ReadAllText(Path.Combine(ProjectRoot, "PRIVACY.md"));

        Assert.Contains("# Code signing policy", codeSigningPolicy, StringComparison.Ordinal);
        Assert.Contains(sponsorship, codeSigningPolicy, StringComparison.Ordinal);
        Assert.Contains("Authors", codeSigningPolicy, StringComparison.Ordinal);
        Assert.Contains("Reviewers", codeSigningPolicy, StringComparison.Ordinal);
        Assert.Contains("Approvers", codeSigningPolicy, StringComparison.Ordinal);
        Assert.Contains("PRIVACY.md", codeSigningPolicy, StringComparison.Ordinal);

        Assert.Contains(
            "This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.",
            privacyPolicy,
            StringComparison.Ordinal);
        Assert.Contains("https://www.qweather.com/terms/privacy", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("https://www.microsoft.com/en-us/privacy/privacystatement", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("Clipboard history is enabled by default for fresh installations", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("全新安装默认启用剪贴板历史", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("https://modeldial.com/api/v1/radar/latest.json", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("Model Radar is disabled by default", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("模型雷达默认关闭", privacyPolicy, StringComparison.Ordinal);
        Assert.Contains("cache\\modeldial-radar.json", privacyPolicy, StringComparison.Ordinal);

        foreach (var entryPoint in new[]
                 {
                     "README.md",
                     "README.zh-CN.md",
                     "README.en-US.md",
                     "README.ja-JP.md",
                     "README.es-ES.md",
                     Path.Combine("docs", "release-unidesk.md")
                 })
        {
            var content = File.ReadAllText(Path.Combine(ProjectRoot, entryPoint));
            Assert.Contains("## Code signing policy", content, StringComparison.Ordinal);
            Assert.Contains(sponsorship, content, StringComparison.Ordinal);
            Assert.Contains("CODE_SIGNING_POLICY.md", content, StringComparison.Ordinal);
            Assert.Contains("PRIVACY.md", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void V210ReleaseDocumentation_ShouldCoverModelRadarAndFreshInstallModuleDefaults()
    {
        var releaseNotes = File.ReadAllText(Path.Combine(ProjectRoot, "docs", "release-unidesk.md"));
        var releaseAudit = File.ReadAllText(Path.Combine(ProjectRoot, "docs", "release-audit-2.1.0.md"));
        var testMatrix = File.ReadAllText(Path.Combine(ProjectRoot, "docs", "release-test-matrix-2.1.0.md"));

        Assert.Contains("Model Radar", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("模型雷达", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("时间天气、硬件监视、待办事项和快速便签", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("2026-08-30 模型雷达候选修订", releaseAudit, StringComparison.Ordinal);
        Assert.Contains("isDirty=true", releaseAudit, StringComparison.Ordinal);
        Assert.Contains("MR-01", testMatrix, StringComparison.Ordinal);
        Assert.Contains("MR-06", testMatrix, StringComparison.Ordinal);
        Assert.Contains("全新安装默认启用时间天气、硬件监视、待办事项和快速便签", testMatrix, StringComparison.Ordinal);
    }
}
