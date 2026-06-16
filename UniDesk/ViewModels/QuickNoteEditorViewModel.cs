using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class QuickNoteEditorViewModel : ObservableObject, IDisposable
{
    private readonly IQuickNoteService _quickNoteService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly DispatcherTimer _saveTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly DateTime _createdAt;
    private readonly int _sortOrder;
    private bool _isLoading;
    private bool _isDeleted;
    private int _noteId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _saveStatus = string.Empty;

    public string WindowTitle => _noteId > 0 ? L("QuickNote.Edit") : L("QuickNote.New");

    public QuickNoteEditorViewModel(
        IQuickNoteService quickNoteService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        QuickNote? note = null)
    {
        _quickNoteService = quickNoteService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        SaveStatus = L("QuickNote.AutoSave");
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _saveTimer.Tick += SaveTimer_OnTick;

        _isLoading = true;
        try
        {
            if (note == null)
            {
                _createdAt = DateTime.UtcNow;
                _sortOrder = 0;
                return;
            }

            _noteId = note.Id;
            _createdAt = note.CreatedAt == default ? DateTime.UtcNow : note.CreatedAt;
            _sortOrder = note.SortOrder;
            Title = note.Title;
            Content = note.Content;
            IsPinned = note.IsPinned;
            SaveStatus = L("QuickNote.Saved");
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnTitleChanged(string value) => ScheduleSave();

    partial void OnContentChanged(string value) => ScheduleSave();

    partial void OnIsPinnedChanged(bool value) => ScheduleSave();

    public async Task FlushAndCleanupAsync()
    {
        _saveTimer.Stop();
        await SaveNowAsync();

        if (!_isDeleted && _noteId > 0 && IsEmpty)
        {
            await _quickNoteService.DeleteQuickNoteAsync(_noteId);
            _noteId = 0;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickNote.DeleteConfirm"), L("Dialog.DeleteConfirmTitle")))
        {
            return false;
        }

        _saveTimer.Stop();
        if (_noteId > 0)
        {
            await _quickNoteService.DeleteQuickNoteAsync(_noteId);
        }

        _isDeleted = true;
        _notificationService.ShowSuccessMessage(L("QuickNote.Deleted"));
        return true;
    }

    public void CopyContent()
    {
        var text = string.IsNullOrWhiteSpace(Content) ? Title : Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            _notificationService.ShowWarningMessage(L("QuickNote.ContentEmpty"));
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _notificationService.ShowSuccessMessage(L("Common.Copied"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNoteEditorViewModel.CopyContent");
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
        }
    }

    private bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(Content);

    private void ScheduleSave()
    {
        if (_isLoading || _isDeleted)
        {
            return;
        }

        SaveStatus = L("QuickNote.Saving");
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void SaveTimer_OnTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        if (_isDeleted || IsEmpty)
        {
            SaveStatus = L("QuickNote.AutoSave");
            return;
        }

        await _saveLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (_noteId <= 0)
            {
                var id = await _quickNoteService.CreateQuickNoteAsync(new QuickNote
                {
                    Title = Title ?? string.Empty,
                    Content = Content ?? string.Empty,
                    IsPinned = IsPinned,
                    SortOrder = _sortOrder,
                    CreatedAt = _createdAt,
                    UpdatedAt = now
                });

                if (id <= 0)
                {
                    SaveStatus = L("QuickNote.SaveFailed");
                    return;
                }

                _noteId = id;
                OnPropertyChanged(nameof(WindowTitle));
            }
            else
            {
                await _quickNoteService.UpdateQuickNoteAsync(new QuickNote
                {
                    Id = _noteId,
                    Title = Title ?? string.Empty,
                    Content = Content ?? string.Empty,
                    IsPinned = IsPinned,
                    SortOrder = _sortOrder,
                    CreatedAt = _createdAt,
                    UpdatedAt = now
                });
            }

            SaveStatus = L("QuickNote.Saved");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNoteEditorViewModel.SaveNowAsync");
            SaveStatus = L("QuickNote.SaveFailed");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTimer_OnTick;
        _saveLock.Dispose();
    }

    private string L(string key) => _localizationService.GetString(key);
}
