using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public sealed record ModelRadarDecisionCard
{
    public string ModelName { get; init; } = string.Empty;
    public string ReasoningEffort { get; init; } = string.Empty;
    public string ScoreText { get; init; } = "--";
    public string DimensionSummary { get; init; } = string.Empty;
    public IReadOnlyList<ModelRadarDimension> Dimensions { get; init; } = [];
    public string ToolTipText { get; init; } = string.Empty;
}

public sealed record ModelRadarDimension(string Label, string ScoreText);

public sealed record ModelRadarDisplayRow
{
    public int Position { get; init; }
    public string ModelName { get; init; } = string.Empty;
    public string ReasoningEffort { get; init; } = string.Empty;
    public string ScoreText { get; init; } = "--";
    public string DecisionTagsText { get; init; } = string.Empty;
    public string ToolTipText { get; init; } = string.Empty;
}

public partial class ModelRadarViewModel : ObservableObject, IDisposable
{
    private readonly IModelRadarService _service;
    private readonly ILocalizationService _localizationService;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshInterval;
    private readonly bool _startAutomaticRefresh;
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _lifecycleCts;
    private Task? _enableTask;
    private Task? _automaticRefreshTask;
    private Task? _activeRefreshTask;
    private DateTimeOffset _nextAutomaticRefreshUtc;
    private int _generation;
    private bool _disposed;
    private bool _isEnabled;

    [ObservableProperty]
    private ModelRadarCacheState _state = ModelRadarCacheState.Loading;

    [ObservableProperty]
    private ModelRadarSnapshot? _snapshot;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isOfflineCache;

    [ObservableProperty]
    private ModelRadarCategory _selectedCategory = ModelRadarCategory.Overall;

    [ObservableProperty]
    private IReadOnlyList<ModelRadarListItem> _visibleRankings = [];

    [ObservableProperty]
    private IReadOnlyList<ModelRadarDisplayRow> _visibleRows = [];

    [ObservableProperty]
    private ModelRadarDecisionCard? _overallDecision;

    [ObservableProperty]
    private ModelRadarDecisionCard? _valueDecision;

