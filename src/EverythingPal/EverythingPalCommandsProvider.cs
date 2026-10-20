// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using EverythingPal.Core;
using EverythingPal.Localization;
using EverythingPal.Services;

namespace EverythingPal;

public partial class EverythingPalCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private IFallbackCommandItem[] _fallbacks = [];
    private readonly EverythingPalPage _page = new();
    private EverythingFallbackGroup? _fallbackGroup;
    private int _fallbackRows = -1;

    public EverythingPalCommandsProvider()
    {
        DisplayName = Strings.AppDisplayName;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = EverythingPalSettings.Instance.Settings;

        _commands =
        [
            new CommandItem(_page)
            {
                Title = DisplayName,
                Subtitle = Strings.AppCommandSubtitle,
                MoreCommands = [new CommandContextItem(Settings.SettingsPage)],
            },
        ];

        RebuildFallbackGroup(EverythingPalSettings.Instance.FallbackRows);

        // Warm the shell icon cache in the background so the first searches (especially the
        // home-screen fallback rows, which the host only keeps for a short while) get cache hits.
        ShellIconProvider.WarmUpInBackground();

        // Row count is a user setting. FallbackCommands() below rebuilds the group when the count
        // differs, so the returned list is always current; CmdPal is expected to cache that list and
        // re-read it only on its own reload / fallback-settings message, so a changed row count may
        // not take effect on the home screen until the extension is reloaded (defensive rebuild,
        // harmless either way).
        EverythingPalSettings.Instance.Settings.SettingsChanged +=
            (_, _) => RebuildFallbackGroup(EverythingPalSettings.Instance.FallbackRows);
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands()
    {
        // Defensive: rebuild if the row-count setting changed since the host last queried us. The
        // host may keep using its cached list until its own reload, but whenever it asks again it
        // gets the current count.
        RebuildFallbackGroup(EverythingPalSettings.Instance.FallbackRows);
        return _fallbacks;
    }

    private void RebuildFallbackGroup(int rows)
    {
        rows = Math.Clamp(rows, 0, 5);
        if (rows == _fallbackRows)
        {
            return;
        }

        _fallbackRows = rows;
        _fallbackGroup = rows > 0 ? new EverythingFallbackGroup(rows) : null;
        _fallbacks = _fallbackGroup?.Items ?? [];

        // The previous group is intentionally left undisposed: Command Palette keeps using the
        // fallback items it already fetched, and disposing their group would break later
        // UpdateQuery calls (blank rows) until the extension is reloaded.
    }
}
