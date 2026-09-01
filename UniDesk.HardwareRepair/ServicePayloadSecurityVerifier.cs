using System.Security.AccessControl;
using System.Security.Principal;

namespace UniDesk.HardwareRepair;

internal sealed record ServicePayloadSecurityVerificationResult(bool IsSecure, string Reason);

internal interface IServicePayloadSecurityVerifier
{
    ServicePayloadSecurityVerificationResult Verify(string serviceBinaryPath);
}

internal sealed class ServicePayloadSecurityVerifier : IServicePayloadSecurityVerifier
{
    private const string TrustedInstallerSid =
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    private const FileSystemRights DangerousWriteRights =
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.WriteAttributes |
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    private const FileSystemRights AncestorDangerousRights =
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    public ServicePayloadSecurityVerificationResult Verify(string serviceBinaryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(false, "Service payload ACL verification is only supported on Windows.");
        }

        try
        {
            var binaryPath = Path.GetFullPath(serviceBinaryPath);
            if (!File.Exists(binaryPath))
            {
                return new(false, "Service executable is missing.");
            }

            var serviceDirectory = Path.GetDirectoryName(binaryPath);
            var applicationDirectory = serviceDirectory == null
                ? null
                : Directory.GetParent(serviceDirectory)?.FullName;
            if (serviceDirectory == null || applicationDirectory == null)
            {
                return new(false, "Service payload parent directories could not be resolved.");
            }

            var expectedApplicationDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "UniDesk");
            if (!string.Equals(
                    Path.TrimEndingDirectorySeparator(applicationDirectory),
                    Path.TrimEndingDirectorySeparator(expectedApplicationDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "Service payload is not under the protected UniDesk Common Program Files directory.");
            }

            var expectedServiceDirectory = Path.Combine(applicationDirectory, "HardwareService");
            if (!string.Equals(
                    Path.TrimEndingDirectorySeparator(serviceDirectory),
                    Path.TrimEndingDirectorySeparator(expectedServiceDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "Service payload is not in the expected HardwareService directory.");
            }

            foreach (var protectedBoundary in GetProtectedInstallationBoundaries(applicationDirectory))
            {
                var ancestorResult = VerifyAncestorPath(protectedBoundary);
                if (!ancestorResult.IsSecure)
                {
                    return ancestorResult;
                }
            }

            var paths = new List<string> { applicationDirectory, serviceDirectory };
            paths.AddRange(Directory.EnumerateFileSystemEntries(
                serviceDirectory,
                "*",
                SearchOption.AllDirectories));

            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var result = VerifyPath(path);
                if (!result.IsSecure)
                {
                    return result;
                }
            }

            return new(true, "Service payload ACLs are restricted to trusted writers.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(
                false,
                $"ACL verification could not complete: {ex.GetType().Name} (0x{ex.HResult:X8}).");
        }
    }

    internal static string GetProtectedInstallationRoot(string directoryPath)
    {
        return Directory.GetParent(Path.GetFullPath(directoryPath))?.FullName
            ?? throw new InvalidOperationException("Protected installation root could not be resolved.");
    }

    internal static IReadOnlyList<string> GetProtectedInstallationBoundaries(string directoryPath)
    {
        var commonProgramFiles = GetProtectedInstallationRoot(directoryPath);
        var programFiles = Directory.GetParent(commonProgramFiles)?.FullName
            ?? throw new InvalidOperationException("Protected Program Files boundary could not be resolved.");
        return [commonProgramFiles, programFiles];
    }

    private static ServicePayloadSecurityVerificationResult VerifyAncestorPath(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return new(false, $"Reparse points are not allowed in the service payload ancestor chain: {path}");
        }

        var security = new DirectoryInfo(path).GetAccessControl(
            AccessControlSections.Access | AccessControlSections.Owner);
        var owner = (SecurityIdentifier?)security.GetOwner(typeof(SecurityIdentifier));
        if (owner == null || !IsTrustedPrincipal(owner))
        {
            return new(false, $"Untrusted owner on service payload ancestor: {path}");
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0 ||
                (rule.FileSystemRights & AncestorDangerousRights) == 0)
            {
                continue;
            }

            var identity = (SecurityIdentifier)rule.IdentityReference;
            if (!IsTrustedPrincipal(identity))
            {
                return new(false, $"Untrusted principal {identity.Value} can replace a service payload ancestor: {path}");
            }
        }

        return new(true, "Service payload ancestor ACL is secure.");
    }

    private static ServicePayloadSecurityVerificationResult VerifyPath(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return new(false, $"Reparse points are not allowed in the service payload: {path}");
        }

        FileSystemSecurity security = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
            : new FileInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

        var owner = (SecurityIdentifier?)security.GetOwner(typeof(SecurityIdentifier));
        if (owner == null || !IsTrustedPrincipal(owner))
        {
            return new(false, $"Untrusted owner on service payload path: {path}");
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                !HasDangerousWriteRights(rule.FileSystemRights))
            {
                continue;
            }

            var identity = (SecurityIdentifier)rule.IdentityReference;
            if (!IsTrustedPrincipal(identity))
            {
                return new(false, $"Untrusted principal {identity.Value} can modify: {path}");
            }
        }

        return new(true, "Path ACL is secure.");
    }

    internal static bool HasDangerousWriteRights(FileSystemRights rights) =>
        (rights & DangerousWriteRights) != 0;

    private static bool IsTrustedPrincipal(SecurityIdentifier identity) =>
        identity.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        identity.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        identity.Value.Equals(TrustedInstallerSid, StringComparison.Ordinal);
}
