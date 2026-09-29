using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace UniDesk.Tests;

public sealed class ClockLayoutTests
{
    [Theory]
    [InlineData(150, 0.9, false)]
    [InlineData(150, 1.18, false)]
    [InlineData(126, 1.18, false)]
    [InlineData(220, 1.18, false)]
    [InlineData(112, 1.18, true)]
    public void ClockTime_ShouldRemainReadableWhenDateIsLong(double width, double fontScale, bool collapsed)
    {
        RunSta(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var document = XDocument.Load(Path.Combine(directory.FullName, "UniDesk", "Controls", "TimeWeatherModuleView.xaml"));
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var clock = new XElement(document.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "ClockHotspot"));
            clock.Attribute("MouseLeftButtonDown")!.Remove();
            clock.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
            // Load the production layout with deterministic text and font values, without starting the app or its services.
            foreach (var block in clock.Descendants().Where(e => e.Name.LocalName == "TextBlock"))
            {
                var binding = (string?)block.Attribute("Text");
                var isTime = binding == "{Binding ClockTimeText}";
                block.SetAttributeValue("Text", isTime ? "08:43" : binding == "{Binding ClockDateText}" ? "miércoles, 30 de septiembre de 2026" : "Lunar August twentieth");
                block.SetAttributeValue("FontSize", (collapsed ? (isTime ? 30 : 10) : Math.Round((isTime ? 43 : 11) * fontScale, 1)).ToString(CultureInfo.InvariantCulture));
                block.SetAttributeValue("Foreground", "White");
                block.Attribute("Visibility")?.Remove();
                block.Elements().Where(e => e.Name.LocalName == "TextBlock.Style").Remove();
            }
            var host = (Border)XamlReader.Parse(clock.ToString());
            System.Windows.Documents.TextElement.SetFontFamily(host,
                new FontFamily("pack://application:,,,/UniDesk;component/Resources/Fonts/#Inter"));
            host.Measure(new Size(width, 160));
            host.Arrange(new Rect(0, 0, width, 160));
            host.UpdateLayout();
            var blocks = Descendants(host).OfType<TextBlock>().ToArray();
            var time = blocks.Single(block => block.Text == "08:43");
            var natural = new FormattedText(time.Text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                new Typeface(time.FontFamily, time.FontStyle, time.FontWeight, time.FontStretch), time.FontSize, Brushes.White, 1);
            var transformed = time.TransformToAncestor(host).TransformBounds(new Rect(0, 0, natural.WidthIncludingTrailingWhitespace, natural.Height));
            Assert.True(transformed.Right <= width + 0.5, $"Time right={transformed.Right:F2}, available={width}");
            Assert.True(transformed.Width >= natural.WidthIncludingTrailingWhitespace * 0.8,
                $"Long date reduced clock to {transformed.Width / natural.WidthIncludingTrailingWhitespace:P0} of its intended size.");
            var date = blocks.Single(block => block.Text.StartsWith("miércoles", StringComparison.Ordinal));
            Assert.True(date.ActualWidth <= width + 0.5);
            Assert.True(date.TransformToAncestor(host).TransformBounds(new Rect(0, 0, date.ActualWidth, date.ActualHeight)).Bottom <= host.ActualHeight + 0.5);
            if (!collapsed && width == 126)
            {
                var lunar = blocks.Single(block => block.Text == "Lunar August twentieth");
                var lunarBounds = lunar.TransformToAncestor(host).TransformBounds(new Rect(lunar.RenderSize));
                Assert.True(lunarBounds.Right <= width + 0.5, $"Lunar text escapes clock column: {lunarBounds}");
                Assert.True(lunar.ActualHeight >= lunar.FontSize * 1.5, "Long lunar text should wrap instead of clipping.");
            }
        });
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

    internal static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Register WPF's pack URI parser even when a resource-only test runs first.
                _ = Application.Current;
                action();
            }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF verification did not finish.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
