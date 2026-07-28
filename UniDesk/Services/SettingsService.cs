using UniDesk.Helpers;

namespace UniDesk.Services;

public class SettingsService : ISettingsService, IDisposable
{
    private const string WeatherApiKeySetting = "WeatherApiKey";
    private readonly IDatabaseService _databaseService;
    private readonly IUserDataProtector _userDataProtector;
    private readonly Dictionary<string, string?> _cache = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly Dictionary<string, string?> _pendingWrites = new();
    private readonly object _stateLock = new();
    private CancellationTokenSource? _flushCts;
    private Task? _flushTask;
    private bool _disposed;

    public SettingsService(IDatabaseService databaseService)
        : this(databaseService, new DpapiUserDataProtector())
    {
    }

    public SettingsService(
        IDatabaseService databaseService,
        IUserDataProtector userDataProtector)
    {
        _databaseService = databaseService;
        _userDataProtector = userDataProtector;
    }

    public async Task InitializeAsync()
    {
        await _databaseService.InitializeAsync();
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        lock (_stateLock)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }
        }

        var value = await GetSettingFromDatabaseAsync(key);
        lock (_stateLock)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }

            _cache[key] = value;
        }

        return value;
    }

    public string? GetSetting(string key)
    {
        lock (_stateLock)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }
        }

        var value = GetSettingFromDatabaseAsync(key).GetAwaiter().GetResult();
        lock (_stateLock)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }

            _cache[key] = value;
        }

        return value;
    }

    public async Task SetSettingAsync(string key, string? value)
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        await SaveSettingToDatabaseAsync(key, value);
        lock (_stateLock)
        {
            _cache[key] = value;
        }
    }

    public void SetSetting(string key, string? value)
    {
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
        lock (_stateLock)
        {
            _cache.Clear();
        }
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsService.FlushPendingSaves");
        }
    }

    private void QueueSave(string key, string? value)
    {
        CancellationTokenSource? previousCts;
        CancellationTokenSource currentCts;

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            currentCts = new CancellationTokenSource();
            _cache[key] = value;
            _pendingWrites[key] = value;
            previousCts = _flushCts;
            _flushCts = currentCts;
            _flushTask = Task.Run(() => FlushAfterDelayAsync(currentCts));
        }

        previousCts?.Cancel();
    }

    private async Task FlushAfterDelayAsync(CancellationTokenSource cancellationSource)
    {
        try
        {
            await Task.Delay(50, cancellationSource.Token).ConfigureAwait(false);
            if (!cancellationSource.IsCancellationRequested)
            {
                await FlushPendingSavesAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsService.FlushAfterDelay");
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_flushCts, cancellationSource))
                {
                    _flushCts = null;
                }
            }

            cancellationSource.Dispose();
        }
    }

    public async Task FlushPendingSavesAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            Dictionary<string, string?> batch;
            lock (_stateLock)
            {
                if (_pendingWrites.Count == 0) return;
                batch = new Dictionary<string, string?>(_pendingWrites);
                _pendingWrites.Clear();
            }

            try
            {
                foreach (var (key, value) in batch)
                {
                    await SaveSettingToDatabaseAsync(key, value);
                }
            }
            catch
            {
                lock (_stateLock)
                {
                    foreach (var (key, value) in batch)
                    {
                        if (!_pendingWrites.ContainsKey(key))
                        {
                            _pendingWrites[key] = value;
                        }
                    }
                }

                throw;
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
            var storedValue = await _databaseService.QuerySingleAsync(
                "SELECT Value FROM Settings WHERE Key = @p0",
                reader => reader.GetString(0),
                key);
            return DecodeFromStorage(key, storedValue);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"SettingsService.Get({key})");
            throw;
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
                var storedValue = EncodeForStorage(key, value);
                await _databaseService.ExecuteNonQueryAsync(
                    "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
                    key, storedValue);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"SettingsService.Set({key})");
            throw;
        }
    }

    private string EncodeForStorage(string key, string value) =>
        string.Equals(key, WeatherApiKeySetting, StringComparison.Ordinal)
            ? _userDataProtector.Protect(value)
            : value;

    private string? DecodeFromStorage(string key, string? storedValue)
    {
        if (!string.Equals(key, WeatherApiKeySetting, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(storedValue) ||
            !_userDataProtector.IsProtected(storedValue))
        {
            return storedValue;
        }

        if (_userDataProtector.TryUnprotect(storedValue, out var plaintext))
        {
            return plaintext;
        }

        Logger.LogWarning(
            "WeatherApiKey 的受保护值无法由当前 Windows 用户解密。",
            "SettingsService.Get");
        return null;
    }

    public void Dispose()
    {
        CancellationTokenSource? flushCts;
        Task? flushTask;
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            flushCts = _flushCts;
            flushTask = _flushTask;
            _flushCts = null;
            _flushTask = null;
        }

        flushCts?.Cancel();
        if (flushTask != null)
        {
            flushTask.GetAwaiter().GetResult();
        }
        else
        {
            flushCts?.Dispose();
        }
        FlushPendingSaves();
    }
}
