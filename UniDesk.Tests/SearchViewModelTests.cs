using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk.Tests;

public class SearchViewModelTests
{
    [Fact]
    public async Task SearchNowAsync_ShouldGroupResultsInProductOrder()
    {
        var service = new StubSearchService(
        [
            new(SearchResultKind.Shortcut, 5, "工具", "", "tool.exe"),
            new(SearchResultKind.QuickNote, 1, "便签", "内容", ""),
            new(SearchResultKind.Todo, 2, "待办", "", "")
        ]);
        var viewModel = new SearchViewModel(service, new StubLocalizationService(), _ => Task.CompletedTask)
        {
            SearchText = "项目"
        };

        await viewModel.SearchNowAsync();

        Assert.Equal(
            [SearchResultKind.QuickNote, SearchResultKind.Todo, SearchResultKind.Shortcut],
            viewModel.Groups.Select(group => group.Kind));
    }

    [Fact]
    public async Task ActivateResultCommand_ShouldDelegateSelectedResult()
    {
        SearchResultItem? activated = null;
        var result = new SearchResultItem(SearchResultKind.Todo, 9, "待办", "", "");
        var viewModel = new SearchViewModel(
            new StubSearchService([result]),
            new StubLocalizationService(),
            item =>
            {
                activated = item;
                return Task.CompletedTask;
            });

        await viewModel.ActivateResultCommand.ExecuteAsync(result);

        Assert.Same(result, activated);
    }

    private sealed class StubSearchService(IReadOnlyList<SearchResultItem> results) : ISearchService
    {
        public Task<IReadOnlyList<SearchResultItem>> SearchAsync(
            string keyword,
            int limitPerKind = 5,
            CancellationToken cancellationToken = default) => Task.FromResult(results);
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged { add { } remove { } }
        public string CurrentLanguage => "zh-CN";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("zh-CN");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => language ?? "zh-CN";
        public void SetLanguage(string? language) { }
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) => string.Format(key, args);
    }
}
