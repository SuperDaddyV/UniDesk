namespace UniDesk.Models;

public enum ModelRadarCacheState
{
    Loading,
    Fresh,
    Stale,
    Unavailable,
    Pending,
    SchemaError
}

public enum ModelRadarCategory
{
    Overall,
    Backend,
    Frontend,
    Knowledge
}

public sealed record ModelRadarEntry
{
    public int PublishedPosition { get; init; }
    public int Rank { get; init; }
    public int BackendRank { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ReasoningEffort { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public double? OverallScore { get; init; }
    public double? BackendScore { get; init; }
    public double? FrontendScore { get; init; }
    public double? KnowledgeScore { get; init; }
    public long? ElapsedMilliseconds { get; init; }
    public double? EstimatedReferenceCostUsd { get; init; }
    public IReadOnlyList<string> DecisionTags { get; init; } = [];
}

public sealed record ModelRadarListItem(
    int Position,
    ModelRadarEntry Entry,
    double? Score);

public sealed record ModelRadarSnapshot
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string BatchId { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public bool IsPending { get; init; }
    public ModelRadarEntry? OverallLeader { get; init; }
    public ModelRadarEntry? ValueRecommendation { get; init; }
    public IReadOnlyList<ModelRadarListItem> OverallTop { get; init; } = [];
    public IReadOnlyList<ModelRadarListItem> BackendTop { get; init; } = [];
    public IReadOnlyList<ModelRadarListItem> FrontendTop { get; init; } = [];
    public IReadOnlyList<ModelRadarListItem> KnowledgeTop { get; init; } = [];
}
