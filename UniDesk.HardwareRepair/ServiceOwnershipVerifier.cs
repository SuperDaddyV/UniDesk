using Microsoft.Win32;
using System.Security;

namespace UniDesk.HardwareRepair;

internal enum ServiceOwnershipStatus
{
    Missing,
    Owned,
    Foreign,
    Unavailable
}

internal sealed record ServiceOwnershipVerificationResult(
    ServiceOwnershipStatus Status,
    string Reason);

internal interface IServiceOwnershipVerifier
{
    ServiceOwnershipVerificationResult Verify(string serviceName, string expectedBinaryPath);
}

internal sealed class ServiceOwnershipVerifier : IServiceOwnershipVerifier
{
    public ServiceOwnershipVerificationResult Verify(string serviceName, string expectedBinaryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(ServiceOwnershipStatus.Unavailable, "Service ownership verification requires Windows.");
        }

        try
        {
            using var services = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            using var service = services?.OpenSubKey(serviceName);
            if (service == null)
            {
                return new(ServiceOwnershipStatus.Missing, "Service does not exist.");
            }

            var imagePath = service.GetValue(
                "ImagePath",
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (!WindowsCommandPathParser.TryGetExecutablePath(imagePath, out var actualBinaryPath))
            {
                return new(ServiceOwnershipStatus.Unavailable, "Service ImagePath is missing or invalid.");
            }

            var expected = Path.GetFullPath(expectedBinaryPath);
            var actual = Path.GetFullPath(Environment.ExpandEnvironmentVariables(actualBinaryPath));
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
                ? new(ServiceOwnershipStatus.Owned, "Service ImagePath matches the packaged service binary.")
                : new(ServiceOwnershipStatus.Foreign, "Service ImagePath points outside this installation.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                   NotSupportedException or SecurityException)
        {
            return new(
                ServiceOwnershipStatus.Unavailable,
                $"Service ImagePath could not be verified: {ex.GetType().Name} (0x{ex.HResult:X8}).");
        }
    }
}

internal static class WindowsCommandPathParser
{
    internal static bool TryGetExecutablePath(string? command, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote <= 1 ||
                (closingQuote + 1 < trimmed.Length && !char.IsWhiteSpace(trimmed[closingQuote + 1])))
            {
                return false;
            }

            executablePath = trimmed[1..closingQuote];
            return executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        var executableEnd = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (executableEnd < 0)
        {
            return false;
        }

        var boundary = executableEnd + 4;
        if (boundary < trimmed.Length && !char.IsWhiteSpace(trimmed[boundary]))
        {
            return false;
        }

        executablePath = trimmed[..boundary];
        return true;
    }
}
