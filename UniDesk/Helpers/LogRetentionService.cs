using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace UniDesk.Helpers;

public static class LogRetentionService
{
    public static int DeleteExpiredLogs(
        string directory,
        DateOnly today,
        int retentionDays = 7)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("日志目录不能为空。", nameof(directory));
        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (!Directory.Exists(directory)) return 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"无法枚举旧日志：{ex.Message}");
            return 0;
        }

        var cutoff = today.AddDays(-(retentionDays - 1));
        var deleted = 0;
        foreach (var file in files)
        {
            if (!string.Equals(Path.GetExtension(file), ".log", StringComparison.Ordinal))
                continue;
            if (!DateOnly.TryParseExact(
                    Path.GetFileNameWithoutExtension(file),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var logDate) ||
                logDate >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"无法删除旧日志 {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return deleted;
    }
}
