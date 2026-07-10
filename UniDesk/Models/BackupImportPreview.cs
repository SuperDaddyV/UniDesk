namespace UniDesk.Models;

public sealed class BackupImportPreview
{
    public int SettingCount { get; init; }
    public int ShortcutCount { get; init; }
    public int TodoCount { get; init; }
    public int QuickNoteCount { get; init; }
    public int ClipboardHistoryCount { get; init; }
    public int TextSnippetCount { get; init; }
    public bool ContainsSensitivePlaintext { get; init; }
    public IReadOnlyList<BackupShortcutPreview> Shortcuts { get; init; } = [];
}
