// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Globalization;

namespace EverythingPal.Core;

/// <summary>
/// Builds the exact ES command line. Keeping this pure makes it testable (and regression-proof
/// against switches that do not exist, e.g. `-folder` / `-select`, which exit with code 6).
/// </summary>
public static class EsArguments
{
    public static IReadOnlyList<string> Build(string normalizedQuery, string exportFile, SearchOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportFile);
        ArgumentNullException.ThrowIfNull(options);

        var max = Math.Clamp(options.MaxResults, 1, 500);
        var sort = string.IsNullOrWhiteSpace(options.Sort) ? "name-ascending" : options.Sort;
        var ipcTimeout = Math.Clamp(options.IpcTimeoutMs, 100, 10000);

        return
        [
            "-export-json", exportFile,
            "-n", max.ToString(CultureInfo.InvariantCulture),
            "-sort", sort,
            "-date-format", "1",
            "-size",
            "-date-modified",
            "-attributes",
            "-no-result-error",
            "-timeout", ipcTimeout.ToString(CultureInfo.InvariantCulture),
            "-search", normalizedQuery,
        ];
    }
}
