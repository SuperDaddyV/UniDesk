using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public class ModelRadarViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DisabledViewModel_ShouldNotReadCacheOrStartNetwork()
    {
        var service = new FakeModelRadarService();
        using var viewModel = new ModelRadarViewModel(service, new TestLocalizationService());

        Assert.False(viewModel.IsEnabled);
        Assert.Equal(0, service.ReadCacheCallCount);
        Assert.Equal(0, service.RefreshCallCount);
        Assert.False(viewModel.IsAutomaticRefreshScheduled);
    }

    [Fact]
    public async Task EnableWithFreshCache_ShouldScheduleAutomaticRefreshAndDisableShouldCancelIt()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new FakeModelRadarService
        {
            CacheResult = Success(CreateSnapshot(), now)
        };
        using var viewModel = new ModelRadarViewModel(service, new TestLocalizationService());

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsAutomaticRefreshScheduled);
        Assert.Equal(0, service.RefreshCallCount);

        await viewModel.SetEnabledAsync(false);

        Assert.False(viewModel.IsAutomaticRefreshScheduled);
        Assert.False(viewModel.IsEnabled);
        Assert.Equal(0, service.RefreshCallCount);
    }

    [Fact]
    public async Task EnableAsync_WithFreshCache_ShouldDisplayCacheWithoutNetworkRequest()
    {
        var snapshot = CreateSnapshot();
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now.AddHours(-1))
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsEnabled);
        Assert.Same(snapshot, viewModel.Snapshot);
        Assert.Equal(ModelRadarCacheState.Fresh, viewModel.State);
        Assert.Equal(1, service.ReadCacheCallCount);
        Assert.Equal(0, service.RefreshCallCount);
    }

    [Fact]
    public async Task EnableAsync_WithStaleCache_ShouldDisplayItBeforeRefreshCompletes()
    {
        var cached = CreateSnapshot("cached");
        var refreshed = CreateSnapshot("refreshed");
        var refreshCompletion = new TaskCompletionSource<ModelRadarServiceResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(cached, Now.AddHours(-7)),
            RefreshHandler = _ => refreshCompletion.Task
        };
        using var viewModel = CreateViewModel(service);

        var enabling = viewModel.SetEnabledAsync(true);
        await service.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(cached, viewModel.Snapshot);
        Assert.Equal(ModelRadarCacheState.Stale, viewModel.State);

        refreshCompletion.SetResult(Success(refreshed, Now));
        await enabling;

        Assert.Same(refreshed, viewModel.Snapshot);
        Assert.Equal(ModelRadarCacheState.Fresh, viewModel.State);
    }

    [Fact]
    public async Task DisableAsync_ShouldCancelRefreshAndSuppressLateResult()
    {
        var lateSnapshot = CreateSnapshot("late");
        var refreshCompletion = new TaskCompletionSource<ModelRadarServiceResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken serviceCancellation = default;
        var service = new FakeModelRadarService
        {
            CacheResult = Result(ModelRadarServiceStatus.NotFound),
            RefreshHandler = token =>
            {
                serviceCancellation = token;
                return refreshCompletion.Task;
            }
        };
        using var viewModel = CreateViewModel(service);

        var enabling = viewModel.SetEnabledAsync(true);
        await service.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await viewModel.SetEnabledAsync(false);

        Assert.True(serviceCancellation.IsCancellationRequested);
        Assert.False(viewModel.IsEnabled);
        Assert.False(viewModel.IsAutomaticRefreshScheduled);

        refreshCompletion.SetResult(Success(lateSnapshot, Now));
        await enabling;

        Assert.Null(viewModel.Snapshot);
    }

    [Fact]
    public async Task RefreshCommand_ShouldNotStartConcurrentRequests()
    {
        var cached = CreateSnapshot("cached");
        var refreshed = CreateSnapshot("refreshed");
        var refreshCompletion = new TaskCompletionSource<ModelRadarServiceResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(cached, Now),
            RefreshHandler = _ => refreshCompletion.Task
        };
        using var viewModel = CreateViewModel(service);
        await viewModel.SetEnabledAsync(true);

        var first = viewModel.RefreshCommand.ExecuteAsync(null);
        await service.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsRefreshing);
        Assert.False(viewModel.CanRefresh);
        Assert.Equal(1, service.RefreshCallCount);

        refreshCompletion.SetResult(Success(refreshed, Now));
        await Task.WhenAll(first, second);

        Assert.False(viewModel.IsRefreshing);
        Assert.True(viewModel.CanRefresh);
        Assert.Same(refreshed, viewModel.Snapshot);
    }

    [Fact]
    public async Task PendingSnapshot_ShouldSelectBackendAndDisableUnavailableCategories()
    {
        var pending = CreateSnapshot("pending", isPending: true);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(pending, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.Equal(ModelRadarCacheState.Pending, viewModel.State);
        Assert.Equal(ModelRadarCategory.Backend, viewModel.SelectedCategory);
        Assert.False(viewModel.IsOverallCategoryEnabled);
        Assert.True(viewModel.IsBackendCategoryEnabled);
        Assert.False(viewModel.IsFrontendCategoryEnabled);
        Assert.False(viewModel.IsKnowledgeCategoryEnabled);
        Assert.Equal(pending.BackendTop, viewModel.VisibleRankings);
    }

    [Fact]
    public async Task DisplayRows_ShouldKeepCompleteConfigurationAndUseDashesForMissingMetrics()
    {
        var entry = new ModelRadarEntry
        {
            Id = "provider:model:missing",
            Model = "full-model-name",
            DisplayName = "full-model-name / Max",
            ReasoningEffort = "max",
            Route = "official_login",
            BackendScore = 91,
            DecisionTags = ["recommended", "value"]
        };
        var snapshot = new ModelRadarSnapshot
        {
            SchemaVersion = "1.1",
            BatchId = "backend-batch",
            PublishedAt = Now,
            IsPending = true,
            ValueRecommendation = entry,
            BackendTop = [new ModelRadarListItem(1, entry, entry.BackendScore)]
        };
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        var row = Assert.Single(viewModel.VisibleRows);
        Assert.Equal("full-model-name", row.ModelName);
        Assert.Equal("max", row.ReasoningEffort);
        Assert.Equal("91.0", row.ScoreText);
        Assert.Equal("recommended · value", row.DecisionTagsText);
        Assert.Contains("--", row.ToolTipText, StringComparison.Ordinal);
        Assert.Contains("official_login", row.ToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisplayRows_WithoutDecisionTags_ShouldNotRenderPlaceholderText()
    {
        var snapshot = CreateSnapshot();
        var entry = snapshot.OverallTop[0].Entry with { DecisionTags = [] };
        snapshot = snapshot with
        {
            OverallLeader = entry,
            ValueRecommendation = null,
            OverallTop = [new ModelRadarListItem(1, entry, entry.OverallScore)]
        };
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.Equal(string.Empty, Assert.Single(viewModel.VisibleRows).DecisionTagsText);
    }

    [Fact]
    public async Task RefreshFailure_WithCache_ShouldKeepSnapshotAndMarkOfflineStale()
    {
        var cached = CreateSnapshot("cached");
        var service = new FakeModelRadarService
        {
            CacheResult = Success(cached, Now),
            RefreshHandler = _ => Task.FromResult(Result(ModelRadarServiceStatus.NetworkError))
        };
        using var viewModel = CreateViewModel(service);
        await viewModel.SetEnabledAsync(true);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(cached, viewModel.Snapshot);
        Assert.Equal(ModelRadarCacheState.Stale, viewModel.State);
        Assert.True(viewModel.IsOfflineCache);
    }

    [Theory]
    [InlineData(ModelRadarServiceStatus.NetworkError, ModelRadarCacheState.Unavailable)]
    [InlineData(ModelRadarServiceStatus.ResponseTooLarge, ModelRadarCacheState.Unavailable)]
    [InlineData(ModelRadarServiceStatus.SchemaError, ModelRadarCacheState.SchemaError)]
    public async Task EnableAsync_WithoutCache_ShouldExposeFailureState(
        ModelRadarServiceStatus serviceStatus,
        ModelRadarCacheState expectedState)
    {
        var service = new FakeModelRadarService
        {
            CacheResult = Result(ModelRadarServiceStatus.NotFound),
            RefreshHandler = _ => Task.FromResult(Result(serviceStatus))
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.Null(viewModel.Snapshot);
        Assert.Equal(expectedState, viewModel.State);
    }

    private static ModelRadarViewModel CreateViewModel(FakeModelRadarService service) =>
        new(
            service,
            new TestLocalizationService(),
            new FixedTimeProvider(Now),
            TimeSpan.FromHours(6),
            startAutomaticRefresh: false);

    private static ModelRadarSnapshot CreateSnapshot(string batchId = "overall-batch", bool isPending = false)
    {
        var entry = new ModelRadarEntry
        {
            Id = $"provider:model:{batchId}",
            Model = "model-name",
            DisplayName = "model-name / High",
            ReasoningEffort = "high",
            Route = "official_login",
            OverallScore = isPending ? null : 88,
            BackendScore = 86,
            FrontendScore = isPending ? null : 90,
            KnowledgeScore = isPending ? null : 85,
            DecisionTags = ["value"]
        };
        var backendTop = new[] { new ModelRadarListItem(1, entry, entry.BackendScore) };
        var overallTop = isPending
            ? Array.Empty<ModelRadarListItem>()
            : [new ModelRadarListItem(1, entry, entry.OverallScore)];

        return new ModelRadarSnapshot
        {
            SchemaVersion = "1.1",
            BatchId = batchId,
            PublishedAt = new DateTimeOffset(2026, 8, 29, 2, 44, 55, TimeSpan.Zero),
            IsPending = isPending,
            OverallLeader = isPending ? null : entry,
            ValueRecommendation = entry,
            OverallTop = overallTop,
            BackendTop = backendTop,
            FrontendTop = isPending ? [] : [new ModelRadarListItem(1, entry, entry.FrontendScore)],
            KnowledgeTop = isPending ? [] : [new ModelRadarListItem(1, entry, entry.KnowledgeScore)]
        };
    }

    private static ModelRadarServiceResult Success(
        ModelRadarSnapshot snapshot,
        DateTimeOffset cachedAtUtc) =>
        new()
        {
            Status = ModelRadarServiceStatus.Success,
            Snapshot = snapshot,
            CachedAtUtc = cachedAtUtc,
            CachePersisted = true
        };

    private static ModelRadarServiceResult Result(ModelRadarServiceStatus status) =>
        new() { Status = status };

    private sealed class FakeModelRadarService : IModelRadarService
    {
        public ModelRadarServiceResult CacheResult { get; set; } = Result(ModelRadarServiceStatus.NotFound);
        public Func<CancellationToken, Task<ModelRadarServiceResult>> RefreshHandler { get; set; } =
            _ => Task.FromResult(Result(ModelRadarServiceStatus.NetworkError));
        public int ReadCacheCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }
        public TaskCompletionSource RefreshStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelRadarServiceResult> ReadCacheAsync(CancellationToken cancellationToken = default)
        {
            ReadCacheCallCount++;
            return Task.FromResult(CacheResult);
        }

        public Task<ModelRadarServiceResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            RefreshStarted.TrySetResult();
            return RefreshHandler(cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged;
        public string CurrentLanguage => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) =>
            string.Format(CultureInfo.InvariantCulture, "{0}: {1}", key, string.Join(", ", args));
    }
}
