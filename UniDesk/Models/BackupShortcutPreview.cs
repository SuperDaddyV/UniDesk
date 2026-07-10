namespace UniDesk.Models;

public sealed record BackupShortcutPreview(
    string Name,
    string Path,
    string? LaunchArguments,
    bool IsRisky);
