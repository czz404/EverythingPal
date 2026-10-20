# EverythingPal

Everything 搜索扩展 for [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (CmdPal).

An Everything search extension for Command Palette, built as a **managed-only** CmdPal extension:
it never ships or P/Invokes `Everything64.dll`; it queries Everything through the official
[`es.exe`](https://www.voidtools.com/support/everything/command_line_interface/) CLI and parses its
JSON export. This keeps the extension free of native binaries and works with any Everything 1.4 / 1.5
installation that ships ES.

> Status: **working preview** — live search page, home-search fallback and a settings page are in place.

## Features

- **Live search page**: type a query, results stream in from Everything (150 ms debounce).
- **Home-search fallback**: type a name on the Command Palette home screen and the best Everything
  match appears directly; Enter opens it, the context menu can reveal it in Explorer, copy the path,
  or locate it in the Everything GUI.
- **Real paths, not shortcuts**: results are the actual files/folders (drive roots such as
  `D:\wantai` included), unlike Recent-items shortcuts.
- **Managed only**: no `Everything64.dll` redistribution; only `es.exe` is required.
- **Settings**: `es.exe` path, result count, sort order (see below).

## Requirements

- Windows 11 (x64), PowerToys with Command Palette (`Microsoft.CommandPalette` 0.13+)
- Everything 1.5a (or 1.4) installed and running
- `es.exe` (ships with Everything; also available via `winget install voidtools.Everything.Cli`)
- For development: .NET 10 SDK, Windows SDK 10.0.26100 (MSIX packaging/signing), Developer Mode

## Install (development build)

```powershell
pwsh -File scripts/deploy.ps1 -RestartCmdPal
```

The script builds, packs, signs (self-signed `CN=czz404` dev certificate, trusted once in
`LocalMachine\TrustedPeople`) and installs the MSIX. Re-run after every change, then run `Reload` →
**Reload Command Palette extensions** inside Command Palette.

> **Why not `Add-AppxPackage -Register`?** A loose-registered package cannot host the packaged COM
> server: `CoResumeClassObjects` fails with `0x80004015 CO_E_WRONG_SERVER_IDENTITY`. A properly
> signed and installed MSIX does not have this problem.

## Settings

`%LOCALAPPDATA%\EverythingPal\settings.json` (also editable from the Command Palette settings page,
reachable from the `Everything 搜索` command's context menu):

| Key | Default | Meaning |
| --- | --- | --- |
| `everythingPal.EsExePath` | *(empty = auto)* | Path to `es.exe` (or its folder). Auto-detect checks `%LOCALAPPDATA%\Microsoft\WindowsApps`, the Everything install dirs and `PATH`. |
| `everythingPal.MaxResults` | `50` | Result limit passed to ES (`-n`), clamped to 1–500. |
| `everythingPal.Sort` | `name-ascending` | ES `-sort` value: `name-ascending`, `date-modified-descending`, `path`, `run-count-descending`, ... |

> Command Palette only includes **built-in** fallbacks in the home search by default. For a
> third-party extension the fallback has to be enabled once in Command Palette's fallback settings
> (`IncludeInGlobalResults`).

## Project layout

```
src/EverythingPal/
  Program.cs                        COM/WinRT server host (-RegisterProcessAsComServer; --search-test helper)
  EverythingPalExtension.cs         IExtension implementation (GUID must match the manifest CLSID)
  EverythingPalCommandsProvider.cs  top-level command + fallback registration
  Pages/EverythingPalPage.cs        DynamicListPage with debounced es.exe search
  Services/EverythingSearch.cs      es.exe invocation, UTF-8 JSON parsing, ranking
  Services/EverythingPalSettings.cs settings storage (JsonSettingsManager)
  Commands/ItemCommands.cs          open / reveal / copy path / locate in Everything
  Commands/EverythingFallbackItem.cs home-search fallback row
scripts/deploy.ps1                  build -> layout -> makeappx -> signtool -> Add-AppxPackage
```

## Design notes

- **es.exe invocation**: `-export-json <temp> -n <limit> -sort <sort> -date-format 1 -size
  -date-modified -attributes -no-result-error -timeout 1500 -search "<query>"`.
  `-export-json` is important: ES writes **console code page** (GBK) bytes to stdout, but UTF-8 to
  the export file, so CJK paths survive.
- **Folders**: ES marks folders with a trailing separator in the JSON; that is the folder check.
- **Exit codes**: `9` = no results (`-no-result-error`), `8` = Everything not running, `4/6` =
  bad arguments (stdout then contains the help dump — never parse it).
- **Ranking**: ES has no relevance score, so results are re-ranked by exact name, prefix, stem
  match, substring; folders win ties.
- **Path handling**: links/actions always use the full path directly; never build URIs by
  concatenating `file:///` (drive-root paths such as `D:` throw `UriFormatException`).

## Roadmap

- [x] MSIX + COM registration, extension loads in CmdPal
- [x] `es.exe` live search, open/reveal/copy actions
- [x] home-search fallback
- [x] settings page
- [ ] unit tests for query normalization / ranking / root paths
- [ ] CI (windows-latest build + MSIX artifact)
- [ ] release packaging / optional Store or winget distribution

## License

MIT — see [LICENSE](LICENSE).
