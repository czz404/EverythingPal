// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;

namespace EverythingPal.Localization;

/// <summary>
/// User-visible strings for the extension. The neutral (English) resources live in
/// <c>Resources\Strings.resx</c>; <c>Resources\Strings.zh-Hans.resx</c> is the Simplified Chinese
/// translation. Lookup uses <see cref="CultureInfo.CurrentUICulture"/> (so a zh-CN system falls back
/// to zh-Hans automatically) and can be forced through the <c>EVERYTHINGPAL_LANG</c> environment
/// variable (<c>en</c>, <c>zh-Hans</c>, <c>zh-CN</c>, or any culture name) for tests/screenshots.
/// A missing key returns the key itself instead of throwing.
///
/// Technical diagnostics (Core search errors, log lines, the manifest metadata) intentionally stay
/// in English.
/// </summary>
internal static class Strings
{
    private const string ResourceBaseName = "EverythingPal.Resources.Strings";

    private static readonly ResourceManager Manager = new(ResourceBaseName, typeof(Strings).Assembly);

    internal static string AppDisplayName => Get("App.DisplayName");

    internal static string AppCommandSubtitle => Get("App.CommandSubtitle");

    internal static string PageTitle => Get("Page.Title");

    internal static string PageName => Get("Page.Name");

    internal static string PagePlaceholderText => Get("Page.PlaceholderText");

    internal static string SearchFailedTitle => Get("Page.SearchFailedTitle");

    internal static string NoResultsTitle => Get("Status.NoResultsTitle");

    internal static string NoResultsSubtitle => Get("Status.NoResultsSubtitle");

    internal static string EverythingNotRunningTitle => Get("Status.EverythingNotRunningTitle");

    internal static string EverythingNotRunningSubtitle => Get("Status.EverythingNotRunningSubtitle");

    internal static string EsExeNotFoundTitle => Get("Status.EsExeNotFoundTitle");

    internal static string EsExeNotFoundSubtitle => Get("Status.EsExeNotFoundSubtitle");

    internal static string TimeoutTitle => Get("Status.TimeoutTitle");

    internal static string TimeoutSubtitle => Get("Status.TimeoutSubtitle");

    internal static string QueryFailedTitle => Get("Status.QueryFailedTitle");

    internal static string UnknownError => Get("Status.UnknownError");

    internal static string FallbackTimeoutTitle => Get("Fallback.TimeoutTitle");

    internal static string FallbackQueryFailedTitle => Get("Fallback.QueryFailedTitle");

    internal static string FallbackRetryHint => Get("Fallback.RetryHint");

    internal static string FallbackEverythingNotRunningSubtitle => Get("Fallback.EverythingNotRunningSubtitle");

    internal static string FallbackEsExeNotFoundSubtitle => Get("Fallback.EsExeNotFoundSubtitle");

    internal static string OpenAction => Get("Action.Open");

    internal static string OpenFolderAction => Get("Action.OpenFolder");

    internal static string RevealInExplorerAction => Get("Action.RevealInExplorer");

    internal static string CopyPathAction => Get("Action.CopyPath");

    internal static string CopyPathToast => Get("Action.CopyPathToast");

    internal static string CopyFailedToast => Get("Action.CopyFailedToast");

    internal static string OpenItemFailedToast => Get("Action.OpenItemFailedToast");

    internal static string RevealInExplorerFailedToast => Get("Action.RevealInExplorerFailedToast");

    internal static string LocateInEverythingAction => Get("Action.LocateInEverything");

    internal static string LocateInEverythingFailedToast => Get("Action.LocateInEverythingFailedToast");

    internal static string EverythingExeNotFoundToast => Get("Action.EverythingExeNotFoundToast");

    internal static string StartEverythingAction => Get("Action.StartEverything");

    internal static string StartEverythingToast => Get("Action.StartEverythingToast");

    internal static string StartEverythingFailedToast => Get("Action.StartEverythingFailedToast");

    internal static string EverythingNotInstalledToast => Get("Action.EverythingNotInstalledToast");

    internal static string DownloadEverythingAction => Get("Action.DownloadEverything");

    internal static string DetailsName => Get("Details.Name");

