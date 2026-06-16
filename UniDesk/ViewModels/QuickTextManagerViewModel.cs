using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class QuickTextManagerViewModel : ObservableObject
{
    private readonly IQuickTextService _quickTextService;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly double _panelWidth;
    private List<ClipboardHistoryItem> _allHistory = [];
    private List<TextSnippet> _allSnippets = [];

    [ObservableProperty]
    private QuickTextMode _selectedMode = QuickTextMode.History;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "全部";

    public ObservableCollection<ClipboardHistoryItem> ClipboardHistory { get; } = new();

    public ObservableCollection<TextSnippet> TextSnippets { get; } = new();

    public IReadOnlyList<string> Categories { get; } = ["全部", "默认", "工作", "开发", "生活", "AI"];

    public bool IsHistorySelected => SelectedMode == QuickTextMode.History;

    public bool IsSnippetsSelected => SelectedMode == QuickTextMode.Snippets;

    public QuickTextManagerViewModel(
        IQuickTextService quickTextService,
        IClipboardMonitorService clipboardMonitorService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        double panelWidth)
    {
        _quickTextService = quickTextService;
        _clipboardMonitorService = clipboardMonitorService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _panelWidth = panelWidth;
        _ = ReloadAsync();
    }

    partial void OnSelectedModeChanged(QuickTextMode value)
    {
        OnPropertyChanged(nameof(IsHistorySelected));
        OnPropertyChanged(nameof(IsSnippetsSelected));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    [RelayCommand]
    private void SelectMode(string? mode)
    {
        SelectedMode = string.Equals(mode, "Snippets", StringComparison.OrdinalIgnoreCase)
            ? QuickTextMode.Snippets
            : QuickTextMode.History;
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        _allHistory = await _quickTextService.GetClipboardHistoryAsync(10_000);
        _allSnippets = await _quickTextService.GetTextSnippetsAsync();
        ApplyFilters();
    }

    [RelayCommand]
    private async Task CopyHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Content))
        {
            return;
        }

        if (_clipboardMonitorService.TrySetText(item.Content))
        {
            await _quickTextService.RecordClipboardTextAsync(item.Content);
            await ReloadAsync();
            _notificationService.ShowSuccessMessage(L("Common.Copied"));
        }
    }

    [RelayCommand]
    private async Task FavoriteHistoryAsync(ClipboardHistoryItem? item)
    {
        var snippet = await _quickTextService.CreateSnippetFromHistoryAsync(item);
        if (snippet == null)
        {
            _notificationService.ShowWarningMessage(L("QuickText.FavoriteFailed"));
            return;
        }

        SelectedMode = QuickTextMode.Snippets;
        await ReloadAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.Favorited"));
    }

    [RelayCommand]
    private async Task DeleteHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null)
        {
            return;
        }

        await _quickTextService.DeleteClipboardHistoryAsync(item.Id);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickText.ClearHistoryConfirm"), L("QuickText.ClearHistoryTitle")))
        {
            return;
        }

        await _quickTextService.ClearClipboardHistoryAsync();
        await ReloadAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.HistoryCleared"));
    }

    [RelayCommand]
    private async Task CopySnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null || string.IsNullOrWhiteSpace(snippet.Content))
        {
            return;
        }

        if (_clipboardMonitorService.TrySetText(snippet.Content))
        {
            await _quickTextService.MarkSnippetUsedAsync(snippet.Id);
            await ReloadAsync();
            _notificationService.ShowSuccessMessage(L("Common.Copied"));
        }
    }

    [RelayCommand]
    private async Task NewSnippetAsync()
    {
        var window = new TextSnippetEditWindow(new TextSnippetEditViewModel(_quickTextService, _localizationService), _panelWidth)
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null)
        {
            return;
        }

        var window = new TextSnippetEditWindow(new TextSnippetEditViewModel(_quickTextService, _localizationService, snippet), _panelWidth)
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null)
        {
            return;
        }

        if (!_notificationService.ShowConfirmDialog(
                _localizationService.Format("QuickText.DeleteSnippetConfirmFormat", snippet.DisplayTitle),
                L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _quickTextService.DeleteTextSnippetAsync(snippet.Id);
        await ReloadAsync();
    }

    private void ApplyFilters()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        ClipboardHistory.Clear();
        foreach (var item in _allHistory.Where(item => Matches(query, item.Content)))
        {
            ClipboardHistory.Add(item);
        }

        TextSnippets.Clear();
        foreach (var snippet in _allSnippets.Where(snippet =>
                     (SelectedCategory == "全部" || string.Equals(snippet.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)) &&
                     (Matches(query, snippet.Title) || Matches(query, snippet.Content) || Matches(query, snippet.Category))))
        {
            TextSnippets.Add(snippet);
        }
    }

    private static bool Matches(string query, string? value) =>
        string.IsNullOrWhiteSpace(query) ||
        (!string.IsNullOrWhiteSpace(value) &&
         value.Contains(query, StringComparison.OrdinalIgnoreCase));

    private string L(string key) => _localizationService.GetString(key);
}
