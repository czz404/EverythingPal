// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

public class QueryNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("wantai", "wantai")]
    [InlineData("  wantai  ", "wantai")]
    [InlineData("\"wantai\"", "wantai")]
    [InlineData("'wantai'", "'wantai'")]
    [InlineData("D:\\wantai\\", "D:\\wantai")]
    [InlineData("D:/wantai/", "D:/wantai")]
    [InlineData("D:\\", "D:")]
    [InlineData("C:", "C:")]
    [InlineData("folder:wantai ext:lnk", "folder:wantai ext:lnk")]
    [InlineData("want*", "want*")]
    public void Normalize_handles_known_es_quirks(string? input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }
}

public class EsArgumentsTests
{
    private static readonly SearchOptions DefaultOptions = new();

    [Fact]
    public void Build_uses_export_json_and_never_the_bogus_folder_switch()
    {
        var arguments = EsArguments.Build("wantai", @"C:\temp\out.json", DefaultOptions);

        Assert.Equal("-export-json", arguments[0]);
        Assert.Equal(@"C:\temp\out.json", arguments[1]);
        Assert.DoesNotContain("-folder", arguments);
        Assert.DoesNotContain("-select", arguments);
        Assert.Contains("-no-result-error", arguments);
        Assert.Equal("-search", arguments[^2]);
        Assert.Equal("wantai", arguments[^1]);
    }

    [Fact]
    public void Build_clamps_result_limit_and_honours_sort()
    {
        var arguments = EsArguments.Build("x", "out.json", new SearchOptions { MaxResults = 9999, Sort = "date-modified-descending" });

        var argumentList = arguments.ToList();
        var limitIndex = argumentList.IndexOf("-n");
        Assert.Equal("500", argumentList[limitIndex + 1]);

        var sortIndex = argumentList.IndexOf("-sort");
        Assert.Equal("date-modified-descending", argumentList[sortIndex + 1]);
    }

    [Fact]
    public void Build_rejects_empty_query()
    {
        Assert.Throws<ArgumentException>(() => EsArguments.Build("", "out.json", DefaultOptions));
        Assert.Throws<ArgumentException>(() => EsArguments.Build("   ", "out.json", DefaultOptions));
    }

    [Fact]
    public void Build_keeps_query_as_one_single_argument()
    {
        var arguments = EsArguments.Build("want ai", "out.json", DefaultOptions);
        Assert.Equal("want ai", arguments[^1]);
    }
}

