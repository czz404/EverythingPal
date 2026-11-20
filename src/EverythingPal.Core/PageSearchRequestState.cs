// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

public enum PageSearchActionKind
{
    None,
    Start,
}

/// <summary>Result of feeding one host search-text callback into <see cref="PageSearchRequestState"/>.</summary>
public readonly record struct PageSearchAction(PageSearchActionKind Kind, string Query)
{
    public bool ShouldStart => Kind == PageSearchActionKind.Start;

    public static PageSearchAction None => new(PageSearchActionKind.None, string.Empty);
}

/// <summary>
/// Tracks the deferred initial query of a result page across the host callbacks.
///
/// CmdPal can call <c>UpdateSearchText</c> with the text we just pre-filled, *before* the first
/// <c>GetItems</c>. Treating that echo as a new query would clear the pending marker and the page
/// would never start the search. The rule is therefore:
///   * an update identical to the pending query keeps the marker for the first GetItems();
///   * an update with genuinely different text supersedes the marker and starts immediately;
///   * once the marker has been consumed, an identical update is just a host re-send (no search).
///
/// This is pure state so the three callback orders are unit-testable without the WinRT page.
/// </summary>
public sealed class PageSearchRequestState
{
    private string _currentQuery = string.Empty;
    private string? _pendingQuery;

    public string CurrentQuery => _currentQuery;

    public string? PendingQuery => _pendingQuery;

    /// <summary>Records the query a home fallback row wants the page to show.</summary>
    public void SetPrefill(string? query)
    {
        var normalized = Normalize(query);
        _pendingQuery = normalized;
        _currentQuery = normalized;
    }

    /// <summary>Drops the deferred query without touching the current query.</summary>
    public void ClearPending() => _pendingQuery = null;

    /// <summary>
    /// Consumes the deferred query for the first <c>GetItems</c>. Returns null when nothing is
    /// pending; the caller must clear it before starting the search (RaiseItemsChanged re-enters).
    /// </summary>
    public string? TakePendingQuery()
    {
        var pending = _pendingQuery;
        _pendingQuery = null;
        return pending;
    }

    /// <summary>Feeds one <c>UpdateSearchText(old, new)</c> callback and returns what to do.</summary>
    public PageSearchAction OnUpdateSearchText(string? newSearch)
    {
        var query = Normalize(newSearch);

        // The host may fire the activation callback with the old empty text while the deferred
        // home query is still waiting for GetItems(); do not erase the pre-filled query.
        if (query.Length == 0 && _pendingQuery is { Length: > 0 })
        {
            return PageSearchAction.None;
        }

        if (_pendingQuery is not null)
        {
            // The host is echoing the text we just pre-filled. Keep the marker: the first GetItems
            // is the one place that starts this search.
            if (string.Equals(query, _pendingQuery, StringComparison.Ordinal))
            {
                return PageSearchAction.None;
            }

            // Genuinely different input supersedes the deferred query.
            _pendingQuery = null;
        }

        // Re-sent text after the deferred search already started/completed is not a new search.
        if (string.Equals(query, _currentQuery, StringComparison.Ordinal))
        {
            return PageSearchAction.None;
        }

        _currentQuery = query;
        return new PageSearchAction(PageSearchActionKind.Start, query);
    }

    private static string Normalize(string? query) => query?.Trim() ?? string.Empty;
}
