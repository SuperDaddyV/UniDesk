using System.Text.Json;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using UniDesk.Models;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public sealed class CompactDashboardRevisionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static IEnumerable<object[]> RuntimeSelections =>
        from id in new[] { "TimeWeather", "HardwareMonitor", "ModelRadar", "Todos" }
        from expanded in new[] { false, true }
        from compact in new[] { false, true }
        from collapsed in new[] { false, true }
        select new object[] { id, expanded, compact, collapsed };

    [Theory]
    [MemberData(nameof(RuntimeSelections))]
    public void ActiveModule_ShouldFollowTheCurrentModeOnly(string id, bool expanded, bool compact, bool collapsed)
    {
        // Exercise the pure selection rule without starting the app's desktop services.
        var viewModel = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        typeof(MainWindowViewModel).GetField("<ModuleSettings>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new ObservableCollection<ModuleSetting>([
                new ModuleSetting { ModuleId = id, IsEnabled = expanded, IsCompactEnabled = compact }]));
        typeof(MainWindowViewModel).GetField("_isPanelCollapsed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, collapsed);
        Assert.Equal(collapsed ? compact : expanded, viewModel.IsModuleActive(id));
    }

    [Theory]
    [InlineData("TimeWeather", true, false)]
    [InlineData("HardwareMonitor", false, true)]
    [InlineData("ModelRadar", false, true)]
    [InlineData("Todos", true, false)]
    public void CompactSelection_ShouldSurviveNormalizeCloneAndSettingsEdit(string id, bool expanded, bool compact)
    {
        var json = $$"""[{"moduleId":"{{id}}","isEnabled":{{expanded.ToString().ToLowerInvariant()}},"isCompactEnabled":{{compact.ToString().ToLowerInvariant()}}}]""";
        var input = JsonSerializer.Deserialize<List<ModuleSetting>>(json, JsonOptions)!;
        var normalized = DashboardModuleCatalog.Normalize(input).Single(module => module.ModuleId == id);
        var edited = ModuleSettingOptionViewModel.FromModel(normalized.Clone()).ToModel();
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(edited, JsonOptions));
        Assert.Equal(expanded, result.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.True(result.RootElement.TryGetProperty("isCompactEnabled", out var enabled), "Independent compact setting was lost.");
        Assert.Equal(compact, enabled.GetBoolean());
    }

    [Theory]
    [InlineData("TimeWeather", false, true)]
    [InlineData("HardwareMonitor", false, true)]
    [InlineData("Todos", false, true)]
    [InlineData("ModelRadar", false, false)]
    [InlineData("ModelRadar", true, true)]
    [InlineData("QuickNotes", true, false)]
    public void LegacySettings_ShouldPreservePreviousCompactVisibility(string id, bool expanded, bool compact)
    {
        var module = DashboardModuleCatalog.Normalize([new ModuleSetting { ModuleId = id, IsEnabled = expanded }])
            .Single(item => item.ModuleId == id);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(module, JsonOptions));
        Assert.True(result.RootElement.TryGetProperty("isCompactEnabled", out var enabled));
        Assert.Equal(compact, enabled.GetBoolean());
    }

    [Fact]
    public void Radar_ShouldHaveOneRowOfSixCategoriesInRequestedOrder_WithoutValueOrDisclaimer()
    {
        var xml = XDocument.Load(Path.Combine(Root, "UniDesk", "Controls", "ModelRadarModuleView.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var categories = xml.Descendants().Single(item => (string?)item.Attribute(x + "Name") == "RadarCategories");
        var buttons = categories.Elements().Where(item => item.Name.LocalName == "Button").ToArray();
        Assert.Equal(new[] { "Overall", "Knowledge", "Backend", "Aesthetic", "Frontend", "Maintainability" },
            buttons.Select(button => ((string)button.Attribute("CommandParameter")!).Split('.')[^1].TrimEnd('}')));
        Assert.All(buttons, button => Assert.True((string?)button.Attribute("Grid.Row") is null or "0"));
        Assert.DoesNotContain("ValueDecision", xml.ToString());
        Assert.DoesNotContain("NoValueRecommendation", xml.ToString());
        Assert.DoesNotContain("PublicBenchmarkDisclaimer", xml.ToString());
        Assert.Single(xml.Descendants(), item => (string?)item.Attribute("Text") == "{Binding RankingDescription}");
        Assert.DoesNotContain("{Binding ScoreBasisText}", xml.ToString());
    }

    [Fact]
    public void CompactLayout_ShouldRemoveValueAndFixedHeight_AndExposeIndependentToggles()
    {
        var xaml = File.ReadAllText(Path.Combine(Root, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
        var settings = File.ReadAllText(Path.Combine(Root, "UniDesk", "Controls", "Settings", "ModulesSettingsPage.xaml"));
        var window = File.ReadAllText(Path.Combine(Root, "UniDesk", "MainWindow.xaml.cs"));
        Assert.DoesNotContain("CompactValue", xaml);
        Assert.DoesNotContain("Value=\"156\"", xaml);
        Assert.DoesNotContain("CollapsedPanelHeight = 206", window);
        Assert.Contains("IsCompactEnabled", settings);
        Assert.Contains("CompactLayout", xaml);
    }
}