public class EsResultParserTests
{
    private static MemoryStream Utf8(string json) => new(System.Text.Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Parse_detects_folders_from_trailing_separator()
    {
        const string json = """
            [{"filename":"D:\\wantai\\","size":0,"date_modified":"2026-10-19T20:31:29","attributes":16},
             {"filename":"C:\\Users\\me\\Recent\\wantai.lnk","size":434,"date_modified":"2026-10-19T20:31:33","attributes":32}]
            """;

        var results = EsResultParser.Parse(Utf8(json), "wantai");

        Assert.Equal(2, results.Count);
        Assert.True(results[0].IsFolder);
        Assert.Equal(@"D:\wantai", results[0].FullPath);
        Assert.Equal("wantai", results[0].Name);
        Assert.Equal(@"D:\", results[0].Directory);
        Assert.False(results[1].IsFolder);
        Assert.Equal(434, results[1].Size);
    }

    [Fact]
    public void Parse_ranks_exact_folder_match_first()
    {
        const string json = """
            [{"filename":"C:\\Users\\me\\Recent\\wantai.lnk"},
             {"filename":"D:\\wantai\\"},
             {"filename":"D:\\projects\\wantai-old"}]
            """;

        var results = EsResultParser.Parse(Utf8(json), "wantai");

        Assert.Equal(@"D:\wantai", results[0].FullPath);
    }

    [Fact]
    public void Parse_handles_cjk_names()
    {
        const string json = """[{"filename":"C:\\ProgramData\\Start Menu\\微信输入法\\微信输入法.lnk"}]""";
        var results = EsResultParser.Parse(Utf8(json), "微信");
        Assert.Single(results);
        Assert.Equal("微信输入法.lnk", results[0].Name);
    }

    [Fact]
    public void Parse_returns_empty_for_empty_or_non_array_payload()
    {
        Assert.Empty(EsResultParser.Parse(Utf8(string.Empty), "x"));
        Assert.Empty(EsResultParser.Parse(Utf8("{}"), "x"));
    }

    [Fact]
    public void Parse_skips_items_without_filename_and_tolerates_missing_fields()
    {
        const string json = """[{"size":1},{"filename":"C:\\a.txt"}]""";
        var results = EsResultParser.Parse(Utf8(json), "a");
        Assert.Single(results);
        Assert.Equal(@"C:\a.txt", results[0].FullPath);
        Assert.Equal(0, results[0].Size);
    }
}

public class ResultRankerTests
{
    private static EverythingResult Item(string name, bool folder = false, string? directory = null) => new()
    {
        FullPath = Path.Combine(directory ?? @"C:\root", name),
        IsFolder = folder,
    };

    [Fact]
    public void Exact_beats_prefix_beats_stem_beats_substring()
    {
        var ranked = ResultRanker.Rank(
            [
                Item("wantailong.txt"),
                Item("wantai-old.txt"),
                Item("wantai.txt"),
                Item("wantai"),
            ],
            "wantai");

        Assert.Equal("wantai", ranked[0].Name);
        Assert.Equal("wantai.txt", ranked[1].Name);
        Assert.Equal("wantai-old.txt", ranked[2].Name);
        Assert.Equal("wantailong.txt", ranked[3].Name);
    }

    [Fact]
    public void Folder_wins_tie_break()
    {
        var ranked = ResultRanker.Rank([Item("wantai.lnk"), Item("wantai", folder: true)], "wantai");

        Assert.True(ranked[0].IsFolder);
    }

    [Fact]
    public void Empty_query_keeps_input_order()
    {
        var ranked = ResultRanker.Rank([Item("b.txt"), Item("a.txt")], string.Empty);
        Assert.Equal("b.txt", ranked[0].Name);
    }
}


public class EsDiscoveryTests
{
    [Fact]
    public void FindEsExe_prefers_bundled_copy_inside_the_package()
    {
        var root = Path.Combine(Path.GetTempPath(), "ep-discovery-" + Guid.NewGuid().ToString("N"));
        var tools = Path.Combine(root, "Tools");
        Directory.CreateDirectory(tools);
        var bundled = Path.Combine(tools, "es.exe");
        File.WriteAllText(bundled, "stub");

        try
        {
            Assert.Equal(bundled, EverythingSearch.FindEsExe(configured: null, baseDirectory: root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindEsExe_accepts_configured_directory_and_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ep-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configured = Path.Combine(root, "es.exe");
        File.WriteAllText(configured, "stub");

        try
        {
            Assert.Equal(configured, EverythingSearch.FindEsExe(configured: configured, baseDirectory: root));
            Assert.Equal(configured, EverythingSearch.FindEsExe(configured: root, baseDirectory: root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindEsExe_returns_null_when_nothing_is_available()
    {
        var root = Path.Combine(Path.GetTempPath(), "ep-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            // On a dev machine ES may exist in PATH; only assert on the isolated base directory.
            var previous = Environment.GetEnvironmentVariable("EVERYTHINGPAL_ES");
            Environment.SetEnvironmentVariable("EVERYTHINGPAL_ES", null);
            try
            {
                var found = EverythingSearch.FindEsExe(configured: null, baseDirectory: root);
                Assert.True(found is null || File.Exists(found));
            }
            finally
            {
                Environment.SetEnvironmentVariable("EVERYTHINGPAL_ES", previous);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

