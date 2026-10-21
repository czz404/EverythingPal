// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Decides whether a fuzzy (<see cref="SearchQueryBuilder"/>) run should be retried with the original
/// query, and which outcome to return. Kept pure so the retry semantics are unit-testable without
/// spawning es.exe.
///
/// Why a retry exists at all: Everything 1.4 does not understand the <c>nocase:</c>/<c>ignorews:</c>
/// modifiers and returns zero results instead of an error. That is indistinguishable from a genuine
/// miss, so the caller re-runs the user's query verbatim and prefers its result. Only "empty"
/// outcomes are worth a second run; a timeout / not-running / missing-ES result is returned as-is
/// (retrying would just repeat the same failure and double the latency).
/// </summary>
public static class SearchRetryPolicy
{
    /// <summary>
    /// True when the first (fuzzy) run should be followed by a run of the original query:
    /// the expressions differ, the fuzzy run produced no results, and the failure is one a plain-query
    /// retry can plausibly fix.
    /// </summary>
    public static bool ShouldRetry(SearchOutcome fuzzyOutcome, string fuzzyExpression, string originalExpression)
    {
        ArgumentNullException.ThrowIfNull(fuzzyOutcome);
        ArgumentNullException.ThrowIfNull(fuzzyExpression);
        ArgumentNullException.ThrowIfNull(originalExpression);

        if (string.Equals(fuzzyExpression, originalExpression, StringComparison.Ordinal))
        {
            return false;
        }

        if (fuzzyOutcome.Status == SearchStatus.Ok && fuzzyOutcome.Results.Count > 0)
        {
            return false;
        }

        return fuzzyOutcome.Status is SearchStatus.Ok or SearchStatus.NoResults or SearchStatus.QueryFailed;
    }

    /// <summary>
    /// Picks the outcome to return after the original-query retry:
    /// original results win; if both runs were empty the result is <see cref="SearchStatus.NoResults"/>;
    /// otherwise the original run's error is returned (e.g. Everything stopped between the two runs).
    /// </summary>
    public static SearchOutcome Select(SearchOutcome fuzzyOutcome, SearchOutcome originalOutcome)
    {
        ArgumentNullException.ThrowIfNull(fuzzyOutcome);
        ArgumentNullException.ThrowIfNull(originalOutcome);

        if (originalOutcome.Status == SearchStatus.Ok && originalOutcome.Results.Count > 0)
        {
            return originalOutcome;
        }

        // Defensive: Search only retries when the fuzzy run was empty.
        if (fuzzyOutcome.Status == SearchStatus.Ok && fuzzyOutcome.Results.Count > 0)
        {
            return fuzzyOutcome;
        }

        if (originalOutcome.Status is SearchStatus.Ok or SearchStatus.NoResults)
        {
            return new SearchOutcome { Status = SearchStatus.NoResults };
        }

        return originalOutcome;
    }
}
