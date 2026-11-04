namespace UniDesk.Tests;

public sealed class InstallerHardwareUpgradeTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Installer_UncheckedHardwareTaskMustNotRemoveAnExistingService()
    {
        var install = ReadSection("procedure InstallHardwareComponent", "function TryExtractExecutablePath");

        Assert.DoesNotContain("--remove-service", install, StringComparison.Ordinal);
        Assert.DoesNotContain("'delete {#HardwareServiceName}'", install, StringComparison.Ordinal);
        Assert.Contains("--install-or-repair", install, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldSkipOnlyUncheckedHardwareWithoutAnExistingOwnedComponent()
    {
        var install = ReadSection("procedure InstallHardwareComponent", "function TryExtractExecutablePath");
        var maintenance = install.IndexOf("'--install-or-repair'", StringComparison.Ordinal);
        var skip = install.IndexOf(
            "if (not WizardIsTaskSelected('completehardware')) and" + Environment.NewLine +
            "    (not PreserveExistingHardwareComponent) then",
            StringComparison.Ordinal);

        Assert.True(skip >= 0 && skip < maintenance,
            "Only an unchecked task without a previously owned component may skip maintenance.");
        Assert.Contains("Exit;", install[skip..maintenance], StringComparison.Ordinal);
        Assert.DoesNotContain("HardwareServiceExists", install[skip..maintenance], StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldRememberOwnedComponentBeforeStoppingOrMigratingIt()
    {
        var prepare = ReadSection("function PrepareToInstall", "function NextButtonClick");
        var owned = prepare.IndexOf("if not IsHardwareServiceOwned then", StringComparison.Ordinal);
        var remember = prepare.IndexOf("PreserveExistingHardwareComponent := True;", StringComparison.Ordinal);
        var stop = prepare.IndexOf("'stop {#HardwareServiceName}'", StringComparison.Ordinal);
        Assert.True(owned >= 0 && remember > owned && stop > remember,
            "Preservation must follow ownership validation and precede stopping the protected service.");

        var legacy = ReadSection("function RetireLegacyAppHostedHardwareService", "function ResetDirectoryChildrenAcl");
        var legacyOwnership = legacy.IndexOf("GetLegacyAppHostedOwnedServicePath", StringComparison.Ordinal);
        var legacyRemember = legacy.IndexOf("PreserveExistingHardwareComponent := True;", StringComparison.Ordinal);
        var retire = legacy.IndexOf("Result := RetireOwnedHardwareServiceAt", StringComparison.Ordinal);
        Assert.True(legacyOwnership >= 0 && legacyRemember > legacyOwnership && retire > legacyRemember,
            "An owned legacy service must remain selected for migration after its registration is removed.");
        Assert.DoesNotContain("PreserveExistingHardwareComponent := False", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("PreserveExistingHardwareComponent := False", legacy, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PrepareRetryMustRetainStoppedServiceForCancellationRecovery()
    {
        var prepare = ReadSection("function PrepareToInstall", "function NextButtonClick");
        Assert.Contains(
            "StoppedOwnedHardwareService := StoppedOwnedHardwareService or (ResultCode = 0);",
            prepare,
            StringComparison.Ordinal);

        var exit = ReadSection("procedure DeinitializeSetup", "function InitializeUninstall");
        Assert.Contains("RestartOwnedHardwareServiceIfNeeded;", exit, StringComparison.Ordinal);
        var restart = ReadSection("procedure RestartOwnedHardwareServiceIfNeeded", "function PrepareToInstall");
        Assert.Contains("if not IsHardwareServiceOwned then", restart, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLegacyAppHostedOwnedServicePath", restart, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_UnfinishedLegacyMigrationMustBeReportedWithoutRestartingTheOldPath()
    {
        var legacy = ReadSection("function RetireLegacyAppHostedHardwareService", "function ResetDirectoryChildrenAcl");
        Assert.Contains("LegacyHardwareServiceMigrationPending := True;", legacy, StringComparison.Ordinal);
        var prepare = ReadSection("function PrepareToInstall", "function NextButtonClick");
        Assert.DoesNotContain("LegacyHardwareServiceMigrationPending := False", prepare, StringComparison.Ordinal);

        var install = ReadSection("procedure InstallHardwareComponent", "function TryExtractExecutablePath");
        Assert.Contains(
            "if (ResultCode = 0) or (ResultCode = HardwareCompatibilityExitCode) then",
            install,
            StringComparison.Ordinal);
        Assert.Contains("LegacyHardwareServiceMigrationPending := False;", install, StringComparison.Ordinal);

        var exit = ReadSection("procedure DeinitializeSetup", "function InitializeUninstall");
        Assert.Contains("if LegacyHardwareServiceMigrationPending then", exit, StringComparison.Ordinal);
        Assert.Contains("Legacy hardware service migration did not complete", exit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireOwnedHardwareServiceAt", exit, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ExplicitUninstallMustStillRemoveTheOwnedService()
    {
        var uninstall = ReadSection("procedure RemoveOwnedHardwareService", "function CleanupOwnedStartupEntries");
        Assert.Contains("'--remove-service'", uninstall, StringComparison.Ordinal);
        Assert.Contains("if not IsHardwareServiceOwned then", uninstall, StringComparison.Ordinal);
        Assert.DoesNotContain("PreserveExistingHardwareComponent", uninstall, StringComparison.Ordinal);
    }

    private static string ReadSection(string start, string end)
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"))
            .ReplaceLineEndings(Environment.NewLine);
        var startIndex = script.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Installer section not found: {start}");
        var endIndex = script.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Installer section boundary not found: {end}");
        return script[startIndex..endIndex];
    }
}
