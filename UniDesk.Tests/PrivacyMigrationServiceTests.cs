using Microsoft.Data.Sqlite;
using UniDesk.Services;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class PrivacyMigrationServiceTests
{
    private readonly string _databaseFile = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "test_privacy_migration.db");

    [Fact]
    public async Task MigrateAsync_ShouldProtectLegacyValuesAndBeIdempotent()
    {
        var (db, migration, protector) = await InitializeAsync();
        await InsertLegacyValuesAsync(db);

        await migration.MigrateAsync();

        Assert.Equal("fake:v1:weather-plain", await ReadSettingAsync(db));
        Assert.Equal("fake:v1:clipboard-plain", await ReadClipboardAsync(db));
        Assert.Equal("legacy-hash", await ReadClipboardHashAsync(db));
        Assert.Equal(2, protector.ProtectCalls);

        await migration.MigrateAsync();
        Assert.Equal(2, protector.ProtectCalls);
        Cleanup();
    }

    [Fact]
    public async Task MigrateAsync_WhenClipboardUpdateFails_ShouldRollbackWeatherKey()
    {
        var (db, migration, _) = await InitializeAsync();
        await InsertLegacyValuesAsync(db);
        await db.ExecuteNonQueryAsync(
            """
            CREATE TRIGGER fail_privacy_migration
            BEFORE UPDATE OF Content ON ClipboardHistory
            WHEN NEW.Content LIKE 'fake:v1:%'
            BEGIN
                SELECT RAISE(ABORT, 'forced privacy migration failure');
            END
            """);

        await Assert.ThrowsAsync<SqliteException>(() => migration.MigrateAsync());

        Assert.Equal("weather-plain", await ReadSettingAsync(db));
        Assert.Equal("clipboard-plain", await ReadClipboardAsync(db));
        Cleanup();
    }

    private async Task<(DatabaseService Db, PrivacyMigrationService Migration, CountingProtector Protector)> InitializeAsync()
    {
        Cleanup();
        var db = new DatabaseService($"Data Source={_databaseFile}");
        await db.InitializeAsync();
        var protector = new CountingProtector();
        var settings = new SettingsService(db, protector);
        return (db, new PrivacyMigrationService(db, protector, settings), protector);
    }

    private static async Task InsertLegacyValuesAsync(DatabaseService db)
    {
        await db.ExecuteNonQueryAsync(
            "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
            "WeatherApiKey",
            "weather-plain");
        await db.ExecuteNonQueryAsync(
            "INSERT INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt, UseCount) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "clipboard-plain",
            "legacy-hash",
            DateTime.UtcNow.ToString("o"),
            DateTime.UtcNow.ToString("o"),
            1);
    }

    private static Task<string?> ReadSettingAsync(DatabaseService db) =>
        db.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'WeatherApiKey'",
            reader => reader.GetString(0));

    private static Task<string?> ReadClipboardAsync(DatabaseService db) =>
        db.QuerySingleAsync(
            "SELECT Content FROM ClipboardHistory LIMIT 1",
            reader => reader.GetString(0));

    private static Task<string?> ReadClipboardHashAsync(DatabaseService db) =>
        db.QuerySingleAsync(
            "SELECT ContentHash FROM ClipboardHistory LIMIT 1",
            reader => reader.GetString(0));

    private void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databaseFile)) File.Delete(_databaseFile);
    }

    private sealed class CountingProtector : IUserDataProtector
    {
        private const string Prefix = "fake:v1:";
        public int ProtectCalls { get; private set; }
        public string Protect(string plaintext)
        {
            ProtectCalls++;
            return Prefix + plaintext;
        }
        public bool TryUnprotect(string storedValue, out string plaintext)
        {
            plaintext = IsProtected(storedValue) ? storedValue[Prefix.Length..] : string.Empty;
            return IsProtected(storedValue);
        }
        public bool IsProtected(string storedValue) =>
            storedValue.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
