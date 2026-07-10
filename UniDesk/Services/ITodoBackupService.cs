namespace UniDesk.Services;

using UniDesk.Models;

public interface ITodoBackupService
{
    Task ExportToFileAsync(string filePath, BackupExportOptions? options = null);
    Task<BackupImportPlan> PrepareImportAsync(string filePath);
    Task<TodoBackupImportResult> ApplyImportAsync(BackupImportPlan plan);
}
