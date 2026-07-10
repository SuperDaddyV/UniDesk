using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class QuickTextViewModel : ObservableObject, IDisposable
{
    private readonly IQuickTextService _quickTextService;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<double> _getPanelWidth;
    private readonly Func<TextSnippet?, bool> _showSnippetEditor;
    private readonly Action _showManager;
    private int _loadGeneration;
    private bool _disposed;

    public ObservableCollection<ClipboardHistoryItem> ClipboardHistory { get; } = [];
    public ObservableCollection<TextSnippet> TextSnippets { get; } = [];

    [ObservableProperty]
    private QuickTextMode _selectedQuickTextMode = QuickTextMode.History;

    public bool HasClipboardHistory => ClipboardHistory.Count > 0;
    public bool HasTextSnippets => TextSnippets.Count > 0;
    public bool IsQuickTextHistorySelected => SelectedQuickTextMode == QuickTextMode.History;
    public bool IsQuickTextSnippetsSelected => SelectedQuickTextMode == QuickTextMode.Snippets;
    public bool IsEnabled { get; set; } = true;

    public QuickTextViewModel(
        IQuickTextService quickTextService,
        IClipboardMonitorService clipboardMonitorService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        Func<double> getPanelWidth,
        Func<TextSnippet?, bool>? showSnippetEditor = null,
        Action? showManager = null)
    {
        _quickTextService = quickTextService;
        _clipboardMonitorService = clipboardMonitorService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _getPanelWidth = getPanelWidth;
        _showSnippetEditor = showSnippetEditor ?? ShowSnippetEditor;
        _showManager = showManager ?? ShowManager;
        _clipboardMonitorService.ClipboardHistoryChanged += ClipboardMonitor_OnHistoryChanged;
    }

    partial void OnSelectedQuickTextModeChanged(QuickTextMode value)
    {
        OnPropertyChanged(nameof(IsQuickTextHistorySelected));
        OnPropertyChanged(nameof(IsQuickTextSnippetsSelected));
    }

    private void ClipboardMonitor_OnHistoryChanged()
    {
        if (!_disposed && IsEnabled) _ = ReloadAsync();
    }

    [RelayCommand]
    private void SelectQuickTextMode(string? mode)
    {
        SelectedQuickTextMode = string.Equals(mode, "Snippets", StringComparison.OrdinalIgnoreCase)
            ? QuickTextMode.Snippets
            : QuickTextMode.History;
    }

    [RelayCommand]
    private async Task CopyClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Content)) return;
        if (!await _clipboardMonitorService.TrySetTextAsync(item.Content))
        {
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
            return;
        }

        _notificationService.ShowSuccessMessage(L("Common.Copied"));
        try
        {
            await _quickTextService.RecordClipboardTextAsync(item.Content);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickTextViewModel.CopyClipboardHistoryAsync.AfterCopy");
        }
    }

    [RelayCommand]
    private async Task DeleteClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null) return;
        await _quickTextService.DeleteClipboardHistoryAsync(item.Id);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ClearClipboardHistoryAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickText.ClearHistoryConfirm"), L("QuickText.ClearHistoryTitle"))) return;
        await _quickTextService.ClearClipboardHistoryAsync();
        await ReloadAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.HistoryCleared"));
    }

    [RelayCommand]
    private async Task FavoriteClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        var snippet = await _quickTextService.CreateSnippetFromHistoryAsync(item);
        if (snippet == null)
        {
            _notificationService.ShowWarningMessage(L("QuickText.FavoriteFailed"));
            return;
        }

        SelectedQuickTextMode = QuickTextMode.Snippets;
        await ReloadAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.Favorited"));
    }

    [RelayCommand]
    private async Task CopyTextSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null || string.IsNullOrWhiteSpace(snippet.Content)) return;
        if (!await _clipboardMonitorService.TrySetTextAsync(snippet.Content))
        {
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
            return;
        }

        _notificationService.ShowSuccessMessage(L("Common.Copied"));
        try
        {
            await _quickTextService.MarkSnippetUsedAsync(snippet.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickTextViewModel.CopyTextSnippetAsync.AfterCopy");
        }
    }

    [RelayCommand]
    private void NewTextSnippet()
    {
        if (!_showSnippetEditor(null)) return;
        SelectedQuickTextMode = QuickTextMode.Snippets;
        _ = ReloadAsync();
    }

    [RelayCommand]
    private void EditTextSnippet(TextSnippet? snippet)
    {
        if (snippet != null && _showSnippetEditor(snippet)) _ = ReloadAsync();
    }

    [RelayCommand]
    private async Task DeleteTextSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null ||
            !_notificationService.ShowConfirmDialog(
                _localizationService.Format("QuickText.DeleteSnippetConfirmFormat", snippet.DisplayTitle),
                L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _quickTextService.DeleteTextSnippetAsync(snippet.Id);
        await ReloadAsync();
    }

    [RelayCommand]
    private void OpenQuickTextManager()
    {
        _showManager();
        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            var historyTask = _quickTextService.GetClipboardHistoryAsync(5);
            var snippetsTask = _quickTextService.GetTextSnippetsAsync();
            await Task.WhenAll(historyTask, snippetsTask);
            if (generation != _loadGeneration) return;

            void Apply()
            {
                ClipboardHistory.Clear();
                foreach (var item in historyTask.Result.Take(5)) ClipboardHistory.Add(item);
                TextSnippets.Clear();
                foreach (var snippet in snippetsTask.Result.Take(5)) TextSnippets.Add(snippet);
                OnPropertyChanged(nameof(HasClipboardHistory));
                OnPropertyChanged(nameof(HasTextSnippets));
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) Apply();
            else await dispatcher.InvokeAsync(Apply);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickTextViewModel.ReloadAsync");
        }
    }

    private bool ShowSnippetEditor(TextSnippet? snippet)
    {
        var viewModel = snippet == null
            ? new TextSnippetEditViewModel(_quickTextService, _localizationService)
            : new TextSnippetEditViewModel(_quickTextService, _localizationService, snippet);
        var window = new TextSnippetEditWindow(viewModel, _getPanelWidth())
        {
            Owner = App.Current.MainWindow
        };
        return window.ShowDialog() == true;
    }

    private void ShowManager()
    {
        var width = _getPanelWidth();
        var window = new QuickTextManagerWindow(
            new QuickTextManagerViewModel(
                _quickTextService,
                _clipboardMonitorService,
                _notificationService,
                _localizationService,
                width),
            width)
        {
            Owner = App.Current.MainWindow
        };
        window.ShowDialog();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clipboardMonitorService.ClipboardHistoryChanged -= ClipboardMonitor_OnHistoryChanged;
    }

    private string L(string key) => _localizationService.GetString(key);
}
