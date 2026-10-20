// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Everything has no relevance score, so we re-rank its name-sorted results:
/// exact name, prefix, stem (extensionless name) exact/prefix, substring, then the rest.
/// Ties go to folders, then to shorter names (usually the more specific match).
/// </summary>
public static class ResultRanker
{
    public static List<EverythingResult> Rank(IEnumerable<EverythingResult> results, string query)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. results];
        }

        return
        [
            .. results
                .Select(result => (Result: result, Score: MatchScore(result.Name, query)))
                .OrderBy(t => t.Score)
                .ThenBy(t => t.Result.IsFolder ? 0 : 1)
                .ThenBy(t => t.Result.Name.Length)
                .ThenBy(t => t.Result.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => t.Result),
        ];
    }

    internal static int MatchScore(string name, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (stem.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 5;
    }
}
