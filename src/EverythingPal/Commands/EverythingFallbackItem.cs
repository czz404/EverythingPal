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
/// Coordinates the home-search fallback rows.
///
/// CmdPal's fallback model is one row per fallback command, so we register one
/// <see cref="EverythingFallbackItem"/> per row and distribute the top N results across them.
/// That also means the CmdPal "回退命令" settings list shows N entries; each item therefore has a
/// distinct <see cref="FallbackCommandItem.DisplayTitle"/> ("第 1/2/3 行") while the persisted IDs
/// stay stable, so existing fallback settings keep working. The group only runs one search per
/// query: every row receives the same UpdateQuery call, and repeated identical queries are ignored.
/// </summary>
internal sealed partial class EverythingFallbackGroup : IDisposable
{
    // CmdPal detaches/ignores inline fallback updates that do not arrive within its ~200 ms budget.
    // The home path is therefore kept short: 50 ms debounce (still coalesces keystrokes) + one
    // es.exe call (~40 ms warm) puts Title assignment comfortably under 200 ms. The main search page
    // keeps its own, longer, 150 ms debounce.
    private const int DebounceMilliseconds = 50;
    private readonly EverythingFallbackItem[] _items;
    private readonly EverythingPalPage _resultsPage = new();
    private CancellationTokenSource? _cts;
    private string _query = string.Empty;

    public EverythingFallbackGroup(int rowCount)
    {
        rowCount = Math.Clamp(rowCount, 1, 5);
        _items = Enumerable.Range(0, rowCount).Select(index => new EverythingFallbackItem(this, index, rowCount)).ToArray();
    }

    public IFallbackCommandItem[] Items => _items;

    /// <summary>The full result page a home row opens when it represents more than one match.</summary>
    internal EverythingPalPage ResultsPage => _resultsPage;

