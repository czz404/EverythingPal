// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text;

namespace EverythingPal.Core;

/// <summary>
/// One ES invocation split into fixed options and a raw search-expression tail.
///
/// ES 1.1.1.39 has two search switches:
///   * <c>-search</c> / <c>-q</c> calls <c>_es_expect_command_argv()</c>, which explicitly
///     <b>removes quotes</b> from the value (see upstream <c>src/es.c</c>);
///   * <c>-search*</c> / <c>-q*</c> / <c>-s*</c> "eats the rest": it appends the remaining raw
///     command line to the search expression and does not remove quotes.
///
/// Because the expression may contain Everything syntax (quoted phrases, OR, a quoted absolute
/// path), it must go through <c>-search*</c> and must be appended to the Windows command line
/// verbatim. <see cref="ToCommandLine"/> does that; the fixed options are quoted with the standard
/// Windows argument rules.
/// </summary>
public sealed class EsCommandLine
{
    public EsCommandLine(IReadOnlyList<string> fixedArguments, string searchExpression)
    {
        ArgumentNullException.ThrowIfNull(fixedArguments);
        ArgumentNullException.ThrowIfNull(searchExpression);

        FixedArguments = fixedArguments;
        SearchExpression = searchExpression;
    }

    /// <summary>Options before the raw tail (everything except <c>-search*</c> and the query).</summary>
    public IReadOnlyList<string> FixedArguments { get; }

    /// <summary>The user expression, preserved byte-for-byte.</summary>
    public string SearchExpression { get; }

    /// <summary>
    /// Builds the exact <c>ProcessStartInfo.Arguments</c> value. The query is appended without any
    /// quoting/escaping, which is what <c>-search*</c> expects.
    /// </summary>
    public string ToCommandLine()
    {
        var builder = new StringBuilder();
        foreach (var argument in FixedArguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Quote(argument));
        }

        builder.Append(" -search* ");
        builder.Append(SearchExpression);
        return builder.ToString();
    }

    /// <summary>
    /// Standard Windows command-line quoting for one fixed argument: only arguments containing
    /// whitespace or quotes are wrapped, backslashes before a quote (and trailing backslashes) are
    /// doubled. The raw search tail never goes through this method.
    /// </summary>
    internal static string Quote(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = false;
        foreach (var c in argument)
        {
            if (c is ' ' or '\t' or '"')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
