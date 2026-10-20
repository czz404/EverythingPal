// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal.Commands;

/// <summary>Starts the Everything GUI if it is installed but not running.</summary>
internal sealed partial class StartEverythingCommand : InvokableCommand
{
    public override string Name => "启动 Everything";

    public override IconInfo Icon => new("\uE768");

    public override CommandResult Invoke()
    {
        var exe = EverythingLocator.Find();
        if (exe is null)
        {
            return CommandResult.ShowToast("没有找到 Everything，请先安装：https://www.voidtools.com/downloads/");
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            return CommandResult.ShowToast("正在启动 Everything，稍后再试一次");
        }
        catch (Exception ex)
        {
            AppLog.Write($"StartEverythingCommand failed: {ex.Message}");
            return CommandResult.ShowToast("启动 Everything 失败");
        }
    }
}

/// <summary>Opens the Everything download page.</summary>
internal sealed partial class OpenEverythingDownloadPageCommand : InvokableCommand
{
    public override string Name => "下载 Everything";

    public override IconInfo Icon => new("\uE896");

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo("https://www.voidtools.com/downloads/") { UseShellExecute = true });
        return CommandResult.Dismiss();
    }
}
