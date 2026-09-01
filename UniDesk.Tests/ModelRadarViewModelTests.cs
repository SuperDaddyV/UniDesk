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
        Assert.False(viewModel.IsCompactSummaryVisible);
        Assert.False(viewModel.HasCompactRecommendations);
        Assert.Equal(string.Empty, viewModel.CompactOverallLabelText);
        Assert.Equal(string.Empty, viewModel.CompactOverallModelText);
        Assert.Equal(string.Empty, viewModel.CompactOverallScoreText);
        Assert.Equal(string.Empty, viewModel.CompactValueLabelText);
        Assert.Equal(string.Empty, viewModel.CompactValueModelText);
        Assert.Equal(string.Empty, viewModel.CompactValueScoreText);
        Assert.Equal(string.Empty, viewModel.CompactOverallText);
        Assert.Equal(string.Empty, viewModel.CompactValueText);
        Assert.Equal(string.Empty, viewModel.CompactStatusText);
        Assert.Equal(string.Empty, viewModel.CompactToolTipText);
        Assert.Equal(0, service.ReadCacheCallCount);
        Assert.Equal(0, service.RefreshCallCount);
        Assert.False(viewModel.IsAutomaticRefreshScheduled);
    }

    [Fact]
    public async Task CompactSummary_WithCompleteSnapshot_ShouldExposeOfficialRecommendationsAndTooltip()
    {
        var localization = new TestLocalizationService();
        var snapshot = CreateSnapshot(publishedAt: Now);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service, localization);

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsCompactSummaryVisible);
        Assert.True(viewModel.HasCompactRecommendations);
        Assert.Equal("ModelRadar.CompactOverallLabel", viewModel.CompactOverallLabelText);
        Assert.EndsWith(
            "Model-Name/High",
            viewModel.CompactOverallModelText,
            StringComparison.Ordinal);
        Assert.Equal("88.0", viewModel.CompactOverallScoreText);
        Assert.Equal("ModelRadar.CompactValueLabel", viewModel.CompactValueLabelText);
        Assert.Equal("Model-Name/High", viewModel.CompactValueModelText);
        Assert.Equal("88.0", viewModel.CompactValueScoreText);
        Assert.Contains(snapshot.OverallLeader!.Model, viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Contains(snapshot.OverallLeader.ReasoningEffort, viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Contains("88.0", viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Contains("ModelRadar.CompactOverallFormat", viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Contains(snapshot.ValueRecommendation!.Model, viewModel.CompactValueText, StringComparison.Ordinal);
        Assert.Contains(snapshot.ValueRecommendation.ReasoningEffort, viewModel.CompactValueText, StringComparison.Ordinal);
        Assert.Contains("88.0", viewModel.CompactValueText, StringComparison.Ordinal);
        Assert.Contains("ModelRadar.CompactValueFormat", viewModel.CompactValueText, StringComparison.Ordinal);
        Assert.Contains(viewModel.CompactOverallText, viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains(viewModel.CompactValueText, viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains(viewModel.PublishedText, viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains(viewModel.StatusText, viewModel.CompactToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Presentation_ShouldNormalizeModelAndEffortCasingWithoutChangingRawTooltip()
    {
        var snapshot = CreateSnapshot(
            publishedAt: Now,
            model: "gpt-5.6-sol",
            reasoningEffort: "medium",
            valueModel: "claude-opus-5",
            valueReasoningEffort: "xhigh");
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.Equal("GPT5.6-Sol/Medium", viewModel.CompactOverallModelText);
        Assert.Equal("Claude-Opus-5/XHigh", viewModel.CompactValueModelText);
        Assert.Equal("GPT5.6-Sol", viewModel.OverallDecision!.ModelName);
        Assert.Equal("Medium", viewModel.OverallDecision.ReasoningEffort);
        Assert.Equal("Claude-Opus-5", viewModel.ValueDecision!.ModelName);
        Assert.Equal("XHigh", viewModel.ValueDecision.ReasoningEffort);
        var row = Assert.Single(viewModel.VisibleRows);
        Assert.Equal("GPT5.6-Sol", row.ModelName);
        Assert.Equal("Medium", row.ReasoningEffort);
        Assert.Contains("gpt-5.6-sol", viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains("claude-opus-5", viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains("medium", viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains("xhigh", viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Equal("gpt-5.6-sol", snapshot.OverallLeader!.Model);
        Assert.Equal("medium", snapshot.OverallLeader.ReasoningEffort);
        Assert.Equal("claude-opus-5", snapshot.ValueRecommendation!.Model);
        Assert.Equal("xhigh", snapshot.ValueRecommendation.ReasoningEffort);
    }

    [Fact]
    public async Task CompactSummary_WithPendingSnapshot_ShouldExposeStatusWithoutBackendRecommendation()
    {
        var snapshot = CreateSnapshot("pending", isPending: true);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsCompactSummaryVisible);
        Assert.False(viewModel.HasCompactRecommendations);
        Assert.Equal(string.Empty, viewModel.CompactOverallText);
        Assert.Equal(string.Empty, viewModel.CompactValueText);
        Assert.Contains(viewModel.StatusText, viewModel.CompactStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.ValueRecommendation!.Model, viewModel.CompactStatusText, StringComparison.Ordinal);
        Assert.Contains(viewModel.StatusText, viewModel.CompactToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompactSummary_WithoutValueRecommendation_ShouldUseNoValueStatus()
    {
        var snapshot = CreateSnapshot() with { ValueRecommendation = null };
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsCompactSummaryVisible);
        Assert.True(viewModel.HasCompactRecommendations);
        Assert.Equal("ModelRadar.CompactOverallLabel", viewModel.CompactOverallLabelText);
        Assert.EndsWith(
            "Model-Name/High",
            viewModel.CompactOverallModelText,
            StringComparison.Ordinal);
        Assert.Equal("88.0", viewModel.CompactOverallScoreText);
        Assert.Equal("ModelRadar.CompactValueLabel", viewModel.CompactValueLabelText);
        Assert.Equal("ModelRadar.NoValueRecommendation", viewModel.CompactValueModelText);
        Assert.Equal(string.Empty, viewModel.CompactValueScoreText);
        Assert.Contains(snapshot.OverallLeader!.Model, viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Equal("ModelRadar.NoValueRecommendation", viewModel.CompactValueText);
        Assert.Equal(string.Empty, viewModel.CompactStatusText);
        Assert.Contains(viewModel.CompactOverallText, viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains("ModelRadar.NoValueRecommendation", viewModel.CompactToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompactSummary_WithoutSnapshot_ShouldExposeUnavailableStatus()
    {
        var service = new FakeModelRadarService
        {
            CacheResult = Result(ModelRadarServiceStatus.NotFound),
            RefreshHandler = _ => Task.FromResult(Result(ModelRadarServiceStatus.NetworkError))
        };
        using var viewModel = CreateViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.True(viewModel.IsCompactSummaryVisible);
        Assert.False(viewModel.HasCompactRecommendations);
        Assert.Equal(string.Empty, viewModel.CompactOverallText);
        Assert.Equal(string.Empty, viewModel.CompactValueText);
        Assert.Contains(viewModel.StatusText, viewModel.CompactStatusText, StringComparison.Ordinal);
        Assert.Contains(viewModel.StatusText, viewModel.CompactToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompactSummary_WithOfflineCache_ShouldKeepPublishedDateAndOfflineStatusInTooltip()
    {
        var localization = new TestLocalizationService();
        var snapshot = CreateSnapshot(publishedAt: Now.AddDays(-1));
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now),
            RefreshHandler = _ => Task.FromResult(Result(ModelRadarServiceStatus.NetworkError))
        };
        using var viewModel = CreateViewModel(service, localization);

        await viewModel.SetEnabledAsync(true);
        Assert.True(viewModel.HasCompactRecommendations);
        Assert.Contains(
            snapshot.PublishedAt.ToLocalTime().ToString("MM/dd", localization.CurrentCulture),
            viewModel.CompactOverallText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ModelRadar.CompactCached", viewModel.CompactOverallText, StringComparison.Ordinal);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCompactRecommendations);
        Assert.Contains(
            snapshot.PublishedAt.ToLocalTime().ToString("MM/dd", localization.CurrentCulture),
            viewModel.CompactOverallText,
            StringComparison.Ordinal);
        Assert.Contains("ModelRadar.CompactCached", viewModel.CompactOverallText, StringComparison.Ordinal);
        Assert.Contains(viewModel.PublishedText, viewModel.CompactToolTipText, StringComparison.Ordinal);
        Assert.Contains(viewModel.StatusText, viewModel.CompactToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompactSummary_ShouldRefreshAllBindingsAfterLanguageChange()
    {
        var localization = new TestLocalizationService();
        var snapshot = CreateSnapshot(publishedAt: Now);
        var service = new FakeModelRadarService
        {
            CacheResult = Success(snapshot, Now)
        };
        using var viewModel = CreateViewModel(service, localization);
        await viewModel.SetEnabledAsync(true);

        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        localization.SetLanguage("zh-CN");

        Assert.Contains(nameof(viewModel.IsCompactSummaryVisible), changedProperties);
        Assert.Contains(nameof(viewModel.HasCompactRecommendations), changedProperties);
        Assert.Contains(nameof(viewModel.CompactOverallLabelText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactOverallModelText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactOverallScoreText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactValueLabelText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactValueModelText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactValueScoreText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactOverallText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactValueText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactStatusText), changedProperties);
        Assert.Contains(nameof(viewModel.CompactToolTipText), changedProperties);
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
        Assert.Equal("Full-Model-Name", row.ModelName);
        Assert.Equal("Max", row.ReasoningEffort);
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

    private static ModelRadarViewModel CreateViewModel(
        FakeModelRadarService service,
        TestLocalizationService? localization = null) =>
        new(
            service,
            localization ?? new TestLocalizationService(),
            new FixedTimeProvider(Now),
            TimeSpan.FromHours(6),
            startAutomaticRefresh: false);

    private static ModelRadarSnapshot CreateSnapshot(
        string batchId = "overall-batch",
        bool isPending = false,
        DateTimeOffset? publishedAt = null,
        string model = "model-name",
        string reasoningEffort = "high",
        string? valueModel = null,
        string? valueReasoningEffort = null)
    {
        var entry = new ModelRadarEntry
        {
            Id = $"provider:model:{batchId}",
            Model = model,
            DisplayName = $"{model} / {reasoningEffort}",
            ReasoningEffort = reasoningEffort,
            Route = "official_login",
            OverallScore = isPending ? null : 88,
            BackendScore = 86,
            FrontendScore = isPending ? null : 90,
            KnowledgeScore = isPending ? null : 85,
            DecisionTags = ["value"]
        };
        var valueEntry = valueModel == null && valueReasoningEffort == null
            ? entry
            : entry with
            {
                Id = $"provider:value:{batchId}",
                Model = valueModel ?? model,
                DisplayName = $"{valueModel ?? model} / {valueReasoningEffort ?? reasoningEffort}",
                ReasoningEffort = valueReasoningEffort ?? reasoningEffort
            };
        var backendTop = new[] { new ModelRadarListItem(1, entry, entry.BackendScore) };
        var overallTop = isPending
            ? Array.Empty<ModelRadarListItem>()
            : [new ModelRadarListItem(1, entry, entry.OverallScore)];

        return new ModelRadarSnapshot
        {
            SchemaVersion = "1.1",
            BatchId = batchId,
            PublishedAt = publishedAt ?? new DateTimeOffset(2026, 8, 29, 2, 44, 55, TimeSpan.Zero),
            IsPending = isPending,
            OverallLeader = isPending ? null : entry,
            ValueRecommendation = valueEntry,
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
        public string CurrentLanguage { get; private set; } = "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language)
        {
            CurrentLanguage = language ?? "en-US";
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) =>
            string.Format(CultureInfo.InvariantCulture, "{0}: {1}", key, string.Join(", ", args));
    }
}
