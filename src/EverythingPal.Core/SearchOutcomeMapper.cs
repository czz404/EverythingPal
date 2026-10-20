// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Turns the raw result of one <c>es.exe</c> invocation (exit code, stderr, parsed results) into a
/// <see cref="SearchOutcome"/>.
///
/// Why this exists as a pure helper: <see cref="EsArguments"/> always passes <c>-no-result-error</c>,
/// so ES exits with <b>0</b> and an <b>empty export file</b> when nothing matches. Treating exit 0 as
/// <see cref="SearchStatus.Ok"/> made the UI render an empty list instead of its "no results" state.
/// Keeping the mapping dependency-free also makes it unit-testable without spawning es.exe.
///
/// Exit codes (see ES documentation / README):
///   0 = success (may still contain zero results because of <c>-no-result-error</c>),
///   9 = no results (only without that flag),
///   8 = Everything is not running,
///   4/6 = bad command line (stdout is the help dump and is never parsed).
/// </summary>
public static class SearchOutcomeMapper
{
    public static SearchOutcome FromExitCode(
        int exitCode,
        string? stderr = null,
        IReadOnlyList<EverythingResult>? results = null)
    {
        switch (exitCode)
        {
            case 0:
                // `-no-result-error` turns "no matches" into a successful run with an empty export.
                return results is { Count: > 0 }
                    ? new SearchOutcome { Status = SearchStatus.Ok, Results = results }
                    : new SearchOutcome { Status = SearchStatus.NoResults };

            case 9:
                return new SearchOutcome { Status = SearchStatus.NoResults };

            case 8:
                return new SearchOutcome
                {
                    Status = SearchStatus.EverythingNotRunning,
                    Error = "Everything is not running (ES IPC not found).",
                };

            default:
                var detail = (stderr ?? string.Empty).Trim();
                return new SearchOutcome
                {
                    Status = SearchStatus.QueryFailed,
                    Error = detail.Length == 0
                        ? $"es.exe exited with {exitCode}."
                        : $"es.exe exited with {exitCode}: {detail}",
                };
        }
    }
}
