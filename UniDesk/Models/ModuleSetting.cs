namespace UniDesk.Models;

public sealed class ModuleSetting
{
    public string ModuleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }

    public ModuleSetting Clone() => new()
    {
        ModuleId = ModuleId,
        DisplayName = DisplayName,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder
    };
}

public static class DashboardModuleIds
{
    public const string TimeWeather = "TimeWeather";
    public const string HardwareMonitor = "HardwareMonitor";
    public const string Shortcuts = "Shortcuts";
    public const string Todos = "Todos";
    public const string QuickNotes = "QuickNotes";
    public const string QuickText = "QuickText";
    public const string ModelRadar = "ModelRadar";
}

public static class DashboardModuleCatalog
{
    public const string SettingsKey = "ModuleSettings";

    public static IReadOnlyList<ModuleSetting> DefaultModules { get; } =
    [
        new()
        {
            ModuleId = DashboardModuleIds.TimeWeather,
            DisplayName = "时间天气",
            IsEnabled = true,
            SortOrder = 0
        },
        new()
        {
            ModuleId = DashboardModuleIds.HardwareMonitor,
            DisplayName = "硬件监视",
            IsEnabled = true,
            SortOrder = 1
        },
        new()
        {
            ModuleId = DashboardModuleIds.Shortcuts,
            DisplayName = "快捷方式",
            IsEnabled = false,
            SortOrder = 2
        },
        new()
        {
            ModuleId = DashboardModuleIds.Todos,
            DisplayName = "待办事项",
            IsEnabled = true,
            SortOrder = 3
        },
        new()
        {
            ModuleId = DashboardModuleIds.QuickNotes,
            DisplayName = "快速便签",
            IsEnabled = true,
            SortOrder = 4
        },
        new()
        {
            ModuleId = DashboardModuleIds.QuickText,
            DisplayName = "快捷文本",
            IsEnabled = false,
            SortOrder = 5
        },
        new()
        {
            ModuleId = DashboardModuleIds.ModelRadar,
            DisplayName = "模型雷达",
            IsEnabled = false,
            SortOrder = 6
        }
    ];

    public static IReadOnlySet<string> KnownModuleIds { get; } =
        DefaultModules.Select(module => module.ModuleId).ToHashSet(StringComparer.Ordinal);

    public static List<ModuleSetting> CreateDefaultModules() =>
        DefaultModules.Select(module => module.Clone()).ToList();

    public static string GetDisplayName(string moduleId) =>
        DefaultModules.FirstOrDefault(module => module.ModuleId == moduleId)?.DisplayName ?? moduleId;

    public static List<ModuleSetting> Normalize(IEnumerable<ModuleSetting>? modules)
    {
        var incoming = (modules ?? [])
            .Select((module, index) => new { Module = module, Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Module.ModuleId))
            .OrderBy(item => item.Module.SortOrder)
            .ThenBy(item => item.Index)
            .GroupBy(item => item.Module.ModuleId.Trim(), StringComparer.Ordinal)
            .Select(group => group.First().Module)
            .ToList();

        var normalized = new List<ModuleSetting>(DefaultModules.Count + incoming.Count);
        foreach (var module in incoming)
        {
            var moduleId = module.ModuleId.Trim();
            var defaultModule = DefaultModules.FirstOrDefault(candidate => candidate.ModuleId == moduleId);
            normalized.Add(new ModuleSetting
            {
                ModuleId = moduleId,
                DisplayName = defaultModule?.DisplayName ??
                              (string.IsNullOrWhiteSpace(module.DisplayName)
                                  ? moduleId
                                  : module.DisplayName.Trim()),
                IsEnabled = module.IsEnabled,
                SortOrder = normalized.Count
            });
        }

        var existingIds = normalized
            .Select(module => module.ModuleId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var defaultModule in DefaultModules)
        {
            if (!existingIds.Contains(defaultModule.ModuleId))
            {
                var missing = defaultModule.Clone();
                missing.SortOrder = normalized.Count;
                normalized.Add(missing);
            }
        }

        return normalized;
    }
}
