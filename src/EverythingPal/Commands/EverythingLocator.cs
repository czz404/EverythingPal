// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using EverythingPal.Core;
using Microsoft.Win32;

namespace EverythingPal.Commands;

/// <summary>
/// Locates the Everything GUI executable for the "start Everything" and "locate in Everything"
/// actions.
///
/// Everything does not have to be in <c>%ProgramFiles%</c>: 1.5 installs to a user-chosen folder
/// (this machine uses <c>D:\software\Everything</c>) and portable copies live anywhere. The
/// Windows-specific probing happens here; the ordered candidate list and the file probing live in
/// <see cref="EverythingGuiDiscovery"/> (Core) so they are unit-testable. Both call sites share this
/// single implementation.
/// </summary>
internal static class EverythingLocator
{
    private const string EverythingExeName = "Everything.exe";
    private const string AppPathsSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Everything.exe";
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Registry views probed in order: per-user 64-bit, per-user 32-bit (WOW6432Node), machine-wide
    /// 64-bit, machine-wide 32-bit.
    /// </summary>
    private static readonly (RegistryHive Hive, RegistryView View)[] RegistryLocations =
    [
        (RegistryHive.CurrentUser, RegistryView.Registry64),
        (RegistryHive.CurrentUser, RegistryView.Registry32),
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
    ];

    /// <summary>Finds Everything.exe, or returns <c>null</c> when it cannot be located (never throws).</summary>
    internal static string? Find()
    {
        try
        {
            var inputs = new EverythingGuiDiscoveryInputs
            {
                RunningProcessPaths = RunningProcessPaths(),
                AppPathEntries = AppPathEntries(),
                UninstallLocations = UninstallLocations(),
                CommonDirectories = CommonDirectories(),
                PathVariable = Environment.GetEnvironmentVariable("PATH"),
            };

            var found = EverythingGuiDiscovery.Find(inputs);
            if (found is null)
            {
                AppLog.Write("EverythingLocator: Everything.exe was not found in the process list, App Paths, uninstall entries, common directories or PATH.");
            }

            return found;
        }
        catch (Exception ex)
        {
            // Discovery is best-effort: a broken registry/permission must only mean "action unavailable".
            AppLog.Write($"EverythingLocator.Find failed: {ex}");
            return null;
        }
    }

    internal static bool IsRunning() =>
        Process.GetProcessesByName("Everything").Length > 0;

    /// <summary>Executable paths of running Everything processes (covers portable/custom installs).</summary>
    private static List<string> RunningProcessPaths()
    {
        var paths = new List<string>();

        try
        {
            foreach (var process in Process.GetProcessesByName("Everything"))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            paths.Add(path);
                        }
                    }
                    catch
                    {
                        // Access denied (e.g. a service-owned process) or the process exited.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"EverythingLocator: process scan failed: {ex.Message}");
        }

        return paths;
    }

    /// <summary>Default values of the App Paths\Everything.exe registry keys.</summary>
    private static List<string> AppPathEntries()
    {
        var entries = new List<string>();

        foreach (var (hive, view) in RegistryLocations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(AppPathsSubKey);
                if (key?.GetValue(null) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    entries.Add(value);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"EverythingLocator: App Paths read failed for {hive}/{view}: {ex.Message}");
            }
        }

        return entries;
    }

    /// <summary>Install locations from uninstall entries whose display name mentions Everything.</summary>
    private static List<string> UninstallLocations()
    {
        var locations = new List<string>();

        foreach (var (hive, view) in RegistryLocations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(UninstallSubKey);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    using var entry = uninstall.OpenSubKey(subKeyName);
                    if (entry is null)
                    {
                        continue;
                    }

                    var displayName = entry.GetValue("DisplayName") as string;
                    var name = string.IsNullOrWhiteSpace(displayName) ? subKeyName : displayName;
                    if (name.IndexOf("Everything", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (entry.GetValue("InstallLocation") is string installLocation && !string.IsNullOrWhiteSpace(installLocation))
                    {
                        locations.Add(installLocation);
                    }

                    // DisplayIcon is only useful when it actually points at Everything.exe (it often
                    // points at the uninstaller instead).
                    if (entry.GetValue("DisplayIcon") is string displayIcon && IsEverythingExecutable(displayIcon))
                    {
                        locations.Add(displayIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"EverythingLocator: uninstall registry read failed for {hive}/{view}: {ex.Message}");
            }
        }

        return locations;
    }

    /// <summary>Well-known install directories (installer default, 1.5a, per-user, Store alias).</summary>
    private static List<string> CommonDirectories()
    {
        var directories = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var root in new[] { programFiles, programFilesX86 })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            directories.Add(Path.Combine(root, "Everything"));
            directories.Add(Path.Combine(root, "Everything 1.5a"));
            directories.Add(Path.Combine(root, "Everything 1.4"));
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            directories.Add(Path.Combine(localAppData, "Programs", "Everything"));
            directories.Add(Path.Combine(localAppData, "Programs", "Everything 1.5a"));
            directories.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps")); // Store execution alias
            directories.Add(Path.Combine(localAppData, "Everything"));
        }

        return directories;
    }

    /// <summary>True when a registry icon value (possibly quoted and with a ",0" suffix) names Everything.exe.</summary>
    private static bool IsEverythingExecutable(string value)
    {
        var path = value.Trim().Trim('"');

        var comma = path.LastIndexOf(',');
        if (comma >= 0)
        {
            path = path[..comma].Trim().Trim('"');
        }

        return string.Equals(Path.GetFileName(path), EverythingExeName, StringComparison.OrdinalIgnoreCase);
    }
}
