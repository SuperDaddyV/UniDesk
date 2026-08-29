using UniDesk.Models;
using Xunit;

namespace UniDesk.Tests;

public class ModuleSettingsTests
{
    [Fact]
    public void Normalize_ShouldReturnDefaultModules_WhenInputIsMissing()
    {
        var modules = DashboardModuleCatalog.Normalize(null);

        Assert.Equal(
            [
                DashboardModuleIds.TimeWeather,
                DashboardModuleIds.HardwareMonitor,
                DashboardModuleIds.Shortcuts,
                DashboardModuleIds.Todos,
                DashboardModuleIds.QuickNotes,
                DashboardModuleIds.QuickText,
                DashboardModuleIds.ModelRadar
            ],
            modules.Select(module => module.ModuleId));
        Assert.Equal(
            [
                DashboardModuleIds.TimeWeather,
                DashboardModuleIds.HardwareMonitor,
                DashboardModuleIds.Todos,
                DashboardModuleIds.QuickNotes
            ],
            modules.Where(module => module.IsEnabled).Select(module => module.ModuleId));
        Assert.Equal(
            [
                DashboardModuleIds.Shortcuts,
                DashboardModuleIds.QuickText,
                DashboardModuleIds.ModelRadar
            ],
            modules.Where(module => !module.IsEnabled).Select(module => module.ModuleId));
        Assert.Equal(6, Assert.Single(modules, module => module.ModuleId == DashboardModuleIds.ModelRadar).SortOrder);
    }

    [Fact]
    public void Normalize_ShouldAppendDisabledModelRadarToLegacyCustomOrder()
    {
        ModuleSetting[] legacyModules =
        [
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.QuickText,
                DisplayName = "快捷文本",
                IsEnabled = true,
                SortOrder = 0
            },
            new ModuleSetting
            {
                ModuleId = "FutureModule",
                DisplayName = "未来模块",
                IsEnabled = true,
                SortOrder = 1
            },
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.TimeWeather,
                DisplayName = "时间天气",
                IsEnabled = true,
                SortOrder = 2
            },
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.Todos,
                DisplayName = "待办事项",
                IsEnabled = false,
                SortOrder = 3
            },
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.HardwareMonitor,
                DisplayName = "硬件监视",
                IsEnabled = true,
                SortOrder = 4
            },
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.Shortcuts,
                DisplayName = "快捷方式",
                IsEnabled = true,
                SortOrder = 5
            },
            new ModuleSetting
            {
                ModuleId = DashboardModuleIds.QuickNotes,
                DisplayName = "快速便签",
                IsEnabled = true,
                SortOrder = 6
            }
        ];

        var modules = DashboardModuleCatalog.Normalize(legacyModules);

        Assert.Equal(
            [
                DashboardModuleIds.QuickText,
                "FutureModule",
                DashboardModuleIds.TimeWeather,
                DashboardModuleIds.Todos,
                DashboardModuleIds.HardwareMonitor,
                DashboardModuleIds.Shortcuts,
                DashboardModuleIds.QuickNotes,
                DashboardModuleIds.ModelRadar
            ],
            modules.Select(module => module.ModuleId));
        Assert.Equal("未来模块", modules[1].DisplayName);
        Assert.True(Assert.Single(modules, module => module.ModuleId == DashboardModuleIds.QuickText).IsEnabled);
        Assert.True(Assert.Single(modules, module => module.ModuleId == DashboardModuleIds.Shortcuts).IsEnabled);
        Assert.False(Assert.Single(modules, module => module.ModuleId == DashboardModuleIds.Todos).IsEnabled);
        Assert.False(Assert.Single(modules, module => module.ModuleId == DashboardModuleIds.ModelRadar).IsEnabled);
        Assert.Equal(7, modules[^1].SortOrder);
    }

    [Fact]
    public void Normalize_ShouldFillMissingKnownModules()
    {
        var modules = DashboardModuleCatalog.Normalize(
        [
            new()
            {
                ModuleId = DashboardModuleIds.Todos,
                DisplayName = "待办事项",
                IsEnabled = false,
                SortOrder = 0
            }
        ]);

        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.TimeWeather);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.HardwareMonitor);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.Shortcuts);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.Todos && !module.IsEnabled);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.QuickNotes);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.QuickText);
        Assert.Contains(modules, module => module.ModuleId == DashboardModuleIds.ModelRadar && !module.IsEnabled);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], modules.Select(module => module.SortOrder));
    }

    [Fact]
    public void Normalize_ShouldKeepUnknownModulesSafely()
    {
        var modules = DashboardModuleCatalog.Normalize(
        [
            new()
            {
                ModuleId = "FutureModule",
                DisplayName = "未来模块",
                IsEnabled = true,
                SortOrder = 0
            }
        ]);

        Assert.Equal("FutureModule", modules[0].ModuleId);
        Assert.Equal(DashboardModuleIds.ModelRadar, modules[^1].ModuleId);
    }
}
