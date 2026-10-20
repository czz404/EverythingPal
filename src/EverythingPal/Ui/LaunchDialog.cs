// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.InteropServices;
using EverythingPal.Commands;
using EverythingPal.Localization;

namespace EverythingPal.Ui;

/// <summary>
/// Diagnostics dialog shown when the app is launched with no arguments (Start-menu tile) or with
/// <c>--about</c> / <c>--diagnostics</c>.
///
/// Why this exists: the MSIX is a Command Palette *extension* hosted by PowerToys, but Windows still
/// shows a Start-menu entry for it. Without this dialog, launching that entry started the process and
/// exited immediately - a "dead app" for users, and a Store certification risk (policy 10.1.2
/// requires the package to be functional and to detect an incompatible device at launch and explain
/// what is missing). The dialog states what the package is, shows the detected environment and offers
/// the two useful actions: open Command Palette, or open the missing dependency's download page.
///
/// Deliberately Win32-only (<see cref="MessageBoxW"/>): the extension is published self-contained and
/// trimmed, so pulling in WinForms/WPF/WinUI/WindowsAppSDK just for an About box would add megabytes
/// and trimming risk to a package whose real UI lives inside Command Palette. user32's message box is
/// already in the OS, is trim/AOT safe, localizes the Yes/No buttons through the system UI language,
/// and lets the user copy the diagnostics with Ctrl+C.
/// </summary>
internal static class LaunchDialog
{
    private static string Caption => Strings.LaunchDialogCaption;

    // Launching a packaged app: both `shell:AppsFolder\<AUMID>` (through ShellExecute, used first)
    // and `explorer.exe shell:AppsFolder\<AUMID>` (fallback) work without extra COM interop.
    private const string CommandPaletteAumid = @"shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App";
    private const string CommandPalettePackageName = "Microsoft.CommandPalette_8wekyb3d8bbwe";

    private const string EverythingDownloadUrl = "https://www.voidtools.com/downloads/";
    private const string PowerToysInstallUrl = "https://learn.microsoft.com/windows/powertoys/install";

    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-messageboxw
    private const uint MbOk = 0x00000000;
    private const uint MbYesNo = 0x00000004;
    private const uint MbIconQuestion = 0x00000020;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbSetForeground = 0x00010000;
    private const int IdYes = 6;

    private const int ErrorInsufficientBuffer = 122;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    // shell32!SHParseDisplayName resolves a shell namespace path without COM interop; it is used to ask
    // the shell whether an AUMID ("shell:AppsFolder\<package family name>!App") exists.
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    // kernel32!GetCurrentPackageFullName returns the package identity when the process runs from an
    // installed MSIX ("EverythingPal_0.2.0.0_x64__b32a3jmekh2ja"), or APPMODEL_ERROR_NO_PACKAGE when
    // it runs as a loose development build.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, [Out] char[]? packageFullName);

