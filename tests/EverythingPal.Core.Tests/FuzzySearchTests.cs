// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

public class SearchQueryBuilderTests
{
    [Theory]
    [InlineData("term1", "nocase:ignorews:term1")]
    [InlineData("Term1", "nocase:ignorews:Term1")]
    [InlineData("TERM1", "nocase:ignorews:TERM1")]
    [InlineData("wantai", "nocase:ignorews:wantai")]
    [InlineData("微信", "nocase:ignorews:微信")]
    [InlineData("报告", "nocase:ignorews:报告")]
    [InlineData("foo-bar_baz", "nocase:ignorews:foo-bar_baz")]
    [InlineData("report.pdf", "nocase:ignorews:report.pdf")]
    [InlineData("2026", "nocase:ignorews:2026")]
    public void Build_adds_the_fuzzy_modifiers_to_a_plain_single_token(string query, string expected)
    {
        Assert.Equal(expected, SearchQueryBuilder.Build(query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("微")]
    [InlineData("Term 1")]
    [InlineData(" term1")]
    [InlineData("term1 ")]
    [InlineData("ext:jpg")]
    [InlineData("folder:Term 1")]
    [InlineData("want*")]
    [InlineData(@"D:\wantai")]
    [InlineData(@"\downloads")]
    [InlineData("#x41")]
    [InlineData("want|ai")]
    [InlineData("!wantai")]
    [InlineData("<wantai>")]
    [InlineData("\"wantai\"")]
    [InlineData("want?")]
    [InlineData("(wantai)")]
    [InlineData("[wantai]")]
    [InlineData("a&b")]
    [InlineData("~a")]
    public void Build_keeps_advanced_or_non_plain_queries_untouched(string query)
    {
        Assert.Equal(query, SearchQueryBuilder.Build(query));
    }

    [Fact]
    public void Build_keeps_an_already_modified_expression_untouched()
    {
        const string expression = "nocase:ignorews:term1";

        Assert.Equal(expression, SearchQueryBuilder.Build(expression));
    }

    [Fact]
    public void Build_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => SearchQueryBuilder.Build(null!));
    }
}

public class SearchRetryPolicyTests
{
    private static SearchOutcome Outcome(SearchStatus status, params string[] paths) => new()
    {
        Status = status,
        Results = [.. paths.Select(path => new EverythingResult { FullPath = path })],
        Error = status is SearchStatus.QueryFailed or SearchStatus.Timeout
            or SearchStatus.EverythingNotRunning or SearchStatus.EsExeNotFound
            ? "failure"
            : null,
    };

    [Theory]
    [InlineData(SearchStatus.NoResults)]
    [InlineData(SearchStatus.QueryFailed)]
    public void ShouldRetry_is_true_for_a_retryable_empty_fuzzy_run(SearchStatus status)
    {
        var fuzzy = Outcome(status);

        Assert.True(SearchRetryPolicy.ShouldRetry(fuzzy, "nocase:ignorews:term1", "term1"));
    }

    [Fact]
    public void ShouldRetry_is_true_for_an_ok_fuzzy_run_without_results()
    {
        // Defensive: a mapper could theoretically produce Ok with an empty list.
        var fuzzy = Outcome(SearchStatus.Ok);

        Assert.True(SearchRetryPolicy.ShouldRetry(fuzzy, "nocase:ignorews:term1", "term1"));
    }

    [Fact]
    public void ShouldRetry_is_false_when_the_fuzzy_run_has_results()
    {
        var fuzzy = Outcome(SearchStatus.Ok, @"E:\BaiduNetdiskDownload\Term 1");

        Assert.False(SearchRetryPolicy.ShouldRetry(fuzzy, "nocase:ignorews:term1", "term1"));
    }

    [Theory]
    [InlineData(SearchStatus.NoResults)]
    [InlineData(SearchStatus.QueryFailed)]
    public void ShouldRetry_is_false_when_the_expression_was_not_rewritten(SearchStatus status)
    {
        var outcome = Outcome(status);

        Assert.False(SearchRetryPolicy.ShouldRetry(outcome, "Term 1", "Term 1"));
    }

