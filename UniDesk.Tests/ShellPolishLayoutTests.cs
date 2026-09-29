using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace UniDesk.Tests;

public sealed class ShellPolishLayoutTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(320, 1.18, false)]
    [InlineData(340, 1.0, false)]
    [InlineData(520, 0.9, false)]
    [InlineData(320, 1.18, true)]
    public void Header_LongUserTitleMustNotOverlapActions(double width, double scale, bool collapsed)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var title = Read("MainWindow.xaml").Descendants().Single(e => (string?)e.Attribute(X + "Name") == "TitleBar");
            var host = Load(title);
            host.DataContext = new { DisplayTitle = "我的桌面工作与生活助手测试标题", FontScale = scale, IsPanelCollapsed = collapsed };
            Layout(host, width, 70);
            var text = Descendants(host).OfType<TextBlock>().Single(t => t.Text == "我的桌面工作与生活助手测试标题");
            var button = Descendants(host).OfType<Button>().Single(b => b.Name == "SearchButton");
            var titleBounds = text.TransformToAncestor(host).TransformBounds(new Rect(text.RenderSize));
            var actionBounds = button.TransformToAncestor(host).TransformBounds(new Rect(button.RenderSize));
            Assert.True(titleBounds.Right <= actionBounds.Left, $"Title overlaps actions: {titleBounds} / {actionBounds}");
            Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
            Assert.Equal(text.Text, text.ToolTip);
        });
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("es-ES")]
    public void SettingsToggleRows_KeepLabelsClearOfTheirHitTargets(string language)
    {
        ClockLayoutTests.RunSta(() =>
        {
            foreach (var file in new[] { "General", "Desktop", "Appearance", "Shortcuts" })
            {
                var page = Read($"Controls/Settings/{file}SettingsPage.xaml");
                foreach (var grid in page.Descendants(Wpf + "Grid").Where(e => e.Elements(Wpf + "CheckBox").Any()))
                {
                    var host = Load(grid, language);
                    Layout(host, 280, 160);
                    var label = Descendants(host).OfType<TextBlock>().First();
                    var toggle = Descendants(host).OfType<CheckBox>().Single();
                    var labelBounds = label.TransformToAncestor(host).TransformBounds(new Rect(label.RenderSize));
                    var toggleBounds = toggle.TransformToAncestor(host).TransformBounds(new Rect(toggle.RenderSize));
                    Assert.True(labelBounds.Right <= toggleBounds.Left, $"{file}/{language}: label overlaps switch");
                    Assert.True(toggle.ActualWidth >= 28 && toggle.ActualHeight >= 28, $"{file}: toggle hit target too small");
                    Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(toggle)));
                }
            }
        });
    }

    [Fact]
    public void SharedKeyboardFocus_UsesVisibleNonInteractiveAdorner()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var shared = new ResourceDictionary { Source = new Uri("pack://application:,,,/UniDesk;component/Resources/Themes/Shared.xaml") };
            var style = Assert.IsType<Style>(shared[SystemParameters.FocusVisualStyleKey]);
            var control = new Control { Style = style, Width = 80, Height = 32 };
            Layout(control, 80, 32);
            var border = Assert.Single(Descendants(control).OfType<Border>());
            Assert.Equal(new Thickness(2), border.BorderThickness);
            Assert.False(control.IsHitTestVisible);
            Assert.False(control.Focusable);
        });
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("es-ES")]
    public void QuickNoteFooter_ActionsMustNotOverlapAtMinimumPanelWidth(string language)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var window = Read("QuickNoteEditorWindow.xaml");
            var footer = window.Descendants(Wpf + "Border").Single(e => (string?)e.Attribute("Grid.Row") == "2");
            var host = Load(footer, language, window.Element(Wpf + "Window.Resources"));
            Layout(host, 282, 120);
            var buttons = Descendants(host).OfType<Button>().ToArray();
            for (var i = 0; i < buttons.Length; i++)
            for (var j = i + 1; j < buttons.Length; j++)
            {
                var a = buttons[i].TransformToAncestor(host).TransformBounds(new Rect(buttons[i].RenderSize));
                var b = buttons[j].TransformToAncestor(host).TransformBounds(new Rect(buttons[j].RenderSize));
                Assert.False(Rect.Intersect(a, b) is { Width: > 0, Height: > 0 }, $"{language}: {buttons[i].Content} overlaps {buttons[j].Content}");
            }
        });
    }

    [Fact]
    public void QuickTextManager_SummaryRetainsFullRowWidth()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var window = Read("QuickTextManagerWindow.xaml");
            var row = window.Descendants(Wpf + "ItemsControl")
                .Single(e => (string?)e.Attribute("ItemsSource") == "{Binding ClipboardHistory}")
                .Descendants(Wpf + "DataTemplate").Single().Elements().Single();
            var host = Load(row, "es-ES", window.Element(Wpf + "Window.Resources"));
            host.DataContext = new { Summary = "Synthetic clipboard sample used only for layout verification." };
            Layout(host, 360, 130);
            var summary = Descendants(host).OfType<TextBlock>().Single(e => e.Text.StartsWith("Synthetic"));
            Assert.True(summary.ActualWidth >= 320, $"Text squeezed by action buttons: {summary.ActualWidth:F1} DIP");
        });
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("es-ES")]
    public void SettingsNavigation_LocalizedLabelsStayInsideSidebar(string language)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var navigation = Read("SettingsWindow.xaml").Descendants(Wpf + "ListBox").Single();
            var host = Load(navigation, language);
            Layout(host, 136, 440);
            var labels = Descendants(host).OfType<TextBlock>().Where(t => t.Text.Length > 1).ToArray();
            Assert.Equal(7, labels.Length);
            foreach (var label in labels)
            {
                var bounds = label.TransformToAncestor(host).TransformBounds(new Rect(label.RenderSize));
                Assert.True(bounds.Right <= 136.5, $"{language}: {label.Text} overflows to {bounds.Right:F1}");
            }
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    public void HotkeyCapture_LongActionLabelsDoNotSqueezeInput(string language)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var page = Read("Controls/Settings/ShortcutsSettingsPage.xaml");
            var input = page.Descendants(Wpf + "TextBox").Single();
            var host = Load(input.Parent!, language);
            host.DataContext = new { GlobalHotkeyEnabled = true, Hotkey = "Ctrl+Alt+Space" };
            Layout(host, 360, 150);
            var textBox = Descendants(host).OfType<TextBox>().Single();
            Assert.True(textBox.ActualWidth >= 150, $"{language}: hotkey capture only {textBox.ActualWidth:F1} DIP");
        });
    }

    [Fact]
    public void GlassMenu_CheckableItemsKeepAVisibleCheckedState()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/UniDesk;component/Resources/TrayMenu.xaml") };
            var item = new MenuItem { Header = "Pinned", IsCheckable = true, Style = (Style)resources["TrayMenuItemStyle"] };
            Layout(item, 180, 36);
            var check = Assert.IsType<TextBlock>(item.Template.FindName("CheckMark", item));
            Assert.Equal(Visibility.Collapsed, check.Visibility);
            item.IsChecked = true;
            item.UpdateLayout();
            Assert.Equal(Visibility.Visible, check.Visibility);
            Assert.False(check.IsHitTestVisible);
        });
    }

    [Fact]
    public void GlassMenu_MixedItemsAndSeparatorsCanBeLaidOut()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/UniDesk;component/Resources/TrayMenu.xaml") };
            var menu = new ContextMenu { Style = (Style)resources[typeof(ContextMenu)] };
            menu.Items.Add(new MenuItem { Header = "Pinned", IsCheckable = true, IsChecked = true });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Delete", Tag = "Danger" });
            Layout(menu, 240, 140);
            Assert.IsType<Separator>(menu.ItemContainerGenerator.ContainerFromIndex(1));
            Assert.NotNull(((MenuItem)menu.Items[0]).Template.FindName("CheckMark", (MenuItem)menu.Items[0]));
        });
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void KeyboardFocusRing_MustDifferFromPrimaryButtonFill(string theme)
    {
        ClockLayoutTests.RunSta(() =>
        {
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/UniDesk;component/Resources/Themes/Shared.xaml") });
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/UniDesk;component/Resources/Themes/{theme}.xaml") });
            var panel = new StackPanel { Resources = resources };
            var button = new Button { Style = (Style)resources["GlassPrimaryButtonStyle"], Content = "Save" };
            var adorner = new Control { Style = button.FocusVisualStyle, Height = 32 };
            panel.Children.Add(adorner);
            panel.Children.Add(button);
            Layout(panel, 240, 90);
            var ring = Assert.Single(Descendants(adorner).OfType<Border>());
            var ringColor = Assert.IsType<SolidColorBrush>(ring.BorderBrush).Color;
            var fillColor = Assert.IsType<SolidColorBrush>(button.Background).Color;
            var channelDifference = Math.Abs(ringColor.R - fillColor.R) + Math.Abs(ringColor.G - fillColor.G) + Math.Abs(ringColor.B - fillColor.B);
            Assert.True(channelDifference > 120, $"{theme}: focus ring blends into the primary button fill ({ringColor} / {fillColor})");
        });
    }

    [Fact]
    public void GlassMenu_DangerLabelRetainsItsSemanticForeground()
    {
        ClockLayoutTests.RunSta(() =>
        {
            var host = Load(new XElement(Wpf + "Border"));
            var menu = new ContextMenu { Resources = host.Resources };
            var item = new MenuItem { Header = "Delete sample", Tag = "Danger" };
            menu.Items.Add(item);
            Layout(menu, 240, 90);
            var danger = Assert.IsType<SolidColorBrush>(menu.FindResource("DangerBrush")).Color;
            Assert.Equal(danger, Assert.IsType<SolidColorBrush>(item.Foreground).Color);
            var label = Descendants(item).OfType<TextBlock>().Single(t => t.Text == "Delete sample");
            Assert.Equal(danger, Assert.IsType<SolidColorBrush>(label.Foreground).Color);
        });
    }

    internal static XElement Read(string path) => XDocument.Load(Path.Combine(ProjectRoot(), "UniDesk", path)).Root!;

    internal static Border Load(XElement source, string language = "zh-CN", XElement? localResources = null)
    {
        var view = new XElement(source);
        var eventNames = new HashSet<string>(typeof(UIElement).GetEvents().Select(e => e.Name))
        { "Click", "PreviewKeyDown", "Loaded", "Closed", "MouseLeave", "SizeChanged" };
        foreach (var node in view.DescendantsAndSelf())
        {
            node.Attribute(X + "Class")?.Remove();
            foreach (var attribute in node.Attributes().Where(a => eventNames.Contains(a.Name.LocalName)).ToArray()) attribute.Remove();
        }
        var app = Read("App.xaml");
        var resources = new XElement(app.Element(Wpf + "Application.Resources")!.Element(Wpf + "ResourceDictionary")!);
        if (localResources != null)
            foreach (var resource in localResources.Elements())
            {
                var key = (string?)resource.Attribute(X + "Key");
                resources.Elements().Where(e => key != null && (string?)e.Attribute(X + "Key") == key).Remove();
                resources.Add(new XElement(resource));
            }
        foreach (var dictionary in resources.Descendants(Wpf + "ResourceDictionary").Where(e => e.Attribute("Source") != null))
        {
            var path = dictionary.Attribute("Source")!.Value.Replace("Strings.zh-CN", $"Strings.{language}");
            dictionary.SetAttributeValue("Source", $"pack://application:,,,/UniDesk;component/{path}");
        }
        var wrapper = new XElement(Wpf + "Border",
            new XAttribute(XNamespace.Xmlns + "x", X),
            new XAttribute(XNamespace.Xmlns + "helpers", "clr-namespace:UniDesk.Helpers;assembly=UniDesk"),
            new XElement(Wpf + "Border.Resources", resources), view);
        foreach (var node in wrapper.DescendantsAndSelf().Where(e => e.Name.NamespaceName == "clr-namespace:UniDesk.Helpers"))
            node.Name = XName.Get(node.Name.LocalName, "clr-namespace:UniDesk.Helpers;assembly=UniDesk");
        return (Border)XamlReader.Parse(wrapper.ToString());
    }

    internal static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniDesk.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("UniDesk.sln");
    }
}
