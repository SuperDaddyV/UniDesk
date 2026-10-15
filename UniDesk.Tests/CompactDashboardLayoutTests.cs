using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using UniDesk.Models;

namespace UniDesk.Tests;

public sealed class CompactDashboardLayoutTests
{
    public static IEnumerable<object[]> Combinations =>
        from mask in Enumerable.Range(0, 16)
        from width in new[] { 288d, 308d, 488d }
        select new object[] { mask, width };

    [Theory]
    [MemberData(nameof(Combinations))]
    public void CompactModules_ShouldCollapseEntireSlots_AndFitTheirContent(int mask, double width)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var layout = new CompactPanelLayout((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
            var window = CreateWindow();
            window.DataContext = new
            {
                IsPanelCollapsed = true, FontScale = 1.18, CompactLayout = layout,
                HardwareMonitor = new { SystemCpuUsageText = "18%", SystemMemoryUsageText = "59%", SystemCpuTemperatureValueText = "68°C", SystemGpuTemperatureValueText = "49°C" },
                ModelRadar = new { HasCompactRecommendations = true, CompactOverallLabelText = "综合最佳", CompactOverallModelText = "GPT6-Astra/XHigh", CompactOverallScoreText = "88.80", CompactToolTipText = "fixture", CompactStatusText = "" },
                Todos = new { HasCollapsedPanelTodo = true, CollapsedPanelTodo = new TodoItem { Title = "A long task title which should remain on one line" }, CollapsedPanelTodoDueText = "6/15" }
            };
            var host = (Border)window.Content;
            host.DataContext = new
            {
                ClockTimeText = "15:18", ClockDateText = "2026年10月15日 星期四", ClockShortDateText = "2026/10/15 周四", ClockLunarText = "丙午年 九月初六",
                HasWeatherData = true, UseWeatherIconImage = false, WeatherIconGlyph = "☀", WeatherIconForeground = Brushes.White,
                WeatherTemperature = "−18°C", WeatherDescription = "多云", WeatherDetailLine = "空气优 | 湿度52%", WeatherCity = "福州",
                WeatherRangeLine = "−20° ~ −10°", WeatherAirQualityAttributionText = "", CompactWeatherLocationLine = "福州 · 多云", CompactWeatherDetailLine = "空气优 · 湿度52%"
            };
            try
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                host.Measure(new Size(width, double.PositiveInfinity));
                host.Arrange(new Rect(0, 0, width, layout.ContentHeight));
                host.UpdateLayout();
                FrameworkElement Find(string name) => (FrameworkElement)window.FindName(name);
                Assert.Equal(layout.ShowTimeWeather ? Visibility.Visible : Visibility.Collapsed, Find("ClockHotspot").Visibility);
                Assert.Equal(layout.ShowTimeWeather ? Visibility.Visible : Visibility.Collapsed, Find("CompactWeatherSummary").Visibility);
                Assert.Equal(layout.ShowHardware ? Visibility.Visible : Visibility.Collapsed, Find("CompactHardwareSummary").Visibility);
                Assert.Equal(layout.ShowModelRadar ? Visibility.Visible : Visibility.Collapsed, Find("CompactModelRadarSummary").Visibility);
                Assert.Equal(layout.ShowTodos ? Visibility.Visible : Visibility.Collapsed, Find("CompactTodoStrip").Visibility);
                Assert.Equal(layout.HasStatus ? Visibility.Visible : Visibility.Collapsed, Find("CompactStatusStrip").Visibility);
                Assert.InRange(Math.Abs(host.DesiredSize.Height - layout.ContentHeight), 0, 0.5);
                if (layout.HasStatus)
                    Assert.InRange(Math.Abs(Find("CompactStatusStrip").ActualWidth - width), 0, 0.5);
                if (layout.ShowTodos) Assert.True(Find("CompactTodoSummary").ActualWidth >= width - 17,
                    "The todo text must retain the entire available width when the top modules are disabled.");
                if (mask == 0) Assert.Equal(48, layout.WindowHeight);
                if (mask == 8) Assert.Equal(76, layout.WindowHeight);
                if (mask == 15) Assert.Equal(204, layout.WindowHeight);

                foreach (var element in new[] { "ClockHotspot", "CompactHardwareSummary", "CompactWeatherSummary", "CompactModelRadarSummary", "CompactTodoStrip" }.Select(Find).Where(element => element.Visibility == Visibility.Visible))
                {
                    var bounds = Bounds(element, host);
                    Assert.True(bounds.Left >= -0.5 && bounds.Right <= width + 0.5 && bounds.Bottom <= layout.ContentHeight + 0.5, $"{element.Name}: {bounds}");
                }
                if (layout.ShowTimeWeather)
                {
                    var clock = (Border)Find("ClockHotspot");
                    var blocks = Descendants(clock).OfType<TextBlock>().ToArray();
                    var time = blocks.Single(block => block.Text == "15:18");
                    var timeBounds = Bounds(time, clock);
                    Assert.InRange(Math.Abs(timeBounds.Left + timeBounds.Width / 2 - clock.ActualWidth / 2), 0, 0.5);
                    Assert.Equal(TextAlignment.Center, blocks.Single(block => block.Text == "2026/10/15 周四").TextAlignment);
                    Assert.Equal(TextAlignment.Center, blocks.Single(block => block.Text == "丙午年 九月初六").TextAlignment);
                    Assert.True(Bounds(clock, host).Right <= Bounds(Find("CompactWeatherSummary"), host).Left);
                    var weather = Find("CompactWeatherSummary");
                    var temperature = Descendants(weather).OfType<TextBlock>().Single(block => block.Text == "−18°C");
                    var temperatureBounds = Bounds(temperature, weather);
                    Assert.True(temperatureBounds.Left >= -0.5 && temperatureBounds.Right <= weather.ActualWidth + 0.5, $"Temperature clips at {width}: {temperatureBounds}");
                }
                var divider = (Border)Find("CompactTodoStrip");
                Assert.Equal(layout.ShowModelRadar ? 1 : 0, divider.BorderThickness.Top);
            }
            finally { window.Close(); }
        });
    }

    internal static Window CreateWindow()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var document = XDocument.Load(Path.Combine(root, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
        var content = new XElement(document.Root!.Elements().Single());
        content.Descendants().Where(element => element.Name.LocalName == "Popup").Remove();
        content.Descendants().Attributes("MouseLeftButtonDown").Remove();
        var window = XElement.Parse("""
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
                        <helpers:TodoPriorityToBrushConverter x:Key="TodoPriorityBrush"/>
                    </ResourceDictionary>
                </Window.Resources>
            </Window>
            """);
        window.Add(content);
        return (Window)XamlReader.Parse(window.ToString());
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

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