    [Theory]
    [InlineData(SearchStatus.Timeout)]
    [InlineData(SearchStatus.EverythingNotRunning)]
    [InlineData(SearchStatus.EsExeNotFound)]
    public void ShouldRetry_is_false_for_terminal_failures(SearchStatus status)
    {
        var fuzzy = Outcome(status);

        Assert.False(SearchRetryPolicy.ShouldRetry(fuzzy, "nocase:ignorews:term1", "term1"));
    }

    [Fact]
    public void Select_prefers_the_original_query_results()
    {
        var fuzzy = Outcome(SearchStatus.NoResults);
        var original = Outcome(SearchStatus.Ok, @"E:\BaiduNetdiskDownload\Term 1");

        var selected = SearchRetryPolicy.Select(fuzzy, original);

        Assert.Same(original, selected);
    }

    [Fact]
    public void Select_returns_no_results_when_both_runs_are_empty()
    {
        Assert.Equal(
            SearchStatus.NoResults,
            SearchRetryPolicy.Select(Outcome(SearchStatus.NoResults), Outcome(SearchStatus.NoResults)).Status);
        Assert.Equal(
            SearchStatus.NoResults,
            SearchRetryPolicy.Select(Outcome(SearchStatus.NoResults), Outcome(SearchStatus.Ok)).Status);
    }

    [Fact]
    public void Select_returns_the_original_error_when_the_retry_fails()
    {
        var original = Outcome(SearchStatus.QueryFailed);

        var selected = SearchRetryPolicy.Select(Outcome(SearchStatus.NoResults), original);

        Assert.Same(original, selected);
        Assert.Equal("failure", selected.Error);
    }

    [Fact]
    public void Select_keeps_a_terminal_error_from_the_retry()
    {
        var original = Outcome(SearchStatus.EverythingNotRunning);

        var selected = SearchRetryPolicy.Select(Outcome(SearchStatus.NoResults), original);

        Assert.Equal(SearchStatus.EverythingNotRunning, selected.Status);
    }

    [Fact]
    public void Select_falls_back_to_fuzzy_results_if_the_original_is_empty()
    {
        // Defensive: Search only retries when the fuzzy run was empty, but the policy stays safe.
        var fuzzy = Outcome(SearchStatus.Ok, @"D:\wantai");

        var selected = SearchRetryPolicy.Select(fuzzy, Outcome(SearchStatus.NoResults));

        Assert.Same(fuzzy, selected);
    }

    [Fact]
    public void Select_rejects_nulls()
    {
        var outcome = Outcome(SearchStatus.NoResults);

        Assert.Throws<ArgumentNullException>(() => SearchRetryPolicy.Select(null!, outcome));
        Assert.Throws<ArgumentNullException>(() => SearchRetryPolicy.Select(outcome, null!));
    }
}

public class ResultRankerFuzzyTests
{
    private static EverythingResult Item(string name, bool folder = false) => new()
    {
        FullPath = Path.Combine(@"C:\root", name),
        IsFolder = folder,
    };

    [Fact]
    public void Whitespace_insensitive_exact_match_beats_substring()
    {
        var ranked = ResultRanker.Rank([Item("xterm1"), Item("Term 1", folder: true)], "term1");

        Assert.Equal("Term 1", ranked[0].Name);
        Assert.Equal("xterm1", ranked[1].Name);
    }

    [Fact]
    public void Whitespace_insensitive_match_is_case_insensitive()
    {
        var ranked = ResultRanker.Rank([Item("xterm1"), Item("TERM 1")], "term1");

        Assert.Equal("TERM 1", ranked[0].Name);
    }

    [Fact]
    public void Whitespace_insensitive_prefix_beats_substring()
    {
        var ranked = ResultRanker.Rank([Item("xterm1"), Item("Term 1 old")], "term1");

        Assert.Equal("Term 1 old", ranked[0].Name);
    }

    [Fact]
    public void Whitespace_in_the_query_is_ignored()
    {
        var ranked = ResultRanker.Rank([Item("xterm1"), Item("Term1")], "term 1");

        // "Term 1" and "Term1" are the same once whitespace is ignored; shorter name wins the tie.
        Assert.Equal("Term1", ranked[0].Name);
        Assert.Equal("xterm1", ranked[1].Name);
    }
}
