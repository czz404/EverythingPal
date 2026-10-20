// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using EverythingPal.Core;
using EverythingPal.Services;
using EverythingPal.Ui;

namespace EverythingPal;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        AppLog.Write($"Main entry. args=[{string.Join(" | ", args)}]");

        // Development helper: exercise the es.exe search path without the Command Palette UI.
        // Usage: EverythingPal.exe --search-test "wantai"  (headless; prints to stdout)
        if (args.Length > 0 && args[0] == "--search-test")
        {
            var query = args.Length > 1 ? args[1] : string.Empty;
            var outcome = EverythingSearch.Search(query, new SearchOptions { MaxResults = 20 });
            Console.WriteLine($"status: {outcome.Status}");
            foreach (var result in outcome.Results)
            {
                Console.WriteLine($"{(result.IsFolder ? "DIR " : "FILE")} {result.FullPath}");
            }

            if (outcome.Error is not null)
            {
                Console.WriteLine($"error: {outcome.Error}");
            }

            return;
        }

        // Launched with no arguments (the Start-menu tile does this) or by a support request:
        // this MSIX is a Command Palette extension, not a standalone app. Explain that, show the
        // environment diagnostics and offer the useful actions instead of exiting silently - a
        // dead-looking tile is a Store certification risk (policy 10.1.2 "fully functional" /
        // "detect an incompatible device at launch and display a message").
        // This runs before any COM server state is created and on its own STA thread, so the
        // [MTAThread] COM server path below is unaffected.
        if (args.Length == 0 || IsDiagnosticsRequest(args))
        {
            try
            {
                LaunchDialog.Show();
            }
            catch (Exception ex)
            {
                // The launch path must never take the process down; AppLog is the only evidence.
                AppLog.Write($"Launch dialog failed: {ex}");
            }

            return;
        }

        try
        {
            if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
            {
                AppLog.Write("COM server starting");

                global::Shmuelie.WinRTServer.ComServer server = new();

                ManualResetEvent extensionDisposedEvent = new(false);

                // Single extension instance: the host asks for IExtension repeatedly and gets the same object.
                EverythingPalExtension extensionInstance = new(extensionDisposedEvent);
                server.RegisterClass<EverythingPalExtension, IExtension>(() => extensionInstance);
                server.Start();
                AppLog.Write("COM server started; waiting for dispose signal");

                extensionDisposedEvent.WaitOne();

                server.Stop();
                server.UnsafeDispose();
                AppLog.Write("COM server stopped");
            }
        }
        catch (Exception ex)
        {
            // A packaged COM server has no console: log before the process dies.
            AppLog.Write($"FATAL unhandled exception: {ex.GetType().FullName}: {ex.Message}\n{ex}");
            throw;
        }
    }

    /// <summary>True for the explicit diagnostics entry points (same dialog as a no-argument launch).</summary>
    private static bool IsDiagnosticsRequest(string[] args) =>
        string.Equals(args[0], "--about", StringComparison.OrdinalIgnoreCase)
        || string.Equals(args[0], "--diagnostics", StringComparison.OrdinalIgnoreCase);
}




