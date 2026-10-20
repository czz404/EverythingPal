// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Globalization;
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
        new("名称升序（默认）", "name-ascending"),
        new("修改时间倒序", "date-modified-descending"),
        new("修改时间升序", "date-modified-ascending"),
        new("路径", "path"),
        new("运行次数倒序", "run-count-descending"),
    ];

    private readonly TextSetting _esExePath = new(
        Namespaced(nameof(EsExePath)),
        "es.exe 路径",
        "留空则自动探测：%LOCALAPPDATA%\\Microsoft\\WindowsApps、Everything 安装目录、PATH",
        string.Empty);

    private readonly TextSetting _maxResults = new(
        Namespaced(nameof(MaxResults)),
        "结果条数上限",
        "1–500，默认 50。传给 es.exe 的 -n 参数。",
        DefaultMaxResults.ToString(CultureInfo.InvariantCulture));

    private readonly ChoiceSetSetting _sort = new(
        Namespaced(nameof(Sort)),
        "排序方式",
        "传给 es.exe 的 -sort 参数；插件会在返回后再按相关性微调顺序。",
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

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    internal string EsExePath => _esExePath.Value?.Trim() ?? string.Empty;

    internal int MaxResults => int.TryParse(_maxResults.Value, out var value) ? Math.Clamp(value, 1, 500) : DefaultMaxResults;

    internal string Sort => string.IsNullOrWhiteSpace(_sort.Value) ? "name-ascending" : _sort.Value;
}
