using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class SearchViewModel : ObservableObject, IDisposable
{
    private static readonly SearchResultKind[] GroupOrder =
    [
        SearchResultKind.QuickNote,
        SearchResultKind.Todo,
        SearchResultKind.Clipboard,
        SearchResultKind.Snippet,
        SearchResultKind.Shortcut
    ];

    private readonly ISearchService _searchService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<SearchResultItem, Task> _activateResult;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(250));
    private CancellationTokenSource? _searchCts;
    private int _searchVersion;
    private bool _disposed;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<SearchResultGroupViewModel> Groups { get; } = new();

    public event EventHandler? FocusRequested;

    public SearchViewModel(
        ISearchService searchService,
        ILocalizationService localizationService,
        Func<SearchResultItem, Task> activateResult)
    {
        _searchService = searchService;
        _localizationService = localizationService;
        _activateResult = activateResult;
        StatusText = L("Search.Prompt");
    }

    [RelayCommand]
    private void Open()
    {
        IsOpen = true;
        FocusRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        _debouncer.Cancel();
        CancelCurrentSearch();
    }

    [RelayCommand]
    private async Task ActivateResult(SearchResultItem? result)
    {
        if (result == null)
        {
            return;
        }

        await _activateResult(result);
        IsOpen = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_disposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            _searchVersion++;
            Groups.Clear();
            StatusText = L("Search.Prompt");
            IsSearching = false;
            _debouncer.Cancel();
            CancelCurrentSearch();
            return;
        }

        _debouncer.Schedule(() => _ = SearchNowAsync());
    }

    public async Task SearchNowAsync()
    {
        var query = SearchText.Trim();
        if (query.Length == 0)
        {
            Groups.Clear();
            StatusText = L("Search.Prompt");
            return;
        }

        var version = ++_searchVersion;
        var cancellationSource = new CancellationTokenSource();
        var previousSource = Interlocked.Exchange(ref _searchCts, cancellationSource);
        previousSource?.Cancel();
        IsSearching = true;
        StatusText = L("Search.Searching");
        try
        {
            var results = await _searchService.SearchAsync(
                query,
                cancellationToken: cancellationSource.Token);
            if (version != _searchVersion)
            {
                return;
            }

            Groups.Clear();
            foreach (var kind in GroupOrder)
            {
                var items = results.Where(result => result.Kind == kind).ToList();
                if (items.Count > 0)
                {
                    Groups.Add(new SearchResultGroupViewModel(kind, GetGroupTitle(kind), items));
                }
            }

            StatusText = Groups.Count == 0 ? L("Search.Empty") : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SearchViewModel.SearchNowAsync");
            if (version == _searchVersion)
            {
                Groups.Clear();
                StatusText = L("Search.Failed");
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _searchCts, null, cancellationSource);
            cancellationSource.Dispose();
            if (version == _searchVersion)
            {
                IsSearching = false;
            }
        }
    }

    private string GetGroupTitle(SearchResultKind kind) => kind switch
    {
        SearchResultKind.QuickNote => L("Search.GroupQuickNotes"),
        SearchResultKind.Todo => L("Search.GroupTodos"),
        SearchResultKind.Clipboard => L("Search.GroupClipboard"),
        SearchResultKind.Snippet => L("Search.GroupSnippets"),
        SearchResultKind.Shortcut => L("Search.GroupShortcuts"),
        _ => string.Empty
    };

    private string L(string key) => _localizationService.GetString(key);

    private void CancelCurrentSearch()
    {
        Interlocked.Exchange(ref _searchCts, null)?.Cancel();
    }

    public void Dispose()
    {
        _disposed = true;
        _searchVersion++;
        CancelCurrentSearch();
        _debouncer.Dispose();
    }
}
