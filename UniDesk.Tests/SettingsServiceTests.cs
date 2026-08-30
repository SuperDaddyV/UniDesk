using Xunit;
using UniDesk.Services;
using UniDesk.Helpers;
using System.IO;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class SettingsServiceTests
{
    private string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_settings.db");

    private DatabaseService GetDb()
    {
        return new DatabaseService($"Data Source={_testDbFile}");
    }

    private void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbFile))
            {
                File.Delete(_testDbFile);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultTheme()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var theme = await settingsService.GetSettingAsync("Theme");
        
        Assert.Equal("System", theme);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultWindowOpacity()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var opacity = await settingsService.GetSettingAsync("WindowOpacity");
        
        Assert.Equal("0.70", opacity);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultTopMost()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var topMost = await settingsService.GetSettingAsync("TopMost");
        
        Assert.Equal("true", topMost);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultPanelWidth()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var panelWidth = await settingsService.GetSettingAsync("PanelWidth");
        
        Assert.Equal("320", panelWidth);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultHotkey()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var hotkey = await settingsService.GetSettingAsync("Hotkey");
        
        Assert.Equal("Ctrl+Alt+Space", hotkey);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultAutoLocation()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var autoLocation = await settingsService.GetSettingAsync("AutoLocation");
        
        Assert.Equal("true", autoLocation);

        Cleanup();
    }

    [Fact]
    public async Task GetSetting_ShouldReturnDefaultStartup()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);

        await settingsService.InitializeAsync();

        var startup = await settingsService.GetSettingAsync("Startup");

        Assert.Equal("true", startup);
        
        Cleanup();
    }

    [Fact]
    public async Task SetSetting_ShouldUpdateValue()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        await settingsService.SetSettingAsync("Theme", "Dark");
        
        var theme = await settingsService.GetSettingAsync("Theme");
        
        Assert.Equal("Dark", theme);
        
        Cleanup();
    }

    [Fact]
    public async Task SetSettingAsync_AfterQueuedWrite_ShouldWinDeterministically()
    {
        var databaseService = new OrderedWriteDatabaseService();
        var settingsService = new SettingsService(databaseService);

        settingsService.SetValue("Theme", "queued");
        await databaseService.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var directWrite = settingsService.SetSettingAsync("Theme", "direct");
        databaseService.AllowFirstWrite.TrySetResult();
        await directWrite;
        await settingsService.FlushPendingSavesAsync();

        Assert.Equal("direct", databaseService.Values["Theme"]);
        Assert.Equal("direct", settingsService.GetValue("Theme", string.Empty));
        settingsService.Dispose();
    }

    [Fact]
    public async Task QueuedWrite_AfterSetSettingAsync_ShouldWinDeterministically()
    {
        var databaseService = new OrderedWriteDatabaseService();
        var settingsService = new SettingsService(databaseService);

        var directWrite = settingsService.SetSettingAsync("Theme", "direct");
        await databaseService.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        settingsService.SetValue("Theme", "queued");
        databaseService.AllowFirstWrite.TrySetResult();
        await directWrite;
        await settingsService.FlushPendingSavesAsync();

        Assert.Equal("queued", databaseService.Values["Theme"]);
        Assert.Equal("queued", settingsService.GetValue("Theme", string.Empty));
        settingsService.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPreloadAllSettingsForSynchronousReads()
    {
        var databaseService = new PreloadedDatabaseService
        {
            Values = new Dictionary<string, string?>
            {
                ["Theme"] = "Dark",
                ["PanelWidth"] = "480"
            }
        };
        var settingsService = new SettingsService(databaseService);
        var callerThreadId = 0;
        Task? initialization = null;
        using var started = new ManualResetEventSlim();
        var caller = new Thread(() =>
        {
            callerThreadId = Environment.CurrentManagedThreadId;
            initialization = settingsService.InitializeAsync();
            started.Set();
        });

        caller.Start();
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        await initialization!;
        caller.Join();

        Assert.NotEqual(callerThreadId, databaseService.InitializeThreadId);
        Assert.Equal("Dark", settingsService.GetValue("Theme", string.Empty));
        Assert.Equal(480, settingsService.GetSetting<int>("PanelWidth", 0));
        Assert.Equal(0, databaseService.QuerySingleCallCount);

        databaseService.Values["Theme"] = "Light";
        var reloadCallerThreadId = 0;
        Task? reload = null;
        started.Reset();
        var reloadCaller = new Thread(() =>
        {
            reloadCallerThreadId = Environment.CurrentManagedThreadId;
            reload = settingsService.ReloadCacheAsync();
            started.Set();
        });
        reloadCaller.Start();
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        await reload!;
        reloadCaller.Join();

        Assert.Equal("Light", settingsService.GetValue("Theme", string.Empty));
        Assert.Equal(0, databaseService.QuerySingleCallCount);
        Assert.NotEqual(reloadCallerThreadId, databaseService.QueryThreadId);
        settingsService.Dispose();
    }

    [Fact]
    public async Task GetSetting_Generic_ShouldReturnIntValue()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var panelWidth = settingsService.GetSetting<int>("PanelWidth", 0);
        
        Assert.Equal(320, panelWidth);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSetting_Generic_ShouldReturnBoolValue()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        
        await settingsService.InitializeAsync();
        
        var topMost = settingsService.GetSetting<bool>("TopMost", false);
        
        Assert.True(topMost);
        
        Cleanup();
    }

    [Fact]
    public async Task WeatherApiKey_ShouldBeProtectedInDatabaseAndPlaintextThroughService()
    {
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService, new FakeUserDataProtector());
        await settingsService.InitializeAsync();

        await settingsService.SetSettingAsync("WeatherApiKey", "weather-secret");

        var stored = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = @p0",
            reader => reader.GetString(0),
            "WeatherApiKey");
        settingsService.InvalidateCache();
        Assert.Equal("fake:v1:weather-secret", stored);
        Assert.Equal("weather-secret", await settingsService.GetSettingAsync("WeatherApiKey"));
        Cleanup();
    }

    [Fact]
    public async Task WeatherApiKey_WhenProtectedValueIsUnreadable_ShouldReturnNull()
    {
        var databaseService = GetDb();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
            "WeatherApiKey",
            "fake:v1:unreadable");
        var settingsService = new SettingsService(databaseService, new FakeUserDataProtector());

        Assert.Null(await settingsService.GetSettingAsync("WeatherApiKey"));
        Cleanup();
    }

    [Fact]
    public async Task GetSettingAsync_TransientReadFailure_ShouldNotCacheNull()
    {
        var databaseService = new RecoverableDatabaseService
        {
            RemainingReadFailures = 1
        };
        databaseService.Values["Theme"] = "Dark";
        var settingsService = new SettingsService(databaseService);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => settingsService.GetSettingAsync("Theme"));

        Assert.Equal("Dark", await settingsService.GetSettingAsync("Theme"));
        settingsService.Dispose();
    }

    [Fact]
    public async Task FlushPendingSavesAsync_WriteFailure_ShouldThrowAndRetryPendingValues()
    {
        var databaseService = new RecoverableDatabaseService { FailWrites = true };
        var settingsService = new SettingsService(databaseService);

        settingsService.SetValue("Theme", "Dark");

        await Assert.ThrowsAsync<InvalidOperationException>(() => settingsService.FlushPendingSavesAsync());

        databaseService.FailWrites = false;
        await settingsService.FlushPendingSavesAsync();

        Assert.Equal("Dark", databaseService.Values["Theme"]);
        settingsService.Dispose();
    }

    [Fact]
    public async Task SaveBatchAsync_WhenOneSettingFails_ShouldRollBackTheWholeBatchWithoutRetry()
    {
        Cleanup();
        var databaseService = GetDb();
        var settingsService = new SettingsService(databaseService);
        await settingsService.InitializeAsync();
        await settingsService.SetSettingAsync("BatchA", "old-a");
        await settingsService.SetSettingAsync("BatchB", "old-b");
        await databaseService.ExecuteNonQueryAsync(
            "CREATE TRIGGER fail_settings_batch BEFORE INSERT ON Settings WHEN NEW.Key = 'BatchB' BEGIN SELECT RAISE(ABORT, 'forced batch failure'); END");

        await Assert.ThrowsAnyAsync<Exception>(() => settingsService.SaveBatchAsync(
            new Dictionary<string, string?>
            {
                ["BatchA"] = "new-a",
                ["BatchB"] = "new-b"
            }));
        await Task.Delay(150);

        var storedA = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'BatchA'",
            reader => reader.GetString(0));
        var storedB = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'BatchB'",
            reader => reader.GetString(0));
        Assert.Equal("old-a", storedA);
        Assert.Equal("old-b", storedB);
        settingsService.Dispose();
        Cleanup();
    }

    [Fact]
    public async Task ConcurrentSetAndGet_ShouldPersistEveryValueWithoutErrors()
    {
        var databaseService = new RecoverableDatabaseService();
        var settingsService = new SettingsService(databaseService);
        using var start = new ManualResetEventSlim();

        var workers = Enumerable.Range(0, 32).Select(worker => Task.Run(() =>
        {
            start.Wait();
            for (var item = 0; item < 100; item++)
            {
                var key = $"Key-{worker}-{item}";
                var value = $"Value-{worker}-{item}";
                settingsService.SetValue(key, value);
                Assert.Equal(value, settingsService.GetValue(key, string.Empty));
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(workers);
        await settingsService.FlushPendingSavesAsync();

        Assert.Equal(3200, databaseService.Values.Count);
        settingsService.Dispose();
    }

    [Fact]
    public async Task QueueSave_ShouldNotDisposeCancellationSourceBeforeItsFlushTaskCompletes()
    {
        var databaseService = new BlockingDatabaseService();
        var settingsService = new SettingsService(databaseService);

        settingsService.SetValue("First", "1");
        await databaseService.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var ctsField = typeof(SettingsService).GetField("_flushCts", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var taskField = typeof(SettingsService).GetField("_flushTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var firstCts = Assert.IsType<CancellationTokenSource>(ctsField.GetValue(settingsService));
        var firstTask = Assert.IsAssignableFrom<Task>(taskField.GetValue(settingsService));

        settingsService.SetValue("Second", "2");

        Assert.False(firstTask.IsCompleted);
        Assert.Null(Record.Exception(() => _ = firstCts.Token));

        databaseService.AllowWrite.TrySetResult();
        await settingsService.FlushPendingSavesAsync();
        settingsService.Dispose();
    }

    private sealed class RecoverableDatabaseService : IDatabaseService
    {
        public ConcurrentDictionary<string, string?> Values { get; } = new();
        public bool FailWrites { get; set; }
        public int RemainingReadFailures { get; set; }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters)
        {
            if (FailWrites)
            {
                throw new InvalidOperationException("forced settings write failure");
            }

            if (sql.StartsWith("DELETE FROM Settings", StringComparison.Ordinal))
            {
                return Task.FromResult(Values.TryRemove((string)parameters[0]!, out _) ? 1 : 0);
            }

            Values[(string)parameters[0]!] = (string?)parameters[1];
            return Task.FromResult(1);
        }

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromResult(new List<T>());

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters)
        {
            if (RemainingReadFailures-- > 0)
            {
                throw new InvalidOperationException("forced settings read failure");
            }

            if (parameters.Length > 0 &&
                parameters[0] is string key &&
                Values.TryGetValue(key, out var value))
            {
                return Task.FromResult((T?)(object?)value);
            }

            return Task.FromResult<T?>(default);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            operation(this);
    }

    private sealed class OrderedWriteDatabaseService : IDatabaseService
    {
        private int _writeCount;

        public ConcurrentDictionary<string, string?> Values { get; } = new();

        public TaskCompletionSource FirstWriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowFirstWrite { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters)
        {
            if (Interlocked.Increment(ref _writeCount) == 1)
            {
                FirstWriteStarted.TrySetResult();
                await AllowFirstWrite.Task;
            }

            var key = (string)parameters[0]!;
            if (sql.StartsWith("DELETE FROM Settings", StringComparison.Ordinal))
            {
                Values.TryRemove(key, out _);
            }
            else
            {
                Values[key] = (string?)parameters[1];
            }

            return 1;
        }

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromResult(new List<T>());

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromResult<T?>(default);

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            operation(this);
    }

    private sealed class PreloadedDatabaseService : IDatabaseService
    {
        private int _querySingleCallCount;

        public Dictionary<string, string?> Values { get; set; } = new();
        public int InitializeThreadId { get; private set; }
        public int QueryThreadId { get; private set; }
        public int QuerySingleCallCount => _querySingleCallCount;

        public Task InitializeAsync()
        {
            InitializeThreadId = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }

        public Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters) =>
            Task.FromResult(1);

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters)
        {
            QueryThreadId = Environment.CurrentManagedThreadId;
            if (typeof(T) == typeof(KeyValuePair<string, string?>))
            {
                var rows = Values
                    .Select(pair => (T)(object)new KeyValuePair<string, string?>(pair.Key, pair.Value))
                    .ToList();
                return Task.FromResult(rows);
            }

            return Task.FromResult(new List<T>());
        }

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters)
        {
            Interlocked.Increment(ref _querySingleCallCount);
            throw new InvalidOperationException("同步读取不应访问 SQLite 查询单行接口");
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            operation(this);
    }

    private sealed class BlockingDatabaseService : IDatabaseService
    {
        public TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowWrite { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters)
        {
            WriteStarted.TrySetResult();
            await AllowWrite.Task;
            return 1;
        }

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromResult(new List<T>());

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromResult<T?>(default);

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            operation(this);
    }

    private sealed class FakeUserDataProtector : IUserDataProtector
    {
        private const string Prefix = "fake:v1:";

        public string Protect(string plaintext) =>
            string.IsNullOrEmpty(plaintext) ? string.Empty : Prefix + plaintext;

        public bool TryUnprotect(string storedValue, out string plaintext)
        {
            plaintext = string.Empty;
            if (string.IsNullOrEmpty(storedValue)) return true;
            if (!IsProtected(storedValue) || storedValue == Prefix + "unreadable") return false;
            plaintext = storedValue[Prefix.Length..];
            return true;
        }

        public bool IsProtected(string storedValue) =>
            storedValue.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
