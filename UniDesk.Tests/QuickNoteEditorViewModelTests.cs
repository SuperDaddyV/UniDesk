using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public class QuickNoteEditorViewModelTests
{
    [Fact]
    public async Task FlushAndCleanupAsync_WhenCreateFails_ShouldKeepFailureResult()
    {
        var viewModel = new QuickNoteEditorViewModel(
            new FailingQuickNoteService(),
            new NoOpNotificationService(),
            new TestLocalizationService());
        viewModel.Title = "unsaved note";

        var saved = await viewModel.FlushAndCleanupAsync();

        Assert.False(saved);
        Assert.Equal("QuickNote.SaveFailed", viewModel.SaveStatus);
        viewModel.Dispose();
    }

    [Fact]
    public async Task FlushAndCleanupAsync_WhenUpdateFails_ShouldKeepFailureResult()
    {
        var viewModel = new QuickNoteEditorViewModel(
            new FailingQuickNoteService(),
            new NoOpNotificationService(),
            new TestLocalizationService(),
            new QuickNote { Id = 7, Title = "original" });
        viewModel.Title = "changed";

        var saved = await viewModel.FlushAndCleanupAsync();

        Assert.False(saved);
        Assert.Equal("QuickNote.SaveFailed", viewModel.SaveStatus);
        viewModel.Dispose();
    }

    private sealed class FailingQuickNoteService : IQuickNoteService
    {
        public Task<List<QuickNote>> GetAllQuickNotesAsync() => Task.FromResult(new List<QuickNote>());
        public Task<QuickNote?> GetQuickNoteAsync(int id) => Task.FromResult<QuickNote?>(null);
        public Task<int> CreateQuickNoteAsync(QuickNote note) => Task.FromResult(0);
        public Task<bool> UpdateQuickNoteAsync(QuickNote note) => Task.FromResult(false);
        public Task<bool> DeleteQuickNoteAsync(int id) => Task.FromResult(true);
        public Task SetPinnedAsync(int id, bool isPinned) => Task.CompletedTask;
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
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
