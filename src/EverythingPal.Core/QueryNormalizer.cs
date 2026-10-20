// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Normalizes what the user typed into something ES handles predictably.
///
/// Verified ES 1.1.1.39 behaviours this guards against:
///   * an empty query returns the entire index (33 万条/39 MB here);
///   * a quoted query is matched literally, so `"wantai"` returns 0 results;
///   * a trailing separator breaks matching: `D:\wantai\` returns 0 results.
/// Everything operators (`folder:`, `ext:`, `dm:`, wildcards, `|`, `!`) are left untouched.
/// </summary>
public static class QueryNormalizer
{
    public static string Normalize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var normalized = query.Trim();

        // Strip one matching pair of surrounding double quotes ('"' is literal to Everything).
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
        }

        // Trailing separators make ES return nothing; keep drive roots ("D:" / "C:").
        if (normalized.Length > 2)
        {
            normalized = normalized.TrimEnd('\\', '/');
        }

        return normalized.Trim();
    }
}
