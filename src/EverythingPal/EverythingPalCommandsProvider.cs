// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingPal;

public partial class EverythingPalCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbacks;
    private readonly EverythingPalPage _page = new();

    public EverythingPalCommandsProvider()
    {
        DisplayName = "Everything 搜索";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands =
        [
            new CommandItem(_page)
            {
                Title = DisplayName,
                Subtitle = "用 Everything 搜索文件和文件夹",
            },
        ];
        _fallbacks = [new EverythingFallbackItem()];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbacks;
}
