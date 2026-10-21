// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text;

namespace EverythingPal.Core;

/// <summary>
/// Everything has no relevance score, so we re-rank its name-sorted results:
/// exact name, prefix, stem (extensionless name) exact/prefix, substring, then the rest.
/// Ties go to folders, then to shorter names (usually the more specific match).
///
/// Matching is case- and whitespace-insensitive (both sides) so a fuzzy ES hit such as
/// <c>Term 1</c> scores as an exact match for the user's <c>term1</c>.
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
        var normalizedName = RemoveWhitespace(name);
        var normalizedQuery = RemoveWhitespace(query);

        if (normalizedName.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var stem = RemoveWhitespace(Path.GetFileNameWithoutExtension(name));
        if (stem.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (stem.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (normalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 5;
    }

    /// <summary>Strips all whitespace so "Term 1" and "term1" compare equal.</summary>
    private static string RemoveWhitespace(string value)
    {
        var firstWhitespace = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                firstWhitespace = i;
                break;
            }
        }

        if (firstWhitespace < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        builder.Append(value, 0, firstWhitespace);
        for (var i = firstWhitespace; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }
}
