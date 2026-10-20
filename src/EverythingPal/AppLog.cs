// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text;

namespace EverythingPal;

/// <summary>
/// Minimal file logger. The extension runs as a packaged COM server without a console, so this is
/// the only practical way to see what happened during development. Writes to two locations and
/// never throws:
///   %LOCALAPPDATA%\EverythingPal\Logs\extension.log
///   %TEMP%\EverythingPal.log
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();

    private static readonly string[] LogPaths =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EverythingPal",
            "Logs",
            "extension.log"),
        Path.Combine(Path.GetTempPath(), "EverythingPal.log"),
    ];

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [pid {Environment.ProcessId}] {message}{Environment.NewLine}";

        foreach (var path in LogPaths)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never take the extension down.
            }
        }
    }
}
