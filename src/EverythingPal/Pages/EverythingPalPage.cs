// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Commands;
using EverythingPal.Core;
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
        Title = "Everything 搜索";
        Name = "搜索";
        PlaceholderText = "输入文件名或路径片段（Everything 语法可用）";
    }

    public override IListItem[] GetItems() => _items;

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
                    _items = [StatusItem("搜索失败", ex.Message, new NoOpCommand())];
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
                return [StatusItem("没有匹配结果", "换个关键词，或检查 Everything 的索引设置。", new NoOpCommand())];
            case SearchStatus.EverythingNotRunning:
                return
                [
                    StatusItem(
                        "Everything 没有运行",
                        "请启动 Everything（或安装它），然后重新输入。",
                        new StartEverythingCommand(),
                        new CommandContextItem(new StartEverythingCommand()) { Title = "启动 Everything" },
                        new CommandContextItem(new OpenEverythingDownloadPageCommand()) { Title = "下载 Everything" }),
                ];
            case SearchStatus.EsExeNotFound:
                return
                [
                    StatusItem(
                        "找不到 es.exe",
                        "插件自带的 es.exe 缺失，请重新安装插件，或在设置里指定 es.exe 路径。",
                        new OpenEverythingDownloadPageCommand()),
                ];
            case SearchStatus.Timeout:
                return [StatusItem("查询超时", "Everything 可能正忙或索引未加载完成，请稍后重试。", new NoOpCommand())];
            default:
                return [StatusItem("查询失败", outcome.Error ?? "未知错误", new NoOpCommand())];
        }
    }

    private static IListItem BuildItem(EverythingResult result)
    {
        return new ListItem(new OpenItemCommand(result.FullPath, result.IsFolder))
        {
            Title = result.Name,
            Subtitle = result.Directory,
            Icon = result.IsFolder ? Icons.Folder : Icons.File,
            Details = BuildDetails(result),
            MoreCommands =
            [
                new CommandContextItem(new RevealInExplorerCommand(result.FullPath, result.IsFolder)) { Title = "在资源管理器中显示" },
                new CommandContextItem(new CopyItemPathCommand(result.FullPath)) { Title = "复制路径" },
                new CommandContextItem(new OpenInEverythingCommand(result.FullPath)) { Title = "在 Everything 中定位" },
            ],
        };
    }

    private static Details BuildDetails(EverythingResult result)
    {
        var metadata = new List<IDetailsElement>
        {
            new DetailsElement { Key = "名称", Data = new DetailsLink { Text = result.Name, Link = new Uri(result.FullPath) } },
            new DetailsElement { Key = "完整路径", Data = new DetailsLink { Text = result.FullPath, Link = new Uri(result.FullPath) } },
            new DetailsElement { Data = new DetailsSeparator() },
            new DetailsElement { Key = "类型", Data = new DetailsLink { Text = result.IsFolder ? "文件夹" : "文件" } },
        };

        if (!result.IsFolder)
        {
            metadata.Add(new DetailsElement { Key = "大小", Data = new DetailsLink { Text = FormatSize(result.Size) } });
        }

        if (result.DateModified is { } modified)
        {
            metadata.Add(new DetailsElement { Key = "修改时间", Data = new DetailsLink { Text = modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) } });
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




