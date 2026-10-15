using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace UniDesk.Tests;

public sealed class TimeWeatherAlignmentTests
{
    [Theory]
    [InlineData(290, 0.9, false, "2026年10月8日 星期四")]
    [InlineData(310, 1.0, false, "2026年10月8日 星期四")]
    [InlineData(490, 1.18, false, "2026年10月8日 星期四")]
    [InlineData(290, 1.18, false, "miércoles, 30 de septiembre de 2026")]
    [InlineData(290, 1.0, true, "2026年10月8日 星期四")]
    [InlineData(310, 1.18, true, "miércoles, 30 de septiembre de 2026")]
    [InlineData(266, 1.18, false, "2026/10/14 周三")]
    [InlineData(290, 1.18, true, "2026/10/14 周三")]
    public void ClockAndWeather_ShouldUseIndependentCompactRowsAcrossWidthsAndDisplayModes(
        double width, double fontScale, bool collapsed, string dateText)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var source = XDocument.Load(Path.Combine(directory.FullName, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var layout = new XElement(source.Root!.Element(wpf + "Border")!.Element(wpf + "Grid")!);
            // Retain production rows, columns, styles and bindings; isolate the clock/weather
            // portion from calendar, hardware and radar services.
            layout.Elements().Where(element => !element.Name.LocalName.Contains('.') &&
                (string?)element.Attribute(x + "Name") is not ("ClockHotspot" or "WeatherSummary" or "CompactWeatherSummary")).Remove();
            layout.Descendants().Attributes("MouseLeftButtonDown").Remove();
            layout.Descendants().Attributes("RequestNavigate").Remove();
            var windowXaml = XElement.Parse("""
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:helpers="clr-namespace:UniDesk.Helpers;assembly=UniDesk">
                    <Window.Resources>
                        <ResourceDictionary>
                            <ResourceDictionary.MergedDictionaries>
                                <ResourceDictionary Source="pack://application:,,,/UniDesk;component/Resources/Themes/Shared.xaml"/>
                                <ResourceDictionary Source="pack://application:,,,/UniDesk;component/Resources/Themes/Dark.xaml"/>
                            </ResourceDictionary.MergedDictionaries>
                            <FontFamily x:Key="BodyFontFamily">pack://application:,,,/UniDesk;component/Resources/Fonts/#Inter, Microsoft YaHei UI</FontFamily>
                            <FontFamily x:Key="QWeatherIconsFont">Segoe UI</FontFamily>
                            <helpers:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
                            <helpers:InverseBooleanToVisibilityConverter x:Key="InverseBooleanToVisibilityConverter"/>
                            <helpers:StringNotEmptyToVisibilityConverter x:Key="StringNotEmptyToVisibilityConverter"/>
                        </ResourceDictionary>
                    </Window.Resources>
                </Window>
                """);
            windowXaml.Add(layout);
            var window = (Window)XamlReader.Parse(windowXaml.ToString());
            window.DataContext = new { FontScale = fontScale, IsPanelCollapsed = collapsed,
                CompactLayout = new UniDesk.Models.CompactPanelLayout(true, true, true, true) };
            var host = (Grid)window.Content;
            System.Windows.Documents.TextElement.SetFontFamily(host, (FontFamily)window.Resources["BodyFontFamily"]);
            host.DataContext = new
            {
                ClockTimeText = "23:56", ClockDateText = dateText, ClockShortDateText = dateText, ClockLunarText = "丙午年 八月 廿八",
                HasWeatherData = true, UseWeatherIconImage = false, WeatherIconGlyph = "☀",
                WeatherIconForeground = Brushes.White, WeatherTemperature = "-18°C",
                WeatherDescription = collapsed ? "多云" : "Intermittent sunshine",
                WeatherDetailLine = "空气良 38 | 湿度 52%", WeatherCity = "福州",
                WeatherRangeLine = "-18°C ~ -10°C", WeatherAirQualityAttributionText = "Synthetic source",
                CompactWeatherLocationLine = "福州 · 多云", CompactWeatherDetailLine = "空气良 · 湿度52%"
            };
            try
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                for (var pass = 0; pass < 3; pass++)
                {
                    host.Measure(new Size(width, double.PositiveInfinity));
                    host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
                    host.UpdateLayout();
                }
                var blocks = Descendants(host).OfType<TextBlock>().ToArray();
                var date = blocks.Single(block => block.Text == dateText);
                var lunar = blocks.Single(block => block.Text == "丙午年 八月 廿八");
                var detail = blocks.Single(block => block.Text == "空气良 38 | 湿度 52%");
                var location = (Grid)host.FindName("WeatherLocationStrip");
                Assert.Equal(TextWrapping.NoWrap, date.TextWrapping);
                // SecondaryTextStyle supplies a fixed line box independently of FontScale.
                var singleLine = new TextBlock
                {
                    Text = date.Text, FontFamily = date.FontFamily, FontSize = date.FontSize,
                    FontWeight = date.FontWeight, LineHeight = date.LineHeight,
                    LineStackingStrategy = date.LineStackingStrategy, TextWrapping = TextWrapping.NoWrap
                };
                singleLine.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.InRange(Math.Abs(date.ActualHeight - singleLine.DesiredSize.Height), 0, 0.5);
                Assert.Equal(dateText, date.ToolTip);
                var clockGrid = (Grid)((Border)host.FindName("ClockHotspot")).Child;
                Assert.All(clockGrid.RowDefinitions, row => Assert.Null(row.SharedSizeGroup));
                if (collapsed)
                {
                    Assert.Equal(Visibility.Collapsed, detail.Visibility);
                    var compact = (Grid)host.FindName("CompactWeatherSummary");
                    Assert.Equal(Visibility.Visible, compact.Visibility);
                    var environment = blocks.Single(block => block.Text == "空气良 · 湿度52%");
                    Assert.Equal(Visibility.Visible, environment.Visibility);
                    Assert.InRange(Bounds(environment, compact).Bottom, 30, 80);
                }
                var clock = (Border)host.FindName("ClockHotspot");
                var weather = (Grid)host.FindName(collapsed ? "CompactWeatherSummary" : "WeatherSummary");
                Assert.True(Bounds(clock, host).Right <= Bounds(weather, host).Left,
                    "Clock and weather columns must not overlap.");
                foreach (var block in blocks.Where(block => IsDisplayed(block, host)))
                {
                    var bounds = Bounds(block, host);
                    Assert.True(bounds.Left >= -0.5 && bounds.Right <= width + 0.5, $"{block.Text}: {bounds}");
                }
            }
            finally { window.Close(); }
        });
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static bool IsDisplayed(DependencyObject element, DependencyObject root)
    {
        while (element != root)
        {
            if (element is UIElement { Visibility: not Visibility.Visible }) return false;
            element = VisualTreeHelper.GetParent(element);
            if (element == null) return false;
        }
        return true;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
