using Xunit;
using UniDesk.Services;
using UniDesk.Helpers;
using System.IO;
using System.Collections.Concurrent;
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

    private sealed class RecoverableDatabaseService : IDatabaseService
    {
        public ConcurrentDictionary<string, string?> Values { get; } = new();
        public bool FailWrites { get; set; }

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
            params object?[] parameters) => Task.FromResult<T?>(default);

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            throw new NotSupportedException();
    }
}
