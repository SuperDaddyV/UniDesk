using UniDesk.Helpers;

namespace UniDesk.Services;

public class SettingsService : ISettingsService, IDisposable
{
    private const string WeatherApiKeySetting = "WeatherApiKey";
    private readonly IDatabaseService _databaseService;
    private readonly IUserDataProtector _userDataProtector;
    private readonly Dictionary<string, string?> _cache = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly Dictionary<string, PendingWrite> _pendingWrites = new();
    private readonly Dictionary<string, long> _latestWriteVersions = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();
    private CancellationTokenSource? _flushCts;
    private Task? _flushTask;
    private long _writeVersion;
    private int _cacheRefreshVersion;
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
        await Task.Run(() => _databaseService.InitializeAsync()).ConfigureAwait(false);
        await ReloadCacheAsync().ConfigureAwait(false);
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

        return null;
    }

    public async Task SetSettingAsync(string key, string? value)
    {
        long version;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            version = ++_writeVersion;
            _latestWriteVersions[key] = version;
            _pendingWrites.Remove(key);
        }

        await _saveLock.WaitAsync();
        try
        {
            await SaveSettingToDatabaseAsync(key, value);
            lock (_stateLock)
            {
                if (IsLatestWrite(key, version))
                {
                    _cache[key] = value;
                }
            }
        }
        finally
        {
            _saveLock.Release();
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
            if (_disposed)
            {
                return;
            }

            ++_cacheRefreshVersion;
            _cache.Clear();
        }
    }

    public async Task ReloadCacheAsync()
    {
        int refreshVersion;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            refreshVersion = ++_cacheRefreshVersion;
        }

        await Task.Run(() => RefreshCacheAsync(refreshVersion)).ConfigureAwait(false);
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
            var version = ++_writeVersion;
            _latestWriteVersions[key] = version;
            _cache[key] = value;
            _pendingWrites[key] = new PendingWrite(value, version);
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
            Dictionary<string, PendingWrite> batch;
            lock (_stateLock)
            {
                if (_pendingWrites.Count == 0) return;
                batch = new Dictionary<string, PendingWrite>(_pendingWrites);
                _pendingWrites.Clear();
            }

            try
            {
                var values = GetLatestValues(batch);
                if (values.Count > 0)
                {
                    await SaveSettingsBatchToDatabaseAsync(values);
                    UpdateCacheFromSuccessfulWrites(batch);
                }
            }
            catch
            {
                lock (_stateLock)
                {
                    foreach (var (key, pendingWrite) in batch)
                    {
                        if (IsLatestWrite(key, pendingWrite.Version) &&
                            !_pendingWrites.ContainsKey(key))
                        {
                            _pendingWrites[key] = pendingWrite;
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

    public async Task SaveBatchAsync(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        await _saveLock.WaitAsync();
        try
        {
            Dictionary<string, PendingWrite> pendingBeforeSave;
            Dictionary<string, PendingWrite> batch;
            lock (_stateLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                pendingBeforeSave = new Dictionary<string, PendingWrite>(_pendingWrites);
                batch = new Dictionary<string, PendingWrite>(pendingBeforeSave);
                foreach (var (key, value) in values)
                {
                    var version = ++_writeVersion;
                    _latestWriteVersions[key] = version;
                    batch[key] = new PendingWrite(value, version);
                }

                _pendingWrites.Clear();
            }

            try
            {
                var latestValues = GetLatestValues(batch);
                if (latestValues.Count > 0)
                {
                    await SaveSettingsBatchToDatabaseAsync(latestValues);
                    UpdateCacheFromSuccessfulWrites(batch);
                }
            }
            catch
            {
                lock (_stateLock)
                {
                    foreach (var (key, pendingWrite) in pendingBeforeSave)
                    {
                        if (IsLatestWrite(key, pendingWrite.Version) &&
                            !_pendingWrites.ContainsKey(key))
                        {
                            _pendingWrites[key] = pendingWrite;
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

    private Task SaveSettingsBatchToDatabaseAsync(IReadOnlyDictionary<string, string?> values) =>
        _databaseService.ExecuteInTransactionAsync(async session =>
        {
            foreach (var (key, value) in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    await session.ExecuteNonQueryAsync(
                        "DELETE FROM Settings WHERE Key = @p0",
                        key);
                }
                else
                {
                    await session.ExecuteNonQueryAsync(
                        "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
                        key,
                        EncodeForStorage(key, value));
                }
            }

            return true;
        });

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

    private async Task<Dictionary<string, string?>> LoadAllSettingsAsync()
    {
        var settings = await _databaseService.QueryAsync(
            "SELECT Key, Value FROM Settings ORDER BY Key",
            reader => new KeyValuePair<string, string?>(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1)));

        return settings.ToDictionary(
            setting => setting.Key,
            setting => DecodeFromStorage(setting.Key, setting.Value),
            StringComparer.Ordinal);
    }

    private async Task RefreshCacheAsync(int refreshVersion)
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            long writeVersion;
            lock (_stateLock)
            {
                writeVersion = _writeVersion;
            }

            var values = await LoadAllSettingsAsync().ConfigureAwait(false);
            lock (_stateLock)
            {
                if (_disposed || refreshVersion != _cacheRefreshVersion)
                {
                    return;
                }

                foreach (var key in _cache.Keys.ToList())
                {
                    if (!_pendingWrites.ContainsKey(key) &&
                        (!_latestWriteVersions.TryGetValue(key, out var version) ||
                         version <= writeVersion) &&
                        !values.ContainsKey(key))
                    {
                        _cache.Remove(key);
                    }
                }

                foreach (var (key, value) in values)
                {
                    if (!_latestWriteVersions.TryGetValue(key, out var version) ||
                        version <= writeVersion)
                    {
                        _cache[key] = value;
                    }
                }

                foreach (var (key, pendingWrite) in _pendingWrites)
                {
                    _cache[key] = pendingWrite.Value;
                }
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private Dictionary<string, string?> GetLatestValues(
        IReadOnlyDictionary<string, PendingWrite> batch)
    {
        lock (_stateLock)
        {
            return batch
                .Where(item => IsLatestWrite(item.Key, item.Value.Version))
                .ToDictionary(item => item.Key, item => item.Value.Value, StringComparer.Ordinal);
        }
    }

    private void UpdateCacheFromSuccessfulWrites(
        IReadOnlyDictionary<string, PendingWrite> batch)
    {
        lock (_stateLock)
        {
            foreach (var (key, pendingWrite) in batch)
            {
                if (IsLatestWrite(key, pendingWrite.Version))
                {
                    _cache[key] = pendingWrite.Value;
                }
            }
        }
    }

    private bool IsLatestWrite(string key, long version) =>
        _latestWriteVersions.TryGetValue(key, out var latestVersion) &&
        latestVersion == version;

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

    private sealed record PendingWrite(string? Value, long Version);

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
