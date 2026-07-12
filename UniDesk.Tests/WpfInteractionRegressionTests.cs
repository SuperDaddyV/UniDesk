using System.Text.RegularExpressions;

namespace UniDesk.Tests;

public class WpfInteractionRegressionTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void QuickNoteEditor_ShouldDeferCloseAndUseDoneLabel()
    {
        var windowXaml = ReadProjectFile("UniDesk", "QuickNoteEditorWindow.xaml");
        var windowCode = ReadProjectFile("UniDesk", "QuickNoteEditorWindow.xaml.cs");

        Assert.Contains("Content=\"{DynamicResource Common.Done}\"", windowXaml, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke", windowCode, StringComparison.Ordinal);
        Assert.Contains("if (!await _viewModel.FlushAndCleanupAsync())", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await _viewModel.FlushAndCleanupAsync();\n        Close();",
            windowCode.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TodoCompletionCircle_ShouldUseControlClickHandler()
    {
        var viewXaml = ReadProjectFile("UniDesk", "Controls", "TodosModuleView.xaml");
        var viewCode = ReadProjectFile("UniDesk", "Controls", "TodosModuleView.xaml.cs");

        Assert.Contains("MouseLeftButtonUp=\"TodoCheck_OnMouseLeftButtonUp\"", viewXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Ellipse.InputBindings>", viewXaml, StringComparison.Ordinal);
        Assert.Contains("ToggleTodoCommand.Execute", viewCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_ShouldUseIndependentSevenPageGlassLayout()
    {
        var settingsXaml = ReadProjectFile("UniDesk", "SettingsWindow.xaml");

        Assert.Contains("Width=\"720\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"620\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"680\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"560\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsNavigation\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsPages\"", settingsXaml, StringComparison.Ordinal);
        Assert.Equal(7, Regex.Matches(settingsXaml, "<TabItem").Count);
        Assert.DoesNotContain("x:Key=\"DlgBackground\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource GlassWindowBorderStyle}\"", settingsXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ShouldApplyOpacityOnlyToGlassBackground()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var normalized = mainXaml.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"MainGlassBackground\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Opacity=\"{Binding WindowOpacity}\"", mainXaml, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            "x:Name=\"WindowContainer\"[^>]*Opacity=",
            normalized);
    }

    [Fact]
    public void LayeredGlassWindows_ShouldNotRequestRectangularDwmBackdrop()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var settingsXaml = ReadProjectFile("UniDesk", "SettingsWindow.xaml");
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");
        var settingsCode = ReadProjectFile("UniDesk", "SettingsWindow.xaml.cs");

        Assert.Contains("AllowsTransparency=\"True\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("AllowsTransparency=\"True\"", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BackdropMaterialService.Apply", mainCode, StringComparison.Ordinal);
        Assert.DoesNotContain("BackdropMaterialService.Apply", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void GlassComboBox_ShouldUseReadableSelectablePopupTemplate()
    {
        var sharedTheme = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");

        Assert.Contains("x:Key=\"GlassComboBoxItemStyle\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("<Popup x:Name=\"PART_Popup\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{TemplateBinding IsDropDownOpen}\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("<ItemsPresenter", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("Property=\"ItemContainerStyle\" Value=\"{StaticResource GlassComboBoxItemStyle}\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource PrimaryBackgroundBrush}\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource PrimaryTextBrush}\"", sharedTheme, StringComparison.Ordinal);
        Assert.DoesNotContain("StaysOpen=\"False\"", sharedTheme, StringComparison.Ordinal);
    }

    [Fact]
    public void HardwareNetworkRow_ShouldUseStableTransparentTextRendering()
    {
        var hardwareXaml = ReadProjectFile("UniDesk", "Controls", "HardwareMonitorModuleView.xaml");

        Assert.Contains("TextOptions.TextRenderingMode=\"Grayscale\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NetworkReceivedValueText\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NetworkSentValueText\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(hardwareXaml, "Width=\"78\"").Count);
        Assert.Equal(2, Regex.Matches(hardwareXaml, "TextAlignment=\"Left\"").Count);
    }

    [Fact]
    public void SettingsSidebar_ShouldBlendIntoWindowGlass()
    {
        var sharedTheme = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");
        var styleStart = sharedTheme.IndexOf("x:Key=\"GlassSidebarStyle\"", StringComparison.Ordinal);
        var styleEnd = sharedTheme.IndexOf("</Style>", styleStart, StringComparison.Ordinal);
        var sidebarStyle = sharedTheme[styleStart..styleEnd];

        Assert.Contains("Property=\"Background\" Value=\"Transparent\"", sidebarStyle, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"0\"", sidebarStyle, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"12\"", sidebarStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("SecondaryBackgroundBrush", sidebarStyle, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsScrollDrag_ShouldNotInterceptComboBoxItems()
    {
        var settingsCode = ReadProjectFile("UniDesk", "SettingsWindow.xaml.cs");

        Assert.Contains("or ComboBoxItem", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void AppearanceSettings_ShouldHideManualPaletteWhileFollowingSystemTheme()
    {
        var appearanceXaml = ReadProjectFile("UniDesk", "Controls", "Settings", "AppearanceSettingsPage.xaml");

        Assert.Contains("x:Name=\"ManualColorSchemePalette\"", appearanceXaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding FollowSystemTheme, Converter={StaticResource InverseBooleanToVisibilityConverter}}\"",
            appearanceXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsNavigation_ShouldBeLocalizedInEveryLanguage()
    {
        var keys = new[]
        {
            "Settings.NavGeneral",
            "Settings.NavAppearance",
            "Settings.NavModules",
            "Settings.NavDesktop",
            "Settings.NavData",
            "Settings.NavShortcuts",
            "Settings.NavAbout"
        };

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            foreach (var key in keys)
            {
                Assert.Contains($"x:Key=\"{key}\"", resources, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void MainWindow_ShouldExposeGlassGlobalSearchWorkspace()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"SearchButton\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SearchSurface\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GlobalSearchBox\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Search.ActivateResultCommand", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Key.F", mainCode, StringComparison.Ordinal);
        Assert.Contains("TodoSearchResultActivated", mainCode, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));
}
