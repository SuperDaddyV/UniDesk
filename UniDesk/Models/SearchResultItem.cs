namespace UniDesk.Models;

public enum SearchResultKind
{
    QuickNote,
    Todo,
    Clipboard,
    Snippet,
    Shortcut
}

public sealed record SearchResultItem(
    SearchResultKind Kind,
    int Id,
    string Title,
    string Snippet,
    string ActionValue);
