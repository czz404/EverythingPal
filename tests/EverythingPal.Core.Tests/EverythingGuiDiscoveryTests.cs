// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

public class EverythingGuiDiscoveryTests
{
    [Fact]
    public void BuildCandidates_orders_sources_and_appends_the_executable_name()
    {
        var inputs = new EverythingGuiDiscoveryInputs
        {
            RunningProcessPaths = [@"D:\software\Everything\Everything.exe"],
            AppPathEntries = ["\"E:\\Apps\\Everything\\Everything.exe\""],
            UninstallLocations = [@"F:\Portable\Everything"],
            CommonDirectories = [@"C:\Program Files\Everything"],
            PathVariable = @"C:\Tools\Everything;C:\Windows",
        };

        var candidates = EverythingGuiDiscovery.BuildCandidates(inputs);

        Assert.Equal(
            [
                @"D:\software\Everything\Everything.exe",
                @"E:\Apps\Everything\Everything.exe",
                @"F:\Portable\Everything\Everything.exe",
                @"C:\Program Files\Everything\Everything.exe",
                @"C:\Tools\Everything\Everything.exe",
                @"C:\Windows\Everything.exe",
            ],
            candidates);
    }

    [Fact]
    public void BuildCandidates_deduplicates_ignoring_case_and_keeps_the_first_source()
    {
        var inputs = new EverythingGuiDiscoveryInputs
        {
            RunningProcessPaths = [@"D:\software\Everything\Everything.exe"],
            UninstallLocations = [@"d:\SOFTWARE\everything"],
            CommonDirectories = [@"D:\software\Everything"],
            PathVariable = @"D:\software\everything;",
        };

        var candidates = EverythingGuiDiscovery.BuildCandidates(inputs);

        var single = Assert.Single(candidates);
        Assert.Equal(@"D:\software\Everything\Everything.exe", single);
    }

    [Theory]
    [InlineData(@"D:\software\Everything", @"D:\software\Everything\Everything.exe")]
    [InlineData(@"D:\software\Everything\", @"D:\software\Everything\Everything.exe")]
    [InlineData("\"D:\\software\\Everything\\Everything.exe\"", @"D:\software\Everything\Everything.exe")]
    [InlineData(@"D:\software\Everything\Everything.exe,0", @"D:\software\Everything\Everything.exe")]
    [InlineData("\"D:\\software\\Everything\\Everything.exe\",0", @"D:\software\Everything\Everything.exe")]
    [InlineData(@"   D:\software\Everything   ", @"D:\software\Everything\Everything.exe")]
    public void BuildCandidates_normalizes_registry_style_entries(string raw, string expected)
    {
        var candidates = EverythingGuiDiscovery.BuildCandidates(new EverythingGuiDiscoveryInputs
        {
            UninstallLocations = [raw],
        });

        Assert.Equal([expected], candidates);
    }

    [Fact]
    public void BuildCandidates_ignores_blank_entries_and_empty_inputs()
    {
        var candidates = EverythingGuiDiscovery.BuildCandidates(new EverythingGuiDiscoveryInputs
        {
            RunningProcessPaths = [string.Empty, "   "],
            AppPathEntries = [null!, ""],
            UninstallLocations = ["", "  "],
            CommonDirectories = [null!, "  ", ""],
            PathVariable = ";;\"\";",
        });

        Assert.Empty(candidates);
        Assert.Empty(EverythingGuiDiscovery.BuildCandidates(new EverythingGuiDiscoveryInputs()));
    }

    [Fact]
    public void Find_returns_the_first_candidate_that_the_injected_probe_reports()
    {
        var inputs = new EverythingGuiDiscoveryInputs
        {
            RunningProcessPaths = [@"D:\gone\Everything.exe"],
            UninstallLocations = [@"D:\software\Everything"],
            CommonDirectories = [@"C:\Program Files\Everything"],
        };
        var probed = new List<string>();

        var found = EverythingGuiDiscovery.Find(inputs, path =>
        {
            probed.Add(path);
            return path == @"D:\software\Everything\Everything.exe";
        });

        Assert.Equal(@"D:\software\Everything\Everything.exe", found);
        Assert.Equal([@"D:\gone\Everything.exe", @"D:\software\Everything\Everything.exe"], probed);
    }

    [Fact]
    public void Find_returns_null_when_nothing_exists()
    {
        var inputs = new EverythingGuiDiscoveryInputs
        {
            AppPathEntries = [@"D:\a\Everything.exe"],
            UninstallLocations = [@"D:\b"],
            CommonDirectories = [@"C:\Program Files\Everything"],
            PathVariable = @"C:\bin",
        };
        var probed = new List<string>();

        var found = EverythingGuiDiscovery.Find(inputs, path =>
        {
            probed.Add(path);
            return false;
        });

        Assert.Null(found);
        Assert.Equal(4, probed.Count);
    }

    [Fact]
    public void Running_process_path_wins_over_program_files()
    {
        var inputs = new EverythingGuiDiscoveryInputs
        {
            RunningProcessPaths = [@"D:\software\Everything\Everything.exe"],
            CommonDirectories = [@"C:\Program Files\Everything"],
        };

        var found = EverythingGuiDiscovery.Find(inputs, path => true);

        Assert.Equal(@"D:\software\Everything\Everything.exe", found);
    }
}
