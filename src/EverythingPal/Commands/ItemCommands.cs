// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal.Commands;

/// <summary>Opens a file or folder with the shell (default app / Explorer).</summary>
internal sealed partial class OpenItemCommand(string fullPath, bool isFolder) : InvokableCommand
{
    public override string Name => isFolder ? "打开文件夹" : "打开";

    public override IconInfo Icon => new(isFolder ? "\uE8B7" : "\uE8A7");

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            AppLog.Write($"OpenItemCommand failed for '{fullPath}': {ex.Message}");
            return CommandResult.ShowToast("无法打开该项目");
        }
    }
}

/// <summary>Shows the item selected in a new Explorer window.</summary>
internal sealed partial class RevealInExplorerCommand(string fullPath, bool isFolder) : InvokableCommand
{
    public override string Name => "在资源管理器中显示";

    public override IconInfo Icon => new("\uE8B7");

    public override CommandResult Invoke()
    {
        try
        {
            var target = Path.TrimEndingDirectorySeparator(fullPath);
            var psi = isFolder
                ? new ProcessStartInfo("explorer.exe") { ArgumentList = { target } }
                : new ProcessStartInfo("explorer.exe") { ArgumentList = { $"/select,{target}" } };
            psi.UseShellExecute = true;
            Process.Start(psi);
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            AppLog.Write($"RevealInExplorerCommand failed for '{fullPath}': {ex.Message}");
            return CommandResult.ShowToast("无法打开资源管理器");
        }
    }
}

/// <summary>Copies the full path to the clipboard.</summary>
internal sealed partial class CopyItemPathCommand(string fullPath) : InvokableCommand
{
    public override string Name => "复制路径";

    public override IconInfo Icon => new("\uE8C8");

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(fullPath);
            return CommandResult.ShowToast("路径已复制");
        }
        catch (Exception ex)
        {
            AppLog.Write($"CopyPathCommand failed: {ex.Message}");
            return CommandResult.ShowToast("复制失败");
        }
    }
}

/// <summary>Selects the item in the Everything GUI (Everything.exe -select).</summary>
internal sealed partial class OpenInEverythingCommand(string fullPath) : InvokableCommand
{
    public override string Name => "在 Everything 中定位";

    public override IconInfo Icon => new("\uE721");

    public override CommandResult Invoke()
    {
        try
        {
            var exe = FindEverythingExe();
            if (exe is null)
            {
                return CommandResult.ShowToast("未找到 Everything.exe");
            }

            var psi = new ProcessStartInfo(exe);
            psi.ArgumentList.Add("-select");
            psi.ArgumentList.Add(Path.TrimEndingDirectorySeparator(fullPath));
            psi.UseShellExecute = false;
            Process.Start(psi);
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            AppLog.Write($"OpenInEverythingCommand failed: {ex.Message}");
            return CommandResult.ShowToast("无法在 Everything 中定位");
        }
    }

    internal static string? FindEverythingExe()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything 1.5a", "Everything.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>Minimal clipboard helper (the COM server runs MTA, so no WinForms/WinRT clipboard).</summary>
internal static class ClipboardHelper
{
    private const uint CfUnicodeText = 13;
    private const uint GmemnMoveable = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    public static void SetText(string text)
    {
        var bytes = (text.Length + 1) * sizeof(char);
        var handle = GlobalAlloc(GmemnMoveable, (UIntPtr)bytes);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("GlobalAlloc failed");
        }

        try
        {
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("GlobalLock failed");
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException("OpenClipboard failed");
            }

            try
            {
                EmptyClipboard();
                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                {
                    throw new InvalidOperationException("SetClipboardData failed");
                }

                // Ownership transferred to the clipboard.
                handle = IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                GlobalFree(handle);
            }
        }
    }
}


