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

    private const FileSystemRights DangerousRights =
        FileSystemRights.Write |
        FileSystemRights.Modify |
        FileSystemRights.FullControl |
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

            foreach (var ancestor in EnumerateAncestorDirectories(applicationDirectory))
            {
                var ancestorResult = VerifyAncestorPath(ancestor);
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
            return new(false, $"ACL verification could not complete: {ex.Message}");
        }
    }

    internal static IReadOnlyList<string> EnumerateAncestorDirectories(string directoryPath)
    {
        var ancestors = new List<string>();
        var current = Directory.GetParent(Path.GetFullPath(directoryPath));
        while (current != null)
        {
            ancestors.Add(current.FullName);
            current = current.Parent;
        }

        return ancestors;
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
                (rule.FileSystemRights & DangerousRights) == 0)
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

    private static bool IsTrustedPrincipal(SecurityIdentifier identity) =>
        identity.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        identity.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        identity.Value.Equals(TrustedInstallerSid, StringComparison.Ordinal);
}
