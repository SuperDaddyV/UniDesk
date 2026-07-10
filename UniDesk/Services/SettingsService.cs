using UniDesk.Helpers;

namespace UniDesk.Services;

public class SettingsService : ISettingsService, IDisposable
{
    private readonly IDatabaseService _databaseService;
    private readonly Dictionary<string, string?> _cache = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly Dictionary<string, string?> _pendingWrites = new();
    private CancellationTokenSource? _flushCts;

    public SettingsService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task InitializeAsync()
    {
        await _databaseService.InitializeAsync();
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return value;
        }

        value = await GetSettingFromDatabaseAsync(key);
        _cache[key] = value;
        return value;
    }

    public string? GetSetting(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return value;
        }

        value = GetSettingFromDatabaseAsync(key).GetAwaiter().GetResult();
        _cache[key] = value;
        return value;
    }

    public async Task SetSettingAsync(string key, string? value)
    {
        _cache[key] = value;
        await SaveSettingToDatabaseAsync(key, value);
    }

    public void SetSetting(string key, string? value)
    {
        _cache[key] = value;
        QueueSave(key, value);
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        var value = GetSetting(key);
        if (value == null) return defaultValue;
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public string GetValue(string key, string defaultValue)
    {
        return GetSetting(key) ?? defaultValue;
    }

    public void SetValue(string key, string value)
    {
        SetSetting(key, value);
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }

    public void FlushPendingSaves()
    {
        try
        {
            FlushPendingSavesAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void QueueSave(string key, string? value)
    {
        lock (_pendingWrites)
        {
            _pendingWrites[key] = value;
        }

        _flushCts?.Cancel();
        _flushCts?.Dispose();
        _flushCts = new CancellationTokenSource();
        var token = _flushCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(50, token);
                if (!token.IsCancellationRequested)
                {
                    await FlushPendingSavesAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    public async Task FlushPendingSavesAsync()
    {
        Dictionary<string, string?> batch;
        lock (_pendingWrites)
        {
            if (_pendingWrites.Count == 0) return;
            batch = new Dictionary<string, string?>(_pendingWrites);
            _pendingWrites.Clear();
        }

        await _saveLock.WaitAsync();
        try
        {
            foreach (var (key, value) in batch)
            {
                await SaveSettingToDatabaseAsync(key, value);
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task<string?> GetSettingFromDatabaseAsync(string key)
    {
        try
        {
            return await _databaseService.QuerySingleAsync(
                "SELECT Value FROM Settings WHERE Key = @p0",
                reader => reader.GetString(0),
                key);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"SettingsService.Get({key})");
            return null;
        }
    }

    private async Task SaveSettingToDatabaseAsync(string key, string? value)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
            {
                await _databaseService.ExecuteNonQueryAsync(
                    "DELETE FROM Settings WHERE Key = @p0",
                    key);
            }
            else
            {
                await _databaseService.ExecuteNonQueryAsync(
                    "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
                    key, value);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"SettingsService.Set({key})");
        }
    }

    public void Dispose()
    {
        _flushCts?.Cancel();
        _flushCts?.Dispose();
        FlushPendingSaves();
        _saveLock.Dispose();
    }
}
