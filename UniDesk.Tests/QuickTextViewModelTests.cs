using System.Globalization;
using System.Windows;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class QuickTextViewModelTests
{
    [Fact]
    public void SelectModeCommand_ShouldSwitchModes()
    {
        using var viewModel = CreateViewModel(new FakeQuickTextService(), new FakeClipboardMonitor());

        viewModel.SelectQuickTextModeCommand.Execute("Snippets");
        Assert.True(viewModel.IsQuickTextSnippetsSelected);
        viewModel.SelectQuickTextModeCommand.Execute("History");
        Assert.True(viewModel.IsQuickTextHistorySelected);
    }

    [Fact]
    public async Task ClipboardEvent_ShouldReloadOnlyWhileEnabledAndSubscribed()
    {
        var service = new FakeQuickTextService();
        var monitor = new FakeClipboardMonitor();
        var viewModel = CreateViewModel(service, monitor);

        monitor.Raise();
        await WaitUntilAsync(() => service.HistoryLoads == 1);
        viewModel.IsEnabled = false;
        monitor.Raise();
        await Task.Delay(30);
        viewModel.Dispose();
        monitor.Raise();
        await Task.Delay(30);

        Assert.Equal(1, service.HistoryLoads);
    }

    [Fact]
    public async Task CopyDeleteClearAndFavoriteCommands_ShouldUseServices()
    {
        var service = new FakeQuickTextService { FavoriteResult = new TextSnippet { Id = 5, Content = "fav" } };
        var monitor = new FakeClipboardMonitor();
        var notifications = new RecordingNotificationService { ConfirmResult = true };
        using var viewModel = CreateViewModel(service, monitor, notifications);
        var history = new ClipboardHistoryItem { Id = 3, Content = "copy" };

        await viewModel.CopyClipboardHistoryCommand.ExecuteAsync(history);
        await viewModel.DeleteClipboardHistoryCommand.ExecuteAsync(history);
        await viewModel.ClearClipboardHistoryCommand.ExecuteAsync(null);
        await viewModel.FavoriteClipboardHistoryCommand.ExecuteAsync(history);

        Assert.Equal(["copy"], monitor.CopiedTexts);
        Assert.Equal([3], service.DeletedHistoryIds);
        Assert.Equal(1, service.ClearCount);
        Assert.Same(history, service.FavoriteInput);
        Assert.True(viewModel.IsQuickTextSnippetsSelected);
    }

    [Fact]
    public async Task SnippetCommands_ShouldCopyEditAndDelete()
    {
        var service = new FakeQuickTextService();
        var monitor = new FakeClipboardMonitor();
        var notifications = new RecordingNotificationService { ConfirmResult = true };
        var edited = new List<TextSnippet?>();
        using var viewModel = CreateViewModel(
            service,
            monitor,
            notifications,
            showEditor: snippet => { edited.Add(snippet); return true; });
        var snippet = new TextSnippet { Id = 8, Content = "phrase" };

        await viewModel.CopyTextSnippetCommand.ExecuteAsync(snippet);
        viewModel.NewTextSnippetCommand.Execute(null);
        viewModel.EditTextSnippetCommand.Execute(snippet);
        await viewModel.DeleteTextSnippetCommand.ExecuteAsync(snippet);

        Assert.Equal(["phrase"], monitor.CopiedTexts);
        Assert.Equal([8], service.MarkedSnippetIds);
        Assert.Null(edited[0]);
        Assert.Same(snippet, edited[1]);
        Assert.Equal([8], service.DeletedSnippetIds);
    }

    [Fact]
    public async Task ReloadAsync_ShouldPopulateBothCollections()
    {
        var service = new FakeQuickTextService
        {
            History = [new ClipboardHistoryItem { Id = 1, Content = "history" }],
            Snippets = [new TextSnippet { Id = 2, Content = "snippet" }]
        };
        using var viewModel = CreateViewModel(service, new FakeClipboardMonitor());

        await viewModel.ReloadAsync();

        Assert.Equal("history", Assert.Single(viewModel.ClipboardHistory).Content);
        Assert.Equal("snippet", Assert.Single(viewModel.TextSnippets).Content);
        Assert.True(viewModel.HasClipboardHistory);
        Assert.True(viewModel.HasTextSnippets);
    }

    private static QuickTextViewModel CreateViewModel(
        IQuickTextService service,
        FakeClipboardMonitor monitor,
        RecordingNotificationService? notifications = null,
        Func<TextSnippet?, bool>? showEditor = null) =>
        new(
            service,
            monitor,
            notifications ?? new RecordingNotificationService(),
            new TestLocalizationService(),
            () => 360,
            showEditor,
            () => { });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class FakeQuickTextService : IQuickTextService
    {
        public List<ClipboardHistoryItem> History { get; set; } = [];
        public List<TextSnippet> Snippets { get; set; } = [];
        public int HistoryLoads { get; private set; }
        public List<int> DeletedHistoryIds { get; } = [];
        public int ClearCount { get; private set; }
        public ClipboardHistoryItem? FavoriteInput { get; private set; }
        public TextSnippet? FavoriteResult { get; set; }
        public List<int> MarkedSnippetIds { get; } = [];
        public List<int> DeletedSnippetIds { get; } = [];
        public Task<List<ClipboardHistoryItem>> GetClipboardHistoryAsync(int? limit = null)
        {
            HistoryLoads++;
            return Task.FromResult(History.ToList());
        }
        public Task<List<TextSnippet>> GetTextSnippetsAsync() => Task.FromResult(Snippets.ToList());
        public Task<bool> RecordClipboardTextAsync(string? text) => Task.FromResult(true);
        public Task DeleteClipboardHistoryAsync(int id) { DeletedHistoryIds.Add(id); return Task.CompletedTask; }
        public Task ClearClipboardHistoryAsync() { ClearCount++; return Task.CompletedTask; }
        public Task TrimClipboardHistoryAsync(int maxCount) => Task.CompletedTask;
        public Task<int> CreateTextSnippetAsync(TextSnippet snippet) => Task.FromResult(1);
        public Task UpdateTextSnippetAsync(TextSnippet snippet) => Task.CompletedTask;
        public Task DeleteTextSnippetAsync(int id) { DeletedSnippetIds.Add(id); return Task.CompletedTask; }
        public Task<TextSnippet?> CreateSnippetFromHistoryAsync(ClipboardHistoryItem? item)
        {
            FavoriteInput = item;
            return Task.FromResult(FavoriteResult);
        }
        public Task MarkSnippetUsedAsync(int id) { MarkedSnippetIds.Add(id); return Task.CompletedTask; }
    }

    private sealed class FakeClipboardMonitor : IClipboardMonitorService
    {
        public event Action? ClipboardHistoryChanged;
        public List<string> CopiedTexts { get; } = [];
        public void Start(Window window) { }
        public void Stop() { }
        public Task<bool> TrySetTextAsync(string text) { CopiedTexts.Add(text); return Task.FromResult(true); }
        public void Dispose() { }
        public void Raise() => ClipboardHistoryChanged?.Invoke();
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public bool ConfirmResult { get; set; }
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => ConfirmResult;
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
