using System.Text.RegularExpressions;

namespace UniDesk.Tests;

public class WpfInteractionRegressionTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

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
        Assert.Contains("MinHeight=\"420\"", settingsXaml, StringComparison.Ordinal);
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
        Assert.Equal(2, Regex.Matches(hardwareXaml, "Width=\"78\"[\\s\\S]{0,160}TextAlignment=\"Center\"").Count);
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
    public void SettingsContent_ShouldUseNativeScrollingWithoutCapturingMouse()
    {
        var settingsXaml = ReadProjectFile("UniDesk", "SettingsWindow.xaml");
        var settingsCode = ReadProjectFile("UniDesk", "SettingsWindow.xaml.cs");

        Assert.DoesNotContain("ContentScrollViewer_OnPreviewMouse", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("_isScrollDragging", settingsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ContentScrollViewer.CaptureMouse", settingsCode, StringComparison.Ordinal);
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
    public void SettingsEditableTextBoxes_ShouldUseNativeMouseFocus()
    {
        var appearanceXaml = ReadProjectFile("UniDesk", "Controls", "Settings", "AppearanceSettingsPage.xaml");
        var generalXaml = ReadProjectFile("UniDesk", "Controls", "Settings", "GeneralSettingsPage.xaml");

        Assert.Contains("x:Name=\"DisplayTitleTextBox\"", appearanceXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewMouseLeftButtonDown", appearanceXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewMouseLeftButtonDown", generalXaml, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", appearanceXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding City, UpdateSourceTrigger=PropertyChanged}\"", generalXaml, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Matches(generalXaml, "UpdateSourceTrigger=PropertyChanged").Count);
    }

    [Fact]
    public void HardwareDiagnostics_ShouldBeWiredToSettingsAndLocalizedTooltips()
    {
        var dataXaml = ReadProjectFile("UniDesk", "Controls", "Settings", "DataSettingsPage.xaml");
        var hardwareXaml = ReadProjectFile("UniDesk", "Controls", "HardwareMonitorModuleView.xaml");

        Assert.Contains("ExportSensorDiagnosticsCommand", dataXaml, StringComparison.Ordinal);
        Assert.Contains("SystemCpuUsageToolTip", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("SystemCpuTemperatureToolTip", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("SystemGpuUsageToolTip", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("SystemGpuTemperatureToolTip", hardwareXaml, StringComparison.Ordinal);

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            Assert.Contains("Settings.ExportHardwareDiagnostics", resources, StringComparison.Ordinal);
            Assert.Contains("Hardware.MetricAvailableFormat", resources, StringComparison.Ordinal);
            Assert.Contains("Hardware.NoSensor", resources, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HardwareNetworkRow_ShouldCenterEachLabelValuePair()
    {
        var hardwareXaml = ReadProjectFile("UniDesk", "Controls", "HardwareMonitorModuleView.xaml");

        Assert.Contains("x:Name=\"NetworkReceivedGroup\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NetworkSentGroup\"", hardwareXaml, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(hardwareXaml, "SharedSizeGroup=\"NetworkLabel\"").Count);
        Assert.Equal(2, Regex.Matches(hardwareXaml, "SharedSizeGroup=\"NetworkValue\"").Count);
        Assert.Equal(2, Regex.Matches(hardwareXaml, "Width=\"78\"").Count);
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

    [Fact]
    public void MainWindow_ShouldWireModelRadarAsSeventhModule()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");
        var modulesGrid = Regex.Match(
            mainXaml,
            "<Grid x:Name=\"MainModulesGrid\"[\\s\\S]*?</Grid>\\s*</ScrollViewer>");

        Assert.True(modulesGrid.Success);
        Assert.Equal(
            7,
            Regex.Matches(modulesGrid.Value, @"<RowDefinition\s+Height=""Auto""\s*/>").Count);
        Assert.Contains(
            "<controls:ModelRadarModuleView x:Name=\"ModelRadarModule\"",
            mainXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "DashboardModuleIds.ModelRadar => ModelRadarModule",
            mainCode,
            StringComparison.Ordinal);
        Assert.Contains("yield return ModelRadarModule", mainCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelRadarModule_ShouldUseGenericRadarGraphicAndFixedAttributionLinks()
    {
        var radarXaml = ReadProjectFile("UniDesk", "Controls", "ModelRadarModuleView.xaml");
        var radarCode = ReadProjectFile("UniDesk", "Controls", "ModelRadarModuleView.xaml.cs");

        Assert.Contains("x:Class=\"UniDesk.Controls.ModelRadarModuleView\"", radarXaml, StringComparison.Ordinal);
        Assert.Contains("<Path", radarXaml, StringComparison.Ordinal);
        Assert.Contains("Data=", radarXaml, StringComparison.Ordinal);
        Assert.Contains("https://modeldial.com/radar", radarXaml, StringComparison.Ordinal);
        Assert.Contains("https://modeldial.com/data-license", radarXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Logo", radarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<Image", radarXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("e.Uri", radarCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelRadarModule_ShouldUseCompactConsistentTypographyAndHideMissingRankingTags()
    {
        var radarXaml = ReadProjectFile("UniDesk", "Controls", "ModelRadarModuleView.xaml");

        Assert.Contains(
            "Style=\"{StaticResource ModuleHeaderTextStyle}\"",
            radarXaml,
            StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=12", radarXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding BatchText}\"", radarXaml, StringComparison.Ordinal);
        Assert.Equal(
            1,
            radarXaml.Split(
                "Text=\"{Binding PublishedText}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("x:Name=\"OverallConfigurationLine\"", radarXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ValueConfigurationLine\"", radarXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RankingDecisionTags\"", radarXaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding DecisionTagsText, Converter={StaticResource StringNotEmptyToVisibilityConverter}}\"",
            radarXaml,
            StringComparison.Ordinal);

        var fullRankingLink = Regex.Match(
            radarXaml,
            "<Hyperlink x:Name=\"ViewFullRankingLink\"[\\s\\S]*?</Hyperlink>");
        Assert.True(fullRankingLink.Success);
        Assert.Contains("FontWeight=\"SemiBold\"", fullRankingLink.Value, StringComparison.Ordinal);

        foreach (var configuration in new[]
                 {
                     new
                     {
                         Name = "OverallConfigurationLine",
                         EffortBinding = "{Binding OverallDecision.ReasoningEffort}"
                     },
                     new
                     {
                         Name = "ValueConfigurationLine",
                         EffortBinding = "{Binding ValueDecision.ReasoningEffort}"
                     }
                 })
        {
            var configurationLine = Regex.Match(
                radarXaml,
                $"<TextBlock x:Name=\"{configuration.Name}\"[\\s\\S]*?</TextBlock>");
            Assert.True(configurationLine.Success);
            Assert.Contains(configuration.EffortBinding, configurationLine.Value, StringComparison.Ordinal);
            Assert.Contains("FontWeight=\"Bold\"", configurationLine.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ModelRadarResources_ShouldLocalizeTheModuleAndAllReferencedRadarKeys()
    {
        var radarXaml = ReadProjectFile("UniDesk", "Controls", "ModelRadarModuleView.xaml");
        var referencedKeys = Regex.Matches(
                radarXaml,
                @"(?:DynamicResource|StaticResource)\s+(ModelRadar\.[A-Za-z0-9]+)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(referencedKeys);
        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            Assert.Contains("x:Key=\"Module.ModelRadar\"", resources, StringComparison.Ordinal);
            foreach (var key in referencedKeys)
            {
                Assert.Contains($"x:Key=\"{key}\"", resources, StringComparison.Ordinal);
            }

            foreach (var semanticPart in new[]
                     {
                         "Overall",
                         "Value",
                         "Backend",
                         "Frontend",
                         "Knowledge",
                         "Offline",
                         "Refresh",
                         "Ranking"
                     })
            {
                Assert.Contains(
                    referencedKeys,
                    key => key.Contains(semanticPart, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void DesktopSettings_ClipboardLimit_ShouldExposePersistentSingleSelection()
    {
        var desktopXaml = ReadProjectFile("UniDesk", "Controls", "Settings", "DesktopSettingsPage.xaml");
        var sharedTheme = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");

        Assert.Contains("x:Name=\"ClipboardHistoryLimitList\"", desktopXaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ClipboardHistoryLimitOptions}\"", desktopXaml, StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding ClipboardHistoryMaxCount, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            desktopXaml,
            StringComparison.Ordinal);
        Assert.Contains("GlassChipListBoxItemStyle", desktopXaml, StringComparison.Ordinal);
        Assert.Contains("Settings.ClipboardHistoryPrivacyHint", desktopXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"GlassChipListBoxItemStyle\"", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("<Trigger Property=\"IsSelected\" Value=\"True\">", sharedTheme, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectClipboardHistoryLimitCommand", desktopXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CollapsedMainWindow_ShouldUsePurposeBuiltCompactDashboard()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");
        var mainViewModelCode = ReadProjectFile("UniDesk", "ViewModels", "MainWindowViewModel.cs");
        var timeWeatherXaml = ReadProjectFile("UniDesk", "Controls", "TimeWeatherModuleView.xaml");
        var sharedTheme = ReadProjectFile("UniDesk", "Resources", "Themes", "Shared.xaml");

        Assert.Contains("private const double CollapsedPanelHeight = 178;", mainCode, StringComparison.Ordinal);
        Assert.Contains("Element = (FrameworkElement?)TimeWeatherModule", mainCode, StringComparison.Ordinal);
        Assert.DoesNotContain("modules.Take(1)", mainCode, StringComparison.Ordinal);
        Assert.Contains(
            "HardwareMonitor.IsEnabled = IsPanelCollapsed || IsModuleEnabled(DashboardModuleIds.HardwareMonitor)",
            mainViewModelCode,
            StringComparison.Ordinal);
        Assert.Contains(
            "TimeWeather.IsEnabled = IsPanelCollapsed || IsModuleEnabled(DashboardModuleIds.TimeWeather)",
            mainViewModelCode,
            StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExpandedHeaderActions\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompactMoreButton\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"CompactMoreButton_OnClick\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("CompactMoreButton_OnClick", mainCode, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{DynamicResource Common.More}\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleTopMostCommand}\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleWindowLockCommand}\"", mainXaml, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"CompactStatusStrip\"", timeWeatherXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompactHardwareSummary\"", timeWeatherXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Width\" Value=\"56\"/>", timeWeatherXaml, StringComparison.Ordinal);
        var hardwareSummary = Regex.Match(
            timeWeatherXaml,
            "<Border x:Name=\"CompactHardwareSummary\"[\\s\\S]*?</Border>");
        Assert.True(hardwareSummary.Success);
        Assert.DoesNotContain("Background=", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"CompactHardwareLabelStyle\"", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"CompactHardwareValueStyle\"", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Equal(
            4,
            hardwareSummary.Value.Split(
                "Style=\"{StaticResource CompactHardwareLabelStyle}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            4,
            hardwareSummary.Value.Split(
                "Style=\"{StaticResource CompactHardwareValueStyle}\"",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("<Grid.ColumnDefinitions>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("Width=\"52\"", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"22\"/>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"Auto\"/>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Left\"/>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"3,0,0,0\"/>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("<Setter Property=\"HorizontalAlignment\" Value=\"Right\"/>", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("HardwareMonitor.SystemCpuUsageText", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("HardwareMonitor.SystemCpuTemperatureValueText", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("HardwareMonitor.SystemGpuTemperatureValueText", hardwareSummary.Value, StringComparison.Ordinal);
        Assert.Contains("HardwareMonitor.SystemMemoryUsageText", hardwareSummary.Value, StringComparison.Ordinal);

        var weatherSummary = Regex.Match(
            timeWeatherXaml,
            "<Grid x:Name=\"WeatherSummary\"[\\s\\S]*?x:Name=\"QWeatherAttributionLink\"[\\s\\S]*?</Grid>");
        Assert.True(weatherSummary.Success);
        Assert.Contains("ClipToBounds=\"True\"", weatherSummary.Value, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"6,0,0,0\"/>", weatherSummary.Value, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WeatherLocationStrip\"", weatherSummary.Value, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", weatherSummary.Value, StringComparison.Ordinal);

        var statusStrip = Regex.Match(
            timeWeatherXaml,
            "<Border x:Name=\"CompactStatusStrip\"[\\s\\S]*?</Border>");
        Assert.True(statusStrip.Success);
        Assert.Contains("Todos.CollapsedPanelTodo.Title", statusStrip.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("HardwareMonitor.", statusStrip.Value, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Height\" Value=\"128\"/>", timeWeatherXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\"/>", timeWeatherXaml, StringComparison.Ordinal);

        Assert.Contains("<Setter Property=\"FocusVisualStyle\" Value=\"{x:Null}\"/>", sharedTheme, StringComparison.Ordinal);
        Assert.Contains("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", sharedTheme, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentWindows_ShouldCapHeightToTheLogicalWorkArea()
    {
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");
        var settingsCode = ReadProjectFile("UniDesk", "SettingsWindow.xaml.cs");
        var mainViewModelCode = ReadProjectFile("UniDesk", "ViewModels", "MainWindowViewModel.cs");
        var monitorCode = ReadProjectFile("UniDesk", "Helpers", "MonitorWorkAreaProvider.cs");

        Assert.Contains("GetUsableWorkAreaHeight", mainCode, StringComparison.Ordinal);
        Assert.Contains("workArea.Height - WorkAreaMargin", settingsCode, StringComparison.Ordinal);
        Assert.Contains("Math.Max(MinimumCompactHeight", mainCode, StringComparison.Ordinal);
        Assert.Contains("_monitorWorkAreas.GetForPixelPoint", mainCode, StringComparison.Ordinal);
        Assert.Contains("_monitorWorkAreas.GetForWindow", mainCode, StringComparison.Ordinal);
        Assert.Contains("_monitorWorkAreas.GetForWindow(targetHandle)", settingsCode, StringComparison.Ordinal);
        Assert.Contains("MonitorWorkAreaGeometry.Clamp", settingsCode, StringComparison.Ordinal);
        Assert.Contains("WindowStartupLocation.Manual", mainViewModelCode, StringComparison.Ordinal);
        Assert.Contains("GetDpiForMonitor", monitorCode, StringComparison.Ordinal);
        Assert.Contains("MonitorFromPoint", monitorCode, StringComparison.Ordinal);
        Assert.Contains("MonitorFromRect", monitorCode, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemParameters.VirtualScreen", mainCode, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemParameters.WorkArea", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_MonitorTransition_ShouldNotClampOrPersistDuringDrag()
    {
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");
        var handler = Regex.Match(
            mainCode,
            "private void MainWindow_OnLocationChanged[\\s\\S]*?(?=private void SearchButton_OnClick)");

        Assert.True(handler.Success);
        Assert.Contains("_currentMonitor", handler.Value, StringComparison.Ordinal);
        Assert.Contains("_isDragging", handler.Value, StringComparison.Ordinal);
        Assert.Contains("clampPosition: !_isDragging", handler.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ClampToVisibleWorkArea", handler.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveWindowPosition", handler.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ActualSize_ShouldNotBindDirectlyToPreferredSize()
    {
        var mainXaml = ReadProjectFile("UniDesk", "MainWindow.xaml");
        var mainCode = ReadProjectFile("UniDesk", "MainWindow.xaml.cs");

        Assert.DoesNotContain("Height=\"{Binding PanelHeight", mainXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"{Binding PanelWidth", mainXaml, StringComparison.Ordinal);
        Assert.Contains("PanelSizePolicy.ClampActualSize", mainCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsSizeActions_ShouldUseCurrentMonitorRecommendation()
    {
        var settingsCode = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var fitAction = Regex.Match(
            settingsCode,
            "private void FitCurrentScreen\\(\\)[\\s\\S]*?(?=\\s*\\[RelayCommand\\])");
        var resetAction = Regex.Match(
            settingsCode,
            "private void ResetToDefaults\\(\\)[\\s\\S]*?(?=\\s*\\[RelayCommand\\])");

        Assert.True(fitAction.Success);
        Assert.True(resetAction.Success);
        Assert.Contains("PanelSizePolicy.GetRecommendedSize", fitAction.Value, StringComparison.Ordinal);
        Assert.Contains("PanelSizePolicy.GetRecommendedSize", resetAction.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("PanelWidth = 320", resetAction.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("PanelHeight = 702", resetAction.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void MainApplicationManifest_ShouldDeclarePerMonitorV2DpiAwareness()
    {
        var manifest = ReadProjectFile("UniDesk", "app.manifest");

        Assert.Contains(
            "<dpiAware xmlns=\"http://schemas.microsoft.com/SMI/2005/WindowsSettings\">true/pm</dpiAware>",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "<dpiAwareness xmlns=\"http://schemas.microsoft.com/SMI/2016/WindowsSettings\">PerMonitorV2,PerMonitor</dpiAwareness>",
            manifest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WeatherCredentialFailures_ShouldUseLocalizedUiText()
    {
        var settingsCode = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var failureBranch = Regex.Match(
            settingsCode,
            "if \\(!validation\\.IsValid\\)[\\s\\S]{0,600}?return;");

        Assert.True(failureBranch.Success);
        Assert.Contains("L(\"Settings.WeatherCredentialValidationFailed\")", failureBranch.Value, StringComparison.Ordinal);
        Assert.Contains("Logger.LogWarning", failureBranch.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowWarningMessage(validation.Message", failureBranch.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsFailures_ShouldLogInternalDetailsAndShowLocalizedGenericText()
    {
        var settingsCode = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var clearHistoryMethod = Regex.Match(
            settingsCode,
            "private async Task ClearClipboardHistoryFromSettingsAsync\\(\\)[\\s\\S]{0,1000}?\\n    }\\r?\\n\\r?\\n    \\[RelayCommand\\]");

        Assert.True(clearHistoryMethod.Success);
        Assert.Contains("Logger.LogError(ex, \"SettingsViewModel.ClearClipboardHistory\")", clearHistoryMethod.Value, StringComparison.Ordinal);
        Assert.Contains("L(\"QuickText.ClearHistoryFailed\")", clearHistoryMethod.Value, StringComparison.Ordinal);
        Assert.Contains("Logger.LogError(ex, \"SettingsViewModel.Save\")", settingsCode, StringComparison.Ordinal);
        Assert.Contains("ShowErrorMessage(L(\"Settings.SaveFailed\"))", settingsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings.SaveFailedFormat\", ex.Message", settingsCode, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            "_localizationService\\.Format\\(\"Settings\\.[^\"]+\", ex\\.Message\\)",
            settingsCode);

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            foreach (var key in new[]
                     {
                         "Settings.SaveFailed",
                         "Settings.BackupFailed",
                         "Settings.RestoreFailed",
                         "Settings.RestoreAppliedRefreshFailed",
                         "Settings.ResetLayoutFailed",
                         "Settings.HardwareDiagnosticsFailed",
                         "Settings.OpenFailed",
                         "Settings.ApplyAfterSaveFailed",
                         "Settings.ClipboardTrimFailed",
                         "QuickText.ClearHistoryFailed"
                     })
            {
                Assert.Contains($"x:Key=\"{key}\"", resources, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void SettingsSave_ShouldPersistOneAtomicBatchBeforeDerivedMaintenance()
    {
        var settingsCode = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var saveMethod = Regex.Match(
            settingsCode,
            "private async Task Save\\(\\)[\\s\\S]*?RequestClose\\?\\.Invoke\\(this, true\\);[\\s\\S]*?\\n    }");

        Assert.True(saveMethod.Success);
        Assert.Contains("SaveBatchAsync(settingsBatch)", saveMethod.Value, StringComparison.Ordinal);
        Assert.Contains("ApplyModuleSettings(savedModuleSettings!, persist: false)", saveMethod.Value, StringComparison.Ordinal);
        Assert.True(
            saveMethod.Value.IndexOf("SaveBatchAsync(settingsBatch)", StringComparison.Ordinal) <
            saveMethod.Value.IndexOf("ApplyModuleSettings(savedModuleSettings!, persist: false)", StringComparison.Ordinal));
        Assert.DoesNotContain("FlushPendingSavesAsync", saveMethod.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyModuleSettings(BuildModuleSettings(), persist: true)", saveMethod.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCityAsync", saveMethod.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("TrimClipboardHistoryAsync", saveMethod.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_ShouldNotReportCommittedDataAsFailedWhenUiRefreshFails()
    {
        var settingsCode = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var restoreMethod = Regex.Match(
            settingsCode,
            "private async Task RestoreTodosAsync\\(\\)[\\s\\S]*?\\n    }\\r?\\n\\r?\\n    \\[RelayCommand\\]");

        Assert.True(restoreMethod.Success);
        Assert.Contains("SettingsViewModel.Restore.ApplyImport", restoreMethod.Value, StringComparison.Ordinal);
        Assert.Contains("SettingsViewModel.Restore.RefreshAfterCommit", restoreMethod.Value, StringComparison.Ordinal);
        Assert.Contains("Settings.RestoreAppliedRefreshFailed", restoreMethod.Value, StringComparison.Ordinal);
        Assert.Contains("await _settingsService.ReloadCacheAsync()", restoreMethod.Value, StringComparison.Ordinal);
        Assert.True(
            restoreMethod.Value.IndexOf("ApplyImportAsync", StringComparison.Ordinal) <
            restoreMethod.Value.IndexOf("RefreshAfterCommit", StringComparison.Ordinal));
    }

    [Fact]
    public void WeatherRuntimeFailures_ShouldLogDetailsAndShowLocalizedGenericText()
    {
        var weatherCode = ReadProjectFile("UniDesk", "Services", "WeatherService.cs");

        Assert.Contains("Logger.LogError(ex, \"WeatherService.GetWeather.Network\")", weatherCode, StringComparison.Ordinal);
        Assert.Contains("L(\"Weather.NetworkRequestFailed\"", weatherCode, StringComparison.Ordinal);
        Assert.Contains("Logger.LogError(ex, \"WeatherService.GetWeather.Unknown\")", weatherCode, StringComparison.Ordinal);
        Assert.Contains("L(\"Weather.GetWeatherFailed\"", weatherCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Weather.NetworkRequestFailedFormat", weatherCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Weather.GetWeatherFailedFormat", weatherCode, StringComparison.Ordinal);

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            Assert.Contains("x:Key=\"Weather.NetworkRequestFailed\"", resources, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"Weather.GetWeatherFailed\"", resources, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"Settings.WeatherApplyFailed\"", resources, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WeatherPanel_ShouldExposeQWeatherAttributionInEveryDisplayModeAndLanguage()
    {
        var timeWeatherXaml = ReadProjectFile("UniDesk", "Controls", "TimeWeatherModuleView.xaml");

        Assert.Contains("x:Name=\"QWeatherAttributionLink\"", timeWeatherXaml, StringComparison.Ordinal);
        Assert.Contains("NavigateUri=\"https://www.qweather.com\"", timeWeatherXaml, StringComparison.Ordinal);
        Assert.Contains(
            "RequestNavigate=\"QWeatherAttributionLink_OnRequestNavigate\"",
            timeWeatherXaml,
            StringComparison.Ordinal);
        Assert.Contains("{DynamicResource Weather.QWeatherAttribution}", timeWeatherXaml, StringComparison.Ordinal);

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            Assert.Contains("x:Key=\"Weather.QWeatherAttribution\"", resources, StringComparison.Ordinal);
        }
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
