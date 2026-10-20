// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using EverythingPal.Localization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal.Commands;

/// <summary>Starts the Everything GUI if it is installed but not running.</summary>
internal sealed partial class StartEverythingCommand : InvokableCommand
{
    public override string Name => Strings.StartEverythingAction;

    public override IconInfo Icon => new("\uE768");

    public override CommandResult Invoke()
    {
        var exe = EverythingLocator.Find();
        if (exe is null)
        {
            return CommandResult.ShowToast(Strings.EverythingNotInstalledToast);
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            return CommandResult.ShowToast(Strings.StartEverythingToast);
        }
        catch (Exception ex)
        {
            AppLog.Write($"StartEverythingCommand failed: {ex.Message}");
            return CommandResult.ShowToast(Strings.StartEverythingFailedToast);
        }
    }
}

/// <summary>Opens the Everything download page.</summary>
internal sealed partial class OpenEverythingDownloadPageCommand : InvokableCommand
{
    public override string Name => Strings.DownloadEverythingAction;

    public override IconInfo Icon => new("\uE896");

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo("https://www.voidtools.com/downloads/") { UseShellExecute = true });
        return CommandResult.Dismiss();
    }
}
