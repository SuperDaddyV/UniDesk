using Microsoft.Data.Sqlite;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class DataFailurePropagationTests
{
    [Fact]
    public async Task ReadServices_ShouldPropagateDatabaseFailures()
    {
        var database = new ThrowingDatabaseService();
        var settings = new StubSettingsService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TodoService(database).GetAllTodosAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new NoteService(database).GetAllNotesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QuickNoteService(database).GetAllQuickNotesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QuickTextService(database, settings).GetTextSnippetsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ShortcutService(database).GetAllShortcutsAsync());
    }

    [Fact]
    public async Task WriteServices_ShouldPropagateDatabaseFailures()
    {
        var database = new ThrowingDatabaseService();
        var settings = new StubSettingsService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TodoService(database).DeleteTodoAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new NoteService(database).DeleteNoteAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QuickNoteService(database).DeleteQuickNoteAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QuickTextService(database, settings).DeleteTextSnippetAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ShortcutService(database).DeleteShortcutAsync(1));
    }

    [Fact]
    public async Task ClipboardMonitor_WhenPersistenceFails_ShouldContainTheBackgroundFailure()
    {
        var quickTextService = new QuickTextService(
            new ThrowingDatabaseService(),
            new StubSettingsService());
        using var monitor = new ClipboardMonitorService(quickTextService);
        var historyChanged = false;
        monitor.ClipboardHistoryChanged += () => historyChanged = true;

        var recorded = await monitor.TryRecordClipboardTextAsync("ordinary clipboard text");

        Assert.False(recorded);
        Assert.False(historyChanged);
    }

    private sealed class ThrowingDatabaseService : IDatabaseService
    {
        private static InvalidOperationException Failure() => new("forced database failure");

        public Task InitializeAsync() => Task.FromException(Failure());

        public Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters) =>
            Task.FromException<int>(Failure());

        public Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromException<List<T>>(Failure());

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqliteDataReader, T> map,
            params object?[] parameters) => Task.FromException<T?>(Failure());

        public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation) =>
            Task.FromException<T>(Failure());
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<string?> GetSettingAsync(string key) => Task.FromResult<string?>(null);
        public string? GetSetting(string key) => null;
        public Task SetSettingAsync(string key, string? value) => Task.CompletedTask;
        public void SetSetting(string key, string? value) { }
        public T GetSetting<T>(string key, T defaultValue) => defaultValue;
        public string GetValue(string key, string defaultValue) => defaultValue;
        public void SetValue(string key, string value) { }
        public void InvalidateCache() { }
        public Task ReloadCacheAsync() => Task.CompletedTask;
        public Task FlushPendingSavesAsync() => Task.CompletedTask;
        public Task SaveBatchAsync(IReadOnlyDictionary<string, string?> values) => Task.CompletedTask;
    }
}
