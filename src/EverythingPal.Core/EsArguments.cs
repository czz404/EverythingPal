// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Globalization;

namespace EverythingPal.Core;

/// <summary>
/// Builds the exact ES command line. Keeping this pure makes it testable (and regression-proof
/// against switches that do not exist, e.g. `-folder` / `-select`, which exit with code 6).
///
/// The search expression is deliberately NOT part of <see cref="EsCommandLine.FixedArguments"/>:
/// it is appended as a raw tail after <c>-search*</c> (see <see cref="EsCommandLine"/>). The older
/// <c>-search</c> switch strips quotes from its value, which corrupted Everything expressions such
/// as <c>ext:pdf "Annual Report"</c> before ES ever evaluated them.
/// </summary>
public static class EsArguments
{
    public static EsCommandLine Build(string normalizedQuery, string exportFile, SearchOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportFile);
        ArgumentNullException.ThrowIfNull(options);

        var max = Math.Clamp(options.MaxResults, 1, 500);
        var sort = string.IsNullOrWhiteSpace(options.Sort) ? SearchOptions.DefaultSort : options.Sort;
        var ipcTimeout = Math.Clamp(options.IpcTimeoutMs, 100, 10000);

        return new EsCommandLine(
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
            ],
            normalizedQuery);
    }
}
