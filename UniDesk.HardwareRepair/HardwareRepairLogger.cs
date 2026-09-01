using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace UniDesk.HardwareRepair;

internal sealed class HardwareRepairLogger
{
    private const int MaximumLogMessageLength = 4096;
    private const uint FileAppendData = 0x00000004;
    private const uint FileReadAttributes = 0x00000080;
    private const uint ReadControl = 0x00020000;
    private const uint OpenAlways = 4;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeReparsePoint = 0x00000400;
    // Win32 FILE_FLAG_OPEN_REPARSE_POINT: inspect the link itself, never its target.
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    // Win32 FILE_FLAG_BACKUP_SEMANTICS: open directory handles for path-chain locking.
    private const uint FileFlagBackupSemantics = 0x02000000;

    private const FileSystemRights DangerousWriteRights =
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.CreateDirectories |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.WriteAttributes |
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    private static readonly SecurityIdentifier LocalSystemSid =
        new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier AdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly string TrustedInstallerSid =
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    private static readonly Regex CredentialPattern = new(
        @"\b(?<name>(?:(?:weather|modeldial|qweather)[_ -]?)?api[_ -]?key|x-qw-api-key|authorization|token|secret|password)\b\s*[:=]\s*[^\r\n,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex CoordinatePairPattern = new(
        @"-?\d{1,3}\.\d{4,}\s*[,，]\s*-?\d{1,3}\.\d{4,}",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex UriQueryPattern = new(
        @"(?<uri>https?://[^\s?#]+)[?#][^\s]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex WindowsPathPattern = new(
        @"(?:[a-z]:\\|\\\\)[^\r\n]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly string _logPath;

    public HardwareRepairLogger(string? logPath = null)
    {
        _logPath = Path.GetFullPath(logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "UniDesk",
            "logs",
            "hardware-repair.log"));
    }

    public void Log(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (string.IsNullOrWhiteSpace(directory) ||
                !TryAcquireSecureDirectoryChain(directory, out var directoryHandles))
            {
                return;
            }

            try
            {
                using (var stream = TryOpenSecureLogFile(_logPath, out var logHandle)
                    ? new FileStream(logHandle, FileAccess.Write, 4096, isAsync: false)
                    : null)
                {
                    if (stream is null)
                    {
                        return;
                    }

                    stream.Seek(0, SeekOrigin.End);
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {SanitizeMessage(message)}");
                    writer.Flush();
                }
            }
            finally
            {
                DisposeHandles(directoryHandles);
            }
        }
        catch
        {
            // Logging must never make the elevated maintenance operation fail open or crash.
        }
    }

    internal static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var sanitized = UriQueryPattern.Replace(message, "${uri}?[REDACTED]");
        sanitized = CredentialPattern.Replace(sanitized, "${name}=[REDACTED]");
        sanitized = CoordinatePairPattern.Replace(sanitized, "[REDACTED]");
        sanitized = WindowsPathPattern.Replace(sanitized, "[REDACTED]");
        sanitized = sanitized.Replace('\r', ' ').Replace('\n', ' ');

        return sanitized.Length <= MaximumLogMessageLength
            ? sanitized
            : sanitized[..MaximumLogMessageLength] + "…";
    }

    private static bool TryAcquireSecureDirectoryChain(
        string directoryPath,
        out List<SafeFileHandle> directoryHandles)
    {
        directoryHandles = [];
        if (!Path.IsPathFullyQualified(directoryPath) ||
            directoryPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            foreach (var path in EnumeratePathChain(directoryPath))
            {
                var existed = Directory.Exists(path);
                if (!existed)
                {
                    Directory.CreateDirectory(path);
                }

                if (!TryOpenDirectory(path, out var handle))
                {
                    DisposeHandles(directoryHandles);
                    return false;
                }

                directoryHandles.Add(handle);
                if (!existed && !ProtectNewDirectory(path))
                {
                    DisposeHandles(directoryHandles);
                    return false;
                }

                if (!IsOwnedByTrustedPrincipal(path) ||
                    (string.Equals(
                         Path.TrimEndingDirectorySeparator(path),
                         Path.TrimEndingDirectorySeparator(directoryPath),
                         StringComparison.OrdinalIgnoreCase) &&
                     !IsSecureDirectoryAcl(path)))
                {
                    DisposeHandles(directoryHandles);
                    return false;
                }
            }

            return true;
        }
        catch
        {
            DisposeHandles(directoryHandles);
            return false;
        }
    }

    private static void DisposeHandles(IEnumerable<SafeFileHandle> handles)
    {
        foreach (var handle in handles)
        {
            handle.Dispose();
        }
    }

    private static IEnumerable<string> EnumeratePathChain(string directoryPath)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            yield break;
        }

        yield return root;
        var relative = fullPath[root.Length..].Trim(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var current = root;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            yield return current;
        }
    }

    private static bool ProtectNewDirectory(string path)
    {
        try
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(CreateFullControlRule(LocalSystemSid));
            security.AddAccessRule(CreateFullControlRule(AdministratorsSid));
            new DirectoryInfo(path).SetAccessControl(security);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static FileSystemAccessRule CreateFullControlRule(SecurityIdentifier identity) =>
        new(
            identity,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

    private static bool TryOpenDirectory(string path, out SafeFileHandle handle)
    {
        handle = CreateFile(
            path,
            FileReadAttributes | ReadControl,
            (uint)(FileShare.Read | FileShare.Write),
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        if (!GetFileInformationByHandle(handle, out var information) ||
            (information.FileAttributes & FileAttributeReparsePoint) != 0)
        {
            handle.Dispose();
            return false;
        }

        return true;
    }

    private static bool TryOpenSecureLogFile(string path, out SafeFileHandle handle)
    {
        handle = CreateFile(
            path,
            FileAppendData | FileReadAttributes | ReadControl,
            (uint)(FileShare.Read | FileShare.Write),
            IntPtr.Zero,
            OpenAlways,
            FileAttributeNormal | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        if (!GetFileInformationByHandle(handle, out var information) ||
            (information.FileAttributes & FileAttributeReparsePoint) != 0 ||
            information.NumberOfLinks != 1 ||
            !IsSecureFileAcl(path))
        {
            handle.Dispose();
            return false;
        }

        return true;
    }

    private static bool IsSecureDirectoryAcl(string path)
    {
        try
        {
            var security = new DirectoryInfo(path).GetAccessControl(
                AccessControlSections.Access | AccessControlSections.Owner);
            return IsSecureAcl(security);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOwnedByTrustedPrincipal(string path)
    {
        try
        {
            var security = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner);
            var owner = (SecurityIdentifier?)security.GetOwner(typeof(SecurityIdentifier));
            return owner is not null && IsTrustedPrincipal(owner);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSecureFileAcl(string path)
    {
        try
        {
            var security = new FileInfo(path).GetAccessControl(
                AccessControlSections.Access | AccessControlSections.Owner);
            return IsSecureAcl(security);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSecureAcl(FileSystemSecurity security)
    {
        var owner = (SecurityIdentifier?)security.GetOwner(typeof(SecurityIdentifier));
        if (owner is null || !IsTrustedPrincipal(owner))
        {
            return false;
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                (rule.FileSystemRights & DangerousWriteRights) != 0 &&
                !IsTrustedPrincipal((SecurityIdentifier)rule.IdentityReference))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTrustedPrincipal(SecurityIdentifier identity) =>
        identity.Equals(LocalSystemSid) ||
        identity.Equals(AdministratorsSid) ||
        identity.Value.Equals(TrustedInstallerSid, StringComparison.Ordinal);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
