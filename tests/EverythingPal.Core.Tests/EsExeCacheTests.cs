// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using EverythingPal.Core;
using Xunit;

namespace EverythingPal.Core.Tests;

public class EsExePathCacheTests
{
    [Fact]
    public void Set_then_get_returns_the_path_for_the_same_configured_value()
    {
        var cache = new EsExePathCache();

        cache.Set(@"C:\Everything\es.exe", @"C:\bundled\es.exe");

        Assert.Equal(@"C:\bundled\es.exe", cache.Get(@"C:\Everything\es.exe"));
    }

    [Fact]
    public void Changed_configured_value_is_a_miss()
    {
        var cache = new EsExePathCache();
        cache.Set(@"C:\first", @"C:\bundled\es.exe");

        Assert.Null(cache.Get(@"C:\second"));
        Assert.Null(cache.Get(null));
    }

    [Fact]
    public void Key_comparison_is_case_insensitive()
    {
        var cache = new EsExePathCache();
        cache.Set(@"C:\Everything", @"C:\bundled\es.exe");

        Assert.Equal(@"C:\bundled\es.exe", cache.Get(@"c:\everything"));
    }

    [Fact]
    public void A_failed_lookup_is_not_cached()
    {
        var cache = new EsExePathCache();
        cache.Set(@"C:\Everything", null);

        Assert.Null(cache.Get(@"C:\Everything"));
    }

    [Fact]
    public void Clear_removes_the_entry()
    {
        var cache = new EsExePathCache();
        cache.Set(null, @"C:\bundled\es.exe");
        Assert.NotNull(cache.Get(null));

        cache.Clear();

        Assert.Null(cache.Get(null));
    }
}

public class FindEsExeCacheTests
{
    [Fact]
    public void FindEsExe_invalidates_when_the_configured_path_changes_and_when_the_file_disappears()
    {
        var root = Path.Combine(Path.GetTempPath(), "ep-es-cache-" + Guid.NewGuid().ToString("N"));
        var dirA = Path.Combine(root, "a");
        var dirB = Path.Combine(root, "b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        var esA = Path.Combine(dirA, "es.exe");
        var esB = Path.Combine(dirB, "es.exe");
        File.WriteAllText(esA, "stub");
        File.WriteAllText(esB, "stub");

        try
        {
            Assert.Equal(esA, EverythingSearch.FindEsExe(configured: esA));
            Assert.Equal(esB, EverythingSearch.FindEsExe(configured: esB));

            // Delete the cached path of B: the stale cache entry must not be served.
            File.Delete(esB);
            var afterDelete = EverythingSearch.FindEsExe(configured: esB);
            Assert.NotEqual(esB, afterDelete);

            // A is still valid and still served (its own cache entry, independent of B).
            Assert.Equal(esA, EverythingSearch.FindEsExe(configured: esA));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindEsExe_accepts_a_configured_directory_after_a_previous_file_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "ep-es-cache-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "Everything");
        Directory.CreateDirectory(directory);
        var es = Path.Combine(directory, "es.exe");
        File.WriteAllText(es, "stub");
        var other = Path.Combine(root, "other", "es.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(other, "stub");

        try
        {
            Assert.Equal(other, EverythingSearch.FindEsExe(configured: other));
            Assert.Equal(es, EverythingSearch.FindEsExe(configured: directory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
