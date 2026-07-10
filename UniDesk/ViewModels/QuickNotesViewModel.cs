using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class QuickNotesViewModel : ObservableObject
{
    private readonly INoteService _noteService;
    private readonly IQuickNoteService _quickNoteService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<double> _getPanelWidth;
    private readonly Func<QuickNote?, bool> _showEditor;
    private readonly Action<string> _setClipboard;
    private int _notesLoadGeneration;
    private int _quickNotesLoadGeneration;

    public ObservableCollection<NoteItem> Notes { get; } = [];
    public ObservableCollection<QuickNote> QuickNotes { get; } = [];
    public bool HasQuickNotes => QuickNotes.Count > 0;

    public QuickNotesViewModel(
        INoteService noteService,
        IQuickNoteService quickNoteService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        Func<double> getPanelWidth,
        Func<QuickNote?, bool>? showEditor = null,
        Action<string>? setClipboard = null)
    {
        _noteService = noteService;
        _quickNoteService = quickNoteService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _getPanelWidth = getPanelWidth;
        _showEditor = showEditor ?? ShowEditor;
        _setClipboard = setClipboard ?? Clipboard.SetText;
        _ = ReloadLegacyNotesAsync();
    }

    [RelayCommand]
    private Task RefreshNotesAsync() => ReloadLegacyNotesAsync();

    [RelayCommand]
    private void NewNote()
    {
        var window = new NoteEditWindow(new NoteEditViewModel(_noteService, _localizationService))
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true) _ = ReloadLegacyNotesAsync();
    }

    [RelayCommand]
    private void EditNote(NoteItem? note)
    {
        if (note == null) return;
        var window = new NoteEditWindow(new NoteEditViewModel(_noteService, _localizationService, note))
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true) _ = ReloadLegacyNotesAsync();
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteItem? note)
    {
        if (note == null ||
            !_notificationService.ShowConfirmDialog(L("QuickNote.DeleteConfirm"), L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _noteService.DeleteNoteAsync(note.Id);
        await ReloadLegacyNotesAsync();
    }

    public async Task ReloadLegacyNotesAsync()
    {
        var generation = Interlocked.Increment(ref _notesLoadGeneration);
        try
        {
            var notes = await _noteService.GetAllNotesAsync();
            if (generation != _notesLoadGeneration) return;
            await ApplyOnUiAsync(() =>
            {
                Notes.Clear();
                foreach (var note in notes.OrderByDescending(item => item.UpdatedAt)) Notes.Add(note);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNotesViewModel.ReloadLegacyNotesAsync");
            if (generation == _notesLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Common.OperationFailed"));
            }
        }
    }

    [RelayCommand]
    private void NewQuickNote()
    {
        _showEditor(null);
        _ = ReloadAsync();
    }

    [RelayCommand]
    private void EditQuickNote(QuickNote? note)
    {
        if (note == null) return;
        _showEditor(note);
        _ = ReloadAsync();
    }

    [RelayCommand]
    private async Task DeleteQuickNoteAsync(QuickNote? note)
    {
        if (note == null ||
            !_notificationService.ShowConfirmDialog(L("QuickNote.DeleteConfirm"), L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _quickNoteService.DeleteQuickNoteAsync(note.Id);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ToggleQuickNotePinnedAsync(QuickNote? note)
    {
        if (note == null) return;
        await _quickNoteService.SetPinnedAsync(note.Id, !note.IsPinned);
        await ReloadAsync();
    }

    [RelayCommand]
    private void CopyQuickNoteContent(QuickNote? note)
    {
        if (note == null) return;
        var text = string.IsNullOrWhiteSpace(note.Content) ? note.Title : note.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            _notificationService.ShowWarningMessage(L("QuickNote.ContentEmpty"));
            return;
        }

        try
        {
            _setClipboard(text);
            _notificationService.ShowSuccessMessage(L("Common.Copied"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNotesViewModel.CopyQuickNoteContent");
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
        }
    }

    public async Task ReloadAsync()
    {
        var generation = Interlocked.Increment(ref _quickNotesLoadGeneration);
        try
        {
            var notes = await _quickNoteService.GetAllQuickNotesAsync();
            if (generation != _quickNotesLoadGeneration) return;
            await ApplyOnUiAsync(() =>
            {
                QuickNotes.Clear();
                foreach (var note in notes.Take(5)) QuickNotes.Add(note);
                OnPropertyChanged(nameof(HasQuickNotes));
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNotesViewModel.ReloadAsync");
            if (generation == _quickNotesLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Common.OperationFailed"));
            }
        }
    }

    private bool ShowEditor(QuickNote? note)
    {
        var viewModel = note == null
            ? new QuickNoteEditorViewModel(_quickNoteService, _notificationService, _localizationService)
            : new QuickNoteEditorViewModel(_quickNoteService, _notificationService, _localizationService, note);
        var window = new QuickNoteEditorWindow(viewModel, _getPanelWidth())
        {
            Owner = App.Current.MainWindow
        };
        return window.ShowDialog() == true;
    }

    private static async Task ApplyOnUiAsync(Action apply)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) apply();
        else await dispatcher.InvokeAsync(apply);
    }

    private string L(string key) => _localizationService.GetString(key);
}
