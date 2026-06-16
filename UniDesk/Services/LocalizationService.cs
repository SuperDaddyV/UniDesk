using System.Globalization;
using System.Windows;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly HashSet<string> SupportedLanguageCodes =
        new(StringComparer.OrdinalIgnoreCase) { "zh-CN", "en-US", "ja-JP", "es-ES" };

    private readonly List<LanguageOption> _supportedLanguages =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English"),
        new("ja-JP", "日本語"),
        new("es-ES", "Español")
    ];

    private ResourceDictionary? _currentDictionary;
    private ResourceDictionary? _fallbackDictionary;

    public static ILocalizationService? Current { get; private set; }

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage { get; private set; } = ILocalizationService.DefaultLanguage;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo(ILocalizationService.DefaultLanguage);

    public IReadOnlyList<LanguageOption> SupportedLanguages => _supportedLanguages;

    public void Initialize(ISettingsService settingsService)
    {
        Current = this;
        var language = NormalizeLanguage(settingsService.GetValue(
            ILocalizationService.LanguageSettingKey,
            ILocalizationService.DefaultLanguage));
        SetLanguage(language);
        if (!string.Equals(
                settingsService.GetValue(ILocalizationService.LanguageSettingKey, ILocalizationService.DefaultLanguage),
                language,
                StringComparison.OrdinalIgnoreCase))
        {
            settingsService.SetValue(ILocalizationService.LanguageSettingKey, language);
        }
    }

    public string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return ILocalizationService.DefaultLanguage;
        }

        var normalized = language.Trim();
        return SupportedLanguageCodes.Contains(normalized)
            ? _supportedLanguages.First(option => string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase)).Code
            : ILocalizationService.DefaultLanguage;
    }

    public void SetLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        var culture = CultureInfo.GetCultureInfo(normalized);

        CurrentLanguage = normalized;
        CurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        ApplyResourceDictionary(normalized);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (_currentDictionary != null && _currentDictionary.Contains(key))
        {
            return _currentDictionary[key]?.ToString() ?? key;
        }

        _fallbackDictionary ??= LoadDictionary(ILocalizationService.DefaultLanguage);
        if (_fallbackDictionary.Contains(key))
        {
            return _fallbackDictionary[key]?.ToString() ?? key;
        }

        return key;
    }

    public string Format(string key, params object?[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private void ApplyResourceDictionary(string language)
    {
        try
        {
            var dictionary = LoadDictionary(language);
            _currentDictionary = dictionary;

            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                return;
            }

            for (var i = resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var source = resources.MergedDictionaries[i].Source?.OriginalString;
                if (!string.IsNullOrWhiteSpace(source) &&
                    source.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase))
                {
                    resources.MergedDictionaries.RemoveAt(i);
                }
            }

            resources.MergedDictionaries.Add(dictionary);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LocalizationService.ApplyResourceDictionary");
        }
    }

    private static ResourceDictionary LoadDictionary(string language)
    {
        return new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{language}.xaml", UriKind.Relative)
        };
    }
}
