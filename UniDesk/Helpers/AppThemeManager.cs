using System.Windows;

namespace UniDesk.Helpers;

public static class AppThemeManager
{
    private const string LightThemePath = "Resources/Themes/Light.xaml";
    private const string DarkThemePath = "Resources/Themes/Dark.xaml";
    private const string SharedThemePath = "Resources/Themes/Shared.xaml";

    public static void Apply(bool useLightTheme, string? schemeId)
    {
        if (Application.Current == null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var desiredPath = useLightTheme ? LightThemePath : DarkThemePath;
        var themeIndex = -1;
        for (var index = 0; index < dictionaries.Count; index++)
        {
            if (IsThemeDictionary(dictionaries[index]))
            {
                themeIndex = index;
                break;
            }
        }

        if (themeIndex < 0)
        {
            var sharedIndex = FindDictionaryIndex(dictionaries, SharedThemePath);
            dictionaries.Insert(
                sharedIndex >= 0 ? sharedIndex + 1 : 0,
                CreateDictionary(desiredPath));
        }
        else if (!HasSource(dictionaries[themeIndex], desiredPath))
        {
            dictionaries[themeIndex] = CreateDictionary(desiredPath);
        }

        AppColorSchemeCatalog.Apply(schemeId);
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary) =>
        HasSource(dictionary, LightThemePath) || HasSource(dictionary, DarkThemePath);

    private static int FindDictionaryIndex(
        IList<ResourceDictionary> dictionaries,
        string sourcePath)
    {
        for (var index = 0; index < dictionaries.Count; index++)
        {
            if (HasSource(dictionaries[index], sourcePath))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasSource(ResourceDictionary dictionary, string sourcePath) =>
        string.Equals(
            dictionary.Source?.OriginalString,
            sourcePath,
            StringComparison.OrdinalIgnoreCase);

    private static ResourceDictionary CreateDictionary(string sourcePath) =>
        new() { Source = new Uri(sourcePath, UriKind.Relative) };
}
