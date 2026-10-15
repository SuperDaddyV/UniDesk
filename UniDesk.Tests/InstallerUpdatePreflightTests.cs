namespace UniDesk.Tests;

public sealed class InstallerUpdatePreflightTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Installer_ShouldCloseValidatedApplicationPathsBeforeReplacingPayload()
    {
        var script = File.ReadAllText(Path.Combine(Root, "UniDesk.iss"));
        var prepare = script[script.IndexOf("function PrepareToInstall", StringComparison.Ordinal)..script.IndexOf("function NextButtonClick", StringComparison.Ordinal)];
        Assert.Contains("CloseInstalledApplications", prepare);
        Assert.Contains("ShutdownLegacyApplicationPath := '';", prepare);
        Assert.True(prepare.IndexOf("ShutdownLegacyApplicationPath := '';", StringComparison.Ordinal) <
            prepare.IndexOf("GetPreviousUniDeskApplicationDirectory", StringComparison.Ordinal));
        Assert.True(prepare.IndexOf("CloseInstalledApplications", StringComparison.Ordinal) > prepare.IndexOf("IsSafeApplicationInstallTarget", StringComparison.Ordinal));
        Assert.Contains("--close-application", script);
        Assert.Contains("--check-application", script);
        Assert.Contains("ExtractTemporaryFiles", script);
        var beforeCopy = script[script.IndexOf("procedure CurStepChanged", StringComparison.Ordinal)..script.IndexOf("procedure DeinitializeSetup", StringComparison.Ordinal)];
        Assert.Contains("VerifyInstalledApplicationsExited", beforeCopy);
        Assert.DoesNotContain("CloseApplications=force", script);
    }

    [Fact]
    public void Setup_ShouldNotBlockAutomaticExitWithAnEarlyMutexPrompt()
    {
        var script = File.ReadAllText(Path.Combine(Root, "UniDesk.iss"));
        Assert.Contains("AppMutex={code:GetApplicationMutex}", script);
        Assert.Contains("UniDesk.SingleInstance.6B9BD6F1-8E3A-4C5D-9F2B-1A7C8D3E5F9A.Mutex", script);
        foreach (var language in new[] { "chinesesimp", "english", "japanese", "spanish" })
            Assert.Contains(language + ".ApplicationExitFailed=", script);
    }
}
