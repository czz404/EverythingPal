// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal.Commands;

/// <summary>Locates the Everything GUI executable (used for "locate in Everything" and "start Everything").</summary>
internal static class EverythingLocator
{
    private static readonly string[] Candidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", "Everything.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything 1.5a", "Everything.exe"),
    ];

    internal static string? Find()
    {
        foreach (var candidate in Candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static bool IsRunning() =>
        Process.GetProcessesByName("Everything").Length > 0;
}