    internal static string DetailsFullPath => Get("Details.FullPath");

    internal static string DetailsType => Get("Details.Type");

    internal static string DetailsFolder => Get("Details.Folder");

    internal static string DetailsFile => Get("Details.File");

    internal static string DetailsSize => Get("Details.Size");

    internal static string DetailsModified => Get("Details.Modified");

    internal static string SettingsEsExePathLabel => Get("Settings.EsExePathLabel");

    internal static string SettingsEsExePathDescription => Get("Settings.EsExePathDescription");

    internal static string SettingsMaxResultsLabel => Get("Settings.MaxResultsLabel");

    internal static string SettingsMaxResultsDescription => Get("Settings.MaxResultsDescription");

    internal static string SettingsFallbackRowsLabel => Get("Settings.FallbackRowsLabel");

    internal static string SettingsFallbackRowsDescription => Get("Settings.FallbackRowsDescription");

    internal static string SettingsSortLabel => Get("Settings.SortLabel");

    internal static string SettingsSortDescription => Get("Settings.SortDescription");

    internal static string SettingsSortNameAscending => Get("Settings.SortNameAscending");

    internal static string SettingsSortDateModifiedDescending => Get("Settings.SortDateModifiedDescending");

    internal static string SettingsSortDateModifiedAscending => Get("Settings.SortDateModifiedAscending");

    internal static string SettingsSortPath => Get("Settings.SortPath");

    internal static string SettingsSortRunCountDescending => Get("Settings.SortRunCountDescending");

    // Launch diagnostics dialog (no-argument / --about / --diagnostics launch). MessageBox button
    // captions stay system-localized; only the dialog text goes through the resources.

    internal static string LaunchDialogCaption => Get("LaunchDialog.Caption");

    internal static string LaunchDialogOpenCommandPaletteQuestion => Get("LaunchDialog.OpenCommandPaletteQuestion");

    internal static string LaunchDialogInstallPowerToysQuestion => Get("LaunchDialog.InstallPowerToysQuestion");

    internal static string LaunchDialogOpenCommandPaletteFailed => Get("LaunchDialog.OpenCommandPaletteFailed");

    internal static string LaunchDialogDiagnosticsFailedPrefix => Get("LaunchDialog.DiagnosticsFailedPrefix");

    internal static string LaunchDialogLogLabel => Get("LaunchDialog.LogLabel");

    internal static string LaunchDialogIntro => Get("LaunchDialog.Intro");

    internal static string LaunchDialogUsageHeader => Get("LaunchDialog.UsageHeader");

    internal static string LaunchDialogUsageHotkey => Get("LaunchDialog.UsageHotkey");

    internal static string LaunchDialogUsageRequiresEverything => Get("LaunchDialog.UsageRequiresEverything");

    internal static string LaunchDialogUsageFallbackRows => Get("LaunchDialog.UsageFallbackRows");

    internal static string LaunchDialogDiagnosticsHeader => Get("LaunchDialog.DiagnosticsHeader");

    internal static string LaunchDialogEsExePresent => Get("LaunchDialog.EsExePresent");

    internal static string LaunchDialogEsExeMissing => Get("LaunchDialog.EsExeMissing");

    internal static string LaunchDialogPrivacyLine => Get("LaunchDialog.PrivacyLine");

    internal static string LaunchDialogCommandPaletteInstalled => Get("LaunchDialog.CommandPaletteInstalled");

    internal static string LaunchDialogCommandPaletteNotInstalled => Get("LaunchDialog.CommandPaletteNotInstalled");

    internal static string LaunchDialogCommandPaletteUnknown => Get("LaunchDialog.CommandPaletteUnknown");

    internal static string LaunchDialogEverythingRunningUnknownLocation => Get("LaunchDialog.EverythingRunningUnknownLocation");

    internal static string LaunchDialogEverythingNotDetected => Get("LaunchDialog.EverythingNotDetected");

    internal static string LaunchDialogEverythingMissingWarning => Get("LaunchDialog.EverythingMissingWarning");

    internal static string LaunchDialogOpenEverythingDownloadQuestion => Get("LaunchDialog.OpenEverythingDownloadQuestion");

