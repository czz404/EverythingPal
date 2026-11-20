// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Normalizes what the user typed into something ES handles predictably.
///
/// Verified ES 1.1.1.39 behaviours this guards against:
///   * an empty query returns the entire index (33 万条/39 MB here);
///   * a single quoted token is matched as a quoted term, so `"wantai"` no longer finds the
///     `E:\wantai` folder first (and a quoted absolute path such as `"C:\Program Files\App\a.exe"`
///     finds nothing), which is why one copied path / plain token loses its outer quotes;
///   * a trailing separator breaks matching: `D:\wantai\` returns 0 results.
/// Everything operators (`folder:`, `ext:`, `dm:`, wildcards, `|`, `!`) and genuine phrases are left
/// untouched. The function is idempotent: normalizing an already-normalized query changes nothing
/// (the home fallback normalizes first and Core normalizes again).
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

        // Nothing is quoted: only the legacy trailing-separator cleanup applies. Everything
        // expressions (ext:, folder:, |, ! ...) already pass through unchanged.
        if (normalized.IndexOf('"') < 0)        {
            return TrimTrailingSeparators(normalized);
        }

        // Only one fully-quoted expression can lose its outer quotes. Multiple phrases, an operator
        // outside the quotes, or quotes in the middle are advanced syntax: return the expression
        // exactly as typed ("Program Files" | "Windows Kits", ext:pdf "Annual Report").
        if (normalized.Length < 2 || normalized[0] != '"' || normalized[^1] != '"')
        {
            return normalized;
        }

        var inner = normalized[1..^1].Trim();
        if (inner.Length == 0)
        {
            return string.Empty;
        }

        if (inner.Contains('"'))
        {
            return normalized;
        }

        // A single quoted path (what Explorer puts on the clipboard) must be unquoted on purpose:
        // ES treats a quoted path as a literal quoted term and does not find it, even when it only
        // differs by spaces. Paths are recognized by a separator or a drive prefix.
        if (LooksLikePath(inner))
        {
            return TrimTrailingSeparators(inner);
        }

        // Backwards compatibility for the old "wantai" -> wantai behaviour. A quoted phrase with
        // whitespace is deliberately preserved: `"Program Files"` keeps its phrase semantics.
        if (!ContainsWhitespace(inner))
        {
            return TrimTrailingSeparators(inner);
        }

        return normalized;
    }

    private static bool LooksLikePath(string value)
    {
        if (value.Contains('\\') || value.Contains('/'))
        {
            return true;
        }

        return value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimTrailingSeparators(string value)
    {
        // Keep drive roots ("D:" / "C:") and short UNC-like inputs intact.
        if (value.Length <= 2)
        {
            return value;
        }

        var trimmed = value.TrimEnd('\\', '/');
        return trimmed.Length == 0 ? value : trimmed;
    }
}
