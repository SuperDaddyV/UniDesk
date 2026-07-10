using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class QuickNotesViewModelTests
{
    [Fact]
    public async Task ReloadAsync_ShouldLoadFiveQuickNotesAndLegacyNotes()
    {
        var quick = new FakeQuickNoteService
        {
            Notes = Enumerable.Range(1, 6).Select(id => new QuickNote { Id = id, Title = $"quick-{id}" }).ToList()
        };
        var legacy = new FakeNoteService
        {
            Notes =
            [
                new NoteItem { Id = 1, Title = "old", UpdatedAt = DateTime.UtcNow.AddDays(-1) },
                new NoteItem { Id = 2, Title = "new", UpdatedAt = DateTime.UtcNow }
            ]
        };
        var viewModel = CreateViewModel(legacy, quick);

        await viewModel.ReloadAsync();
        await viewModel.ReloadLegacyNotesAsync();

        Assert.Equal(5, viewModel.QuickNotes.Count);
        Assert.True(viewModel.HasQuickNotes);
        Assert.Equal(["new", "old"], viewModel.Notes.Select(note => note.Title));
    }

    [Fact]
    public async Task EditorCommands_ShouldInvokeEditorAndReload()
    {
        var quick = new FakeQuickNoteService();
        var edited = new List<QuickNote?>();
        var viewModel = CreateViewModel(
            new FakeNoteService(),
            quick,
            showEditor: note =>
            {
                edited.Add(note);
                return true;
            });
        var existing = new QuickNote { Id = 4, Title = "existing" };

        viewModel.NewQuickNoteCommand.Execute(null);
        viewModel.EditQuickNoteCommand.Execute(existing);
        await WaitUntilAsync(() => quick.LoadCount >= 2);

        Assert.Equal(2, quick.LoadCount);
        Assert.Null(edited[0]);
        Assert.Same(existing, edited[1]);
    }

    [Fact]
    public async Task PinAndConfirmedDelete_ShouldMutateAndReload()
    {
        var quick = new FakeQuickNoteService();
        var notifications = new RecordingNotificationService { ConfirmResult = true };
        var viewModel = CreateViewModel(new FakeNoteService(), quick, notifications);
        var note = new QuickNote { Id = 7, Title = "note", IsPinned = false };

        await viewModel.ToggleQuickNotePinnedCommand.ExecuteAsync(note);
        await viewModel.DeleteQuickNoteCommand.ExecuteAsync(note);

        Assert.Equal([(7, true)], quick.PinCalls);
        Assert.Equal([7], quick.DeletedIds);
        Assert.Equal(2, quick.LoadCount);
    }

    [Fact]
    public async Task CancelledDelete_ShouldNotDelete()
    {
        var quick = new FakeQuickNoteService();
        var notifications = new RecordingNotificationService { ConfirmResult = false };
        var viewModel = CreateViewModel(new FakeNoteService(), quick, notifications);

        await viewModel.DeleteQuickNoteCommand.ExecuteAsync(new QuickNote { Id = 8 });

        Assert.Empty(quick.DeletedIds);
        Assert.Equal(0, quick.LoadCount);
    }

    [Fact]
    public void CopyCommand_ShouldUseInjectedClipboard()
    {
        var copied = string.Empty;
        var notifications = new RecordingNotificationService();
        var viewModel = CreateViewModel(
            new FakeNoteService(),
            new FakeQuickNoteService(),
            notifications,
            setClipboard: text => copied = text);

        viewModel.CopyQuickNoteContentCommand.Execute(new QuickNote { Title = "title", Content = "body" });

        Assert.Equal("body", copied);
        Assert.Single(notifications.SuccessMessages);
    }

    [Fact]
    public async Task ReloadAsync_OlderCompletion_ShouldNotOverwriteNewerResult()
    {
        var first = new TaskCompletionSource<List<QuickNote>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<List<QuickNote>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var quick = new SequencedQuickNoteService(first.Task, second.Task);
        var viewModel = CreateViewModel(new FakeNoteService(), quick);

        var olderLoad = viewModel.ReloadAsync();
        var newerLoad = viewModel.ReloadAsync();
        second.SetResult([new QuickNote { Title = "newer" }]);
        await newerLoad;
        first.SetResult([new QuickNote { Title = "older" }]);
        await olderLoad;

        Assert.Equal("newer", Assert.Single(viewModel.QuickNotes).Title);
    }

    private static QuickNotesViewModel CreateViewModel(
        INoteService noteService,
        IQuickNoteService quickNoteService,
        RecordingNotificationService? notifications = null,
        Func<QuickNote?, bool>? showEditor = null,
        Action<string>? setClipboard = null) =>
        new(
            noteService,
            quickNoteService,
            notifications ?? new RecordingNotificationService(),
            new TestLocalizationService(),
            () => 360,
            showEditor,
            setClipboard);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        Assert.True(condition());
    }

    private sealed class FakeQuickNoteService : IQuickNoteService
    {
        public List<QuickNote> Notes { get; set; } = [];
        public int LoadCount { get; private set; }
        public List<(int Id, bool Pinned)> PinCalls { get; } = [];
        public List<int> DeletedIds { get; } = [];
        public Task<List<QuickNote>> GetAllQuickNotesAsync()
        {
            LoadCount++;
            return Task.FromResult(Notes.ToList());
        }
        public Task<QuickNote?> GetQuickNoteAsync(int id) => Task.FromResult<QuickNote?>(null);
        public Task<int> CreateQuickNoteAsync(QuickNote note) => Task.FromResult(1);
        public Task UpdateQuickNoteAsync(QuickNote note) => Task.CompletedTask;
        public Task DeleteQuickNoteAsync(int id) { DeletedIds.Add(id); return Task.CompletedTask; }
        public Task SetPinnedAsync(int id, bool isPinned) { PinCalls.Add((id, isPinned)); return Task.CompletedTask; }
    }

    private sealed class SequencedQuickNoteService(params Task<List<QuickNote>>[] loads) : IQuickNoteService
    {
        private int _index;
        public Task<List<QuickNote>> GetAllQuickNotesAsync() => loads[_index++];
        public Task<QuickNote?> GetQuickNoteAsync(int id) => Task.FromResult<QuickNote?>(null);
        public Task<int> CreateQuickNoteAsync(QuickNote note) => Task.FromResult(1);
        public Task UpdateQuickNoteAsync(QuickNote note) => Task.CompletedTask;
        public Task DeleteQuickNoteAsync(int id) => Task.CompletedTask;
        public Task SetPinnedAsync(int id, bool isPinned) => Task.CompletedTask;
    }

    private sealed class FakeNoteService : INoteService
    {
        public List<NoteItem> Notes { get; set; } = [];
        public Task<List<NoteItem>> GetAllNotesAsync() => Task.FromResult(Notes.ToList());
        public Task<NoteItem?> GetNoteAsync(int id) => Task.FromResult<NoteItem?>(null);
        public Task<int> CreateNoteAsync(NoteItem note) => Task.FromResult(1);
        public Task UpdateNoteAsync(NoteItem note) => Task.CompletedTask;
        public Task DeleteNoteAsync(int id) => Task.CompletedTask;
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public bool ConfirmResult { get; set; }
        public List<string> SuccessMessages { get; } = [];
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) => SuccessMessages.Add(message);
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