    internal static string LaunchDialogEverythingNotRunningWarning => Get("LaunchDialog.EverythingNotRunningWarning");

    internal static string LaunchDialogStartEverythingQuestion => Get("LaunchDialog.StartEverythingQuestion");

    internal static string LaunchDialogBundledEsExeMissing => Get("LaunchDialog.BundledEsExeMissing");

    internal static string LaunchDialogStartEverythingFailed => Get("LaunchDialog.StartEverythingFailed");

    internal static string LaunchDialogOpenBrowserFailedPrefix => Get("LaunchDialog.OpenBrowserFailedPrefix");

    internal static string LaunchDialogUnknownDevelopmentVersion => Get("LaunchDialog.UnknownDevelopmentVersion");

    internal static string FormatLaunchDialogVersion(string version) => Format("LaunchDialog.VersionFormat", version);

    internal static string FormatLaunchDialogCommandPaletteLine(string description) =>
        Format("LaunchDialog.CommandPaletteLineFormat", description);

    internal static string FormatLaunchDialogEverythingLine(string description) =>
        Format("LaunchDialog.EverythingLineFormat", description);

    internal static string FormatLaunchDialogEsExeLine(string status) => Format("LaunchDialog.EsExeLineFormat", status);

    internal static string FormatLaunchDialogLogLine(string logPath) => Format("LaunchDialog.LogLineFormat", logPath);

    internal static string FormatLaunchDialogEverythingInstalledRunning(string path) =>
        Format("LaunchDialog.EverythingInstalledRunningFormat", path);

    internal static string FormatLaunchDialogEverythingInstalledNotRunning(string path) =>
        Format("LaunchDialog.EverythingInstalledNotRunningFormat", path);

    internal static string FormatLaunchDialogDevelopmentVersion(string version) =>
        Format("LaunchDialog.DevelopmentVersionFormat", version);

    /// <summary>"(N results)" / "（共 N 个结果）" for the first inline fallback row.</summary>
    [SuppressMessage("Performance", "CA1863", Justification = "The format string is a localized resource resolved per call; these are low-frequency UI paths, not hot loops.")]
    internal static string FormatResultCount(int count) =>
        string.Format(CultureInfo.CurrentCulture, Get("Fallback.ResultCountFormat"), count);

    /// <summary>"Everything Search · home result 1/3" / "Everything 搜索 · 主页结果 1/3" for the CmdPal fallback list.</summary>
    [SuppressMessage("Performance", "CA1863", Justification = "The format string is a localized resource resolved per call; these are low-frequency UI paths, not hot loops.")]
    internal static string FormatFallbackRowTitle(int rowNumber, int totalRows) =>
        totalRows <= 1
            ? Get("Fallback.RowTitleSingle")
            : string.Format(CultureInfo.CurrentCulture, Get("Fallback.RowTitleFormat"), rowNumber, totalRows);

    /// <summary>Formats one localized resource; used by the typed format helpers above.</summary>
    [SuppressMessage("Performance", "CA1863", Justification = "The format string is a localized resource resolved per call; these are low-frequency UI paths, not hot loops.")]
    internal static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    internal static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Manager.GetString(key, ResolveCulture()) ?? key;
    }

    /// <summary>
    /// Culture used for lookup. <c>EVERYTHINGPAL_LANG</c> overrides the UI culture so screenshots and
    /// tests can pick a language; unknown values fall back to the UI culture.
    /// </summary>
    internal static CultureInfo ResolveCulture()
    {
        var requested = Environment.GetEnvironmentVariable("EVERYTHINGPAL_LANG")?.Trim();
        if (string.IsNullOrEmpty(requested))
        {
            return CultureInfo.CurrentUICulture;
        }

        if (requested.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            // zh-CN / zh-Hans resolve to the same satellite; zh-Hant is not translated and falls back
            // to the neutral English resources rather than showing Simplified Chinese.
            return requested.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                   requested.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                   requested.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("en")
                : CultureInfo.GetCultureInfo("zh-Hans");
        }

        if (requested.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("en");
        }

        try
        {
            return CultureInfo.GetCultureInfo(requested);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
    }
}
