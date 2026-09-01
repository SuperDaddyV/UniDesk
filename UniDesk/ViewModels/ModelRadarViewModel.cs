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
}

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

    public bool IsOverallCategoryEnabled => Snapshot is { IsPending: false };

    public bool IsBackendCategoryEnabled => Snapshot != null;

    public bool IsFrontendCategoryEnabled => Snapshot is { IsPending: false };

    public bool IsKnowledgeCategoryEnabled => Snapshot is { IsPending: false };

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
            ? L("ModelRadar.CompactOverallLabel")
            : string.Empty;

    public string CompactOverallModelText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader
            ? $"{CompactFreshnessPrefix}{FormatCompactModel(leader)}"
            : string.Empty;

    public string CompactOverallScoreText =>
        HasCompactRecommendations && Snapshot?.OverallLeader is { } leader
            ? FormatScore(leader.OverallScore)
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
            ? FormatScore(value.OverallScore)
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

            if (Snapshot is { IsPending: false, ValueRecommendation: null })
            {
                return $"{CompactFreshnessPrefix}{L("ModelRadar.NoValueRecommendation")}";
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
            if (HasCompactRecommendations)
            {
                lines.Add(CompactOverallText);
                lines.Add(CompactValueText);
            }
            else if (Snapshot is { IsPending: false, ValueRecommendation: null })
            {
                lines.Add(CompactValueText);
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

    public string RankingDescription => SelectedCategory switch
    {
        ModelRadarCategory.Overall => L("ModelRadar.OverallRankingDescription"),
        ModelRadarCategory.Backend => L("ModelRadar.BackendRankingDescription"),
        ModelRadarCategory.Frontend => L("ModelRadar.FrontendRankingDescription"),
        ModelRadarCategory.Knowledge => L("ModelRadar.KnowledgeRankingDescription"),
        _ => string.Empty
    };

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
                var isStale = _timeProvider.GetUtcNow() - cachedAtUtc >= _refreshInterval;
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
        SelectedCategory = snapshot.IsPending ? ModelRadarCategory.Backend : SelectedCategory;
        if (!IsCategoryEnabled(SelectedCategory))
        {
            SelectedCategory = ModelRadarCategory.Overall;
        }

        RefreshVisibleRankings();
        RefreshPresentation();
        OnPropertyChanged(nameof(IsOverallCategoryEnabled));
        OnPropertyChanged(nameof(IsBackendCategoryEnabled));
        OnPropertyChanged(nameof(IsFrontendCategoryEnabled));
        OnPropertyChanged(nameof(IsKnowledgeCategoryEnabled));
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
            FormatScore(entry.OverallScore));

    private static string FormatCompactModel(ModelRadarEntry entry)
        => $"{FormatDisplayModel(entry.Model)}/{FormatDisplayReasoningEffort(entry.ReasoningEffort)}";

    private static string FormatDisplayModel(string value)
    {
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
            ModelName = FormatDisplayModel(entry.Model),
            ReasoningEffort = FormatDisplayReasoningEffort(entry.ReasoningEffort),
            ScoreText = FormatScore(score),
            DimensionSummary = _localizationService.Format(
                "ModelRadar.DimensionSummaryFormat",
                FormatScore(entry.BackendScore),
                FormatScore(entry.FrontendScore),
                FormatScore(entry.KnowledgeScore))
        };

    private ModelRadarDisplayRow CreateDisplayRow(ModelRadarListItem item)
    {
        var entry = item.Entry;
        return new ModelRadarDisplayRow
        {
            Position = item.Position,
            ModelName = FormatDisplayModel(entry.Model),
            ReasoningEffort = FormatDisplayReasoningEffort(entry.ReasoningEffort),
            ScoreText = FormatScore(item.Score),
            DecisionTagsText = entry.DecisionTags.Count == 0
                ? string.Empty
                : string.Join(" · ", entry.DecisionTags),
            ToolTipText = string.Join(
                Environment.NewLine,
                [
                    $"{L("ModelRadar.OverallScore")}：{FormatScore(entry.OverallScore)}",
                    $"{L("ModelRadar.BackendScore")}：{FormatScore(entry.BackendScore)}",
                    $"{L("ModelRadar.FrontendScore")}：{FormatScore(entry.FrontendScore)}",
                    $"{L("ModelRadar.KnowledgeScore")}：{FormatScore(entry.KnowledgeScore)}",
                    $"{L("ModelRadar.Elapsed")}：{FormatElapsed(entry.ElapsedMilliseconds)}",
                    $"{L("ModelRadar.ReferenceCost")}：{FormatReferenceCost(entry.EstimatedReferenceCostUsd)}",
                    $"{L("ModelRadar.Route")}：{(string.IsNullOrWhiteSpace(entry.Route) ? "--" : entry.Route)}"
                ])
        };
    }

    private string FormatScore(double? value) => value is null
        ? "--"
        : value.Value.ToString("0.0", _localizationService.CurrentCulture);

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
