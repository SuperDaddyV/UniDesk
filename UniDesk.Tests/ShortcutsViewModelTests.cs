using System.Globalization;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class ShortcutsViewModelTests
{
    [Fact]
    public async Task ReloadAndLimitPreview_ShouldOrderAndTruncateVisibleItems()
    {
        var service = new FakeShortcutService(
            Enumerable.Range(1, 8)
                .Select(id => new ShortcutItem { Id = id, Name = $"item-{id}", Path = $@"C:\item-{id}", SortOrder = 9 - id })
                .ToList());
        var viewModel = CreateViewModel(service);

        await viewModel.ReloadAsync();
        viewModel.SetLimitPreview(6);

        Assert.Equal(6, viewModel.Shortcuts.Count);
        Assert.Equal("item-8", viewModel.Shortcuts[0].Name);
        Assert.True(viewModel.HasShortcuts);
    }

    [Fact]
    public async Task AddFromPathsAsync_ShouldPreventDuplicatesAndRespectMaximum()
    {
        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        try
        {
            var service = new FakeShortcutService(
                Enumerable.Range(1, 5)
                    .Select(id => new ShortcutItem { Id = id, Name = $"existing-{id}", Path = id == 1 ? firstPath : $@"C:\existing-{id}", SortOrder = id })
                    .ToList());
            var viewModel = CreateViewModel(service);
            viewModel.SetLimitPreview(6);

            var result = await viewModel.AddFromPathsAsync([firstPath, secondPath, @"C:\missing.file"]);

            Assert.Equal(1, result.AddedCount);
            Assert.Equal(1, result.DuplicateCount);
            Assert.Equal(1, result.InvalidCount);
            Assert.Equal(6, service.Items.Count);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task MoveShortcutAsync_ShouldPersistNewOrder()
    {
        var first = new ShortcutItem { Id = 1, Name = "first", Path = "first", SortOrder = 0 };
        var second = new ShortcutItem { Id = 2, Name = "second", Path = "second", SortOrder = 1 };
        var third = new ShortcutItem { Id = 3, Name = "third", Path = "third", SortOrder = 2 };
        var service = new FakeShortcutService([first, second, third]);
        var viewModel = CreateViewModel(service);
        await viewModel.ReloadAsync();

        await viewModel.MoveShortcutAsync(first, third);

        Assert.Equal([2, 3, 1], service.LastPersistedOrder);
        Assert.Equal(["second", "third", "first"], viewModel.Shortcuts.Select(item => item.Name));
    }

    [Fact]
    public async Task EditMode_ShouldAppendAddPlaceholderBelowLimit()
    {
        var service = new FakeShortcutService([new ShortcutItem { Id = 1, Name = "one", Path = "one" }]);
        var viewModel = CreateViewModel(service);
        await viewModel.ReloadAsync();

        viewModel.ToggleShortcutEditCommand.Execute(null);

        Assert.Equal(2, viewModel.ShortcutDisplayEntries.Count);
        Assert.IsType<AddShortcutPlaceholder>(viewModel.ShortcutDisplayEntries[^1]);
    }

    private static ShortcutsViewModel CreateViewModel(IShortcutService service) =>
        new(
            service,
            new TestSettingsService(),
            new NoOpNotificationService(),
            new TestLocalizationService());

    private sealed class FakeShortcutService(List<ShortcutItem> items) : IShortcutService
    {
        private int _nextId = items.Count + 1;
        public List<ShortcutItem> Items { get; } = items;
        public List<int> LastPersistedOrder { get; private set; } = [];
        public Task<List<ShortcutItem>> GetAllShortcutsAsync() => Task.FromResult(Items.ToList());
        public Task<ShortcutItem?> GetShortcutAsync(int id) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
        public Task<int> CreateShortcutAsync(ShortcutItem shortcut)
        {
            shortcut.Id = _nextId++;
            Items.Add(shortcut);
            return Task.FromResult(shortcut.Id);
        }
        public Task UpdateShortcutAsync(ShortcutItem shortcut) => Task.CompletedTask;
        public Task DeleteShortcutAsync(int id) { Items.RemoveAll(item => item.Id == id); return Task.CompletedTask; }
        public Task UpdateSortOrderAsync(List<int> ids) { LastPersistedOrder = ids.ToList(); return Task.CompletedTask; }
        public Task NormalizeSortOrderAsync() => Task.CompletedTask;
        public Task RefreshMissingIconsAsync() => Task.CompletedTask;
        public Task LaunchShortcutAsync(int id) => Task.CompletedTask;
    }

    private sealed class TestSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<string?> GetSettingAsync(string key) => Task.FromResult(GetSetting(key));
        public string? GetSetting(string key) => _values.GetValueOrDefault(key);
        public Task SetSettingAsync(string key, string? value) { SetSetting(key, value); return Task.CompletedTask; }
        public void SetSetting(string key, string? value) => _values[key] = value;
        public T GetSetting<T>(string key, T defaultValue) => defaultValue;
        public string GetValue(string key, string defaultValue) => GetSetting(key) ?? defaultValue;
        public void SetValue(string key, string value) => SetSetting(key, value);
        public void InvalidateCache() => _values.Clear();
        public Task FlushPendingSavesAsync() => Task.CompletedTask;
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => true;
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged;
        public string CurrentLanguage => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) => key;
    }
}
