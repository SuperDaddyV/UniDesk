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
}
