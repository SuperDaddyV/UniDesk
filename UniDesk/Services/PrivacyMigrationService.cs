namespace UniDesk.Services;

public sealed class PrivacyMigrationService : IPrivacyMigrationService
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserDataProtector _userDataProtector;
    private readonly ISettingsService _settingsService;

    public PrivacyMigrationService(
        IDatabaseService databaseService,
        IUserDataProtector userDataProtector,
        ISettingsService settingsService)
    {
        _databaseService = databaseService;
        _userDataProtector = userDataProtector;
        _settingsService = settingsService;
    }

    public async Task MigrateAsync()
    {
        await _databaseService.ExecuteInTransactionAsync(async session =>
        {
            var weatherApiKey = await session.QuerySingleAsync(
                "SELECT Value FROM Settings WHERE Key = 'WeatherApiKey'",
                reader => reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
            if (!string.IsNullOrEmpty(weatherApiKey) &&
                !_userDataProtector.IsProtected(weatherApiKey))
            {
                await session.ExecuteNonQueryAsync(
                    "UPDATE Settings SET Value = @p0 WHERE Key = 'WeatherApiKey'",
                    _userDataProtector.Protect(weatherApiKey));
            }

            var clipboardRows = await session.QueryAsync(
                "SELECT Id, Content FROM ClipboardHistory",
                reader => new ClipboardRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            foreach (var row in clipboardRows)
            {
                if (string.IsNullOrEmpty(row.Content) ||
                    _userDataProtector.IsProtected(row.Content))
                {
                    continue;
                }

                await session.ExecuteNonQueryAsync(
                    "UPDATE ClipboardHistory SET Content = @p0 WHERE Id = @p1",
                    _userDataProtector.Protect(row.Content),
                    row.Id);
            }

            return true;
        });

        _settingsService.InvalidateCache();
        await _settingsService.ReloadCacheAsync();
    }

    private sealed record ClipboardRow(int Id, string Content);
}
