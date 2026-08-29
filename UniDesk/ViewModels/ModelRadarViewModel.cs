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
    }

    private ModelRadarDecisionCard CreateDecisionCard(ModelRadarEntry entry, double? score) =>
        new()
        {
            ModelName = entry.Model,
            ReasoningEffort = entry.ReasoningEffort,
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
            ModelName = entry.Model,
            ReasoningEffort = entry.ReasoningEffort,
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
        RefreshCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCategoryChanged(ModelRadarCategory value) =>
        OnPropertyChanged(nameof(RankingDescription));

    partial void OnIsOfflineCacheChanged(bool value) =>
        OnPropertyChanged(nameof(StatusText));

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
