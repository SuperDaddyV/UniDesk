using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace UniDesk.Tests;

public sealed class ModulePolishLayoutTests
{
    [Theory]
    [InlineData(142, 1.18)]
    [InlineData(126, 1.18)]
    public void WeatherDescriptionAndNegativeTemperature_ShouldFitRemainingColumn(double width, double fontScale)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = LoadModule("TimeWeatherModuleView.xaml");
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var summary = source.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "WeatherSummary");
            var row = new XElement(summary.Elements().Single(element => element.Name.LocalName == "Grid" &&
                (string?)element.Attribute("Grid.Row") == "0"));
            var temperature = row.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding WeatherTemperature}");
            var description = row.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding WeatherDescription}");
            temperature.SetAttributeValue("Text", "-18°C");
            temperature.SetAttributeValue("FontSize", (24 * fontScale).ToString(CultureInfo.InvariantCulture));
            description.SetAttributeValue("Text", "Intermittent sunshine");
            description.SetAttributeValue("FontSize", (12 * fontScale).ToString(CultureInfo.InvariantCulture));
            RemoveBindingsAndEvents(row);
            var host = (Grid)XamlReader.Parse(row.ToString());
            host.Measure(new Size(width, 160));
            host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
            host.UpdateLayout();
            var blocks = Descendants(host).OfType<TextBlock>().ToArray();
            var renderedTemperature = blocks.Single(block => block.Text == "-18°C");
            var renderedDescription = blocks.Single(block => block.Text == "Intermittent sunshine");
            var temperatureBounds = Bounds(renderedTemperature, host);
            var descriptionBounds = Bounds(renderedDescription, host);
            Assert.True(temperatureBounds.Left >= 57.5, $"Negative temperature intrudes into icon column: {temperatureBounds}");
            Assert.True(temperatureBounds.Right <= width + 0.5, $"Negative temperature exceeds summary: {temperatureBounds}");
            Assert.True(descriptionBounds.Left >= 57.5, $"Weather description is clipped from the left: {descriptionBounds}");
            Assert.True(descriptionBounds.Right <= width + 0.5);
            Assert.True(renderedDescription.ActualHeight > renderedDescription.FontSize * 1.5,
                "Long weather description should wrap within its own column.");
        });
    }

    [Theory]
    [InlineData("NetworkReceivedGroup", "NetworkReceivedValueText")]
    [InlineData("NetworkSentGroup", "NetworkSentValueText")]
    public void HardwareNetworkValueBox_ShouldRetainStableGeometry(string groupName, string valueName)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = LoadModule("HardwareMonitorModuleView.xaml");
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var group = new XElement(source.Descendants().Single(element => (string?)element.Attribute(x + "Name") == groupName));
            var value = group.Descendants().Single(element => (string?)element.Attribute(x + "Name") == valueName);
            value.SetAttributeValue("Text", "128.8 MB/s");
            RemoveBindingsAndEvents(group);
            group.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
            var host = (Grid)XamlReader.Parse(group.ToString());
            host.Measure(new Size(140, 14));
            host.Arrange(new Rect(0, 0, 140, 14));
            host.UpdateLayout();
            var renderedValue = (TextBlock)host.FindName(valueName);
            Assert.Equal(140, host.ActualWidth);
            Assert.Equal(78, renderedValue.ActualWidth);
            Assert.Equal(78, host.ColumnDefinitions[2].ActualWidth);
            Assert.True(renderedValue.TransformToAncestor(host).Transform(new Point()).X >= 62);
        });
    }

    [Theory]
    [InlineData(258)]
    [InlineData(458)]
    public void RadarRanking_ShouldShowCompleteNameAndReasoningWithoutOverlappingScore(double width)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = LoadModule("ModelRadarModuleView.xaml");
            var row = new XElement(source.Descendants().Single(element => element.Name.LocalName == "Border" &&
                (string?)element.Attribute("ToolTip") == "{Binding ToolTipText}"));
            var model = row.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding ModelName}");
            var reasoning = row.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding ReasoningEffort}");
            var score = row.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding ScoreText}");
            model.SetAttributeValue("Text", "Claude-Opus-5-Long-Edition");
            reasoning.SetAttributeValue("Text", "XHigh");
            score.SetAttributeValue("Text", "95.2");
            RemoveBindingsAndEvents(row);
            var host = (Border)XamlReader.Parse(row.ToString());
            host.Measure(new Size(width, 200));
            host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
            host.UpdateLayout();
            var rendered = Descendants(host).OfType<TextBlock>().ToArray();
            var renderedModel = rendered.Single(block => block.Text == "Claude-Opus-5-Long-Edition");
            var renderedReasoning = rendered.Single(block => block.Text == "XHigh");
            var renderedScore = rendered.Single(block => block.Text == "95.2");
            var modelBounds = Bounds(renderedModel, host);
            var reasoningBounds = Bounds(renderedReasoning, host);
            var scoreBounds = Bounds(renderedScore, host);
            Assert.True(modelBounds.Right <= reasoningBounds.Left + 0.5);
            Assert.True(reasoningBounds.Right <= scoreBounds.Left + 0.5);
            Assert.True(scoreBounds.Right <= width + 0.5);
            if (width <= 258)
                Assert.True(renderedModel.ActualHeight > renderedModel.FontSize * 1.5,
                    "Long model configuration should wrap instead of disappearing behind the score.");
            Assert.True(modelBounds.Bottom <= host.ActualHeight + 0.5);
        });
    }

    [Theory]
    [InlineData(320, 1.0)]
    [InlineData(320, 1.18)]
    [InlineData(340, 1.18)]
    public void TodoTitleAndDueDate_ShouldShareLeadingEdgeAndAlignMarkersToTitle(double width, double fontScale)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var source = LoadModule("TodosModuleView.xaml");
            var row = source.Descendants().Single(element => element.Name.LocalName == "Grid" &&
                element.Elements().Any(child => (string?)child.Attribute("Tag") == "TodoCheck"));
            var layout = new XElement(row);
            var title = layout.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding Title}");
            title.SetAttributeValue("Text", "这是一条很长的待办事项标题，需要为日期留出辅助行");
            title.SetAttributeValue("FontSize", (12 * fontScale).ToString(CultureInfo.InvariantCulture));
            title.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
            title.SetAttributeValue(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"), "TodoTitleUnderTest");
            var due = layout.Descendants().Single(element => element.Name.LocalName == "TextBlock" &&
                element.Elements().Any(child => child.Name.LocalName == "TextBlock.Text"));
            due.Elements().Where(child => child.Name.LocalName == "TextBlock.Text").Remove();
            due.SetAttributeValue("Text", "2026-09-28");
            due.SetAttributeValue("FontSize", (11 * fontScale).ToString(CultureInfo.InvariantCulture));
            due.SetAttributeValue(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"), "TodoDueUnderTest");
            RemoveBindingsAndEvents(layout);
            layout.Descendants().Where(element => element.Name.LocalName == "Rectangle").Remove();
            layout.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
            var host = (Grid)XamlReader.Parse(layout.ToString());
            host.Measure(new Size(width, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
            host.UpdateLayout();
            var renderedTitle = (TextBlock)host.FindName("TodoTitleUnderTest");
            var renderedDue = (TextBlock)host.FindName("TodoDueUnderTest");
            var titleBounds = renderedTitle.TransformToAncestor(host).TransformBounds(new Rect(0, 0, renderedTitle.ActualWidth, renderedTitle.ActualHeight));
            var dueBounds = renderedDue.TransformToAncestor(host).TransformBounds(new Rect(0, 0, renderedDue.ActualWidth, renderedDue.ActualHeight));
            Assert.True(titleBounds.Width >= width - 75, $"Title area became {titleBounds.Width:F1} DIP at {width:F1} DIP row width.");
            Assert.True(dueBounds.Top >= titleBounds.Bottom - 0.5, $"Date overlaps the title: {titleBounds}, {dueBounds}");
            Assert.InRange(Math.Abs(dueBounds.Left - titleBounds.Left), 0, 0.5);
            Assert.True(dueBounds.Right <= width + 0.5);
            Assert.True(dueBounds.Bottom <= host.ActualHeight + 0.5);
            var check = host.Children.OfType<Grid>().Single(child => child.Tag as string == "TodoCheck");
            Assert.True(check.ActualWidth >= 28 && check.ActualHeight >= 28);
            var priority = host.Children.OfType<Ellipse>().Single();
            var titleCenter = titleBounds.Top + titleBounds.Height / 2;
            var checkCenter = Bounds(check, host).Top + Bounds(check, host).Height / 2;
            var priorityCenter = Bounds(priority, host).Top + Bounds(priority, host).Height / 2;
            Assert.InRange(Math.Abs(titleCenter - checkCenter), 0, 0.5);
            Assert.InRange(Math.Abs(titleCenter - priorityCenter), 0, 0.5);
        });
    }

    [Fact]
    public void TodoRow_ShouldExposeFullTitleAndUseHoverOrSearchHighlightOnly()
    {
        var module = LoadModule("TodosModuleView.xaml");
        var row = LoadModule("TodoSwipeRow.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var title = module.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding Title}");
        Assert.Equal("{Binding Title}", (string?)title.Attribute("ToolTip"));

        var check = module.Descendants().Single(element => (string?)element.Attribute("Tag") == "TodoCheck");
        Assert.Equal("28", (string?)check.Attribute("Width"));
        Assert.Equal("28", (string?)check.Attribute("Height"));
        var circle = check.Descendants().Single(element => element.Name.LocalName == "Ellipse");
        Assert.Equal("13", (string?)circle.Attribute("Width"));
        Assert.Equal("13", (string?)circle.Attribute("Height"));
        Assert.Equal("1.25", (string?)circle.Attribute("StrokeThickness"));

        var due = module.Descendants().Single(element => element.Name.LocalName == "TextBlock" &&
            element.Elements().Any(child => child.Name.LocalName == "TextBlock.Text"));
        Assert.Equal("Left", (string?)due.Attribute("HorizontalAlignment"));
        Assert.Equal("1", (string?)due.Attribute("Grid.Row"));

        var rowGrid = row.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "RootGrid");
        Assert.Null(rowGrid.Attribute("Height"));
        Assert.Equal("46", (string?)rowGrid.Attribute("MinHeight"));
        var surface = row.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "SwipeSurface");
        var style = surface.Elements().Single(element => element.Name.LocalName == "Border.Style");
        Assert.Contains(style.Descendants().Where(element => element.Name.LocalName == "Setter"),
            setter => (string?)setter.Attribute("Property") == "Background" && (string?)setter.Attribute("Value") == "Transparent");
        Assert.Contains(style.Descendants().Where(element => element.Name.LocalName == "Trigger"),
            trigger => (string?)trigger.Attribute("Property") == "IsMouseOver" &&
                trigger.Elements().Any(setter => setter.Name.LocalName == "Setter" &&
                    (string?)setter.Attribute("Value") == "{DynamicResource GlassHighlightBrush}"));
        Assert.Contains(style.Descendants().Where(element => element.Name.LocalName == "DataTrigger"),
            trigger => (string?)trigger.Attribute("Value") == "True" &&
                (string?)trigger.Attribute("Binding") == "{Binding IsSearchHighlighted, RelativeSource={RelativeSource AncestorType=controls:TodoSwipeRow}}");
    }

    private static XDocument LoadModule(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory.FullName, "UniDesk", "Controls", file));
    }

    private static void RemoveBindingsAndEvents(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf().ToArray())
        {
            element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Remove();
            foreach (var attribute in element.Attributes().Where(attribute =>
                attribute.Value.StartsWith("{", StringComparison.Ordinal) ||
                attribute.Name.LocalName.StartsWith("Mouse", StringComparison.Ordinal)).ToArray())
            {
                if (attribute.Name.LocalName == "FontSize") attribute.Value = "12";
                else attribute.Remove();
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
}
