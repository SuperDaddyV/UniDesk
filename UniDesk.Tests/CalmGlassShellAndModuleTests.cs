namespace UniDesk.Tests;

public class CalmGlassShellAndModuleTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void SettingsWindow_ShouldUseCalmGlassTypographyAndIndependentReadableSurface()
    {
        var settingsXaml = ReadProjectFile("UniDesk", "SettingsWindow.xaml");

        Assert.Contains("FontFamily=\"{DynamicResource BodyFontFamily}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("TextOptions.TextRenderingMode=\"Grayscale\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsGlassBackground\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource SettingsSurfaceBrush}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Opacity=\"1\"", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UniDesk Glass 2.0", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"UniDesk Glass\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{DynamicResource DisplayFontFamily}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource AccentForegroundBrush}\"", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#0D2744", settingsXaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalendarPopup_ShouldUseSemanticThemeResources()
    {
        var calendarXaml = ReadProjectFile("UniDesk", "Controls", "TimeWeatherModuleView.xaml");

        Assert.Contains("Background=\"{DynamicResource WindowSurfaceBrush}\"", calendarXaml, StringComparison.Ordinal);
        Assert.Contains("BorderBrush=\"{DynamicResource DividerBrush}\"", calendarXaml, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource AccentSoftBrush}", calendarXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#E6FFFFFF", calendarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#E2E8F0", calendarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#1E293B", calendarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#64748B", calendarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#1A3B82F6", calendarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#3B82F6", calendarXaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TodoStates_ShouldUseSemanticThemeResources()
    {
        var todosXaml = ReadProjectFile("UniDesk", "Controls", "TodosModuleView.xaml");
        var swipeXaml = ReadProjectFile("UniDesk", "Controls", "TodoSwipeRow.xaml");
        var priorityConverter = ReadProjectFile("UniDesk", "Helpers", "TodoPriorityToBrushConverter.cs");

        Assert.Contains("{DynamicResource MutedTextBrush}", todosXaml, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource WarningBrush}", todosXaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource DangerBrush}\"", swipeXaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource DangerForegroundBrush}\"", swipeXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#D5CCEA", todosXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#BFAFE0", todosXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#5DB7FF", todosXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#FF7E45", todosXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#E53935", swipeXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreground=\"White\"", swipeXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MutedTextBrush", priorityConverter, StringComparison.Ordinal);
        Assert.Contains("WarningBrush", priorityConverter, StringComparison.Ordinal);
        Assert.Contains("DangerBrush", priorityConverter, StringComparison.Ordinal);
        Assert.DoesNotContain("#94A3B8", priorityConverter, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#F59E0B", priorityConverter, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#EF4444", priorityConverter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DataDenseModules_ShouldUseTheSharedDataFont()
    {
        var hardwareXaml = ReadProjectFile("UniDesk", "Controls", "HardwareMonitorModuleView.xaml");
        var modelRadarXaml = ReadProjectFile("UniDesk", "Controls", "ModelRadarModuleView.xaml");

        Assert.Contains("FontFamily=\"{DynamicResource DataFontFamily}\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{DynamicResource DataFontFamily}\"", modelRadarXaml, StringComparison.Ordinal);

        Assert.Contains("TextOptions.TextRenderingMode=\"Grayscale\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(hardwareXaml, "Width=\"78\""));
    }

    private static int CountOccurrences(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the UniDesk repository root.");
    }
}
