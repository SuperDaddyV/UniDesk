using UniDesk.Models;

namespace UniDesk.ViewModels;

public sealed class SearchResultGroupViewModel
{
    public SearchResultGroupViewModel(SearchResultKind kind, string title, IReadOnlyList<SearchResultItem> items)
    {
        Kind = kind;
        Title = title;
        Items = items;
    }

    public SearchResultKind Kind { get; }
    public string Title { get; }
    public IReadOnlyList<SearchResultItem> Items { get; }
}
