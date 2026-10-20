// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace EverythingPal.Core.Tests;

/// <summary>
/// Keeps the two .resx files (English neutral + zh-Hans) and the typed
/// <c>Localization\Strings.cs</c> accessors in sync. The extension project itself is not referenced
/// by this test project (it is a WinRT WinExe), so the files are copied next to the test assembly as
/// plain data (see EverythingPal.Core.Tests.csproj).
/// </summary>
public class LocalizationResourceTests
{
    private static string LocalizationDirectory => Path.Combine(AppContext.BaseDirectory, "Localization");

    private static string EnglishResx => Path.Combine(LocalizationDirectory, "Strings.resx");

    private static string ChineseResx => Path.Combine(LocalizationDirectory, "Strings.zh-Hans.resx");

    private static string AccessorSource => Path.Combine(LocalizationDirectory, "Strings.cs");

    // Matches both Get("Key") and Format("Key", ...) accessor patterns in Localization\Strings.cs.
    private static readonly Regex AccessorCallPattern = new(@"\b(?:Get|Format)\(""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void English_and_Chinese_resources_have_identical_key_sets()
    {
        var english = ReadResources(EnglishResx);
        var chinese = ReadResources(ChineseResx);

        Assert.Equal(english.Keys.OrderBy(k => k, StringComparer.Ordinal), chinese.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void No_resource_value_is_empty()
    {
        foreach (var (key, value) in ReadResources(EnglishResx).Concat(ReadResources(ChineseResx)))
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"Resource '{key}' is empty.");
        }
    }

    [Fact]
    public void Every_resource_key_has_a_typed_accessor()
    {
        var english = ReadResources(EnglishResx);
        var referenced = ReferencedAccessorKeys();

        var missingAccessors = english.Keys.Except(referenced, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var unknownKeys = referenced.Except(english.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        Assert.True(missingAccessors.Length == 0, "Keys without a typed accessor: " + string.Join(", ", missingAccessors));
        Assert.True(unknownKeys.Length == 0, "Accessors for unknown resource keys: " + string.Join(", ", unknownKeys));
        Assert.Equal(english.Count, referenced.Count);
    }

    [Fact]
    public void Every_accessor_key_exists_in_both_resources()
    {
        var chinese = ReadResources(ChineseResx);
        var referenced = ReferencedAccessorKeys();

        var missing = referenced.Where(key => !chinese.ContainsKey(key)).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0, "Accessors missing from Strings.zh-Hans.resx: " + string.Join(", ", missing));
    }

    [Fact]
    public void Chinese_translation_differs_from_english_for_user_facing_text()
    {
        // Guard against accidentally copying the English file to the zh-Hans slot. Keys whose
        // Chinese wording legitimately equals English (none right now) can be added here.
        var english = ReadResources(EnglishResx);
        var chinese = ReadResources(ChineseResx);

        var identical = english
            .Where(pair => string.Equals(pair.Value, chinese[pair.Key], StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

        Assert.True(identical.Length == 0, "Untranslated zh-Hans values: " + string.Join(", ", identical));
    }

    private static Dictionary<string, string> ReadResources(string path)
    {
        Assert.True(File.Exists(path), $"Resource file not found: {path}");

        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in XDocument.Load(path).Root!.Elements("data"))
        {
            var key = data.Attribute("name")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(key), $"Unnamed resource in {path}");

            var value = data.Element("value")?.Value ?? string.Empty;
            if (!resources.TryAdd(key!, value))
            {
                Assert.Fail($"Duplicate resource key '{key}' in {path}");
            }
        }

        Assert.NotEmpty(resources);
        return resources;
    }

    private static HashSet<string> ReferencedAccessorKeys()
    {
        Assert.True(File.Exists(AccessorSource), $"Strings helper source not found: {AccessorSource}");

        var matches = AccessorCallPattern.Matches(File.ReadAllText(AccessorSource));
        Assert.NotEmpty(matches);
        return [.. matches.Select(m => m.Groups[1].Value)];
    }
}
