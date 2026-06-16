using UniDesk.Models;

namespace UniDesk.Services;

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
