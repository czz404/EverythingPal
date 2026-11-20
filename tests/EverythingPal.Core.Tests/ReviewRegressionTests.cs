// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text;
using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

/// <summary>
/// Regression tests for the fixes listed in docs/qa/code-review.md (2026-11-20 review). Kept in a
/// separate file so the original tests remain the behavioural baseline.
/// </summary>
public class QueryNormalizerQuotedExpressionTests
{
    [Theory]
    [InlineData("\"wantai\"", "wantai")]
    [InlineData("\"Program Files\"", "\"Program Files\"")]
    [InlineData("\"Program Files\" | \"Windows Kits\"", "\"Program Files\" | \"Windows Kits\"")]
    [InlineData("ext:pdf \"Annual Report\"", "ext:pdf \"Annual Report\"")]
    [InlineData("\"Annual Report\"", "\"Annual Report\"")]
    [InlineData("\"D:\\wantai\\\"", "D:\\wantai")]
    [InlineData("\"C:\\Program Files\\App\\a.exe\"", "C:\\Program Files\\App\\a.exe")]
    [InlineData("\"\\\\server\\share\\file name.txt\"", "\\\\server\\share\\file name.txt")]
    public void Normalize_keeps_phrases_and_unquotes_a_single_path(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("\"wantai\"")]
    [InlineData("\"Program Files\"")]
    [InlineData("\"Program Files\" | \"Windows Kits\"")]
    [InlineData("ext:pdf \"Annual Report\"")]
    [InlineData("D:\\wantai\\")]
    [InlineData("\"D:\\wantai\\\"")]
    [InlineData("\"C:\\Program Files\\App\\a.exe\"")]
    [InlineData("\"Annual Report\"")]
    [InlineData("folder:wantai ext:lnk")]
    [InlineData("want*")]
    public void Normalize_is_idempotent(string input)
    {
        // The home fallback normalizes first and Core normalizes again. A second pass must not eat
        // quotes or separators that the first pass deliberately kept.
        var once = QueryNormalizer.Normalize(input);
        Assert.Equal(once, QueryNormalizer.Normalize(once));
    }
}

public class FallbackResultPlanTests
{
    [Theory]
    // home rows = 1: the extra fetched row is what distinguishes one match from several.
    [InlineData(1, 0, 0, false, false)]
    [InlineData(1, 1, 1, false, false)]
    [InlineData(1, 2, 1, true, true)]
    // home rows = 3: show at most 3, exact count only when all matches were fetched.
    [InlineData(3, 2, 2, true, false)]
    [InlineData(3, 3, 3, true, false)]
    [InlineData(3, 4, 3, true, true)]
    // A caller that ignores the +1 cap still cannot show more than the configured rows.
    [InlineData(3, 10, 3, true, false)]
    public void Create_matches_the_home_fallback_acceptance_matrix(
        int visibleLimit,
        int returnedCount,
        int expectedVisible,
        bool expectedMultiple,
        bool expectedHitLimit)
    {
        var plan = FallbackResultPlan.Create(visibleLimit, returnedCount);

        Assert.Equal(expectedVisible, plan.VisibleCount);
        Assert.Equal(expectedMultiple, plan.HasMultipleMatches);
        Assert.Equal(expectedHitLimit, plan.HitFetchLimit);
    }

    [Fact]
    public void Zero_home_rows_never_shows_a_row()
    {
        var plan = FallbackResultPlan.Create(0, 5);

        Assert.Equal(0, plan.VisibleCount);
        Assert.True(plan.HasMultipleMatches);
    }
}

public class PageSearchRequestStateTests
{
    private static int RunPrefillThenHostEcho(string prefill, string hostEcho)
    {
        var state = new PageSearchRequestState();
        var starts = 0;

        state.SetPrefill(prefill);
        if (state.OnUpdateSearchText(hostEcho).ShouldStart)
        {
            starts++;
        }

        if (state.TakePendingQuery() is not null)
        {
            starts++;
        }

        // A second GetItems must not start the same deferred query again.
        if (state.TakePendingQuery() is not null)
        {
            starts++;
        }

        return starts;
    }

