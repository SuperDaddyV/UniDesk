using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace UniDesk.Tests;

public sealed class CalmGlassReadabilityTests
{
    private static readonly string ProjectRoot = FindProjectRoot();
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Dark.xaml")]
    [InlineData("Light.xaml")]
    public void ApplicationToolTips_ShouldUseOpaqueHighContrastColorsAndWrap(string themeFile)
    {
        var theme = XDocument.Load(Path.Combine(ProjectRoot, "UniDesk", "Resources", "Themes", themeFile));
        var background = ReadColor(theme, "ToolTipSurfaceColor");
        var foreground = ReadColor(theme, "ToolTipTextColor");
        Assert.Equal(255, background.A);
        Assert.Equal(255, foreground.A);
        Assert.True(Contrast(background, foreground) >= 7,
            $"Tooltip contrast in {themeFile} is only {Contrast(background, foreground):F2}:1.");

        var shared = XDocument.Load(Path.Combine(ProjectRoot, "UniDesk", "Resources", "Themes", "Shared.xaml"));
        var style = shared.Root!.Elements(Wpf + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == "ToolTip" &&
                               element.Attribute(Xaml + "Key") == null);
        var setters = style.Elements(Wpf + "Setter").ToDictionary(
            element => (string?)element.Attribute("Property") ?? string.Empty,
            element => (string?)element.Attribute("Value") ?? string.Empty);
        Assert.Equal("{DynamicResource ToolTipSurfaceBrush}", setters["Background"]);
        Assert.Equal("{DynamicResource ToolTipTextBrush}", setters["Foreground"]);
        Assert.InRange(double.Parse(setters["MaxWidth"], CultureInfo.InvariantCulture), 250, 340);
        Assert.Contains(style.Descendants(Wpf + "TextBlock"),
            block => (string?)block.Attribute("TextWrapping") == "Wrap");
    }

    [Theory]
    [InlineData("Dark.xaml")]
    [InlineData("Light.xaml")]
    public void ApplicationToolTip_ShouldRenderWithReadableForegroundAndWrappedText(string themeFile)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var toolTip = new ToolTip
            {
                Content = "综合得分：95.3 后端得分：95.0 前端得分：96.0 知识得分：95.0 耗时：1,824,477ms 评测参考费用：USD 3.3454"
            };
            toolTip.Resources["BodyFontFamily"] = new FontFamily("Segoe UI");
            toolTip.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/UniDesk;component/Resources/Themes/{themeFile}")
            });
            var shared = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/UniDesk;component/Resources/Themes/Shared.xaml")
            };
            toolTip.Resources.MergedDictionaries.Add(shared);
            toolTip.Style = Assert.IsType<Style>(shared[typeof(ToolTip)]);
            toolTip.Measure(new Size(400, double.PositiveInfinity));
            toolTip.Arrange(new Rect(0, 0, toolTip.DesiredSize.Width, toolTip.DesiredSize.Height));
            toolTip.UpdateLayout();

            var border = FindDescendants(toolTip).OfType<Border>().Single();
            var text = FindDescendants(toolTip).OfType<TextBlock>().Single();
            Assert.Equal(255, Assert.IsType<SolidColorBrush>(border.Background).Color.A);
            Assert.Equal(255, Assert.IsType<SolidColorBrush>(text.Foreground).Color.A);
            Assert.True(toolTip.ActualWidth <= 320.5);
            Assert.True(text.ActualHeight >= text.FontSize * 2, "Tooltip details should wrap within their maximum width.");
        });
    }

    [Fact]
    public void PanelOpacity_ShouldAffectOnlyGlassBackgroundAndPreserveBinding()
    {
        var main = XDocument.Load(Path.Combine(ProjectRoot, "UniDesk", "MainWindow.xaml"));
        var background = main.Descendants(Wpf + "Border")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "MainGlassBackground");
        Assert.Equal("{Binding WindowOpacity}", (string?)background.Attribute("Opacity"));

        var service = File.ReadAllText(Path.Combine(ProjectRoot, "UniDesk", "Services", "WindowService.cs"));
        var start = service.IndexOf("public void SetOpacity(double opacity)", StringComparison.Ordinal);
        var end = service.IndexOf("public double GetCurrentWidth()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = service[start..end];
        Assert.DoesNotContain("WindowContainer", method, StringComparison.Ordinal);
        Assert.Contains("MainGlassBackground", method, StringComparison.Ordinal);
        Assert.Contains("SetCurrentValue", method, StringComparison.Ordinal);
    }

    [Fact]
    public void WeatherAttribution_ShouldStaySmallerThanCityWithOriginalIcon()
    {
        var weather = XDocument.Load(Path.Combine(ProjectRoot, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
        var link = weather.Descendants(Wpf + "Hyperlink")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "QWeatherAttributionLink");
        var label = Assert.IsType<XElement>(link.Parent);
        var style = label.Element(Wpf + "TextBlock.Style")!.Element(Wpf + "Style")!;
        var normalFont = style.Elements(Wpf + "Setter")
            .Single(element => (string?)element.Attribute("Property") == "FontSize");
        Assert.Contains("ConverterParameter=10", (string?)normalFont.Attribute("Value"), StringComparison.Ordinal);
        var collapsedFont = style.Descendants(Wpf + "DataTrigger")
            .Single(element => (string?)element.Attribute("Value") == "True")
            .Elements(Wpf + "Setter")
            .Single(element => (string?)element.Attribute("Property") == "FontSize");
        Assert.Equal("8", (string?)collapsedFont.Attribute("Value"));
        Assert.Equal("https://www.qweather.com", (string?)link.Attribute("NavigateUri"));
        var icon = link.Elements(Wpf + "Run").First();
        Assert.Equal("8", (string?)icon.Attribute("FontSize"));
        Assert.Equal("\uE707", (string?)icon.Attribute("Text"));
    }

    [Fact]
    public void AirQualityAttribution_ShouldNotOccupyCardRowButRemainOnDetailToolTip()
    {
        var weather = XDocument.Load(Path.Combine(ProjectRoot, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
        Assert.DoesNotContain(weather.Descendants(Wpf + "TextBlock"),
            element => (string?)element.Attribute(Xaml + "Name") == "AirQualityAttribution");
        var detail = weather.Descendants(Wpf + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{Binding WeatherDetailLine}");
        var detailStyle = detail.Element(Wpf + "TextBlock.Style")!.Element(Wpf + "Style")!;
        var tooltip = detailStyle.Elements(Wpf + "Setter")
            .Single(element => (string?)element.Attribute("Property") == "ToolTip");
        Assert.Equal("{Binding WeatherAirQualityAttributionText}", (string?)tooltip.Attribute("Value"));
    }

    private static (byte A, byte R, byte G, byte B) ReadColor(XDocument document, string key)
    {
        var value = document.Root!.Elements(Wpf + "Color")
            .Single(element => (string?)element.Attribute(Xaml + "Key") == key).Value;
        Assert.Matches("^#[0-9A-Fa-f]{8}$", value);
        return (
            byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double Contrast((byte A, byte R, byte G, byte B) first, (byte A, byte R, byte G, byte B) second)
    {
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        static double Luminance((byte A, byte R, byte G, byte B) color) =>
            0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the UniDesk repository root.");
    }

    private static IEnumerable<DependencyObject> FindDescendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in FindDescendants(child)) yield return descendant;
        }
    }
}
