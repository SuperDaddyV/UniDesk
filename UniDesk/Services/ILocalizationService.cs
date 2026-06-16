using System.Globalization;
using UniDesk.Models;

namespace UniDesk.Services;

public interface ILocalizationService
{
    const string LanguageSettingKey = "Language";
    const string DefaultLanguage = "zh-CN";

    event EventHandler? LanguageChanged;

    string CurrentLanguage { get; }
    CultureInfo CurrentCulture { get; }
    IReadOnlyList<LanguageOption> SupportedLanguages { get; }

    void Initialize(ISettingsService settingsService);
    string NormalizeLanguage(string? language);
    void SetLanguage(string? language);
    string GetString(string key);
    string Format(string key, params object?[] args);
}
