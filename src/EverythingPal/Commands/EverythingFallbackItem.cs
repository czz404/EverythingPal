// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using EverythingPal.Commands;
using EverythingPal.Services;

namespace EverythingPal;

/// <summary>
/// Fallback item shown on the Command Palette home search when nothing else matches.
///
/// CmdPal's fallback model is one row per extension (the built-in file-search fallback works the
/// same way), so this row surfaces the best Everything match: Enter opens it directly, and the
/// context menu offers reveal / copy path / locate in Everything. Typing "wantai" on the home
/// screen therefore surfaces the real folder D:\wantai instead of a stale Recent shortcut.
/// </summary>
internal sealed partial class EverythingFallbackItem : FallbackCommandItem, IDisposable
{
    private const string CommandId = "czz404.everythingpal.fallback";
    private const int MaxResults = 10;
    private const int DebounceMilliseconds = 150;

    private static readonly NoOpCommand BaseCommand = new() { Id = CommandId };
    private static readonly IconInfo FolderIcon = new("\uE8B7");
    private static readonly IconInfo FileIcon = new("\uE8A5");
    private static readonly IconInfo AppIcon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    private readonly EverythingSearch _search = new();
    private CancellationTokenSource? _cts;
    private string _query = string.Empty;

    internal EverythingFallbackItem()
        : base(BaseCommand, "Everything 搜索", CommandId)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = AppIcon;
    }

    public override void UpdateQuery(string query)
    {
        _query = query?.Trim() ?? string.Empty;

        AppLog.Write($"fallback UpdateQuery: '{_query}'");

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (_query.Length == 0)
        {
            Clear();
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    var outcome = _search.Search(_query, MaxResults);
                    token.ThrowIfCancellationRequested();
                    Apply(outcome);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer keystroke.
                }
                catch (Exception ex)
                {
                    AppLog.Write($"EverythingFallbackItem failed: {ex}");
                }
            },
            token);
    }

    private void Apply(SearchOutcome outcome)
    {
        var topSuffix = outcome.Results.Count > 0 ? $", top='{outcome.Results[0].FullPath}'" : string.Empty;
        AppLog.Write($"fallback result: {outcome.Status}, {outcome.Results.Count} item(s){topSuffix}");
        if (outcome.Status is SearchStatus.EverythingNotRunning or SearchStatus.EsExeNotFound)
        {
            Title = "Everything 不可用";
            Subtitle = outcome.Error ?? "请检查 Everything 是否运行";
            Icon = AppIcon;
            Command = BaseCommand;
            MoreCommands = [];
            return;
        }

        if (outcome.Status != SearchStatus.Ok || outcome.Results.Count == 0)
        {
            Clear();
            return;
        }

        var best = outcome.Results[0];
        var extra = outcome.Results.Count > 1 ? $"（共 {outcome.Results.Count} 个结果，Enter 打开第一个）" : string.Empty;

        Title = best.Name;
        Subtitle = $"{best.Directory}{extra}";
        Icon = best.IsFolder ? FolderIcon : FileIcon;
        Command = new OpenItemCommand(best.FullPath, best.IsFolder);
        MoreCommands =
        [
            new CommandContextItem(new RevealInExplorerCommand(best.FullPath, best.IsFolder))
            {
                Title = "在资源管理器中显示",
            },
            new CommandContextItem(new CopyItemPathCommand(best.FullPath))
            {
                Title = "复制路径",
            },
            new CommandContextItem(new OpenInEverythingCommand(best.FullPath))
            {
                Title = "在 Everything 中定位",
            },
        ];
    }

    private void Clear()
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Command = BaseCommand;
        MoreCommands = [];
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

