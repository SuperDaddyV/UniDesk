namespace UniDesk.Helpers;

public static class SystemThemeSelection
{
    public static bool IsLightTheme(object? registryValue) => registryValue switch
    {
        int value => value != 0,
        long value => value != 0,
        _ => true
    };

    public static string GetEffectiveScheme(
        bool followSystem,
        bool isSystemLight,
        string manualScheme,
        string lightScheme,
        string darkScheme) =>
        AppColorSchemeCatalog.NormalizeId(
            followSystem
                ? isSystemLight ? lightScheme : darkScheme
                : manualScheme);

    public static bool ShouldUseLightSurface(
        bool followSystem,
        bool isSystemLight,
        string? manualScheme)
    {
        if (followSystem)
        {
            return isSystemLight;
        }

        if (string.Equals(manualScheme, "System", StringComparison.OrdinalIgnoreCase))
        {
            return isSystemLight;
        }

        if (string.Equals(manualScheme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(manualScheme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = AppColorSchemeCatalog.NormalizeId(manualScheme);
        return normalized is not "DarkGrey" and not "Black";
    }
}
