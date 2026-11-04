using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public sealed class ModelRadarPresentationRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OutdatedFiveAxisCache_ShouldRefreshImmediatelyAndExposeOldBasis()
    {
        var snapshot = Snapshot(Entry()) with { IsOutdatedPolicy = true };
        var service = new FakeService(snapshot);
        using var viewModel = ViewModel(service);

        await viewModel.SetEnabledAsync(true);

        Assert.Equal(1, service.RefreshCount);
        Assert.Equal("ModelRadar.OutdatedPolicyBasis", viewModel.ScoreBasisText);
        Assert.Contains("ModelRadar.CompactOutdatedPolicy", viewModel.CompactOverallModelText);
        Assert.True(viewModel.IsOfflineCache);
    }

    [Theory]
    [InlineData(true, "ModelRadar.OutdatedPolicyBasis")]
    [InlineData(false, "ModelRadar.RankingDescription")]
    public async Task ExpandedRankingDescription_ShouldIdentifyOldPolicyAndPreserveCurrentDescription(
        bool outdated, string expected)
    {
        var snapshot = Snapshot(Entry()) with { IsOutdatedPolicy = outdated };
        using var viewModel = ViewModel(new FakeService(snapshot));
        await viewModel.SetEnabledAsync(true);

        Assert.Equal(expected, viewModel.RankingDescription);

        viewModel.SelectCategoryCommand.Execute(ModelRadarCategory.Backend);
        Assert.Equal(expected, viewModel.RankingDescription);
    }

    [Fact]
    public async Task DisplayAlias_ShouldChangeVisibleEffortWhilePreservingRawIdentity()
    {
        var entry = Entry() with { ReasoningEffort = "high", DisplayReasoningEffort = "XHigh" };
        using var viewModel = ViewModel(new FakeService(Snapshot(entry)));

        await viewModel.SetEnabledAsync(true);

        Assert.Equal("XHigh", viewModel.CompactOverallEffortText);
        Assert.EndsWith("/XHigh", viewModel.CompactOverallModelText);
        Assert.Equal("XHigh", viewModel.OverallDecision!.ReasoningEffort);
        Assert.Equal("XHigh", Assert.Single(viewModel.VisibleRows).ReasoningEffort);
        Assert.Contains("test-model / high", viewModel.OverallDecision.ToolTipText);
        Assert.Equal("high", viewModel.Snapshot!.OverallLeader!.ReasoningEffort);
    }

    [Theory]
    [InlineData("xhigh", "xhigh", "XHigh")]
    [InlineData("xhigh", "max", "Max")]
    [InlineData("unknown", "high", "High")]
    public async Task DisplayAliasOrRawFallback_ShouldPreserveExistingEffortCasing(
        string raw, string display, string expected)
    {
        var entry = Entry() with { ReasoningEffort = raw, DisplayReasoningEffort = display };
        using var viewModel = ViewModel(new FakeService(Snapshot(entry)));
        await viewModel.SetEnabledAsync(true);

        Assert.Equal(expected, viewModel.CompactOverallEffortText);
        Assert.EndsWith($"/{expected}", viewModel.CompactOverallModelText);
        Assert.Equal(expected, viewModel.OverallDecision!.ReasoningEffort);
        var row = Assert.Single(viewModel.VisibleRows);
        Assert.Equal(expected, row.ReasoningEffort);
        Assert.Contains($"config=test-model~{raw}", row.ModelDetailUri!.Query);
        Assert.Equal(raw, viewModel.Snapshot!.OverallLeader!.ReasoningEffort);
    }

    [Fact]
    public async Task CategoryMetrics_ShouldFollowAxisWhileLeaderKeepsOverallMetrics()
    {
        var entry = Entry() with
        {
            ElapsedMilliseconds = 999999,
            EstimatedReferenceCostUsd = 99,
            Metrics = new Dictionary<ModelRadarCategory, ModelRadarMetrics>
            {
                [ModelRadarCategory.Overall] = new(125000, 1.25),
                [ModelRadarCategory.Backend] = new(1500, 0.25)
            }
        };
        using var viewModel = ViewModel(new FakeService(Snapshot(entry)));
        await viewModel.SetEnabledAsync(true);
        var leaderTooltip = viewModel.OverallDecision!.ToolTipText;
        Assert.Contains("ModelRadar.ElapsedFormat: 2, 5", leaderTooltip);
        Assert.Contains("USD 1.25", leaderTooltip);

        viewModel.SelectCategoryCommand.Execute(ModelRadarCategory.Backend);

        var row = Assert.Single(viewModel.VisibleRows);
        Assert.Contains("ModelRadar.ElapsedSecondsFormat: 1.5", row.ToolTipText);
        Assert.Contains("USD 0.25", row.ToolTipText);
        Assert.DoesNotContain("999", row.ToolTipText);
        Assert.Equal(leaderTooltip, viewModel.OverallDecision!.ToolTipText);

        viewModel.SelectCategoryCommand.Execute(ModelRadarCategory.Aesthetic);
        row = Assert.Single(viewModel.VisibleRows);
        Assert.Contains("ModelRadar.Elapsed：--", row.ToolTipText);
        Assert.Contains("ModelRadar.ReferenceCost：--", row.ToolTipText);
        Assert.Equal(leaderTooltip, viewModel.OverallDecision.ToolTipText);
    }

    [Fact]
    public async Task EvaluationSource_ShouldUseConnectionAndExplainMetricScope()
    {
        var entry = Entry() with { ConnectionId = "published-source", Route = "obsolete-route" };
        using var viewModel = ViewModel(new FakeService(Snapshot(entry)));
        await viewModel.SetEnabledAsync(true);

        var tooltip = Assert.Single(viewModel.VisibleRows).ToolTipText;
        Assert.Contains("ModelRadar.EvaluationSource：published-source", tooltip);
        Assert.DoesNotContain("obsolete-route", tooltip);
        Assert.Contains("ModelRadar.EvaluationMetricsNote", tooltip);
    }

    [Fact]
    public async Task HighlightAndOfficialLinks_ShouldFollowEntryAndSelectedAxis()
    {
        var entry = Entry() with { IsRecentHighlight = true, ReasoningEffort = "high", DisplayReasoningEffort = "XHigh" };
        using var viewModel = ViewModel(new FakeService(Snapshot(entry)));
        await viewModel.SetEnabledAsync(true);

        var row = Assert.Single(viewModel.VisibleRows);
        Assert.True(Property<bool>(row, "IsRecentHighlight"));
        Assert.Equal("ModelRadar.New", Property<string>(row, "RecentHighlightText"));
        Assert.Equal("modeldial.com", Property<Uri>(row, "ModelDetailUri").Host);
        Assert.Contains("config=test-model~high", Property<Uri>(row, "ModelDetailUri").Query);
        Assert.DoesNotContain("XHigh", Property<Uri>(row, "ModelDetailUri").AbsoluteUri);
        Assert.Contains("axis=overall", Property<Uri>(viewModel, "FullRankingUri").Query);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SelectCategoryCommand.Execute(ModelRadarCategory.Backend);

        Assert.Contains("FullRankingUri", changed);
        Assert.Contains("axis=backend", Property<Uri>(viewModel, "FullRankingUri").Query);
        Assert.Contains("axis=backend", Property<Uri>(Assert.Single(viewModel.VisibleRows), "ModelDetailUri").Query);
        Assert.Contains("axis=overall", Property<Uri>(viewModel.OverallDecision!, "ModelDetailUri").Query);
    }

    [Fact]
    public async Task EntryWithoutOfficialHighlight_ShouldNotDisplayNewLabel()
    {
        using var viewModel = ViewModel(new FakeService(Snapshot(Entry())));
        await viewModel.SetEnabledAsync(true);

        var row = Assert.Single(viewModel.VisibleRows);
        Assert.False(Property<bool>(row, "IsRecentHighlight"));
        Assert.Equal(string.Empty, Property<string>(row, "RecentHighlightText"));
    }

    private static T Property<T>(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name);
        Assert.NotNull(property);
        return Assert.IsType<T>(property.GetValue(instance));
    }

    private static ModelRadarEntry Entry() => new()
    {
        Id = "source:test-model:high",
        Model = "test-model",
        ReasoningEffort = "high",
        OverallScore = 89,
        BackendScore = 91,
        AestheticScore = 80
    };

    private static ModelRadarSnapshot Snapshot(ModelRadarEntry entry) => new()
    {
        SchemaVersion = "2.0",
        PublishedAt = Now,
        OverallLeader = entry,
        OverallTop = [new(1, entry, entry.OverallScore)],
        BackendTop = [new(1, entry, entry.BackendScore)],
        AestheticTop = [new(1, entry, entry.AestheticScore)]
    };

    private static ModelRadarViewModel ViewModel(FakeService service) => new(
        service, new Localization(), new FixedTimeProvider(), TimeSpan.FromHours(6), startAutomaticRefresh: false);

    private sealed class FakeService(ModelRadarSnapshot snapshot) : IModelRadarService
    {
        public int RefreshCount { get; private set; }
        public Task<ModelRadarServiceResult> ReadCacheAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelRadarServiceResult
            {
                Status = ModelRadarServiceStatus.Success,
                Snapshot = snapshot,
                CachedAtUtc = Now
            });
        public Task<ModelRadarServiceResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return Task.FromResult(new ModelRadarServiceResult { Status = ModelRadarServiceStatus.NetworkError });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Localization : ILocalizationService
    {
        public event EventHandler? LanguageChanged { add { } remove { } }
        public string CurrentLanguage => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language) { }
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) => key == "ModelRadar.ReferenceCostFormat"
            ? $"USD {args[0]}"
            : $"{key}: {string.Join(", ", args)}";
    }
}
