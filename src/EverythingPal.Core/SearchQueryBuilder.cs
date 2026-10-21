// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Builds the ES search expression for a user query, adding Everything 1.5's fuzzy modifiers when
/// that is safe.
///
/// Why: a plain ES query for <c>term1</c> does not match a folder named <c>Term 1</c> (the whitespace
/// is significant), which is exactly the kind of near-miss users expect search to forgive. Everything
/// 1.5 supports <c>nocase:</c> (ignore case) and <c>ignorews:</c> (ignore whitespace) modifiers, and
/// this combination is verified to work with ES 1.1.1.39 / Everything 1.5.0.1423b:
/// <code>
/// nocase:ignorews:term1   ->  matches  E:\...\Term 1\
/// </code>
///
/// Scope/limits (deliberately conservative):
///   * only a <b>plain single token</b> is rewritten. Multi-word queries are AND-ed by ES and the
///     whitespace modifier does not collapse the token boundaries, so rewriting them is not useful.
///   * any Everything syntax characters (<c>:</c> modifiers, <c>|</c>, <c>!</c>, wildcards, quoting,
///     grouping, <c>#</c> numeric, <c>&amp;</c>, <c>~</c>) or a path separator keep the query as typed;
///     advanced syntax must keep its original meaning.
///   * Everything 1.4 does not know these modifiers and answers with zero results (not an error), so
///     <see cref="EverythingSearch"/> falls back to the original query when the fuzzy run is empty.
/// </summary>
public static class SearchQueryBuilder
{
    /// <summary>Everything 1.5 modifier prefix: force case-insensitive matching and ignore whitespace.</summary>
    public const string FuzzyPrefix = "nocase:ignorews:";

    /// <summary>Shorter queries are not worth a second ES process run. Single CJK characters are length 1.</summary>
    private const int MinimumFuzzyLength = 2;

    /// <summary>Characters that carry Everything syntax/meaning and therefore disable the rewrite.</summary>
    private static readonly char[] SyntaxCharacters =
    [
        ':', '|', '!', '<', '>', '"', '*', '?', '(', ')', '[', ']', '#', '&', '~', '\\',
    ];

    /// <summary>
    /// Returns <c>nocase:ignorews:&lt;query&gt;</c> for a plain single-token query, otherwise the query
    /// unchanged. The query should already be normalized by <see cref="QueryNormalizer"/>.
    /// </summary>
    public static string Build(string normalizedQuery)
    {
        ArgumentNullException.ThrowIfNull(normalizedQuery);

        return IsPlainSingleToken(normalizedQuery)
            ? FuzzyPrefix + normalizedQuery
            : normalizedQuery;
    }

    private static bool IsPlainSingleToken(string query)
    {
        if (query.Length < MinimumFuzzyLength)
        {
            return false;
        }

        foreach (var c in query)
        {
            if (char.IsWhiteSpace(c) || Array.IndexOf(SyntaxCharacters, c) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
