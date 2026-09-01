using System.IO;
using System.Text.RegularExpressions;

namespace UniDesk.Helpers;

public static class Logger
{
    private const int MaximumLogFieldLength = 4096;
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
    private static readonly object Sync = new();
    private static readonly AsyncLocal<string?> LogDirectoryOverride = new();
    private static string? _processLogDirectoryOverride;

    internal static void UseProcessLogDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        lock (Sync)
        {
            _processLogDirectoryOverride = Path.GetFullPath(directory);
        }
    }

    internal static IDisposable UseLogDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var previous = LogDirectoryOverride.Value;
        LogDirectoryOverride.Value = Path.GetFullPath(directory);
        return new LogDirectoryScope(previous);
    }

    public static void LogError(Exception ex, string source = "Error")
    {
        ArgumentNullException.ThrowIfNull(ex);
        Write("ERROR", source, FormatExceptionIdentity(ex));
    }

    public static void LogWarning(string message, string source = "Warning")
    {
        Write("WARN", source, message);
    }

    public static void LogInfo(string message, string source = "Info")
    {
        Write("INFO", source, message);
    }

    private static void Write(string level, string source, string message)
    {
        try
        {
            string? processLogDirectoryOverride;
            lock (Sync)
            {
                processLogDirectoryOverride = _processLogDirectoryOverride;
            }

            var logDirectory = LogDirectoryOverride.Value ?? processLogDirectoryOverride;
            if (logDirectory == null)
            {
                DirectoryHelper.EnsureDirectoriesExist();
                logDirectory = DirectoryHelper.LogsDirectory;
            }
            else
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logFile = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var safeSource = Sanitize(source, maximumLength: 256);
            var safeMessage = Sanitize(message, MaximumLogFieldLength);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{safeSource}] {safeMessage}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(logFile, line);
            }
        }
        catch
        {
        }
    }

    private static string FormatExceptionIdentity(Exception exception)
    {
        var identities = new List<string>(capacity: 3);
        for (var current = exception; current != null && identities.Count < 3; current = current.InnerException)
        {
            identities.Add($"{current.GetType().Name} (0x{current.HResult:X8})");
        }

        return string.Join(" <- ", identities);
    }

    private static string Sanitize(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = UriQueryPattern.Replace(value, "${uri}?[REDACTED]");
        sanitized = CredentialPattern.Replace(sanitized, "${name}=[REDACTED]");
        sanitized = CoordinatePairPattern.Replace(sanitized, "[REDACTED]");
        sanitized = WindowsPathPattern.Replace(sanitized, "[REDACTED]");
        sanitized = sanitized.Replace('\r', ' ').Replace('\n', ' ');

        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength] + "…";
    }

    private sealed class LogDirectoryScope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public LogDirectoryScope(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LogDirectoryOverride.Value = _previous;
        }
    }
}
