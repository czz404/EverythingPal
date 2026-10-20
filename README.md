# EverythingPal

Everything 搜索扩展 for [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (CmdPal).

An Everything search extension for Command Palette, built as a **managed-only** CmdPal extension:
it never ships or P/Invokes `Everything64.dll`; it queries Everything through the official
[`es.exe`](https://www.voidtools.com/support/everything/command_line_interface/) CLI and parses its
JSON export.

**The MSIX bundles the MIT-licensed `es.exe`** (from <https://github.com/voidtools/ES>), so users
only need Everything itself installed and running — no separate ES download or PATH setup.

> Status: **working preview** — live search page, multi-row home-search fallback, details pane and
> settings page are in place; CI/CD builds signed-or-unsigned MSIX for x64 and ARM64.

## Features

- **Live search page**: type a query, results stream in from Everything (150 ms debounce).
- **Home-search fallback with multiple rows**: type a name on the Command Palette home screen and the
  top results appear directly (default 3 rows, 0–5 in settings). Enter opens the item; the context
  menu can reveal it in Explorer, copy the path, or locate it in Everything.
- **Real paths, not shortcuts**: results are the actual files/folders (drive roots such as
  `D:\wantai` included), unlike Recent-items shortcuts.
- **Details pane**: size, modified time and clickable full path for each result.
- **Actionable errors**: if Everything is not running you get a *Start Everything* action; if the
  bundled `es.exe` is missing you are told to reinstall; timeouts explain that the index may still be
  loading.
- **Bundled, MIT-licensed ES client**: zero native SDK dependencies, works with Everything 1.4/1.5.
- **Settings**: result count, sort order, fallback rows, optional `es.exe` override.

## Requirements

- Windows 11 (x64 or ARM64), PowerToys with Command Palette (`Microsoft.CommandPalette` 0.13+)
- **Everything 1.4 or 1.5a installed and running** (the extension only queries it)
- For development: .NET 10 SDK, Windows SDK 10.0.26100 (MSIX packaging/signing), Developer Mode

## Install

### From a release

Download `EverythingPal_<version>_x64.msix` (or `_ARM64.msix`) from the
[releases page](https://github.com/czz404/EverythingPal/releases).

- If the release is **signed**, install it normally: `Add-AppxPackage .\EverythingPal_..._x64.msix`.
  The signing certificate must be trusted on the machine.
- If the release is **unsigned**, it can only be installed on a machine with Developer Mode enabled,
  and must be signed first for normal sideloading (see below).

### Build from source (developer loop)

```powershell
pwsh -File scripts/deploy.ps1 -RestartCmdPal
```

The script builds, downloads the latest `es.exe` from `voidtools/ES`, packs the MSIX, signs it with a
self-signed `CN=czz404` dev certificate (trusted once in `LocalMachine\TrustedPeople`) and installs it.
Re-run after every change, then run `Reload` → **Reload Command Palette extensions** in Command Palette.

> **Why not `Add-AppxPackage -Register`?** A loose-registered package cannot host the packaged COM
> server: `CoResumeClassObjects` fails with `0x80004015 CO_E_WRONG_SERVER_IDENTITY`. A signed and
> installed MSIX does not have this problem.

## CI / CD

- `.github/workflows/ci.yml` — on every push/PR: unit tests (Windows) + Release x64 build and an
  **unsigned MSIX artifact**.
- `.github/workflows/release.yml` — on `v*` tags (or manual dispatch):
  1. resolves the package version from the tag,
  2. downloads the **latest `es.exe`** for x64 and ARM64 from <https://github.com/voidtools/ES>,
  3. builds both architectures, bundles `Tools\es.exe` + `THIRD-PARTY-NOTICES.md`,
  4. signs the MSIX when signing secrets are configured,
  5. creates a **draft GitHub release** with the `.msix` assets.

### Optional signing

Add these repository secrets to produce signed releases:

| Secret | Meaning |
| --- | --- |
| `SIGNING_PFX_BASE64` | Base64 of the code-signing `.pfx` (publisher must match `Identity/@Publisher` = `CN=czz404`) |
| `SIGNING_PFX_PASSWORD` | Password of the `.pfx` |

Without them the workflow still produces installable-on-dev-mode (or locally signable) MSIX files.
For public distribution prefer a trusted certificate (e.g. Azure Trusted Signing, SignPath, or the
Microsoft Store, which signs on your behalf).

## Settings

`%LOCALAPPDATA%\EverythingPal\settings.json` (also editable from the Command Palette settings page,
reachable from the `Everything 搜索` command's context menu):

| Key | Default | Meaning |
| --- | --- | --- |
| `everythingPal.EsExePath` | *(empty = auto)* | `es.exe` path or directory. Auto-detect: explicit setting → bundled `Tools\es.exe` → Everything install dirs → PATH. |
| `everythingPal.MaxResults` | `50` | Result limit (`-n`), clamped to 1–500. |
| `everythingPal.Sort` | `name-ascending` | ES `-sort`: `name-ascending`, `date-modified-descending`, `path`, `run-count-descending`, ... |
| `everythingPal.FallbackRows` | `3` | Home-search inline rows (0–5; 0 disables the fallback). Requires enabling `IncludeInGlobalResults` in Command Palette's fallback settings (third-party fallbacks default to *not* included). |

## Project layout

```
src/EverythingPal/                 CmdPal extension (MSIX, COM server, UI pages)
src/EverythingPal.Core/            pure search logic (no WinRT): ES arguments, JSON parsing,
                                   ranking, query normalization, es.exe discovery — unit tested
tests/EverythingPal.Core.Tests/    28 xUnit tests
scripts/deploy.ps1                 build -> fetch ES -> layout -> makeappx -> signtool -> install
```

## Design notes

- **ES invocation**: `-export-json <temp> -n <limit> -sort <sort> -date-format 1 -size
  -date-modified -attributes -no-result-error -timeout 1500 -search "<query>"`.
  `-export-json` matters: ES writes the console code page (GBK) to stdout but UTF-8 to the export
  file, so CJK paths survive.
- **Folders**: ES marks folders with a trailing separator in the JSON.
- **Exit codes**: `9` = no results, `8` = Everything not running, `4/6` = bad arguments (stdout then
  contains the help dump — never parsed).
- **Ranking**: ES has no relevance score; results are re-ranked by exact name, prefix, stem match,
  substring, then folders and shorter names.
- **Path handling**: drive-root paths such as `D:` are handled explicitly; URIs are built with
  `new Uri(fullPath)`, never by concatenating `file:///`.

## Roadmap

- [x] live search page, multi-row fallback, details pane, settings
- [x] bundled `es.exe` (auto-updated from voidtools/ES at build time)
- [x] unit tests + CI + tag-driven release workflow
- [ ] code signing for public distribution (trusted certificate / Store)
- [ ] optional Store or winget publishing

## License

MIT — see [LICENSE](LICENSE). Third-party notices in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
