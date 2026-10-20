// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Globalization;
using EverythingPal.Core;
using EverythingPal.Localization;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal.Services;

/// <summary>
/// User settings, exposed through the Command Palette settings page.
/// Stored at %LOCALAPPDATA%\EverythingPal\settings.json.
/// </summary>
internal sealed class EverythingPalSettings : JsonSettingsManager
{
    private const string Namespace = "everythingPal";
    private const int DefaultMaxResults = 50;

    private static readonly List<ChoiceSetSetting.Choice> SortChoices =
    [
        new(Strings.SettingsSortNameAscending, "name-ascending"),
        new(Strings.SettingsSortDateModifiedDescending, "date-modified-descending"),
        new(Strings.SettingsSortDateModifiedAscending, "date-modified-ascending"),
        new(Strings.SettingsSortPath, "path"),
        new(Strings.SettingsSortRunCountDescending, "run-count-descending"),
    ];

    private readonly TextSetting _esExePath = new(
        Namespaced(nameof(EsExePath)),
        Strings.SettingsEsExePathLabel,
        Strings.SettingsEsExePathDescription,
        string.Empty);

    private readonly TextSetting _maxResults = new(
        Namespaced(nameof(MaxResults)),
        Strings.SettingsMaxResultsLabel,
        Strings.SettingsMaxResultsDescription,
        DefaultMaxResults.ToString(CultureInfo.InvariantCulture));

    private readonly TextSetting _fallbackRows = new(
        Namespaced(nameof(FallbackRows)),
        Strings.SettingsFallbackRowsLabel,
        Strings.SettingsFallbackRowsDescription,
        "3");

    private readonly ChoiceSetSetting _sort = new(
        Namespaced(nameof(Sort)),
        Strings.SettingsSortLabel,
        Strings.SettingsSortDescription,
        SortChoices);

    internal static readonly EverythingPalSettings Instance = new();

    private EverythingPalSettings()
    {
        var directory = Utilities.BaseSettingsPath("EverythingPal");
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "settings.json");

        LoadSettings();

        Settings.Add(_esExePath);
        Settings.Add(_maxResults);
        Settings.Add(_sort);
        Settings.Add(_fallbackRows);

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    internal string EsExePath => _esExePath.Value?.Trim() ?? string.Empty;

    internal int MaxResults => int.TryParse(_maxResults.Value, out var value) ? Math.Clamp(value, 1, 500) : DefaultMaxResults;
    internal int FallbackRows => int.TryParse(_fallbackRows.Value, out var value) ? Math.Clamp(value, 0, 5) : 3;


    internal string Sort => string.IsNullOrWhiteSpace(_sort.Value) ? "name-ascending" : _sort.Value;

    internal SearchOptions ToSearchOptions(int? maxResultsOverride = null) => new()
    {
        EsExePath = string.IsNullOrWhiteSpace(EsExePath) ? null : EsExePath,
        MaxResults = Math.Clamp(maxResultsOverride ?? MaxResults, 1, 500),
        Sort = Sort,
    };
}


