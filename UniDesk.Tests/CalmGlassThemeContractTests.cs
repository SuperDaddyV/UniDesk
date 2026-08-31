using System.Collections;
using System.Text.RegularExpressions;
using System.Resources;
using UniDesk.Helpers;

namespace UniDesk.Tests;

public class CalmGlassThemeContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    private static readonly string[] SemanticBrushKeys =
    [
        "WindowSurfaceBrush",
        "SettingsSurfaceBrush",
        "SecondarySurfaceBrush",
        "CardSurfaceBrush",
        "CardHoverBrush",
        "ControlSurfaceBrush",
        "PrimaryTextBrush",
        "SecondaryTextBrush",
        "MutedTextBrush",
        "AccentBrush",
        "AccentForegroundBrush",
        "AccentSoftBrush",
        "FocusRingBrush",
        "DividerBrush",
        "SuccessBrush",
        "WarningBrush",
        "DangerBrush",
        "DangerForegroundBrush"
    ];

    [Fact]
    public void App_ShouldResolveCalmGlassTypographyAndThemeDictionaries()
    {
        var app = ReadProjectFile("UniDesk", "App.xaml");

        Assert.Contains("Resources/Themes/Shared.xaml", app, StringComparison.Ordinal);
        Assert.Contains("Resources/Themes/Dark.xaml", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DisplayFontFamily\"", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"BodyFontFamily\"", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DataFontFamily\"", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"CaptionFontFamily\"", app, StringComparison.Ordinal);
        Assert.Contains("Space Grotesk", app, StringComparison.Ordinal);
        Assert.Contains("JetBrains Mono", app, StringComparison.Ordinal);
        Assert.Contains("Microsoft YaHei UI", app, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light.xaml")]
    [InlineData("Dark.xaml")]
    public void ThemeDictionaries_ShouldDefineEverySemanticBrush(string fileName)
    {
        var theme = ReadProjectFile("UniDesk", "Resources", "Themes", fileName);

        foreach (var key in SemanticBrushKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", theme, StringComparison.Ordinal);
        }

        Assert.Contains("x:Key=\"PrimaryBackgroundBrush\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SecondaryBackgroundBrush\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ModuleBackgroundBrush\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"GlassHighlightBrush\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"AccentForegroundBrush\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DangerForegroundBrush\"", theme, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light.xaml", "#FF27272A", "#FFFFFFFF")]
    [InlineData("Dark.xaml", "#FF27272A", "#FF1E1E2E")]
    public void ThemeDictionaries_ShouldUseContrastingAccentAndDangerForegrounds(
        string fileName,
        string accentForeground,
        string dangerForeground)
    {
        var theme = ReadProjectFile("UniDesk", "Resources", "Themes", fileName);

        Assert.Contains(
            $"<Color x:Key=\"AccentForegroundColor\">{accentForeground}</Color>",
            theme,
            StringComparison.Ordinal);
        Assert.Contains(
            $"<Color x:Key=\"DangerForegroundColor\">{dangerForeground}</Color>",
            theme,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Fonts_ShouldBeEmbeddedWithOflNotices()
    {
        var project = ReadProjectFile("UniDesk", "UniDesk.csproj");
        var fontDirectory = Path.Combine(ProjectRoot, "UniDesk", "Resources", "Fonts");

        Assert.Contains("<Resource Include=\"Resources\\Fonts\\*.ttf\"", project, StringComparison.Ordinal);

        var fonts = Directory.GetFiles(fontDirectory, "*.ttf");
        Assert.NotEmpty(fonts);
        Assert.Contains(fonts, path => Path.GetFileName(path).Contains("SpaceGrotesk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fonts, path => Path.GetFileName(path).Contains("SpaceGrotesk-SemiBold", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fonts, path => Path.GetFileName(path).Contains("JetBrainsMono", StringComparison.OrdinalIgnoreCase));

        using var resourceStream = typeof(App).Assembly.GetManifestResourceStream("UniDesk.g.resources");
        Assert.NotNull(resourceStream);
        using var resourceReader = new ResourceReader(resourceStream!);
        var embeddedNames = resourceReader.Cast<DictionaryEntry>()
            .Select(entry => entry.Key.ToString())
            .Where(name => name != null)
            .ToArray();
        Assert.Contains("resources/fonts/spacegrotesk-regular.ttf", embeddedNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("resources/fonts/spacegrotesk-semibold.ttf", embeddedNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("resources/fonts/jetbrainsmono-regular.ttf", embeddedNames, StringComparer.OrdinalIgnoreCase);

        var notices = Directory.GetFiles(
            Path.Combine(ProjectRoot, "installer-assets", "licenses"),
            "*OFL*.txt");
        Assert.NotEmpty(notices);
        foreach (var notice in notices)
        {
            var contents = File.ReadAllText(notice);
            Assert.Contains("SIL Open Font License", contents, StringComparison.Ordinal);
            Assert.Contains("Version 1.1", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedTypography_ShouldProvideReadableTextTokens()
    {
        var shared = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");

        foreach (var key in new[]
                 {
                     "DisplayTextStyle",
                     "BodyTextStyle",
                     "DataTextStyle",
                     "CaptionTextStyle"
                 })
        {
            Assert.Contains($"x:Key=\"{key}\"", shared, StringComparison.Ordinal);
        }

        var implicitTextBlockStyle = ExtractImplicitTextBlockStyle(shared);
        Assert.Contains("BodyFontFamily", implicitTextBlockStyle, StringComparison.Ordinal);
        Assert.Contains("TextOptions.TextRenderingMode", implicitTextBlockStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"Grayscale\"", implicitTextBlockStyle, StringComparison.Ordinal);
        Assert.Contains("PrimaryTextBrush", implicitTextBlockStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Property=\"FontSize\"", implicitTextBlockStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Property=\"LineHeight\"", implicitTextBlockStyle, StringComparison.Ordinal);

        var bodyTextStyle = ExtractStyle(shared, "BodyTextStyle");
        Assert.Contains("Property=\"FontSize\" Value=\"13\"", bodyTextStyle, StringComparison.Ordinal);
        Assert.Contains("Property=\"LineHeight\" Value=\"20\"", bodyTextStyle, StringComparison.Ordinal);

        var dataTextStyle = ExtractStyle(shared, "DataTextStyle");
        Assert.Contains("Property=\"LineHeight\" Value=\"18\"", dataTextStyle, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedInteractiveStyles_ShouldDefineAllVisualStatesWithoutTransforms()
    {
        var shared = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");

        var buttonStyle = ExtractStyle(shared, "CalmButtonStyle");
        Assert.Contains("IsMouseOver", buttonStyle, StringComparison.Ordinal);
        Assert.Contains("IsPressed", buttonStyle, StringComparison.Ordinal);
        Assert.Contains("IsKeyboardFocused", buttonStyle, StringComparison.Ordinal);
        Assert.Contains("IsEnabled", buttonStyle, StringComparison.Ordinal);
        Assert.Contains("FocusRingBrush", buttonStyle, StringComparison.Ordinal);

        var textBoxStyle = ExtractStyle(shared, "CalmTextBoxStyle");
        Assert.Contains("IsMouseOver", textBoxStyle, StringComparison.Ordinal);
        Assert.Contains("IsKeyboardFocused", textBoxStyle, StringComparison.Ordinal);
        Assert.Contains("IsEnabled", textBoxStyle, StringComparison.Ordinal);
        Assert.Contains("FocusRingBrush", textBoxStyle, StringComparison.Ordinal);

        Assert.DoesNotContain("DropShadowEffect", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("ScaleTransform", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderTransform", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("#38FFFFFF", shared, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#0D2744", shared, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ColorSchemeCatalog_ShouldPreserveEightIdsAndOnlyUpdateAccentDerivedResources()
    {
        Assert.Equal(
            new[]
            {
                "Taro",
                "KleinBlueLight",
                "DustyRose",
                "MatchaGreen",
                "HollyGreen",
                "LightGrey",
                "DarkGrey",
                "Black"
            },
            AppColorSchemeCatalog.All.Select(scheme => scheme.Id));

        var catalogSource = ReadProjectFile("UniDesk", "Helpers", "AppColorSchemeCatalog.cs");
        Assert.Contains("AccentSoftColor", catalogSource, StringComparison.Ordinal);
        Assert.Contains("FocusRingColor", catalogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetColor(dictionary, \"PrimaryBackgroundColor\"", catalogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetColor(dictionary, \"SecondaryBackgroundColor\"", catalogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetColor(dictionary, \"ModuleBackgroundColor\"", catalogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetColor(dictionary, \"DividerColor\"", catalogSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeThemeSwitch_ShouldReplaceLightAndDarkDictionariesBeforeApplyingAccent()
    {
        var manager = ReadProjectFile("UniDesk", "Helpers", "AppThemeManager.cs");
        var app = ReadProjectFile("UniDesk", "App.xaml.cs");
        var settings = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var main = ReadProjectFile("UniDesk", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("Resources/Themes/Light.xaml", manager, StringComparison.Ordinal);
        Assert.Contains("Resources/Themes/Dark.xaml", manager, StringComparison.Ordinal);
        Assert.Contains("AppColorSchemeCatalog.Apply", manager, StringComparison.Ordinal);
        Assert.Contains("AppThemeManager.Apply", app, StringComparison.Ordinal);
        Assert.Contains("AppThemeManager.Apply", settings, StringComparison.Ordinal);
        Assert.Contains("systemThemeService: _systemThemeService", main, StringComparison.Ordinal);
        Assert.DoesNotContain("AppColorSchemeCatalog.Apply", main, StringComparison.Ordinal);
    }

    private static string ExtractStyle(string document, string key)
    {
        var match = Regex.Match(
            document,
            "<Style x:Key=\"" + Regex.Escape(key) + "\"[\\s\\S]*?</Style>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Missing style: {key}");
        return match.Value;
    }

    private static string ExtractImplicitTextBlockStyle(string document)
    {
        var match = Regex.Match(
            document,
            "<Style TargetType=\"TextBlock\">[\\s\\S]*?</Style>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Missing implicit TextBlock style");
        return match.Value;
    }

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
