namespace UniDesk.Tests;

public class ReleasePipelineTests
{
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
        Assert.Contains("MyAppSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyHardwareServiceSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyHardwareRepairSourceDir", installerScript, StringComparison.Ordinal);
        Assert.Contains("MyOutputDir", installerScript, StringComparison.Ordinal);
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
        var signPathSetup = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "docs",
            "signpath-foundation-setup.md"));
        Assert.Contains("App/UniDesk.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Contains("HardwareService/UniDesk.HardwareService.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Contains("HardwareRepair/UniDesk.HardwareRepair.dll", signPathSetup, StringComparison.Ordinal);
        Assert.Equal(
            2,
            workflow.Split("signpath/github-action-submit-signing-request@v2", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("gh release create", workflow, StringComparison.OrdinalIgnoreCase);
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
}
