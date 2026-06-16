namespace UniDesk.Models;

public enum UpdateCheckStatus
{
    UpdateAvailable,
    Latest,
    CurrentNewerThanLatest,
    Failed
}

public sealed class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string ReleaseName { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public string ReleaseUrl { get; init; } = string.Empty;
    public string? InstallerDownloadUrl { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public bool IsUpdateAvailable => Status == UpdateCheckStatus.UpdateAvailable;
}
