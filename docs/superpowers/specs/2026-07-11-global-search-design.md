# UniDesk Global Search Design

**Date:** 2026-07-11  
**Status:** Approved for implementation

## Goal

Add a fast search workspace to the main title bar that searches Quick Notes, incomplete and completed Todos, clipboard history, text snippets, and shortcuts without changing the database schema.

## User Experience

- A search icon in the main title bar opens a glass search surface directly below the title bar.
- `Ctrl+F` opens and focuses the search box while the main window is active.
- Typing waits 250 milliseconds before querying; newer input cancels or supersedes older results.
- Results are grouped into Quick Notes, Todos, clipboard history, text snippets, and shortcuts, with at most five results per group.
- `Escape` closes search. An empty query shows a localized prompt rather than querying.
- Quick Note results open the existing editor; Todo results bring the Todo module into view and highlight the matching row for two seconds; clipboard and snippet results copy the content; shortcut results use the existing safe launch path.
- The first version has no FTS table, command syntax, ranking configuration, persisted history, or “more results” page.

## Architecture

- `ISearchService.SearchAsync(string, int, CancellationToken)` owns read-only SQLite search and returns grouped-neutral `SearchResultItem` records.
- `SearchViewModel` owns query state, debouncing, groups, status text, and result activation. It delegates module-specific actions through one callback supplied by `MainWindowViewModel`.
- `MainWindowViewModel` coordinates existing module view models; it does not contain SQL or debounce logic.
- The search service uses bounded, escaped `LIKE` queries. It must not rely on `Task.WhenAll` for SQLite concurrency because the provider's async APIs execute synchronously in this application.

## Search Semantics

- `%`, `_`, and the escape character are treated as literal user text.
- Quick Notes match title or content.
- Todos match title and order incomplete items first.
- Clipboard history matches content.
- Text snippets match title or content.
- Shortcuts match name or path.
- Matching is case-insensitive under SQLite's supported collation behavior. Search failures are logged and produce a localized failure state without crashing the window.

## Accessibility and Localization

All prompt, group, empty, failure, copy, and launch labels exist in Simplified Chinese, English, Japanese, and Spanish. Search results are keyboard reachable; Enter activates the selected result and Escape closes the surface.

## Verification

- Unit tests cover LIKE escaping and snippets with matches at the beginning, middle, end, Chinese text, emoji, `%`, and `_`.
- Database integration tests cover all five result kinds and the per-kind limit.
- View-model tests cover debounce supersession, empty queries, grouping, and activation delegation.
- Structural WPF tests cover the title-bar button, `Ctrl+F`, search surface, and result bindings.

## Out of Scope

FTS5, fuzzy matching, web search, command execution, search analytics, database migration, and new dependencies.
