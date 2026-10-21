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
    private CancellationTokenSource? _cts;

    public EverythingPalPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = Strings.PageTitle;
        Name = Strings.PageName;
        PlaceholderText = Strings.PagePlaceholderText;
    }

    public override IListItem[] GetItems() => _items;

    /// <summary>
    /// Pre-fills the page search text (used when a home-screen fallback row opens the full result
    /// list). The dynamic-list page starts its normal debounced search from <see cref="UpdateSearchText"/>.
    /// </summary>
    internal void SetSearchQuery(string query) => SearchText = query;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _query = newSearch?.Trim() ?? string.Empty;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (_query.Length == 0)
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

                    var outcome = EverythingSearch.Search(_query, EverythingPalSettings.Instance.ToSearchOptions());
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
                    // A newer keystroke superseded this query; it owns the UI state.
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




