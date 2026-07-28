using System.Security.Cryptography;

namespace UniDesk.Tests;

public class InstallerHardwareComponentTests
{
    private const string ExpectedPawnIoSha256 =
        "a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0";

    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Installer_ShouldDefaultToDisclosedCompleteHardwareComponent()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));
        var taskLines = script.Split('\n')
            .Where(line => line.StartsWith("Name: \"", StringComparison.Ordinal))
            .ToArray();
        var desktopTask = Assert.Single(taskLines, line =>
            line.StartsWith("Name: \"desktopicon\"", StringComparison.Ordinal));
        var hardwareTask = Assert.Single(taskLines, line =>
            line.StartsWith("Name: \"completehardware\"", StringComparison.Ordinal));

        Assert.Contains("PrivilegesRequired=admin", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64os", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64os", script, StringComparison.Ordinal);
        Assert.DoesNotContain("x64compatible", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UsePreviousTasks=no", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Flags:", desktopTask, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Flags:", hardwareTask, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Name: \"completehardware\"", script, StringComparison.Ordinal);
        Assert.Contains("将安装 PawnIO 驱动和以 LocalSystem 运行的只读硬件监控服务", script, StringComparison.Ordinal);
        Assert.Contains("WizardIsTaskSelected('completehardware')", script, StringComparison.Ordinal);
        Assert.Contains("--install-or-repair", script, StringComparison.Ordinal);
        Assert.Contains("UniDeskHardwareService", script, StringComparison.Ordinal);
        Assert.Contains("procedure InstallHardwareComponent", script, StringComparison.Ordinal);
        Assert.Contains("[Run]", script, StringComparison.Ordinal);
        Assert.Contains("runasoriginaluser", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Filename: \"{app}\\{#MyAppExeName}\"; Description: \"{cm:LaunchProgram",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("runascurrentuser", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Filename: \"{app}\\Hardware\\PawnIO_setup.exe\"; Parameters: \"-install -silent\"",
            script,
            StringComparison.Ordinal);

        var manifest = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk", "app.manifest"));
        Assert.Contains("requestedExecutionLevel level=\"asInvoker\"", manifest, StringComparison.Ordinal);

        var verifier = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "HardwarePackageVerifier.cs"));
        Assert.Contains(ExpectedPawnIoSha256, verifier, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-AuthenticodeSignature", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_HardwareComponentFailure_ShouldWarnWithoutAbortingBaseInstallation()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("procedure ReportHardwareComponentFailure", script, StringComparison.Ordinal);
        Assert.Contains("HardwareRepairFailed", script, StringComparison.Ordinal);
        Assert.Contains("Log(", script, StringComparison.Ordinal);
        Assert.Contains("MsgBox(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RaiseException", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldRemoveOwnedServiceButPreserveSharedPawnIoByDefault()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("--remove-service", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("procedure RemoveOwnedHardwareService", script, StringComparison.Ordinal);
        Assert.Contains("'delete {#HardwareServiceName}'", script, StringComparison.Ordinal);
        Assert.Contains("是否同时卸载共享的 PawnIO 驱动", script, StringComparison.Ordinal);
        Assert.Contains("if (not UninstallSilent) and", script, StringComparison.Ordinal);
        Assert.Contains("{cm:RemovePawnIoPrompt}", script, StringComparison.Ordinal);
        Assert.Contains("{cm:PawnIoRemoveFailed}", script, StringComparison.Ordinal);
        Assert.Contains("{cm:HardwareServiceStopFailed}", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MsgBox('PawnIO", script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Filename: \"{app}\\Hardware\\PawnIO_setup.exe\"; Parameters: \"-uninstall -silent\"",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Repair_ShouldElevateOnlyTheDedicatedHelper()
    {
        var maintenanceService = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk",
            "Services",
            "HardwareMonitoringMaintenanceService.cs"));

        Assert.Contains("UniDesk.HardwareRepair.exe", maintenanceService, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = true", maintenanceService, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldPackageDedicatedElevatedRepairHelper()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));
        var helperProject = Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "UniDesk.HardwareRepair.csproj");
        var helperManifest = Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "app.manifest");
        var helperRunner = Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "ProcessRunner.cs");

        Assert.True(File.Exists(helperProject), "Dedicated repair helper project is missing.");
        Assert.True(File.Exists(helperManifest), "Dedicated repair helper manifest is missing.");
        Assert.True(File.Exists(helperRunner), "Dedicated repair helper runner is missing.");
        Assert.Contains(
            "requestedExecutionLevel level=\"requireAdministrator\"",
            File.ReadAllText(helperManifest),
            StringComparison.Ordinal);
        Assert.Contains("MyHardwareRepairSourceDir", script, StringComparison.Ordinal);
        Assert.Contains("DestDir: \"{app}\\HardwareRepair\"", script, StringComparison.Ordinal);
        Assert.Contains("--install-or-repair", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UniDesk_Setup.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("create {#HardwareServiceName} binPath=", script, StringComparison.OrdinalIgnoreCase);

        var runner = File.ReadAllText(helperRunner);
        Assert.Contains("ArgumentList.Add", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Arguments =", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void BundledPawnIo_ShouldMatchPinnedOfficialBinary()
    {
        var path = Path.Combine(ProjectRoot, "installer-assets", "PawnIO_setup.exe");

        Assert.True(File.Exists(path), "Pinned PawnIO installer is missing.");
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

        Assert.Equal(ExpectedPawnIoSha256, actual);
    }
}