    public bool IsEnabled
    {
        get => _isEnabled;
        private set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(CanRefresh));
                RefreshCommand.NotifyCanExecuteChanged();
                NotifyCompactSummaryChanged();
            }
        }
    }

    public bool CanRefresh => IsEnabled && !IsRefreshing;

    public bool IsOverallCategoryEnabled => Snapshot?.OverallTop.Count > 0;

    public bool IsBackendCategoryEnabled => Snapshot?.BackendTop.Count > 0;

    public bool IsFrontendCategoryEnabled => Snapshot?.FrontendTop.Count > 0;

    public bool IsKnowledgeCategoryEnabled => Snapshot?.KnowledgeTop.Count > 0;

    public bool IsAestheticCategoryEnabled => Snapshot?.AestheticTop.Count > 0;

    public bool IsMaintainabilityCategoryEnabled => Snapshot?.MaintainabilityTop.Count > 0;

    public string ScoreBasisText => Snapshot == null ? string.Empty : L(
        Snapshot.IsLegacySnapshot ? "ModelRadar.LegacyBasis" : "ModelRadar.FiveAxisBasis");

    public bool HasSnapshot => Snapshot != null;

    public bool HasNoSnapshot => Snapshot == null;

    public bool HasOverallLeader => Snapshot?.OverallLeader != null;

    public bool HasValueRecommendation => Snapshot?.ValueRecommendation != null;

    public bool IsCompactSummaryVisible => IsEnabled;

    public bool HasCompactRecommendations =>
        IsEnabled &&
        Snapshot is
        {
            IsPending: false,
            OverallLeader: not null
        };

    public string CompactOverallLabelText =>
        HasCompactRecommendations
            ? string.IsNullOrEmpty(CompactFreshnessPrefix)
                ? L("ModelRadar.CompactOverallLabel")
                : CompactFreshnessPrefix.TrimEnd(' ', '·')
            : string.Empty;

    public string CompactOverallModelNameText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader ? DisplayModel(leader) : string.Empty;

    public string CompactOverallEffortText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader ? FormatDisplayReasoningEffort(leader.ReasoningEffort) : string.Empty;

    public string CompactOverallModelText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader
            ? $"{CompactFreshnessPrefix}{FormatCompactModel(leader)}"
            : string.Empty;

    public string CompactOverallScoreText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader
            ? FormatOverallScore(leader.OverallScore)
            : string.Empty;

    public string CompactValueLabelText =>
        HasCompactRecommendations
            ? L("ModelRadar.CompactValueLabel")
            : string.Empty;

    public string CompactValueModelText =>
        HasCompactRecommendations && Snapshot?.ValueRecommendation is { } value
            ? FormatCompactModel(value)
            : HasCompactRecommendations
                ? L("ModelRadar.NoValueRecommendation")
                : string.Empty;

    public string CompactValueScoreText =>
        HasCompactRecommendations && Snapshot?.ValueRecommendation is { } value
            ? FormatOverallScore(value.OverallScore)
            : string.Empty;

    public string CompactOverallText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader
            ? $"{CompactFreshnessPrefix}{FormatCompactRecommendation(
                "ModelRadar.CompactOverallFormat",
                leader)}"
            : string.Empty;

    public string CompactValueText =>
        HasCompactRecommendations && Snapshot?.ValueRecommendation is { } value
            ? FormatCompactRecommendation("ModelRadar.CompactValueFormat", value)
            : Snapshot is { IsPending: false, ValueRecommendation: null }
                ? L("ModelRadar.NoValueRecommendation")
                : string.Empty;

    public string CompactStatusText
    {
        get
        {
            if (!IsEnabled || HasCompactRecommendations)
            {
                return string.Empty;
            }

            return $"{CompactFreshnessPrefix}{StatusText}";
        }
    }

    public string CompactToolTipText
    {
        get
        {
            if (!IsEnabled)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            lines.Add(ScoreBasisText);
            if (HasCompactRecommendations)
            {
                lines.Add(CompactOverallText);
            }

            if (!string.IsNullOrWhiteSpace(PublishedText))
            {
                lines.Add(PublishedText);
            }

            if (!string.IsNullOrWhiteSpace(StatusText))
            {
                lines.Add(StatusText);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public string StatusText
    {
        get
        {
            if (IsRefreshing)
            {
                return L("ModelRadar.Refreshing");
            }

            if (IsOfflineCache)
            {
                if (State == ModelRadarCacheState.SchemaError)
                {
                    return L("ModelRadar.SchemaErrorCached");
                }

                return L("ModelRadar.OfflineStale");
            }

            return State switch
            {
                ModelRadarCacheState.Loading => L("ModelRadar.Loading"),
                ModelRadarCacheState.Fresh => L("ModelRadar.Latest"),
                ModelRadarCacheState.Stale => L("ModelRadar.OfflineStale"),
                ModelRadarCacheState.Unavailable => L("ModelRadar.Unavailable"),
                ModelRadarCacheState.Pending => L("ModelRadar.Pending"),
                ModelRadarCacheState.SchemaError => L("ModelRadar.SchemaError"),
                _ => L("ModelRadar.Unavailable")
            };
        }
    }

    public string PublishedText => Snapshot == null
        ? string.Empty
        : _localizationService.Format(
            "ModelRadar.PublishedFormat",
            Snapshot.PublishedAt.ToLocalTime().ToString("g", _localizationService.CurrentCulture));

    public string RankingDescription => Snapshot is { IsLegacySnapshot: true }
        ? L("ModelRadar.LegacyRankingDescription")
        : L("ModelRadar.RankingDescription");

    internal bool IsAutomaticRefreshScheduled { get; private set; }

    public ModelRadarViewModel(
        IModelRadarService service,
        ILocalizationService localizationService)
        : this(
            service,
            localizationService,
            TimeProvider.System,
            TimeSpan.FromHours(6),
            startAutomaticRefresh: true)
    {
    }

    internal ModelRadarViewModel(
        IModelRadarService service,
        ILocalizationService localizationService,
        TimeProvider timeProvider,
        TimeSpan refreshInterval,
        bool startAutomaticRefresh)
    {
        _service = service;
        _localizationService = localizationService;
        _timeProvider = timeProvider;
        _refreshInterval = refreshInterval;
        _startAutomaticRefresh = startAutomaticRefresh;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;
    }

    public Task SetEnabledAsync(bool enabled)
    {
        CancellationTokenSource? cancellation = null;
        Task[] tasksToDrain = [];

        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (enabled)
            {
                if (IsEnabled)
                {
                    return _enableTask ?? Task.CompletedTask;
                }

                IsEnabled = true;
                State = ModelRadarCacheState.Loading;
                IsOfflineCache = false;
                Snapshot = null;
                VisibleRankings = [];
                SelectedCategory = ModelRadarCategory.Overall;
                var generation = ++_generation;
                var cts = new CancellationTokenSource();
                _lifecycleCts = cts;
                _enableTask = EnableCoreAsync(generation, cts);
                return _enableTask;
            }

            if (!IsEnabled && _lifecycleCts == null)
            {
                return Task.CompletedTask;
            }

            IsEnabled = false;
            _generation++;
            cancellation = _lifecycleCts;
            _lifecycleCts = null;
            tasksToDrain = new[] { _enableTask, _automaticRefreshTask, _activeRefreshTask }
                .Where(task => task != null)
                .Cast<Task>()
                .ToArray();
            _enableTask = null;
            _automaticRefreshTask = null;
            _activeRefreshTask = null;
            IsAutomaticRefreshScheduled = false;
        }

        CancelSafely(cancellation);
        if (cancellation != null)
        {
            _ = DisposeCancellationAfterTasksAsync(cancellation, tasksToDrain);
        }

        IsRefreshing = false;
        Snapshot = null;
        VisibleRankings = [];
        RefreshPresentation();
        IsOfflineCache = false;
        State = ModelRadarCacheState.Loading;
        return Task.CompletedTask;
    }

    private async Task EnableCoreAsync(int generation, CancellationTokenSource cts)
    {
        try
        {
            var cacheResult = await _service.ReadCacheAsync(cts.Token);
            if (!IsCurrent(generation, cts))
            {
                return;
            }

            if (cacheResult is
                {
                    Status: ModelRadarServiceStatus.Success,
                    Snapshot: { } cachedSnapshot,
                    CachedAtUtc: { } cachedAtUtc
                })
            {
                var isStale = cachedSnapshot.IsLegacySnapshot ||
                              _timeProvider.GetUtcNow() - cachedAtUtc >= _refreshInterval;
                ApplySnapshot(cachedSnapshot, isStale ? ModelRadarCacheState.Stale : ModelRadarCacheState.Fresh);
                _nextAutomaticRefreshUtc = isStale
                    ? _timeProvider.GetUtcNow()
                    : cachedAtUtc + _refreshInterval;

                if (isStale)
                {
                    await StartRefreshAsync(generation, cts);
                }
            }
            else
            {
                await StartRefreshAsync(generation, cts);
            }

            if (_startAutomaticRefresh && IsCurrent(generation, cts))
            {
                StartAutomaticRefresh(generation, cts);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch
        {
            if (IsCurrent(generation, cts))
            {
                ApplyFailure(ModelRadarServiceStatus.NetworkError);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh), AllowConcurrentExecutions = false)]
    private async Task RefreshAsync()
    {
        CancellationTokenSource? cts;
        int generation;
        lock (_lifecycleLock)
        {
            if (_disposed || !IsEnabled || IsRefreshing)
            {
                return;
            }

            cts = _lifecycleCts;
            generation = _generation;
        }

        if (cts != null)
        {
            await StartRefreshAsync(generation, cts);
        }
    }

    private Task StartRefreshAsync(int generation, CancellationTokenSource cts)
    {
        lock (_lifecycleLock)
        {
            if (!IsCurrentLocked(generation, cts) ||
                _activeRefreshTask is { IsCompleted: false })
            {
                return _activeRefreshTask ?? Task.CompletedTask;
            }

            _activeRefreshTask = RefreshCoreAsync(generation, cts);
            return _activeRefreshTask;
        }
    }

    private async Task RefreshCoreAsync(int generation, CancellationTokenSource cts)
    {
        if (!IsCurrent(generation, cts))
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            var result = await _service.RefreshAsync(cts.Token);
            if (!IsCurrent(generation, cts))
            {
                return;
            }

            if (result is { Status: ModelRadarServiceStatus.Success, Snapshot: { } snapshot })
            {
                ApplySnapshot(snapshot, ModelRadarCacheState.Fresh);
            }
            else if (result.Status != ModelRadarServiceStatus.AlreadyRefreshing)
            {
                ApplyFailure(result.Status);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch
        {
            if (IsCurrent(generation, cts))
            {
                ApplyFailure(ModelRadarServiceStatus.NetworkError);
            }
        }
        finally
        {
            if (IsCurrent(generation, cts))
            {
                IsRefreshing = false;
                _nextAutomaticRefreshUtc = _timeProvider.GetUtcNow() + _refreshInterval;
            }
        }
    }

    private void ApplySnapshot(ModelRadarSnapshot snapshot, ModelRadarCacheState state)
    {
        Snapshot = snapshot;
        IsOfflineCache = false;
        State = snapshot.IsPending ? ModelRadarCacheState.Pending : state;
        SelectedCategory = snapshot.IsPending && snapshot.BackendTop.Count > 0 ? ModelRadarCategory.Backend : SelectedCategory;
        if (!IsCategoryEnabled(SelectedCategory))
        {
            SelectedCategory = Enum.GetValues<ModelRadarCategory>().FirstOrDefault(IsCategoryEnabled);
        }

        RefreshVisibleRankings();
        RefreshPresentation();
    }

    private void ApplyFailure(ModelRadarServiceStatus status)
    {
        if (Snapshot != null)
        {
            IsOfflineCache = true;
            State = status == ModelRadarServiceStatus.SchemaError
                ? ModelRadarCacheState.SchemaError
                : Snapshot.IsPending
                    ? ModelRadarCacheState.Pending
                    : ModelRadarCacheState.Stale;
            RefreshPresentation();
            return;
        }

        IsOfflineCache = false;
        State = status == ModelRadarServiceStatus.SchemaError
            ? ModelRadarCacheState.SchemaError
            : ModelRadarCacheState.Unavailable;
        RefreshPresentation();
    }

    [RelayCommand]
    private void SelectCategory(ModelRadarCategory category)
    {
        if (!IsCategoryEnabled(category))
        {
            return;
        }

        SelectedCategory = category;
        RefreshVisibleRankings();
        RefreshPresentation();
    }

    private bool IsCategoryEnabled(ModelRadarCategory category) => category switch
    {
        ModelRadarCategory.Overall => IsOverallCategoryEnabled,
        ModelRadarCategory.Backend => IsBackendCategoryEnabled,
        ModelRadarCategory.Frontend => IsFrontendCategoryEnabled,
        ModelRadarCategory.Knowledge => IsKnowledgeCategoryEnabled,
        ModelRadarCategory.Aesthetic => IsAestheticCategoryEnabled,
        ModelRadarCategory.Maintainability => IsMaintainabilityCategoryEnabled,
        _ => false
    };

    private void RefreshVisibleRankings()
    {
        VisibleRankings = Snapshot == null
            ? []
            : SelectedCategory switch
            {
                ModelRadarCategory.Overall => Snapshot.OverallTop,
                ModelRadarCategory.Backend => Snapshot.BackendTop,
                ModelRadarCategory.Frontend => Snapshot.FrontendTop,
                ModelRadarCategory.Knowledge => Snapshot.KnowledgeTop,
                ModelRadarCategory.Aesthetic => Snapshot.AestheticTop,
                ModelRadarCategory.Maintainability => Snapshot.MaintainabilityTop,
                _ => []
            };
    }

    private void RefreshPresentation()
    {
        OverallDecision = Snapshot?.OverallLeader is { } leader
            ? CreateDecisionCard(leader, leader.OverallScore)
            : null;
        ValueDecision = Snapshot?.ValueRecommendation is { } value
            ? CreateDecisionCard(
                value,
                Snapshot.IsPending ? value.BackendScore : value.OverallScore)
            : null;
        VisibleRows = VisibleRankings.Select(CreateDisplayRow).ToArray();

        OnPropertyChanged(nameof(HasSnapshot));
        OnPropertyChanged(nameof(HasNoSnapshot));
        OnPropertyChanged(nameof(HasOverallLeader));
        OnPropertyChanged(nameof(HasValueRecommendation));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PublishedText));
        OnPropertyChanged(nameof(RankingDescription));
        OnPropertyChanged(nameof(ScoreBasisText));
        NotifyCompactSummaryChanged();
    }

    private string CompactFreshnessPrefix
    {
        get
        {
            if (Snapshot is not { } snapshot)
            {
                return string.Empty;
            }

            var localPublishedAt = snapshot.PublishedAt.ToLocalTime();
            var localNow = _timeProvider.GetUtcNow().ToLocalTime();
            var prefix = localPublishedAt.Date == localNow.Date
                ? string.Empty
                : $"{localPublishedAt.ToString("MM/dd", _localizationService.CurrentCulture)} · ";

            if (snapshot.IsLegacySnapshot)
            {
                prefix += $"{L("ModelRadar.CompactLegacyBasis")} · ";
            }

            if (IsOfflineCache || State == ModelRadarCacheState.Stale)
            {
                prefix += $"{L("ModelRadar.CompactCached")} · ";
            }

            return prefix;
        }
    }

    private string FormatCompactRecommendation(string key, ModelRadarEntry entry) =>
        _localizationService.Format(
            key,
            entry.Model,
            entry.ReasoningEffort,
            FormatOverallScore(entry.OverallScore));

    private static string FormatCompactModel(ModelRadarEntry entry)
        => $"{DisplayModel(entry)}/{FormatDisplayReasoningEffort(entry.ReasoningEffort)}";

    private static string DisplayModel(ModelRadarEntry entry) => FormatDisplayModel(
        string.IsNullOrWhiteSpace(entry.ModelDisplayName) ? entry.Model : entry.ModelDisplayName);

    private static string FormatDisplayModel(string value)
    {
        var words = value.Trim().Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 0 && words[0].Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            var display = new List<string>();
            var previousWasVersion = false;
            foreach (var word in words)
            {
                var isVersion = word.Length <= 2 && word.All(char.IsAsciiDigit);
                if (isVersion && previousWasVersion)
                    display[^1] += "." + word;
                else
                    display.Add(FormatDisplayIdentifierSegment(word));
                previousWasVersion = isVersion;
            }
            return string.Join(" ", display);
        }

        var segments = value.Trim().Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        if (segments[0].Equals("gpt", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
        {
            var suffix = string.Join("-", segments.Skip(2).Select(FormatDisplayIdentifierSegment));
            var displayName = $"GPT{FormatDisplayIdentifierSegment(segments[1])}";
            return string.IsNullOrEmpty(suffix) ? displayName : $"{displayName}-{suffix}";
        }

        return string.Join("-", segments.Select(FormatDisplayIdentifierSegment));
    }

    private static string FormatDisplayReasoningEffort(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "xhigh" or "x-high" => "XHigh",
            _ => FormatDisplayIdentifierSegment(normalized)
        };
    }

    private static string FormatDisplayIdentifierSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.ToLowerInvariant() switch
        {
            "ai" => "AI",
            "api" => "API",
            "deepseek" => "DeepSeek",
            "glm" => "GLM",
            "gpt" => "GPT",
            "minimax" => "MiniMax",
            _ => $"{char.ToUpperInvariant(value[0])}{value[1..].ToLowerInvariant()}"
        };
    }

    private void NotifyCompactSummaryChanged()
    {
        OnPropertyChanged(nameof(IsCompactSummaryVisible));
        OnPropertyChanged(nameof(HasCompactRecommendations));
        OnPropertyChanged(nameof(CompactOverallLabelText));
        OnPropertyChanged(nameof(CompactOverallModelText));
        OnPropertyChanged(nameof(CompactOverallModelNameText));
        OnPropertyChanged(nameof(CompactOverallEffortText));
        OnPropertyChanged(nameof(CompactOverallScoreText));
        OnPropertyChanged(nameof(CompactValueLabelText));
        OnPropertyChanged(nameof(CompactValueModelText));
        OnPropertyChanged(nameof(CompactValueScoreText));
        OnPropertyChanged(nameof(CompactOverallText));
        OnPropertyChanged(nameof(CompactValueText));
        OnPropertyChanged(nameof(CompactStatusText));
        OnPropertyChanged(nameof(CompactToolTipText));
    }

    private ModelRadarDecisionCard CreateDecisionCard(ModelRadarEntry entry, double? score) =>
        new()
        {
            ModelName = DisplayModel(entry),
            ReasoningEffort = FormatDisplayReasoningEffort(entry.ReasoningEffort),
            ScoreText = Snapshot is { IsPending: true } ? FormatScore(score) : FormatOverallScore(score),
            ToolTipText = CreateDisplayRow(new ModelRadarListItem(0, entry, score)).ToolTipText,
            Dimensions =
            [
                new(L("ModelRadar.BackendCategory"), FormatScore(entry.BackendScore)),
                new(L("ModelRadar.FrontendCategory"), FormatScore(entry.FrontendScore)),
                new(L("ModelRadar.KnowledgeCategory"), FormatScore(entry.KnowledgeScore)),
                new(L("ModelRadar.AestheticCategory"), FormatScore(entry.AestheticScore)),
                new(L("ModelRadar.MaintainabilityCategory"), FormatScore(entry.MaintainabilityScore))
            ],
            DimensionSummary = _localizationService.Format(
                "ModelRadar.DimensionSummaryFormat",
                FormatScore(entry.BackendScore),
                FormatScore(entry.FrontendScore),
                FormatScore(entry.KnowledgeScore),
                FormatScore(entry.AestheticScore),
                FormatScore(entry.MaintainabilityScore))
        };

    private ModelRadarDisplayRow CreateDisplayRow(ModelRadarListItem item)
    {
        var entry = item.Entry;
        var decisionTagsText = entry.DecisionTags.Count == 0
            ? string.Empty
            : string.Join(" · ", entry.DecisionTags);
        var tooltipLines = new List<string>
        {
            $"{entry.Model} / {entry.ReasoningEffort}",
            ScoreBasisText,
            $"{L("ModelRadar.OverallScore")}：{FormatOverallScore(entry.OverallScore)}",
            $"{L("ModelRadar.BackendScore")}：{FormatScore(entry.BackendScore)}",
            $"{L("ModelRadar.FrontendScore")}：{FormatScore(entry.FrontendScore)}",
            $"{L("ModelRadar.KnowledgeScore")}：{FormatScore(entry.KnowledgeScore)}",
            $"{L("ModelRadar.AestheticScore")}：{FormatScore(entry.AestheticScore)}",
            $"{L("ModelRadar.MaintainabilityScore")}：{FormatScore(entry.MaintainabilityScore)}",
            $"{L("ModelRadar.Elapsed")}：{FormatElapsed(entry.ElapsedMilliseconds)}",
            $"{L("ModelRadar.ReferenceCost")}：{FormatReferenceCost(entry.EstimatedReferenceCostUsd)}",
            $"{L("ModelRadar.Route")}：{(string.IsNullOrWhiteSpace(entry.Route) ? "--" : entry.Route)}"
        };
        if (decisionTagsText.Length > 0)
            tooltipLines.Add(decisionTagsText);

        return new ModelRadarDisplayRow
        {
            Position = item.Position,
            ModelName = DisplayModel(entry),
            ReasoningEffort = FormatDisplayReasoningEffort(entry.ReasoningEffort),
            ScoreText = SelectedCategory == ModelRadarCategory.Overall ? FormatOverallScore(item.Score) : FormatScore(item.Score),
            DecisionTagsText = decisionTagsText,
            ToolTipText = string.Join(Environment.NewLine, tooltipLines)
        };
    }

    private string FormatScore(double? value) => value is null
        ? "--"
        : value.Value.ToString("0.0", _localizationService.CurrentCulture);

    private string FormatOverallScore(double? value) => value is null
        ? "--"
        : value.Value.ToString("0.00", _localizationService.CurrentCulture);

    private string FormatElapsed(long? value) => value is null
        ? "--"
        : _localizationService.Format(
            "ModelRadar.ElapsedFormat",
            value.Value.ToString("N0", _localizationService.CurrentCulture));

    private string FormatReferenceCost(double? value) => value is null
        ? "--"
        : _localizationService.Format(
            "ModelRadar.ReferenceCostFormat",
            value.Value.ToString("0.####", _localizationService.CurrentCulture));

    private void StartAutomaticRefresh(int generation, CancellationTokenSource cts)
    {
        lock (_lifecycleLock)
        {
            if (!IsCurrentLocked(generation, cts) ||
                _automaticRefreshTask is { IsCompleted: false })
            {
                return;
            }

            IsAutomaticRefreshScheduled = true;
            _automaticRefreshTask = RunAutomaticRefreshAsync(generation, cts);
        }
    }

    private async Task RunAutomaticRefreshAsync(int generation, CancellationTokenSource cts)
    {
        try
        {
            while (IsCurrent(generation, cts))
            {
                var delay = _nextAutomaticRefreshUtc - _timeProvider.GetUtcNow();
                if (delay < TimeSpan.FromSeconds(1))
                {
                    delay = TimeSpan.FromSeconds(1);
                }

                await Task.Delay(delay, _timeProvider, cts.Token);
                if (IsCurrent(generation, cts))
                {
                    await StartRefreshAsync(generation, cts);
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        finally
        {
            if (IsCurrent(generation, cts))
            {
                IsAutomaticRefreshScheduled = false;
            }
        }
    }

    partial void OnIsRefreshingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(StatusText));
        NotifyCompactSummaryChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    partial void OnStateChanged(ModelRadarCacheState value)
    {
        OnPropertyChanged(nameof(StatusText));
        NotifyCompactSummaryChanged();
    }

    partial void OnSnapshotChanged(ModelRadarSnapshot? value)
    {
        OnPropertyChanged(nameof(IsOverallCategoryEnabled));
        OnPropertyChanged(nameof(IsBackendCategoryEnabled));
        OnPropertyChanged(nameof(IsFrontendCategoryEnabled));
        OnPropertyChanged(nameof(IsKnowledgeCategoryEnabled));
        OnPropertyChanged(nameof(IsAestheticCategoryEnabled));
        OnPropertyChanged(nameof(IsMaintainabilityCategoryEnabled));
        OnPropertyChanged(nameof(HasSnapshot));
        OnPropertyChanged(nameof(HasNoSnapshot));
        OnPropertyChanged(nameof(HasOverallLeader));
        OnPropertyChanged(nameof(HasValueRecommendation));
        OnPropertyChanged(nameof(PublishedText));
        NotifyCompactSummaryChanged();
    }

    partial void OnSelectedCategoryChanged(ModelRadarCategory value) =>
        OnPropertyChanged(nameof(RankingDescription));

    partial void OnIsOfflineCacheChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        NotifyCompactSummaryChanged();
    }

    private bool IsCurrent(int generation, CancellationTokenSource cts)
    {
        lock (_lifecycleLock)
        {
            return IsCurrentLocked(generation, cts);
        }
    }

    private bool IsCurrentLocked(int generation, CancellationTokenSource cts) =>
        !_disposed &&
        IsEnabled &&
        generation == _generation &&
        ReferenceEquals(_lifecycleCts, cts);

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshPresentation();
    }

    private string L(string key) => _localizationService.GetString(key);

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static async Task DisposeCancellationAfterTasksAsync(
        CancellationTokenSource cancellation,
        IReadOnlyList<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        Task[] tasks;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            cancellation = _lifecycleCts;
            _lifecycleCts = null;
            tasks = new[] { _enableTask, _automaticRefreshTask, _activeRefreshTask }
                .Where(task => task != null)
                .Cast<Task>()
                .ToArray();
            _enableTask = null;
            _automaticRefreshTask = null;
            _activeRefreshTask = null;
            IsAutomaticRefreshScheduled = false;
        }

        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        CancelSafely(cancellation);
        if (cancellation != null)
        {
            _ = DisposeCancellationAfterTasksAsync(cancellation, tasks);
        }
    }
}
