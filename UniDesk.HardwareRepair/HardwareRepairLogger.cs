namespace UniDesk.HardwareRepair;

internal sealed class HardwareRepairLogger
{
    private readonly string _logPath;

    public HardwareRepairLogger(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "UniDesk",
            "logs",
            "hardware-repair.log");
    }

    public void Log(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                _logPath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
