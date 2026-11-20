// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Commands;
using EverythingPal.Core;
using EverythingPal.Localization;
using EverythingPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal;

/// <summary>
/// Main search page: type a query, get live results from Everything via the bundled es.exe.
/// </summary>
internal sealed partial class EverythingPalPage : DynamicListPage, IDisposable
{
    private const int DebounceMilliseconds = 150;
    private IListItem[] _items = [];
    private string _query = string.Empty;
    private string? _pendingQuery;
    private CancellationTokenSource? _cts;

    public EverythingPalPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = Strings.PageTitle;
        Name = Strings.PageName;
        PlaceholderText = Strings.PagePlaceholderText;

        // Every result item already carries Details (name, full path, type, size, modified). The
        // toolkit defaults this to false, which is why the host never showed the details pane.
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        // A home fallback row opened this page with a pre-filled search box. Start that search only
        // now that the host actually shows the page, so merely hovering the home results does not
        // spawn a full 50-result query. Consume the marker BEFORE starting: RaiseItemsChanged makes
        // the host call GetItems() again, and starting a search on every call would loop forever.
        var pending = _pendingQuery;
        if (pending is not null)
        {
            _pendingQuery = null;
            StartSearch(pending);
        }

        return _items;
    }

    /// <summary>
    /// Pre-fills the page search text (used when a home-screen fallback row opens the full result
    /// list). The search itself runs on the first <see cref="GetItems"/> call: setting SearchText
    /// through its normal setter would start the debounced query immediately and generate the full
    /// result page (plus icons) even when the user never opens it.
    /// </summary>
    internal void SetSearchQuery(string query)
    {
        var normalized = query?.Trim() ?? string.Empty;

        _pendingQuery = normalized;
        _query = normalized;
        _items = [];

        // Drop any search still running for the previously displayed query: this page is only
        // navigated to from the home rows, so a leftover task must not repopulate the old list.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsLoading = false;

        // Bypass DynamicListPage.SearchText's setter (which calls UpdateSearchText immediately).
        SetSearchNoUpdate(normalized);
    }

    /// <summary>
    /// Drops a deferred initial query without touching the currently displayed list. Used when a
    /// newer home search has no results (or fails) and the shared page must not later open with the
    /// previous query.
    /// </summary>
    internal void ClearPendingSearch() => _pendingQuery = null;

    public void Dispose()
    {
        _pendingQuery = null;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var query = newSearch?.Trim() ?? string.Empty;

        // The host may fire the activation callback with the old empty text while the deferred
        // home query is still waiting for GetItems(); do not let that erase the pre-filled query.
        if (query.Length == 0 && _pendingQuery is { Length: > 0 })
        {
            return;
        }

        // A manual edit supersedes the deferred home-page query.
        _pendingQuery = null;

        // CmdPal can re-send the text we just pre-filled. That is not a new search: starting one
        // here would duplicate the deferred search and eventually keep re-querying ES.
        if (string.Equals(query, _query, StringComparison.Ordinal))
        {
            return;
        }

        StartSearch(query);
    }

    private void StartSearch(string query)
    {
        _query = query;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (query.Length == 0)
        {
            _items = [];
            IsLoading = false;
            RaiseItemsChanged();
            return;
        }

        IsLoading = true;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    // Capture the query for this task only: _query may change while ES runs.
                    var outcome = await EverythingSearch.SearchAsync(
                        query,
                        EverythingPalSettings.Instance.ToSearchOptions(),
                        token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    var items = await BuildItemsAsync(outcome, token).ConfigureAwait(false);

                    // A newer keystroke may have started while icons were resolving: never overwrite
                    // the newer query's results / loading state from this stale task.
                    token.ThrowIfCancellationRequested();

                    _items = items;
                    IsLoading = false;
                    RaiseItemsChanged(_items.Length);
                }
                catch (OperationCanceledException)
                {
                    // A newer keystroke (or a closed page) superseded this query; it owns the UI state.
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        // Stale failure: the newer query owns the UI state.
                        return;
                    }

                    AppLog.Write($"EverythingPalPage search failed: {ex}");
                    _items = [StatusItem(Strings.SearchFailedTitle, ex.Message, new NoOpCommand())];
                    IsLoading = false;
                    RaiseItemsChanged(_items.Length);
                }
            },
            token);
    }

    private static async Task<IListItem[]> BuildItemsAsync(SearchOutcome outcome, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        switch (outcome.Status)
        {
            case SearchStatus.Ok:
                // Icon extraction is off the UI thread (this method runs inside Task.Run) and cached
                // per file type; resolving the page in parallel keeps the cold path at a few ms.
                return await Task.WhenAll(outcome.Results.Select(result => BuildItemAsync(result, token))).ConfigureAwait(false);
            case SearchStatus.NoResults:
                return [StatusItem(Strings.NoResultsTitle, Strings.NoResultsSubtitle, new NoOpCommand())];
            case SearchStatus.EverythingNotRunning:
                return
                [
                    StatusItem(
                        Strings.EverythingNotRunningTitle,
                        Strings.EverythingNotRunningSubtitle,
                        new StartEverythingCommand(),
                        new CommandContextItem(new StartEverythingCommand()) { Title = Strings.StartEverythingAction },
                        new CommandContextItem(new OpenEverythingDownloadPageCommand()) { Title = Strings.DownloadEverythingAction }),
                ];
            case SearchStatus.EsExeNotFound:
                return
                [
                    StatusItem(
                        Strings.EsExeNotFoundTitle,
                        Strings.EsExeNotFoundSubtitle,
                        new OpenEverythingDownloadPageCommand()),
                ];
            case SearchStatus.Timeout:
                return [StatusItem(Strings.TimeoutTitle, Strings.TimeoutSubtitle, new NoOpCommand())];
            default:
                return [StatusItem(Strings.QueryFailedTitle, outcome.Error ?? Strings.UnknownError, new NoOpCommand())];
        }
    }

    private static async Task<IListItem> BuildItemAsync(EverythingResult result, CancellationToken token)
    {
        var icon = await ShellIconProvider.GetIconAsync(result.FullPath, result.IsFolder).ConfigureAwait(false);

        // The query may have been superseded while the icon was resolving; abort before building
        // (and handing back) a stale list.
        token.ThrowIfCancellationRequested();

        return new ListItem(new OpenItemCommand(result.FullPath, result.IsFolder))
        {
            Title = result.Name,
            Subtitle = result.Directory,
            Icon = icon ?? (result.IsFolder ? Icons.Folder : Icons.File),
            Details = BuildDetails(result),
            MoreCommands =
            [
                new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder)) { Title = Strings.RevealInExplorerAction },
                new CommandContextItem(new CopyItemPathCommand(result.FullPath)) { Title = Strings.CopyPathAction },
                new CommandContextItem(new OpenInEverythingCommand(result.FullPath)) { Title = Strings.LocateInEverythingAction },
            ],
        };
    }

    private static Details BuildDetails(EverythingResult result)
    {
        var metadata = new List<IDetailsElement>
        {
            new DetailsElement { Key = Strings.DetailsName, Data = new DetailsLink { Text = result.Name, Link = new Uri(result.FullPath) } },
            new DetailsElement { Key = Strings.DetailsFullPath, Data = new DetailsLink { Text = result.FullPath, Link = new Uri(result.FullPath) } },
            new DetailsElement { Data = new DetailsSeparator() },
            new DetailsElement { Key = Strings.DetailsType, Data = new DetailsLink { Text = result.IsFolder ? Strings.DetailsFolder : Strings.DetailsFile } },
        };

        if (!result.IsFolder)
        {
            metadata.Add(new DetailsElement { Key = Strings.DetailsSize, Data = new DetailsLink { Text = FormatSize(result.Size) } });
        }

        if (result.DateModified is { } modified)
        {
            metadata.Add(new DetailsElement { Key = Strings.DetailsModified, Data = new DetailsLink { Text = modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) } });
        }

        return new Details { Metadata = [.. metadata] };
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }

    private static ListItem StatusItem(string title, string subtitle, ICommand command, params IContextItem[] moreCommands) =>
        new ListItem(command)
        {
            Title = title,
            Subtitle = subtitle,
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
            MoreCommands = moreCommands,
        };
}




