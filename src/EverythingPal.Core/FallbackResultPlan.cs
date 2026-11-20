// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Decides how many of the returned home-fallback rows to show and what pressing Enter on them
/// should do.
///
/// The home query deliberately fetches <c>visibleLimit + 1</c> results: the extra row is not shown,
/// it only proves that a second match exists. Asking ES for exactly <c>visibleLimit</c> rows (the old
/// behaviour) made a one-row home result always look like the only match, so Enter opened the file
/// instead of the full result page.
/// </summary>
public readonly record struct FallbackResultPlan(int VisibleCount, bool HasMultipleMatches, bool HitFetchLimit)
{
    public static FallbackResultPlan Create(int visibleLimit, int returnedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(returnedCount);

        if (visibleLimit < 1)
        {
            return new FallbackResultPlan(0, returnedCount > 1, false);
        }

        var visible = Math.Min(visibleLimit, returnedCount);
        var multiple = returnedCount > 1;

        // True when ES returned every row it was allowed to: there may be more matches that were not
        // fetched, so the exact total is unknown and the first row must say "more results".
        var hitFetchLimit = returnedCount == visibleLimit + 1;

        return new FallbackResultPlan(visible, multiple, hitFetchLimit);
    }
}