    [Fact]
    public void Prefill_A_then_host_echo_A_then_two_GetItems_starts_A_once()
    {
        Assert.Equal(1, RunPrefillThenHostEcho("A", "A"));
    }

    [Fact]
    public void Prefill_A_then_GetItems_then_host_echo_A_starts_A_once()
    {
        var state = new PageSearchRequestState();
        var starts = 0;

        state.SetPrefill("A");
        if (state.TakePendingQuery() is not null)
        {
            starts++;
        }

        if (state.OnUpdateSearchText("A").ShouldStart)
        {
            starts++;
        }

        Assert.Equal(1, starts);
    }

    [Fact]
    public void Prefill_A_then_user_types_B_starts_only_B()
    {
        var state = new PageSearchRequestState();

        state.SetPrefill("A");
        var action = state.OnUpdateSearchText("B");

        Assert.True(action.ShouldStart);
        Assert.Equal("B", action.Query);
        Assert.Null(state.TakePendingQuery());

        // The host re-sending B after the search started must not start a second search.
        Assert.False(state.OnUpdateSearchText("B").ShouldStart);
    }

    [Fact]
    public void Activation_with_empty_text_keeps_the_pending_query()
    {
        var state = new PageSearchRequestState();

        state.SetPrefill("A");
        Assert.False(state.OnUpdateSearchText(string.Empty).ShouldStart);
        Assert.Equal("A", state.TakePendingQuery());
    }
}

public class ExplicitSortPreservationTests
{
    private static MemoryStream Utf8(string json) => new(Encoding.UTF8.GetBytes(json));

    // Deliberately in the reverse of name order: the newer, longer name comes first.
    private const string DateOrderJson = """
        [{"filename":"C:\\reports\\long-report.pdf","size":10,"date_modified":"2026-11-20T10:00:00"},
         {"filename":"C:\\reports\\a.pdf","size":10,"date_modified":"2026-11-19T10:00:00"}]
        """;

    [Fact]
    public void Date_modified_descending_keeps_the_order_es_returned()
    {
        var results = EsResultParser.Parse(Utf8(DateOrderJson), "ext:pdf", "date-modified-descending");

        Assert.Equal(@"C:\reports\long-report.pdf", results[0].FullPath);
        Assert.Equal(@"C:\reports\a.pdf", results[1].FullPath);
    }

    [Fact]
    public void Run_count_descending_keeps_the_order_es_returned()
    {
        // Run count is not part of EverythingResult; ES sorted it already and we must not touch it.
        const string json = """
            [{"filename":"C:\\tools\\z-launcher.exe"},{"filename":"C:\\tools\\a-launcher.exe"}]
            """;

        var results = EsResultParser.Parse(Utf8(json), "launcher", "run-count-descending");

        Assert.Equal(@"C:\tools\z-launcher.exe", results[0].FullPath);
        Assert.Equal(@"C:\tools\a-launcher.exe", results[1].FullPath);
    }

    [Fact]
    public void Default_sort_still_re_ranks_by_relevance()
    {
        // Same data as the date test: without an explicit sort the legacy ranker must stay active
        // (shorter name wins), proving the gate is wired to the configured sort, not to the parser.
        var defaultSorted = EsResultParser.Parse(Utf8(DateOrderJson), "ext:pdf");
        var explicitDefault = EsResultParser.Parse(Utf8(DateOrderJson), "ext:pdf", SearchOptions.DefaultSort);

        Assert.Equal(@"C:\reports\a.pdf", defaultSorted[0].FullPath);
        Assert.Equal(@"C:\reports\a.pdf", explicitDefault[0].FullPath);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("name-ascending", true)]
    [InlineData("date-modified-descending", false)]
    [InlineData("date-modified-ascending", false)]
    [InlineData("path", false)]
    [InlineData("run-count-descending", false)]
    public void Relevance_ranking_is_only_used_for_the_default_sort(string? sort, bool expected)
    {
        Assert.Equal(expected, ResultRanker.UsesRelevanceRanking(sort));
    }
}
