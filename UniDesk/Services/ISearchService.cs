using UniDesk.Models;

namespace UniDesk.Services;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string keyword,
        int limitPerKind = 5,
        CancellationToken cancellationToken = default);
}
