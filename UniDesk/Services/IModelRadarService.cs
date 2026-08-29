using UniDesk.Models;

namespace UniDesk.Services;

public enum ModelRadarServiceStatus
{
    Success,
    NotFound,
    InvalidCache,
    NetworkError,
    SchemaError,
    ResponseTooLarge,
    AlreadyRefreshing
}

public sealed record ModelRadarServiceResult
{
    public ModelRadarServiceStatus Status { get; init; }
    public ModelRadarSnapshot? Snapshot { get; init; }
    public DateTimeOffset? CachedAtUtc { get; init; }
    public bool CachePersisted { get; init; }
}

public interface IModelRadarService
{
    Task<ModelRadarServiceResult> ReadCacheAsync(CancellationToken cancellationToken = default);
    Task<ModelRadarServiceResult> RefreshAsync(CancellationToken cancellationToken = default);
}
