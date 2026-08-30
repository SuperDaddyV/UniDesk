using System.IO;

namespace UniDesk.Helpers;

public static class Logger
{
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
        Write("ERROR", source, $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
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
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(logFile, line);
            }
        }
        catch
        {
        }
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
