using UniDesk.Models;

namespace UniDesk.Services;

public sealed class BackupImportPlan
{
    internal BackupImportPlan(object document, BackupImportPreview preview)
    {
        Document = document;
        Preview = preview;
    }

    internal object Document { get; }
    public BackupImportPreview Preview { get; }
}
