// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text;
using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

public class SearchOutcomeMapperTests
{
    private static EverythingResult Result(string path) => new() { FullPath = path };

    [Fact]
    public void Exit_zero_with_results_is_ok()
    {
        var results = new[] { Result(@"C:\a.txt"), Result(@"D:\b") };

        var outcome = SearchOutcomeMapper.FromExitCode(0, string.Empty, results);

        Assert.Equal(SearchStatus.Ok, outcome.Status);
        Assert.Same(results, outcome.Results);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void Exit_zero_with_no_results_is_no_results_not_ok()
    {
        // Regression: `-no-result-error` makes ES exit 0 and write an empty export file for "no
        // matches". Mapping exit 0 straight to Ok produced a blank list in the UI.
        var outcome = SearchOutcomeMapper.FromExitCode(0, string.Empty, []);

        Assert.Equal(SearchStatus.NoResults, outcome.Status);
        Assert.Empty(outcome.Results);
    }

    [Fact]
    public void Exit_zero_without_parsed_results_is_no_results()
    {
        Assert.Equal(SearchStatus.NoResults, SearchOutcomeMapper.FromExitCode(0).Status);
        Assert.Equal(SearchStatus.NoResults, SearchOutcomeMapper.FromExitCode(0, "  ").Status);
    }

    [Fact]
    public void Exit_nine_is_no_results()
    {
        var outcome = SearchOutcomeMapper.FromExitCode(9, string.Empty, null);

        Assert.Equal(SearchStatus.NoResults, outcome.Status);
        Assert.Empty(outcome.Results);
    }

    [Fact]
    public void Exit_eight_is_everything_not_running()
    {
        var outcome = SearchOutcomeMapper.FromExitCode(8);

        Assert.Equal(SearchStatus.EverythingNotRunning, outcome.Status);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
    }

    [Fact]
    public void Other_exit_codes_are_query_failures_with_stderr()
    {
        var outcome = SearchOutcomeMapper.FromExitCode(6, "  unknown option -folder  ");

        Assert.Equal(SearchStatus.QueryFailed, outcome.Status);
        Assert.Contains("6", outcome.Error);
        Assert.Contains("unknown option -folder", outcome.Error);
    }

    [Fact]
    public void Query_failure_without_stderr_still_has_an_error()
    {
        var outcome = SearchOutcomeMapper.FromExitCode(4);

        Assert.Equal(SearchStatus.QueryFailed, outcome.Status);
        Assert.Equal("es.exe exited with 4.", outcome.Error);
    }
}

public class EverythingResultTests
{
    [Theory]
    [InlineData(@"D:\", true, "D:")]
    [InlineData(@"D:/", true, "D:")]
    [InlineData("D:", true, "D:")]
    [InlineData(@"C:\", true, "C:")]
    public void Drive_roots_use_the_volume_name_as_title(string fullPath, bool isFolder, string expectedName)
    {
        var result = new EverythingResult { FullPath = fullPath, IsFolder = isFolder };

        Assert.Equal(expectedName, result.Name);
        Assert.Equal(string.Empty, result.Directory);
    }

    [Fact]
    public void Normal_file_and_folder_names_are_unchanged()
    {
        Assert.Equal("wantai.txt", new EverythingResult { FullPath = @"C:\Users\me\wantai.txt" }.Name);
        Assert.Equal(@"C:\Users\me", new EverythingResult { FullPath = @"C:\Users\me\wantai.txt" }.Directory);

        var folder = new EverythingResult { FullPath = @"D:\wantai\", IsFolder = true };
        Assert.Equal("wantai", folder.Name);
        Assert.Equal(@"D:\", folder.Directory);
    }

    [Fact]
    public void Root_does_not_break_the_ranker()
    {
        var results = new[]
        {
            new EverythingResult { FullPath = @"C:\Users\me\D-drive.txt" },
            new EverythingResult { FullPath = @"D:\", IsFolder = true },
        };

        var ranked = ResultRanker.Rank(results, "D:");

        Assert.Equal(@"D:\", ranked[0].FullPath);
        Assert.Equal("D:", ranked[0].Name);
    }

    [Fact]
    public void Parser_maps_a_drive_root_result_to_the_volume_name()
    {
        const string json = """[{"filename":"D:\\","size":0,"date_modified":"2026-10-19T20:31:29","attributes":16}]""";

        var results = EsResultParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)), "D:");

        var root = Assert.Single(results);
        Assert.True(root.IsFolder);
        Assert.Equal("D:", root.Name);
        Assert.Equal(@"D:\", root.FullPath);
        Assert.Equal(string.Empty, root.Directory);
    }

    [Fact]
    public void ParseFile_treats_a_missing_export_file_as_no_results()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ep-missing-{Guid.NewGuid():N}.json");

        Assert.Empty(EsResultParser.ParseFile(missing, "x"));
    }
}
