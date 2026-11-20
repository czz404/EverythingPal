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
    private const int DefaultFallbackRows = 1;

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
        DefaultFallbackRows.ToString(CultureInfo.InvariantCulture));

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

        // Register the settings BEFORE loading: Settings.Update() only visits the settings that are
        // already in the collection, so loading first (the previous order) silently dropped every
        // persisted value and left the defaults in place after a restart.
        Settings.Add(_esExePath);
        Settings.Add(_maxResults);
        Settings.Add(_sort);
        Settings.Add(_fallbackRows);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    internal string EsExePath => _esExePath.Value?.Trim() ?? string.Empty;

    internal int MaxResults => int.TryParse(_maxResults.Value, out var value) ? Math.Clamp(value, 1, 500) : DefaultMaxResults;
    internal int FallbackRows => int.TryParse(_fallbackRows.Value, out var value) ? Math.Clamp(value, 0, 5) : DefaultFallbackRows;


    internal string Sort => string.IsNullOrWhiteSpace(_sort.Value) ? SearchOptions.DefaultSort : _sort.Value;

    internal SearchOptions ToSearchOptions(int? maxResultsOverride = null) => new()
    {
        EsExePath = string.IsNullOrWhiteSpace(EsExePath) ? null : EsExePath,
        MaxResults = Math.Clamp(maxResultsOverride ?? MaxResults, 1, 500),
        Sort = Sort,
    };
}


