// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Commands;
using EverythingPal.Core;
using EverythingPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal;

/// <summary>
/// Coordinates the home-search fallback rows.
///
/// CmdPal's fallback model is one row per fallback command, so we register several
/// <see cref="EverythingFallbackItem"/>s and distribute the top N results across them. This gives
/// inline multi-result search on the home screen while staying within the SDK contract.
/// </summary>
internal sealed partial class EverythingFallbackGroup : IDisposable
{
    private const int DebounceMilliseconds = 150;

    private readonly EverythingSearch _search = new();
    private readonly EverythingFallbackItem[] _items;
    private CancellationTokenSource? _cts;
    private string _query = string.Empty;

    public EverythingFallbackGroup(int rowCount)
    {
        rowCount = Math.Clamp(rowCount, 1, 5);
        _items = Enumerable.Range(0, rowCount).Select(index => new EverythingFallbackItem(this, index)).ToArray();
    }

    public IFallbackCommandItem[] Items => _items;

    internal void OnUpdateQuery(string? query)
    {
        _query = QueryNormalizer.Normalize(query);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (_query.Length == 0)
        {
            ClearAll();
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    var outcome = _search.Search(_query, EverythingPalSettings.Instance.ToSearchOptions(maxResultsOverride: _items.Length));
                    token.ThrowIfCancellationRequested();
                    Distribute(outcome);
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

    private void Distribute(SearchOutcome outcome)
    {
        AppLog.Write($"fallback result: {outcome.Status}, {outcome.Results.Count} item(s)");

        ClearAll();

        if (outcome.Status != SearchStatus.Ok)
        {
            _items[0].ShowStatus(outcome.Status, outcome.Error);
            return;
        }

        for (var i = 0; i < _items.Length && i < outcome.Results.Count; i++)
        {
            _items[i].ShowResult(outcome.Results[i], outcome.Results.Count);
        }
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
        GC.SuppressFinalize(this);
    }
}

/// <summary>One home-search row, showing the N-th Everything result.</summary>
internal sealed partial class EverythingFallbackItem(EverythingFallbackGroup group, int index)
    : FallbackCommandItem(new NoOpCommand { Id = "czz404.everythingpal.fallback" }, "Everything 搜索", $"czz404.everythingpal.fallback.{index}")
{
    private static readonly IconInfo AppIcon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    public override void UpdateQuery(string query) => group.OnUpdateQuery(query);

    internal void ShowResult(EverythingResult result, int totalCount)
    {
        var extra = totalCount > 1 && index == 0 ? $"（共 {totalCount} 个结果）" : string.Empty;

        Title = result.Name;
        Subtitle = $"{result.Directory}{extra}";
        Icon = result.IsFolder ? Icons.Folder : Icons.File;
        Command = new OpenItemCommand(result.FullPath, result.IsFolder);
        MoreCommands =
        [
            new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder)) { Title = "在资源管理器中显示" },
            new CommandContextItem(new CopyItemPathCommand(result.FullPath)) { Title = "复制路径" },
            new CommandContextItem(new OpenInEverythingCommand(result.FullPath)) { Title = "在 Everything 中定位" },
        ];
    }

    internal void ShowStatus(SearchStatus status, string? error)
    {
        Title = status switch
        {
            SearchStatus.EverythingNotRunning => "Everything 没有运行",
            SearchStatus.EsExeNotFound => "找不到 es.exe",
            SearchStatus.Timeout => "Everything 查询超时",
            _ => "Everything 查询失败",
        };

        Subtitle = status switch
        {
            SearchStatus.EverythingNotRunning => "请启动 Everything（或安装它），然后重新输入",
            SearchStatus.EsExeNotFound => "插件自带的 es.exe 缺失，请重新安装插件",
            _ => error ?? "请稍后重试",
        };

        Icon = AppIcon;
        Command = status == SearchStatus.EverythingNotRunning
            ? new StartEverythingCommand()
            : new OpenEverythingDownloadPageCommand();
        MoreCommands =
        [
            new CommandContextItem(new StartEverythingCommand()) { Title = "启动 Everything" },
            new CommandContextItem(new OpenEverythingDownloadPageCommand()) { Title = "下载 Everything" },
        ];
    }

    internal void Clear()
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Command = new NoOpCommand { Id = "czz404.everythingpal.fallback" };
        MoreCommands = [];
    }
}

/// <summary>Icons shared by the list page and the fallback rows.</summary>
internal static class Icons
{
    internal static readonly IconInfo Folder = new("\uE8B7");
    internal static readonly IconInfo File = new("\uE8A5");
}

