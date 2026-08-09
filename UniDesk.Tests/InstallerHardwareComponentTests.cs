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
        Assert.Contains("MinVersion=10.0.18362", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64os", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64os", script, StringComparison.Ordinal);
        Assert.DoesNotContain("x64compatible", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UsePreviousTasks=no", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Flags:", desktopTask, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Flags:", hardwareTask, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Name: \"completehardware\"", script, StringComparison.Ordinal);
        Assert.Contains("将安装 PawnIO 驱动和以 LocalSystem 运行的只读硬件监控服务", script, StringComparison.Ordinal);
        Assert.DoesNotContain("InfoBeforeFile", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WizardIsTaskSelected('completehardware')", script, StringComparison.Ordinal);
        Assert.Contains("--install-or-repair", script, StringComparison.Ordinal);
        Assert.Contains("UniDeskHardwareService", script, StringComparison.Ordinal);
        Assert.Contains("procedure InstallHardwareComponent", script, StringComparison.Ordinal);
        Assert.Contains("[Run]", script, StringComparison.Ordinal);
        Assert.Contains("runasoriginaluser", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Filename: \"{app}\\{#MyAppExeName}\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains("Description: \"{cm:LaunchProgram", script, StringComparison.Ordinal);
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
        Assert.Contains("function IsHardwareServiceOwned", script, StringComparison.Ordinal);
        Assert.Contains("if not IsHardwareServiceOwned then", script, StringComparison.Ordinal);
        Assert.Contains("ServiceOwnershipInvalid", File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "HardwareRepairExitCode.cs")), StringComparison.Ordinal);
        Assert.Contains("--cleanup-startup", script, StringComparison.Ordinal);

        var prepareIndex = script.IndexOf("function PrepareToInstall", StringComparison.Ordinal);
        var prepareHardeningIndex = script.IndexOf(
            "if not HardenInstallationPayload then",
            prepareIndex,
            StringComparison.Ordinal);
        var serviceStopIndex = script.IndexOf(
            "'stop {#HardwareServiceName}'",
            prepareIndex,
            StringComparison.Ordinal);
        Assert.True(
            prepareHardeningIndex > prepareIndex && serviceStopIndex > prepareHardeningIndex,
            "Potentially failing ACL hardening must complete before the existing service is stopped.");
        Assert.Contains("StoppedOwnedHardwareService", script, StringComparison.Ordinal);
        Assert.Contains("WaitForHardwareServiceStopped", script, StringComparison.Ordinal);
        Assert.Contains("'/F /FI \"SERVICES eq {#HardwareServiceName}\"'", script, StringComparison.Ordinal);
        Assert.Contains("procedure RestartOwnedHardwareServiceIfNeeded", script, StringComparison.Ordinal);
        Assert.Contains("procedure DeinitializeSetup", script, StringComparison.Ordinal);
        Assert.Contains("RestartOwnedHardwareServiceIfNeeded;", script, StringComparison.Ordinal);

        var removeIndex = script.IndexOf("procedure RemoveOwnedHardwareService", StringComparison.Ordinal);
        var fallbackOwnershipIndex = script.IndexOf(
            "if not IsHardwareServiceOwned then",
            removeIndex,
            StringComparison.Ordinal);
        var fallbackRetirementIndex = script.IndexOf(
            "RetireUnsafeOwnedHardwareService(ExpandConstant('{app}'))",
            removeIndex,
            StringComparison.Ordinal);
        Assert.True(
            fallbackOwnershipIndex > removeIndex && fallbackRetirementIndex > fallbackOwnershipIndex,
            "Uninstall fallback can delete a same-named foreign service.");
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
    public void Installer_ShouldHardenInstallPayloadBeforeRunningElevatedMaintenance()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("function HardenInstallationPayload: Boolean", script, StringComparison.Ordinal);
        Assert.Contains("{sys}\\icacls.exe", script, StringComparison.Ordinal);
        Assert.Contains("/setowner", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/inheritance:r", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/remove:g", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/T /C /L /Q", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("*S-1-5-11", script, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-32-545", script, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-18:(OI)(CI)F", script, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-32-544:(OI)(CI)F", script, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-32-545:(OI)(CI)RX", script, StringComparison.Ordinal);
        Assert.Contains("ForceDirectories(AppPath)", script, StringComparison.Ordinal);
        Assert.Contains("if not HardenInstallationPayload then", script, StringComparison.Ordinal);
        Assert.Contains(
            "Result := ExpandConstant('{cm:HardwareAclFailed}')",
            script,
            StringComparison.Ordinal);

        var hardeningIndex = script.IndexOf("if not HardenInstallationPayload then", StringComparison.Ordinal);
        var maintenanceIndex = script.IndexOf("InstallHardwareComponent;", hardeningIndex, StringComparison.Ordinal);
        Assert.True(hardeningIndex >= 0, "Installer does not enforce payload ACL hardening.");
        Assert.True(maintenanceIndex > hardeningIndex, "Maintenance runs before payload ACL hardening.");

        var verifier = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "ServicePayloadSecurityVerifier.cs"));
        Assert.Contains("GetOwner", verifier, StringComparison.Ordinal);
        Assert.Contains("GetAccessRules", verifier, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", verifier, StringComparison.Ordinal);
        Assert.Contains("BuiltinAdministratorsSid", verifier, StringComparison.Ordinal);
        Assert.Contains("ServicePayloadSecurityInvalid", File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "HardwareRepairExitCode.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ShouldValidateTargetOwnershipBeforeRecursiveAclHardening()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("function IsSafeInstallationTargetForAcl", script, StringComparison.Ordinal);
        Assert.Contains("function IsKnownUniDeskInstallationDirectory", script, StringComparison.Ordinal);
        Assert.Contains("function IsDirectoryEmpty", script, StringComparison.Ordinal);
        Assert.Contains("function ContainsReparsePoint", script, StringComparison.Ordinal);
        Assert.Contains("FileAttributeReparsePoint", script, StringComparison.Ordinal);
        Assert.Contains("RegQueryStringValue(", script, StringComparison.Ordinal);
        Assert.Contains("HKLM64,", script, StringComparison.Ordinal);
        Assert.Contains("{4B0F3B03-7F5D-4B5D-B2F4-6816B931C7D2}_is1", script, StringComparison.Ordinal);
        Assert.Contains("ExtractFileDrive(NormalizedPath)", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{win}')", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{pf}')", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{pf32}')", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{commonappdata}')", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{userprofile}')", script, StringComparison.Ordinal);
        Assert.Contains("if not DirExists(NormalizedPath) then", script, StringComparison.Ordinal);
        Assert.Contains("Result := IsDirectoryEmpty(NormalizedPath)", script, StringComparison.Ordinal);
        Assert.Contains("IsKnownUniDeskInstallationDirectory(NormalizedPath)", script, StringComparison.Ordinal);
        Assert.Contains("ContainsReparsePoint(NormalizedPath)", script, StringComparison.Ordinal);
        Assert.Contains("not IsSafeInstallationTargetForAcl(AppPath)", script, StringComparison.Ordinal);
        Assert.Contains("ValidatedAclTarget", script, StringComparison.Ordinal);
        Assert.Contains("not IsSameDirectory(AppPath, ValidatedAclTarget)", script, StringComparison.Ordinal);
        Assert.Contains("ValidatedAclTarget := NormalizeDirectoryPath(AppPath)", script, StringComparison.Ordinal);

        var validationIndex = script.IndexOf(
            "not IsSafeInstallationTargetForAcl(AppPath)",
            StringComparison.Ordinal);
        var recursiveHardeningIndex = script.IndexOf(
            "HardenDirectoryAcl(AppPath)",
            validationIndex,
            StringComparison.Ordinal);
        Assert.True(validationIndex >= 0, "Installer does not validate the target directory scope.");
        Assert.True(
            recursiveHardeningIndex > validationIndex,
            "Recursive ACL hardening runs before target ownership validation.");
    }

    [Fact]
    public void Installer_ShouldRequireTheProtectedProgramFilesTargetForEveryInstallation()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("DisableDirPage=yes", script, StringComparison.Ordinal);
        Assert.Contains("UsePreviousAppDir=no", script, StringComparison.Ordinal);
        Assert.Contains("function IsProtectedInstallTargetAllowed", script, StringComparison.Ordinal);
        Assert.Contains("function VerifyProtectedInstallAncestorAcl", script, StringComparison.Ordinal);
        Assert.Contains("GetAccessRules", script, StringComparison.Ordinal);
        Assert.Contains("PropagationFlags]::InheritOnly", script, StringComparison.Ordinal);
        Assert.Contains(
            "SetEnvironmentVariable('UNIDESK_PROTECTED_ROOT'",
            script,
            StringComparison.Ordinal);
        Assert.Contains("$env:UNIDESK_PROTECTED_ROOT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$env:ProgramFiles", script, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{autopf}\\{#MyAppName}')", script, StringComparison.Ordinal);
        Assert.Contains("{cm:HardwareProtectedLocationRequired}", script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "or clear the complete hardware monitoring option",
            script,
            StringComparison.OrdinalIgnoreCase);

        var prepareIndex = script.IndexOf("function PrepareToInstall", StringComparison.Ordinal);
        var protectedTargetIndex = script.IndexOf(
            "if not IsProtectedInstallTargetAllowed",
            prepareIndex,
            StringComparison.Ordinal);
        var ancestorAclIndex = script.IndexOf(
            "if not VerifyProtectedInstallAncestorAcl",
            prepareIndex,
            StringComparison.Ordinal);
        var stopIndex = script.IndexOf("'stop {#HardwareServiceName}'", prepareIndex, StringComparison.Ordinal);
        Assert.True(
            protectedTargetIndex > prepareIndex && ancestorAclIndex > protectedTargetIndex && stopIndex > ancestorAclIndex,
            "The protected target and its ancestor ACLs must be rejected before stopping an existing service.");

        var verifier = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "ServicePayloadSecurityVerifier.cs"));
        Assert.Contains("EnumerateAncestorDirectories", verifier, StringComparison.Ordinal);
        Assert.Contains("AncestorDangerousRights", verifier, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", verifier, StringComparison.Ordinal);
        Assert.Contains("PropagationFlags.InheritOnly", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void Upgrade_ShouldSafelyMigrateALegacyUnsafeInstallWithoutExecutingItsUninstaller()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk.iss"));

        Assert.Contains("function IsHardwareServiceOwnedAt", script, StringComparison.Ordinal);
        Assert.Contains("function GetRegisteredUniDeskInstallationDirectory", script, StringComparison.Ordinal);
        Assert.Contains("function HasLegacyUnsafeRegisteredInstallation", script, StringComparison.Ordinal);
        Assert.Contains("function RetireUnsafeOwnedHardwareService", script, StringComparison.Ordinal);
        Assert.Contains("'config {#HardwareServiceName} start= disabled'", script, StringComparison.Ordinal);
        Assert.Contains("'stop {#HardwareServiceName}'", script, StringComparison.Ordinal);
        Assert.Contains("'delete {#HardwareServiceName}'", script, StringComparison.Ordinal);
        Assert.Contains("{cm:HardwareUnsafeServiceRetired}", script, StringComparison.Ordinal);
        Assert.Contains("手动删除旧程序文件夹", script, StringComparison.Ordinal);
        Assert.DoesNotContain("卸载旧版主程序", script, StringComparison.Ordinal);
        Assert.DoesNotContain("unins000.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("if LegacyUnsafeInstallation then", script, StringComparison.Ordinal);
        Assert.Contains("LegacyMigrationPath := LegacyInstallPath", script, StringComparison.Ordinal);
        Assert.Contains("PersistLegacyStartupMigrationMarker", script, StringComparison.Ordinal);
        Assert.Contains("CleanupOwnedStartupEntries", script, StringComparison.Ordinal);
        Assert.Contains("function IsHardwareServiceStopped", script, StringComparison.Ordinal);
        Assert.Contains("function WaitForHardwareServiceStopped", script, StringComparison.Ordinal);
        Assert.Contains("StopSafe := WaitForHardwareServiceStopped", script, StringComparison.Ordinal);
        Assert.Contains("'/F /FI \"SERVICES eq {#HardwareServiceName}\"'", script, StringComparison.Ordinal);

        var prepareIndex = script.IndexOf("function PrepareToInstall", StringComparison.Ordinal);
        var retirementIndex = script.IndexOf(
            "RetireLegacyUnsafeHardwareService",
            prepareIndex,
            StringComparison.Ordinal);
        var protectedTargetIndex = script.IndexOf(
            "if not IsProtectedInstallTargetAllowed",
            prepareIndex,
            StringComparison.Ordinal);
        Assert.True(
            retirementIndex > prepareIndex && protectedTargetIndex > retirementIndex,
            "An owned legacy unsafe service would remain running when setup migrates to the protected target.");

        var legacyRegistrationIndex = script.IndexOf(
            "LegacyUnsafeInstallation := HasLegacyUnsafeRegisteredInstallation",
            prepareIndex,
            StringComparison.Ordinal);
        Assert.True(
            legacyRegistrationIndex > prepareIndex,
            "A legacy custom installation without a service would not enter the trusted migration path.");
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