    /// <summary>Log file shared with <see cref="AppLog"/>; shown in the dialog so support can find it.</summary>
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EverythingPal",
        "Logs",
        "extension.log");

    /// <summary>Shows the diagnostics dialog on a dedicated STA thread and waits until it is closed.</summary>
    internal static void Show()
    {
        try
        {
            AppLog.Write("LaunchDialog: showing launch diagnostics dialog");

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                ShowCore();
                return;
            }

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    ShowCore();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })
            {
                Name = "EverythingPal.LaunchDialog",
            };

            // Shell helpers (and any future richer dialog) are happiest on an STA thread; Program.Main
            // is [MTAThread] for the COM server, hence the dedicated thread. Joining keeps the process
            // alive until the user dismisses the dialog.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                AppLog.Write($"LaunchDialog: dialog thread failed: {failure}");
            }
        }
        catch (Exception ex)
        {
            // The launch path must never take the process down with an unhandled exception.
            AppLog.Write($"LaunchDialog.Show failed: {ex}");
        }
    }

    private static void ShowCore()
    {
        try
        {
            var diagnostics = CollectDiagnostics();
            AppLog.Write(
                "LaunchDialog: commandPalette=" +
                (diagnostics.CommandPaletteInstalled?.ToString() ?? "unknown") +
                $", everythingPath='{diagnostics.EverythingPath}'" +
                $", everythingRunning={diagnostics.EverythingRunning}" +
                $", esExePresent={diagnostics.EsExePresent}" +
                $", version={diagnostics.Version}");

            // When the Command Palette probe is inconclusive, assume it is present: offering to open it
            // is harmless, while wrongly offering PowerToys installation would be confusing.
            var commandPaletteInstalled = diagnostics.CommandPaletteInstalled != false;
            var question = commandPaletteInstalled
                ? Strings.LaunchDialogOpenCommandPaletteQuestion
                : Strings.LaunchDialogInstallPowerToysQuestion;

            if (!AskYesNo(BuildMessage(diagnostics, question), MbYesNo | MbIconInformation))
            {
                OfferPrerequisiteAction(diagnostics);
                return;
            }

            if (!commandPaletteInstalled)
            {
                OpenUrl(PowerToysInstallUrl);
                return;
            }

            if (!OpenCommandPalette())
            {
                ShowMessage(Strings.LaunchDialogOpenCommandPaletteFailed, MbOk | MbIconWarning);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog.ShowCore failed: {ex}");

            // Last resort: still tell the user something instead of exiting silently.
            try
            {
                ShowMessage(
                    Strings.LaunchDialogDiagnosticsFailedPrefix + Environment.NewLine + LogPath +
                    Environment.NewLine + Environment.NewLine + ex.Message,
                    MbOk | MbIconWarning);
            }
            catch
            {
                // Nothing else can be done here; the log above is the evidence.
            }
        }
    }

    private sealed record Diagnostics(
        bool? CommandPaletteInstalled,
        string? EverythingPath,
        bool EverythingRunning,
        bool EsExePresent,
        string Version);

    private static Diagnostics CollectDiagnostics()
    {
        string? everythingPath = null;
        var everythingRunning = false;

        try
        {
            // Reuses the extension's own Everything.exe discovery (registry App Paths, uninstall
            // entries, running process paths, common directories, PATH). It never throws.
            everythingPath = EverythingLocator.Find();
            everythingRunning = IsEverythingRunning();
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: Everything probe failed: {ex.Message}");
        }

        var esExePath = Path.Combine(AppContext.BaseDirectory, "Tools", "es.exe");
        var esExePresent = false;
        try
        {
            esExePresent = File.Exists(esExePath);
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: es.exe probe failed: {ex.Message}");
        }

        return new Diagnostics(
            ProbeCommandPaletteInstalled(),
            everythingPath,
            everythingRunning,
            esExePresent,
            GetVersionText());
    }

    private static bool IsEverythingRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("Everything");
            var running = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return running;
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: Everything process probe failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks for the Command Palette package through the shell AppsFolder (AUMID), falling back to the
    /// package data folder. <c>null</c> means "could not determine".
    /// </summary>
    private static bool? ProbeCommandPaletteInstalled()
    {
        try
        {
            var hr = SHParseDisplayName(CommandPaletteAumid, IntPtr.Zero, out var pidl, 0, out _);
            if (pidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pidl);
            }

            if (hr == 0)
            {
                return true;
            }

            // ERROR_FILE_NOT_FOUND / ERROR_PATH_NOT_FOUND: shell:AppsFolder resolved, AUMID is unknown.
            if (hr is unchecked((int)0x80070002) or unchecked((int)0x80070003))
            {
                return false;
            }

            AppLog.Write($"LaunchDialog: shell:AppsFolder probe returned 0x{hr:X8}");
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: shell:AppsFolder probe failed: {ex.Message}");
        }

        try
        {
            var packageData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                CommandPalettePackageName);
            return Directory.Exists(packageData) ? true : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetVersionText()
    {
        var packageFullName = TryGetPackageFullName();
        if (packageFullName is not null)
        {
            // Name_Version_Architecture_ResourceId_PublisherId
            var parts = packageFullName.Split('_');
            if (parts.Length >= 2 && parts[1].Length > 0)
            {
                return parts[1];
            }

            return packageFullName;
        }

        // No package identity: loose development build (bin\...\EverythingPal.exe).
        var assemblyVersion = typeof(LaunchDialog).Assembly.GetName().Version;
        return assemblyVersion is null
            ? Strings.LaunchDialogUnknownDevelopmentVersion
            : Strings.FormatLaunchDialogDevelopmentVersion(assemblyVersion.ToString());
    }

    private static string? TryGetPackageFullName()
    {
        try
        {
            uint length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            if (result != ErrorInsufficientBuffer || length == 0)
            {
                return null;
            }

            var buffer = new char[(int)length];
            result = GetCurrentPackageFullName(ref length, buffer);
            if (result != 0)
            {
                return null;
            }

            var end = Array.IndexOf(buffer, '\0');
            return end < 0 ? new string(buffer) : new string(buffer, 0, end);
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: package identity probe failed: {ex.Message}");
            return null;
        }
    }

    private static string BuildMessage(Diagnostics diagnostics, string question)
    {
        var lines = new List<string>
        {
            Strings.LaunchDialogIntro,
            string.Empty,
            Strings.LaunchDialogUsageHeader,
            Strings.LaunchDialogUsageHotkey,
            Strings.LaunchDialogUsageRequiresEverything,
            Strings.LaunchDialogUsageFallbackRows,
            string.Empty,
            Strings.FormatLaunchDialogVersion(diagnostics.Version),
            Strings.LaunchDialogDiagnosticsHeader,
            Strings.FormatLaunchDialogCommandPaletteLine(DescribeCommandPalette(diagnostics.CommandPaletteInstalled)),
            Strings.FormatLaunchDialogEverythingLine(DescribeEverything(diagnostics)),
            Strings.FormatLaunchDialogEsExeLine(
                diagnostics.EsExePresent ? Strings.LaunchDialogEsExePresent : Strings.LaunchDialogEsExeMissing),
            string.Empty,
            Strings.FormatLaunchDialogLogLine(LogPath),
            Strings.LaunchDialogPrivacyLine,
            string.Empty,
            question,
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeCommandPalette(bool? installed) => installed switch
    {
        true => Strings.LaunchDialogCommandPaletteInstalled,
        false => Strings.LaunchDialogCommandPaletteNotInstalled,
        _ => Strings.LaunchDialogCommandPaletteUnknown,
    };

    private static string DescribeEverything(Diagnostics diagnostics)
    {
        if (diagnostics.EverythingPath is not null)
        {
            return diagnostics.EverythingRunning
                ? Strings.FormatLaunchDialogEverythingInstalledRunning(diagnostics.EverythingPath)
                : Strings.FormatLaunchDialogEverythingInstalledNotRunning(diagnostics.EverythingPath);
        }

        return diagnostics.EverythingRunning
            ? Strings.LaunchDialogEverythingRunningUnknownLocation
            : Strings.LaunchDialogEverythingNotDetected;
    }

    /// <summary>Second step: if the user declined opening Command Palette, offer to fix a prerequisite.</summary>
    private static void OfferPrerequisiteAction(Diagnostics diagnostics)
    {
        if (diagnostics.CommandPaletteInstalled != false)
        {
            if (diagnostics.EverythingPath is null && !diagnostics.EverythingRunning)
            {
                if (AskYesNo(
                    Strings.LaunchDialogEverythingMissingWarning + Environment.NewLine + Environment.NewLine +
                    Strings.LaunchDialogOpenEverythingDownloadQuestion,
                    MbYesNo | MbIconQuestion))
                {
                    OpenUrl(EverythingDownloadUrl);
                }

                return;
            }

            if (!diagnostics.EverythingRunning && diagnostics.EverythingPath is not null)
            {
                if (AskYesNo(
                    Strings.LaunchDialogEverythingNotRunningWarning + Environment.NewLine + Environment.NewLine +
                    Strings.LaunchDialogStartEverythingQuestion,
                    MbYesNo | MbIconQuestion))
                {
                    StartEverything(diagnostics.EverythingPath);
                }

                return;
            }
        }

        if (!diagnostics.EsExePresent)
        {
            ShowMessage(
                Strings.LaunchDialogBundledEsExeMissing + Environment.NewLine +
                Strings.LaunchDialogLogLabel + Environment.NewLine + LogPath,
                MbOk | MbIconWarning);
        }
    }

    private static bool OpenCommandPalette()
    {
        if (TryStart(new ProcessStartInfo(CommandPaletteAumid) { UseShellExecute = true }))
        {
            AppLog.Write("LaunchDialog: Command Palette activation requested");
            return true;
        }

        // Fallback: some configurations fail to translate the shell: moniker through ShellExecute
        // directly, but explorer.exe always understands it.
        if (TryStart(new ProcessStartInfo("explorer.exe", CommandPaletteAumid) { UseShellExecute = true }))
        {
            AppLog.Write("LaunchDialog: Command Palette activation requested via explorer.exe");
            return true;
        }

        return false;
    }

    private static void StartEverything(string everythingPath)
    {
        if (TryStart(new ProcessStartInfo(everythingPath) { UseShellExecute = true }))
        {
            AppLog.Write($"LaunchDialog: started Everything from '{everythingPath}'");
            return;
        }

        ShowMessage(Strings.LaunchDialogStartEverythingFailed, MbOk | MbIconWarning);
    }

    private static void OpenUrl(string url)
    {
        if (TryStart(new ProcessStartInfo(url) { UseShellExecute = true }))
        {
            AppLog.Write($"LaunchDialog: opened '{url}'");
            return;
        }

        ShowMessage(
            Strings.LaunchDialogOpenBrowserFailedPrefix + Environment.NewLine + url,
            MbOk | MbIconWarning);
    }

    private static bool TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            // ShellExecute-based starts return a Process that may be null (the shell owns it); both a
            // null return and no exception mean the shell accepted the request.
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: start '{startInfo.FileName}' failed: {ex.Message}");
            return false;
        }
    }

    private static bool AskYesNo(string message, uint flags) =>
        ShowMessageBox(message, flags | MbSetForeground) == IdYes;

    private static void ShowMessage(string message, uint flags) =>
        ShowMessageBox(message, flags | MbSetForeground);

    private static int ShowMessageBox(string message, uint flags)
    {
        try
        {
            return MessageBoxW(IntPtr.Zero, message, Caption, flags);
        }
        catch (Exception ex)
        {
            AppLog.Write($"LaunchDialog: MessageBoxW failed: {ex.Message}");
            return 0;
        }
    }
}
