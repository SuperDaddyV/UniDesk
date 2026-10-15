using System.Windows;

namespace UniDesk.Models;

public sealed record CompactPanelLayout(bool ShowTimeWeather, bool ShowHardware, bool ShowModelRadar, bool ShowTodos)
{
    public bool HasStatus => ShowModelRadar || ShowTodos;
    public double TopHeight => ShowTimeWeather ? 96 : ShowHardware ? 64 : 0;
    public Thickness StatusMargin => new(0, TopHeight > 0 ? 8 : 0, 0, 0);
    public double ContentHeight => TopHeight + (HasStatus ? StatusMargin.Top + 4 +
        (ShowModelRadar ? 24 : 0) + (ShowTodos ? 24 : 0) : 0);
    // Title bar (38), window border (2), bottom inset (8).
    public double WindowHeight => 48 + ContentHeight;
    // Keep one flexible column for the spanning status strip even with no top modules.
    public GridLength ClockColumn => new(ShowTimeWeather || !ShowHardware ? 1 : 0, GridUnitType.Star);
    public GridLength HardwareColumn => ShowHardware
        ? ShowTimeWeather ? new GridLength(64) : new GridLength(1, GridUnitType.Star)
        : new GridLength(0);
    public GridLength WeatherColumn => new(ShowTimeWeather ? 1.1 : 0, GridUnitType.Star);

    public static CompactPanelLayout FromModules(IEnumerable<ModuleSetting> modules)
    {
        var enabled = modules.Where(DashboardModuleCatalog.IsCompactEnabled)
            .Select(module => module.ModuleId).ToHashSet(StringComparer.Ordinal);
        return new(enabled.Contains(DashboardModuleIds.TimeWeather), enabled.Contains(DashboardModuleIds.HardwareMonitor),
            enabled.Contains(DashboardModuleIds.ModelRadar), enabled.Contains(DashboardModuleIds.Todos));
    }
}
