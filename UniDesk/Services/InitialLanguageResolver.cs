using System.Globalization;

namespace UniDesk.Services;

public static class InitialLanguageResolver
{
    private const string ArgumentPrefix = "--initial-language=";

    public static string Resolve(
        IEnumerable<string>? arguments,
        CultureInfo windowsUiCulture)
    {
        ArgumentNullException.ThrowIfNull(windowsUiCulture);

        var installerLanguage = arguments?
            .FirstOrDefault(argument => argument.StartsWith(
                ArgumentPrefix,
                StringComparison.OrdinalIgnoreCase));
        if (installerLanguage != null)
        {
            var mapped = MapLanguage(installerLanguage[ArgumentPrefix.Length..]);
            if (mapped != null)
            {
                return mapped;
            }
        }

        return MapLanguage(windowsUiCulture.Name)
            ?? MapLanguage(windowsUiCulture.TwoLetterISOLanguageName)
            ?? ILocalizationService.DefaultLanguage;
    }

    private static string? MapLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim();
        if (normalized.Equals("chinesesimp", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }

        if (normalized.Equals("english", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        if (normalized.Equals("japanese", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return "ja-JP";
        }

        if (normalized.Equals("spanish", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return "es-ES";
        }

        return null;
    }
}
