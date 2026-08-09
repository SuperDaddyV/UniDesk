namespace UniDesk.Services;

public interface ISettingsService
{
    Task InitializeAsync();
    Task<string?> GetSettingAsync(string key);
    string? GetSetting(string key);
    Task SetSettingAsync(string key, string? value);
    void SetSetting(string key, string? value);
    T GetSetting<T>(string key, T defaultValue);
    string GetValue(string key, string defaultValue);
    void SetValue(string key, string value);
    void InvalidateCache();
    Task FlushPendingSavesAsync();
    Task SaveBatchAsync(IReadOnlyDictionary<string, string?> values);
}