    internal void OnUpdateQuery(string? query)
    {
        var normalized = QueryNormalizer.Normalize(query);

        // CmdPal calls UpdateQuery once per fallback row for the same keystroke. The first call has
        // already started the debounced search (or cleared the rows), so the remaining rows must not
        // restart it; and a re-sent unchanged query keeps the current results.
        if (string.Equals(normalized, _query, StringComparison.Ordinal))
        {
            return;
        }

        _query = normalized;
        var searchQuery = _query;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (searchQuery.Length == 0)
        {
            ClearAll();
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    var outcome = EverythingSearch.Search(searchQuery, EverythingPalSettings.Instance.ToSearchOptions(maxResultsOverride: _items.Length));
                    token.ThrowIfCancellationRequested();
                    await DistributeAsync(outcome, searchQuery, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer keystroke.
                }
                catch (Exception ex)
                {
                    AppLog.Write($"EverythingFallbackGroup failed: {ex}");
                }
            },
            token);
    }

    private async Task DistributeAsync(SearchOutcome outcome, string query, CancellationToken token)
    {
        AppLog.Write($"fallback result: {outcome.Status}, {outcome.Results.Count} item(s)");

        // Do not touch the rows on behalf of a query that was already superseded.
        token.ThrowIfCancellationRequested();
        ClearAll();

        // A successful search with zero hits (or Core's NoResults) keeps one informative row instead
        // of blank rows: the query stays the title so CmdPal's live re-scoring keeps it, with the
        // localized "no results" status in the subtitle. (Older Core versions return Ok/0.)
        var status = outcome.Status == SearchStatus.Ok && outcome.Results.Count == 0
            ? SearchStatus.NoResults
            : outcome.Status;

        if (status == SearchStatus.NoResults)
        {
            token.ThrowIfCancellationRequested();
            _items[0].ShowNoResults(query);
            return;
        }

        if (status != SearchStatus.Ok)
        {
            token.ThrowIfCancellationRequested();
            _items[0].ShowStatus(status, outcome.Error, query);
            return;
        }

        var visible = Math.Min(_items.Length, outcome.Results.Count);

        // The shared full-result page is pre-filled once (not once per row) so pressing Enter on an
        // inline row that represents multiple matches opens the complete list immediately.
        _resultsPage.SetSearchQuery(query);

        var tasks = new Task[visible];
        for (var i = 0; i < visible; i++)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            tasks[i] = _items[i].ShowResultAsync(outcome.Results[i], outcome.Results.Count, query, token);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private void ClearAll()
    {
        foreach (var item in _items)
        {
            item.Clear();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _resultsPage.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>One home-search row, showing the N-th Everything result.</summary>
internal sealed partial class EverythingFallbackItem(EverythingFallbackGroup group, int index, int totalRows)
    : FallbackCommandItem(
        new NoOpCommand { Id = "czz404.everythingpal.fallback" },
        Strings.FormatFallbackRowTitle(index + 1, totalRows),
        $"czz404.everythingpal.fallback.{index}")
{
    private static readonly IconInfo AppIcon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    public override void UpdateQuery(string query) => group.OnUpdateQuery(query);

    internal async Task ShowResultAsync(EverythingResult result, int totalCount, string query, CancellationToken token)
    {
        var extra = totalCount > 1 && index == 0 ? Strings.FormatResultCount(totalCount) : string.Empty;

        // Fill the row immediately: CmdPal only keeps inline fallback rows for a short budget, so
        // the row content must not wait for shell icon extraction. The icon is upgraded below.
        Title = result.Name;
        Subtitle = $"{result.Directory}{extra}";
        Icon = result.IsFolder ? Icons.Folder : Icons.File;

        // A row that represents several matches opens the complete result page (the same query);
        // a single match keeps the convenient "open the file" primary action and offers the full
        // page in the context menu. This makes the "共 N 个结果" subtitle meaningful.
        var openItem = new OpenItemCommand(result.FullPath, result.IsFolder);
        if (totalCount > 1)
        {
            Command = group.ResultsPage;
            MoreCommands =
            [
                new CommandContextItem(openItem) { Title = Strings.OpenAction },
                new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder)) { Title = Strings.RevealInExplorerAction },
                new CommandContextItem(new CopyItemPathCommand(result.FullPath)) { Title = Strings.CopyPathAction },
                new CommandContextItem(new OpenInEverythingCommand(result.FullPath)) { Title = Strings.LocateInEverythingAction },
            ];
        }
        else
        {
            Command = openItem;
            MoreCommands =
            [
                new CommandContextItem(group.ResultsPage) { Title = Strings.ShowAllResultsAction },
                new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder)) { Title = Strings.RevealInExplorerAction },
                new CommandContextItem(new CopyItemPathCommand(result.FullPath)) { Title = Strings.CopyPathAction },
                new CommandContextItem(new OpenInEverythingCommand(result.FullPath)) { Title = Strings.LocateInEverythingAction },
            ];
        }

        var icon = await ShellIconProvider.GetIconAsync(result.FullPath, result.IsFolder).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            // A newer query owns the row; never overwrite its icon from this stale result.
            return;
        }

        Icon = icon ?? (result.IsFolder ? Icons.Folder : Icons.File);
    }

    internal void ShowStatus(SearchStatus status, string? error, string query)
    {
        var statusTitle = status switch
        {
            SearchStatus.EverythingNotRunning => Strings.EverythingNotRunningTitle,
            SearchStatus.EsExeNotFound => Strings.EsExeNotFoundTitle,
            SearchStatus.Timeout => Strings.FallbackTimeoutTitle,
            _ => Strings.FallbackQueryFailedTitle,
        };

        var detail = status switch
        {
            SearchStatus.EverythingNotRunning => Strings.FallbackEverythingNotRunningSubtitle,
            SearchStatus.EsExeNotFound => Strings.FallbackEsExeNotFoundSubtitle,
            _ => error ?? Strings.FallbackRetryHint,
        };

        // CmdPal re-scores global fallback rows against the typed query, so a status-only title is
        // filtered out of the home results. The query therefore becomes the title (the row keeps
        // matching what the user typed) and the localized status + detail go in the subtitle.
        Title = query.Length == 0 ? statusTitle : query;
        Subtitle = query.Length == 0 ? detail : $"{statusTitle} · {detail}";

        Icon = AppIcon;
        Command = status == SearchStatus.EverythingNotRunning
            ? new StartEverythingCommand()
            : new OpenEverythingDownloadPageCommand();
        MoreCommands =
        [
            new CommandContextItem(new StartEverythingCommand()) { Title = Strings.StartEverythingAction },
            new CommandContextItem(new OpenEverythingDownloadPageCommand()) { Title = Strings.DownloadEverythingAction },
        ];
    }

    /// <summary>
    /// Zero-hit row. There is no action to offer, but leaving the rows fully blank made CmdPal render
    /// empty rows that still carried the previous result's icon under 回退. Keep the query as the
    /// title (scoreable against what the user typed) and the localized "no results" status in the
    /// subtitle; rows 2..N stay fully cleared.
    /// </summary>
    internal void ShowNoResults(string query)
    {
        var statusTitle = Strings.NoResultsTitle;
        Title = query.Length == 0 ? statusTitle : query;
        Subtitle = $"{statusTitle} · {Strings.NoResultsSubtitle}";
        Icon = AppIcon;
        Command = new NoOpCommand { Id = "czz404.everythingpal.fallback" };
        MoreCommands = [];
    }

    internal void Clear()
    {
        Title = string.Empty;
        Subtitle = string.Empty;

        // Also drop the previous result's icon, otherwise a cleared fallback row renders as a blank
        // row with a stale file icon.
        Icon = null;
        Command = new NoOpCommand { Id = "czz404.everythingpal.fallback" };
        MoreCommands = [];
    }
}

/// <summary>Icons shared by the list page and the fallback rows (fallback glyphs when no shell icon).</summary>
internal static class Icons
{
    internal static readonly IconInfo Folder = new("\uE8B7");
    internal static readonly IconInfo File = new("\uE8A5");
}

