using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace UniDesk.Tests;

public sealed class ModelRadarRankingLayoutTests
{
    [Fact]
    public void DecisionTags_ShouldNotIncreaseRankingRowHeight()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);

            var document = XDocument.Load(Path.Combine(directory.FullName, "UniDesk", "Controls", "ModelRadarModuleView.xaml"));
            var template = document.Descendants().Single(element => element.Name.LocalName == "Border" &&
                (string?)element.Attribute("ToolTip") == "{Binding ToolTipText}");

            var untaggedHeight = MeasureRow(template, string.Empty);
            var taggedHeight = MeasureRow(template, "recommended · lightweight · speed · value");
            Assert.InRange(taggedHeight - untaggedHeight, -0.5, 0.5);
        });
    }

    private static double MeasureRow(XElement template, string tags)
    {
        var row = new XElement(template);
        foreach (var element in row.Descendants())
        {
            var binding = (string?)element.Attribute("Text");
            switch (binding)
            {
                case "{Binding ModelName}": element.SetAttributeValue("Text", "GPT5.6-Sol"); break;
                case "{Binding ReasoningEffort}": element.SetAttributeValue("Text", "Medium"); break;
                case "{Binding ScoreText}": element.SetAttributeValue("Text", "95.2"); break;
                case "{Binding DecisionTagsText}":
                    element.SetAttributeValue("Text", tags);
                    element.SetAttributeValue("Visibility", tags.Length == 0 ? "Collapsed" : "Visible");
                    break;
            }
        }

        foreach (var element in row.DescendantsAndSelf().ToArray())
        {
            element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Remove();
            foreach (var attribute in element.Attributes().Where(attribute =>
                attribute.Value.StartsWith("{", StringComparison.Ordinal)).ToArray())
                attribute.Remove();
        }

        var host = (Border)XamlReader.Parse(row.ToString());
        host.Measure(new Size(258, 200));
        host.Arrange(new Rect(0, 0, 258, host.DesiredSize.Height));
        host.UpdateLayout();
        return host.ActualHeight;
    }
}
