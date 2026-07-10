namespace UniDesk.Services;

using UniDesk.Models;

public interface ITodoBackupService
{
    Task ExportToFileAsync(string filePath, BackupExportOptions? options = null);
    Task<TodoBackupImportResult> ImportFromFileAsync(string filePath);
}
