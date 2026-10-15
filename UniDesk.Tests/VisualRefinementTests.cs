using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using UniDesk.Models;

namespace UniDesk.Tests;

public sealed class VisualRefinementTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(240, 0.9)]
    [InlineData(240, 1.18)]
    [InlineData(420, 1.18)]
    public void LongTodo_ShouldStopAtTwoLines_AndKeepDateBelow(double width, double scale)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = Read("TodosModuleView.xaml").Descendants().Single(element => element.Name.LocalName == "Grid" &&
                element.Elements().Any(child => (string?)child.Attribute("Tag") == "TodoCheck"));
            var todo = new TodoItem { Title = string.Concat(Enumerable.Repeat("这是一条包含多段内容的长任务。", 15)), DueDate = new DateTime(2026, 10, 16) };
            var window = Region(source, todo, scale);
            try
            {
                Layout(window, width);
                var title = Descendants(window.Content).OfType<TextBlock>().Single(block => block.Text == todo.Title);
                var due = Descendants(window.Content).OfType<TextBlock>().Single(block => Grid.GetRow(block) == 1);
                Assert.InRange(title.ActualHeight, 24 * scale, 32 * scale + 0.5);
                Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
                var titleBounds = Bounds(title, (Visual)window.Content);
                var dueBounds = Bounds(due, (Visual)window.Content);
                Assert.True(dueBounds.Top >= titleBounds.Bottom + 2, $"Date must be separated from the capped title: {titleBounds} / {dueBounds}");
                Assert.InRange(Math.Abs(titleBounds.Left - dueBounds.Left), 0, 0.5);
                Assert.Equal(todo.Title, title.ToolTip);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(288)]
    [InlineData(308)]
    [InlineData(488)]
    public void CompactRadar_ShouldKeepModelEffortAndScoreReadableOnOneLine(double width)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var window = CompactDashboardLayoutTests.CreateWindow();
            window.DataContext = new
            {
                IsPanelCollapsed = true, FontScale = 1.18, CompactLayout = new CompactPanelLayout(false, false, true, false),
                ModelRadar = new
                {
                    HasCompactRecommendations = true, CompactOverallLabelText = "综合最高",
                    CompactOverallModelText = "Claude-Sonnet-5-5/XHigh", CompactOverallModelNameText = "Claude-Sonnet-5-5",
                    CompactOverallEffortText = "XHigh", CompactOverallScoreText = "82.30", CompactToolTipText = "fixture", CompactStatusText = ""
                }
            };
            try
            {
                Layout(window, width);
                var summary = (FrameworkElement)window.FindName("CompactModelRadarSummary");
                var blocks = Descendants(summary).OfType<TextBlock>().ToArray();
                var model = Assert.Single(blocks, block => block.Text == "Claude-Sonnet-5-5");
                var effort = Assert.Single(blocks, block => block.Text == "XHigh");
                var score = Assert.Single(blocks, block => block.Text == "82.30");
                foreach (var block in new[] { model, effort, score })
                {
                    var natural = new FormattedText(block.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, Brushes.White, 1);
                    Assert.True(block.ActualWidth + 0.5 >= natural.WidthIncludingTrailingWhitespace, $"{block.Text} is truncated at {width} DIP.");
                    Assert.True(Bounds(block, summary).Bottom <= 24.5);
                }
                Assert.True(Bounds(model, summary).Right <= Bounds(effort, summary).Left);
                Assert.True(Bounds(effort, summary).Right <= Bounds(score, summary).Left);
                var group = (FrameworkElement)window.FindName("CompactModelRadarOverallRecommendation");
                var groupBounds = Bounds(group, summary);
                Assert.InRange(Math.Abs(groupBounds.Left + groupBounds.Width / 2 - summary.ActualWidth / 2), 0, 0.5);
                Assert.InRange(Bounds(effort, summary).Left - Bounds(model, summary).Right, 0, 8);
                Assert.InRange(group.ActualWidth, 0, summary.ActualWidth);
                Assert.Equal(24, summary.ActualHeight);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CompactHardware_ShouldShowOnlyUsage_AtOriginalWidth(bool showTimeWeather, bool missingGpu)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var window = CompactDashboardLayoutTests.CreateWindow();
            window.DataContext = new
            {
                IsPanelCollapsed = true, FontScale = 1.18, CompactLayout = new CompactPanelLayout(showTimeWeather, true, false, false),
                HardwareMonitor = new { SystemCpuUsageText = "100%", SystemCpuUsageToolTip = "CPU usage", SystemCpuTemperatureValueText = "100℃", SystemMemoryUsageText = "97%", SystemGpuUsageText = missingGpu ? "--%" : "99%", SystemGpuUsageToolTip = "GPU usage", SystemGpuTemperatureValueText = "101℃" }
            };
            try
            {
                Layout(window, 288);
                var group = (FrameworkElement)window.FindName("CompactHardwareSummary");
                var blocks = Descendants(group).OfType<TextBlock>().ToArray();
                var cpu = Assert.Single(blocks, block => block.Text == "CPU");
                var gpu = Assert.Single(blocks, block => block.Text == "GPU");
                var mem = Assert.Single(blocks, block => block.Text == "MEM");
                var values = new[] { "100%", missingGpu ? "--%" : "99%", "97%" }
                    .Select(text => Assert.Single(blocks, block => block.Text == text)).ToArray();
                Assert.Equal(50, group.ActualWidth);
                Assert.Equal(48, group.ActualHeight);
                Assert.Equal(64, new CompactPanelLayout(true, true, false, false).HardwareColumn.Value);
                Assert.Equal(6, blocks.Length);
                Assert.DoesNotContain(blocks, block => block.Text.Contains('℃'));
                Assert.Equal("CPU usage", values[0].ToolTip);
                Assert.Equal("GPU usage", values[1].ToolTip);
                foreach (var (label, usage, row) in new[] { (cpu, values[0], 0), (gpu, values[1], 1), (mem, values[2], 2) })
                {
                    Assert.Equal(row, Grid.GetRow(label));
                    Assert.Equal(row, Grid.GetRow(usage));
                    Assert.Equal(1, Grid.GetRowSpan(label));
                    Assert.Equal(0, Grid.GetColumn(label));
                    Assert.Equal(1, Grid.GetColumn(usage));
                    Assert.True(Bounds(label, group).Right < Bounds(usage, group).Left);
                    Assert.InRange(Bounds(usage, group).Left - Bounds(label, group).Right, 0, 18);
                }
                Assert.All(blocks, block =>
                {
                    var bounds = Bounds(block, group);
                    Assert.True(bounds.Left >= -0.5 && bounds.Right <= group.ActualWidth + 0.5 && bounds.Bottom <= group.ActualHeight + 0.5, $"{block.Text} escapes the hardware group: {bounds}");
                    var natural = new FormattedText(block.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, Brushes.White, 1);
                    Assert.True(block.ActualWidth + 0.5 >= natural.WidthIncludingTrailingWhitespace, $"{block.Text} is clipped.");
                });
                Assert.All(values, value => Assert.InRange(Math.Abs(Bounds(value, group).Right - Bounds(values[0], group).Right), 0, 0.5));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void QuickTextSelection_ShouldRemainDistinctWhenSwitchingModes()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = Read("QuickTextModuleView.xaml").Descendants().Single(element => element.Name.LocalName == "Grid" &&
                element.Elements().Count(child => (string?)child.Attribute("Command") == "{Binding SelectQuickTextModeCommand}") == 2);
            var window = Region(source, new { IsQuickTextHistorySelected = true, IsQuickTextSnippetsSelected = false }, 1.18);
            try
            {
                foreach (var history in new[] { true, false })
                {
                    ((FrameworkElement)window.Content).DataContext = new { IsQuickTextHistorySelected = history, IsQuickTextSnippetsSelected = !history };
                    Layout(window, 260);
                    var buttons = Descendants(window.Content).OfType<Button>().ToArray();
                    var active = buttons[history ? 0 : 1];
                    var inactive = buttons[history ? 1 : 0];
                    Assert.Equal(inactive.FontWeight, active.FontWeight);
                    Assert.Equal(new Thickness(0), active.BorderThickness);
                    Assert.Equal(((SolidColorBrush)window.FindResource("GlassHighlightBrush")).Color, ((SolidColorBrush)active.Background).Color);
                    Assert.NotEqual(((SolidColorBrush)inactive.Background).Color, ((SolidColorBrush)active.Background).Color);
                    Assert.Equal(inactive.ActualHeight, active.ActualHeight);
                }
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void QuickTextItems_ShouldAlignLeft_AndRemainVerticallyCentered(bool snippet, bool pinned, bool longText)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var templates = Read("QuickTextModuleView.xaml").Descendants()
                .Where(element => element.Name.LocalName == "DataTemplate").ToArray();
            var source = templates[snippet ? 1 : 0].Elements().Single();
            var summary = longText ? string.Concat(Enumerable.Repeat("这是较长的剪贴内容。", 8)) : "剪贴内容";
            var title = longText ? string.Concat(Enumerable.Repeat("长短语标题", 8)) : "示例短语";
            var window = Region(source, new { Summary = summary, DisplayTitle = title, IsPinned = pinned }, 1.18);
            try
            {
                var root = (FrameworkElement)window.Content;
                root.Height = 64;
                Layout(window, 180);
                var blocks = Descendants(root).OfType<TextBlock>().Where(block => block.Text == title || block.Text == summary).ToArray();
                Assert.Equal(snippet ? 2 : 1, blocks.Length);
                foreach (var block in blocks)
                {
                    Assert.Equal(TextAlignment.Left, block.TextAlignment);
                    Assert.Equal(TextTrimming.CharacterEllipsis, block.TextTrimming);
                    var bounds = Bounds(block, root);
                    Assert.True(bounds.Right <= root.ActualWidth - 7.5, "Long text must fit the existing inner right edge.");
                    if (block.Text == summary || !pinned) Assert.InRange(Math.Abs(bounds.Left - 8), 0, 0.5);
                }
                var textBounds = Rect.Empty;
                foreach (var block in blocks) textBounds.Union(Bounds(block, root));
                Assert.InRange(Math.Abs(textBounds.Top + textBounds.Height / 2 - root.ActualHeight / 2), 0, 1);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("claude code", 1d)]
    [InlineData("短语标题 gyp", 1.18d)]
    [InlineData("短语标题 gyp", 1.5d)]
    public void SnippetTitle_ShouldKeepTheWholeCharacterVisible(string title, double scale)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = XDocument.Load(Path.Combine(Root, "UniDesk", "TextSnippetEditWindow.xaml")).Descendants()
                .Single(element => element.Name.LocalName == "TextBox" && ((string?)element.Attribute("Text"))?.Contains("Binding Title,") == true);
            var window = Region(source, new TextSnippet { Title = title }, scale);
            try
            {
                Layout(window, 280);
                var box = (TextBox)window.Content;
                box.FontSize = 12 * scale;
                Layout(window, 280);
                box.ApplyTemplate();
                box.UpdateLayout();
                var viewport = (ScrollViewer)box.Template.FindName("PART_ContentHost", box);
                var character = box.GetRectFromCharacterIndex(title.Length - 1);
                Assert.False(character.IsEmpty);
                Assert.True(viewport.ViewportHeight + 0.5 >= character.Height,
                    $"Title characters are clipped: viewport {viewport.ViewportHeight}, character {character.Height}.");
                Assert.True(character.Top >= 0 && character.Bottom <= box.ActualHeight);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("zh-CN", 220d, "zh-CN", 1d)]
    [InlineData("zh-CN", 220d, "zh-CN", 1.25d)]
    [InlineData("en-US", 220d, "zh-CN", 1d)]
    [InlineData("en-US", 220d, "zh-CN", 1.25d)]
    [InlineData("ja-JP", 220d, "zh-CN", 1d)]
    [InlineData("ja-JP", 220d, "zh-CN", 1.25d)]
    [InlineData("es-ES", 220d, "zh-CN", 1d)]
    [InlineData("es-ES", 220d, "zh-CN", 1.25d)]
    [InlineData("zh-CN", 260d, "zh-CN", 1d)]
    [InlineData("zh-CN", 260d, "zh-CN", 1.25d)]
    [InlineData("en-US", 260d, "zh-CN", 1d)]
    [InlineData("en-US", 260d, "zh-CN", 1.25d)]
    [InlineData("ja-JP", 260d, "zh-CN", 1d)]
    [InlineData("ja-JP", 260d, "zh-CN", 1.25d)]
    [InlineData("es-ES", 260d, "zh-CN", 1d)]
    [InlineData("es-ES", 260d, "zh-CN", 1.25d)]
    [InlineData("zh-CN", 220d, "en-US", 1d)]
    [InlineData("zh-CN", 220d, "en-US", 1.25d)]
    [InlineData("en-US", 220d, "en-US", 1d)]
    [InlineData("en-US", 220d, "en-US", 1.25d)]
    [InlineData("ja-JP", 220d, "en-US", 1d)]
    [InlineData("ja-JP", 220d, "en-US", 1.25d)]
    [InlineData("es-ES", 220d, "en-US", 1d)]
    [InlineData("es-ES", 220d, "en-US", 1.25d)]
    [InlineData("zh-CN", 260d, "en-US", 1d)]
    [InlineData("zh-CN", 260d, "en-US", 1.25d)]
    [InlineData("en-US", 260d, "en-US", 1d)]
    [InlineData("en-US", 260d, "en-US", 1.25d)]
    [InlineData("ja-JP", 260d, "en-US", 1d)]
    [InlineData("ja-JP", 260d, "en-US", 1.25d)]
    [InlineData("es-ES", 260d, "en-US", 1d)]
    [InlineData("es-ES", 260d, "en-US", 1.25d)]
    public void RadarDecision_ShouldUseOneTopLine_AlignedToDimensionTextEdges(string language, double width, string hostCulture, double dpi)
    {
        ClockLayoutTests.RunSta(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(hostCulture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(hostCulture);
            var strings = XDocument.Load(Path.Combine(Root, "UniDesk", "Resources", "Strings." + language + ".xaml"));
            string Label(string key) => strings.Descendants().Single(element => (string?)element.Attribute(X + "Key") == "ModelRadar." + key + "Category").Value;
            var source = Read("ModelRadarModuleView.xaml").Descendants().Single(element => element.Name.LocalName == "Border" &&
                (string?)element.Attribute("ToolTip") == "{Binding OverallDecision.ToolTipText}");
            var state = new
            {
                HasOverallLeader = true,
                OverallDecision = new
                {
                    ModelName = "GPT6-Astra", ReasoningEffort = "XHigh", ScoreText = "88.80", ToolTipText = "fixture",
                    DimensionSummary = "Backend 95.0 · Frontend 96.0 · Knowledge 95.0\nAesthetic 75.0 · Maintenance 61.0",
                    Dimensions = new[]
                    {
                        new { Label = Label("Backend"), ScoreText = "95.0" }, new { Label = Label("Frontend"), ScoreText = "96.0" },
                        new { Label = Label("Knowledge"), ScoreText = "95.0" }, new { Label = Label("Aesthetic"), ScoreText = "75.0" },
                        new { Label = Label("Maintainability"), ScoreText = "61.0" }
                    }
                }
            };
            var window = Region(source, state, 1.18, language);
            VisualTreeHelper.SetRootDpi((Visual)window.Content, new DpiScale(dpi, dpi));
            try
            {
                Layout(window, width);
                var blocks = Descendants(window.Content).OfType<TextBlock>().Where(block => block.Visibility == Visibility.Visible).ToArray();
                var model = blocks.Single(block => block.Name == "OverallConfigurationLine");
                var score = blocks.Single(block => block.Text == "88.80");
                var top = Assert.Single(blocks, block => block.Text == "TOP.1");
                Assert.DoesNotContain(blocks, block => block.Text == strings.Descendants().Single(element => (string?)element.Attribute(X + "Key") == "ModelRadar.OverallBest").Value);
                Assert.Equal(TextWrapping.NoWrap, model.TextWrapping);
                Assert.Equal(TextTrimming.CharacterEllipsis, model.TextTrimming);
                Assert.True(score.FontSize / model.FontSize <= 1.25, "Overall score dominates the model name.");
                var values = blocks.Where(block => state.OverallDecision.Dimensions.Any(dimension => dimension.ScoreText == block.Text)).ToArray();
                Assert.Equal(5, values.Length);
                var rows = values.Select(block => Math.Round(Bounds(block, (Visual)window.Content).Top, 1)).Distinct().ToArray();
                Assert.Single(rows);
                var root = (Visual)window.Content;
                var firstLabel = blocks.Single(block => block.Text == state.OverallDecision.Dimensions[0].Label);
                var lastLabel = blocks.Single(block => block.Text == state.OverallDecision.Dimensions[^1].Label);
                var firstBounds = Bounds(firstLabel, root);
                var naturalLabel = new FormattedText(firstLabel.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(firstLabel.FontFamily, firstLabel.FontStyle, firstLabel.FontWeight, firstLabel.FontStretch), firstLabel.FontSize, Brushes.White, 1);
                var firstTextLeft = firstBounds.Left + (firstBounds.Width - naturalLabel.WidthIncludingTrailingWhitespace) / 2;
                var guide = Descendants(window.Content).OfType<TextBlock>().Single(block => block.Name == "OverallBackendAlignmentGuide");
                var leftError = Math.Abs(Bounds(top, root).Left - firstTextLeft);
                Assert.True(leftError <= 1, $"Left edge error={leftError:F4}; guide={guide.Text}/{guide.ActualWidth:F4}; label={firstLabel.Text}/{naturalLabel.WidthIncludingTrailingWhitespace:F4}; font={firstLabel.FontFamily}; DPI={VisualTreeHelper.GetDpi(firstLabel).PixelsPerDip:F2}; culture={hostCulture}");
                var lastBounds = Bounds(lastLabel, root);
                var scoreBounds = Bounds(score, root);
                var naturalLast = new FormattedText(lastLabel.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(lastLabel.FontFamily, lastLabel.FontStyle, lastLabel.FontWeight, lastLabel.FontStretch), lastLabel.FontSize, Brushes.White, 1);
                var lastTextRight = lastBounds.Left + (lastBounds.Width + naturalLast.WidthIncludingTrailingWhitespace) / 2;
                Assert.InRange(Math.Abs(scoreBounds.Right - lastTextRight), 0, 1);
                var header = new[] { top, model, score }.Select(block => Bounds(block, root)).ToArray();
                Assert.True(header[0].Right <= header[1].Left && header[1].Right <= header[2].Left);
                Assert.All(header, bounds => Assert.InRange(Math.Abs(bounds.Top + bounds.Height / 2 - header[0].Top - header[0].Height / 2), 0, 0.5));
                foreach (var block in new[] { top, score })
                {
                    var natural = new FormattedText(block.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, Brushes.White, 1);
                    Assert.True(block.ActualWidth + 0.5 >= natural.WidthIncludingTrailingWhitespace);
                }
                foreach (var dimension in state.OverallDecision.Dimensions)
                {
                    var label = blocks.Single(block => block.Text == dimension.Label);
                    var value = blocks.Single(block => block.Text == dimension.ScoreText &&
                        ReferenceEquals(VisualTreeHelper.GetParent(block), VisualTreeHelper.GetParent(label)));
                    var labelBounds = Bounds(label, root);
                    var valueBounds = Bounds(value, root);
                    Assert.True(valueBounds.Top >= labelBounds.Bottom);
                    Assert.InRange(Math.Abs(labelBounds.Left + labelBounds.Width / 2 - valueBounds.Left - valueBounds.Width / 2), 0, 0.5);
                    Assert.Equal(dimension.Label, label.ToolTip);
                }
                Assert.All(values, block => Assert.True(Bounds(block, (Visual)window.Content).Right <= width + 0.5));
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MainTitle_ShouldMoveInwardAndUseLargerType_OnlyWhenCollapsed(bool collapsed)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = XDocument.Load(Path.Combine(Root, "UniDesk", "MainWindow.xaml")).Descendants()
                .Single(element => (string?)element.Attribute(X + "Name") == "TitleBar");
            var window = Region(source, new { DisplayTitle = "VsirDesk", IsPanelCollapsed = collapsed, FontScale = 1d }, 1);
            try
            {
                Layout(window, 320);
                var border = (Border)window.Content;
                var title = Descendants(border).OfType<TextBlock>().Single(block => block.Text == "VsirDesk");
                Assert.Equal(collapsed ? new Thickness(28, 8, 10, 2) : new Thickness(18, 8, 14, 6), border.Padding);
                Assert.Equal(18, title.FontSize);
                Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
                Assert.InRange(Math.Abs(Bounds(title, border).Left - border.Padding.Left), 0, 0.5);
                Assert.Equal("VsirDesk", title.ToolTip);
            }
            finally { window.Close(); }
        });
    }

    private static XDocument Read(string file) => XDocument.Load(Path.Combine(Root, "UniDesk", "Controls", file));

    private static Window Region(XElement source, object state, double scale, string language = "zh-CN")
    {
        var resources = new XElement(XDocument.Load(Path.Combine(Root, "UniDesk", "App.xaml")).Descendants()
            .First(element => element.Name.LocalName == "ResourceDictionary"));
        foreach (var dictionary in resources.Descendants().Where(element => element.Attribute("Source") != null))
        {
            var path = dictionary.Attribute("Source")!.Value.Replace("Strings.zh-CN", "Strings." + language);
            dictionary.SetAttributeValue("Source", "pack://application:,,,/UniDesk;component/" + path);
        }
        var region = new XElement(source);
        // Run.Text defaults to TwoWay; these rendering fixtures are intentionally read-only.
        foreach (var run in region.Descendants().Where(element => element.Name.LocalName == "Run"))
        {
            var binding = (string?)run.Attribute("Text");
            if (binding?.StartsWith("{Binding ", StringComparison.Ordinal) == true && !binding.Contains("Mode="))
                run.SetAttributeValue("Text", binding[..^1] + ", Mode=OneWay}");
        }
        foreach (var attribute in region.DescendantsAndSelf().Attributes().Where(attribute =>
            attribute.Name.LocalName is "MouseLeftButtonUp" or "MouseLeftButtonDown" or "Click" or "RequestNavigate").ToArray()) attribute.Remove();
        var window = XElement.Parse("""
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:helpers="clr-namespace:UniDesk.Helpers;assembly=UniDesk">
                <Window.Resources/>
            </Window>
            """);
        window.Elements().Single().Add(resources);
        window.Add(region);
        var markup = window.ToString().Replace("clr-namespace:UniDesk.Helpers\"", "clr-namespace:UniDesk.Helpers;assembly=UniDesk\"");
        var host = (Window)XamlReader.Parse(markup);
        host.DataContext = new { FontScale = scale };
        ((FrameworkElement)host.Content).DataContext = state;
        return host;
    }

    private static void Layout(Window window, double width)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var host = (FrameworkElement)window.Content;
        host.Measure(new Size(width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        host.UpdateLayout();
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static IEnumerable<DependencyObject> Descendants(object value)
    {
        var parent = (DependencyObject)value;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
