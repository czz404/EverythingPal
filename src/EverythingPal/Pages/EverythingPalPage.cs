// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using EverythingPal.Commands;
using EverythingPal.Services;

namespace EverythingPal;

/// <summary>
/// Main search page: type a query, get live results from Everything via es.exe.
/// See <see cref="EverythingSearch"/> for the CLI details and the pitfalls it avoids.
/// </summary>
internal sealed partial class EverythingPalPage : DynamicListPage
{
    private const int MaxResults = 50;
    private const int DebounceMilliseconds = 150;

    private static readonly IconInfo FolderIcon = new("\uE8B7");
    private static readonly IconInfo FileIcon = new("\uE8A5");

    private readonly EverythingSearch _search = new();
    private IListItem[] _items = [];
    private string _query = string.Empty;
    private CancellationTokenSource? _cts;

    public EverythingPalPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Everything 搜索";
        Name = "搜索";
        PlaceholderText = "输入文件名或路径片段（Everything 语法可用）";
    }

    public override IListItem[] GetItems() => _items;

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

                    var outcome = _search.Search(_query, MaxResults);
                    token.ThrowIfCancellationRequested();

                    _items = BuildItems(outcome);
                    IsLoading = false;
                    RaiseItemsChanged(_items.Length);
                }
                catch (OperationCanceledException)
                {
                    // A newer keystroke superseded this query.
                }
                catch (Exception ex)
                {
                    AppLog.Write($"EverythingPalPage search failed: {ex}");
                    _items = [StatusItem("搜索失败", ex.Message)];
                    IsLoading = false;
                    RaiseItemsChanged(_items.Length);
                }
            },
            token);
    }

    private static IListItem[] BuildItems(SearchOutcome outcome)
    {
        switch (outcome.Status)
        {
            case SearchStatus.Ok:
                return [.. outcome.Results.Select(BuildItem)];
            case SearchStatus.NoResults:
                return [StatusItem("没有匹配结果", "换个关键词，或检查 Everything 的索引设置。")];
            case SearchStatus.EverythingNotRunning:
                return [StatusItem("Everything 未运行", "请先启动 Everything（托盘图标），然后重新输入。")];
            case SearchStatus.EsExeNotFound:
                return [StatusItem("找不到 es.exe", "安装 Everything/ES，或把 es.exe 所在目录加入 PATH。")];
            case SearchStatus.Timeout:
                return [StatusItem("查询超时", "Everything 可能正忙，请稍后重试。")];
            default:
                return [StatusItem("查询失败", outcome.Error ?? "未知错误")];
        }
    }

    private static IListItem BuildItem(EverythingResult result)
    {
        var item = new ListItem(new OpenItemCommand(result.FullPath, result.IsFolder))
        {
            Title = result.Name,
            Subtitle = result.Directory,
            Icon = result.IsFolder ? FolderIcon : FileIcon,
            MoreCommands =
            [
                new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder))
                {
                    Title = "在资源管理器中显示",
                },
                new CommandContextItem(new CopyItemPathCommand(result.FullPath))
                {
                    Title = "复制路径",
                },
                new CommandContextItem(new OpenInEverythingCommand(result.FullPath))
                {
                    Title = "在 Everything 中定位",
                },
            ],
        };

        return item;
    }

    private static IListItem StatusItem(string title, string subtitle) =>
        new ListItem(new NoOpCommand())
        {
            Title = title,
            Subtitle = subtitle,
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
        };
}

