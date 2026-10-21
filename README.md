# EverythingPal

Everything 搜索扩展 for [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (CmdPal).

An Everything search extension for Command Palette, built as a **managed-only** CmdPal extension:
it never ships or P/Invokes `Everything64.dll`; it queries Everything through the official
[`es.exe`](https://www.voidtools.com/support/everything/command_line_interface/) CLI and parses its
JSON export.

**The MSIX bundles the MIT-licensed `es.exe`** (from <https://github.com/voidtools/ES>), so users
only need Everything itself installed and running — no separate ES download or PATH setup.

> Status: **published on the Microsoft Store** (2026-10-20) — live search page, home-search fallback,
> real shell file-type icons, details pane, bilingual (zh-Hans / en) UI and a launch diagnostics
> dialog. Install it from
> [apps.microsoft.com/detail/9NFD76MCDRJZ](https://apps.microsoft.com/detail/9NFD76MCDRJZ); the
> source builds a self-contained x64+ARM64 MSIX bundle (see `docs/store/packaging.md`).

## Features

- **Live search page**: type a query, results stream in from Everything (150 ms debounce).
- **Home-search fallback**: type a name on the Command Palette home screen and the best result
  appears directly (default 1 row, 0–5 in settings — each row is one CmdPal fallback command, so
  N rows means N switches under Fallback commands). Enter opens the item; the context menu can
  reveal it in Explorer, copy the path, or locate it in Everything.
- **Real paths, not shortcuts**: results are the actual files/folders (drive roots such as
  `D:\wantai` included), unlike Recent-items shortcuts.
- **Real file-type icons**: each result shows the shell's registered icon for its type (folder,
  image, archive, executable, ...) instead of a generic page/folder glyph. Icons are resolved per
  extension and cached, so searching stays fast; no thumbnail generation on the search path.
- **Clear no-result / error states**: an empty query result says "no matching results"; if Everything
  is not running the fallback row and the search page both offer *Start Everything* / *Download
  Everything* instead of failing silently.
- **Bilingual UI**: Simplified Chinese and English strings follow the Windows display language
  (`EVERYTHINGPAL_LANG` can force a language for testing); the Store listing is bilingual too.
- **Launch diagnostics**: launching the installed package from the Start menu (no arguments) opens
  a small dialog that explains this is a Command Palette extension, checks PowerToys / Everything /
  the bundled `es.exe`, and can open Command Palette — it is not a dead tile. `--about` /
  `--diagnostics` show the same dialog.
- **Details pane**: size, modified time and clickable full path for each result.
- **Actionable errors**: if Everything is not running you get a *Start Everything* action; if the
  bundled `es.exe` is missing you are told to reinstall; timeouts explain that the index may still be
  loading.
- **Bundled, MIT-licensed ES client**: zero native SDK dependencies, works with Everything 1.4/1.5.
- **Settings**: result count, sort order, fallback rows, optional `es.exe` override.

## Requirements

- Windows 11, or Windows 10 version 2004 (build 19041) or later, on x64 or ARM64
- PowerToys with Command Palette (`Microsoft.CommandPalette` 0.13+)
- **Everything 1.4 or 1.5a installed and running** (the extension only queries it)
- For development: .NET 10 SDK, Windows SDK 10.0.26100 (MSIX packaging/signing), Developer Mode

## Install

### From the Microsoft Store (recommended)

[**EverythingPal on the Microsoft Store**](https://apps.microsoft.com/detail/9NFD76MCDRJZ) — Store ID
`9NFD76MCDRJZ`. Windows 11, or Windows 10 2004+; x64 or ARM64. The Store handles signing and updates.

### From a release

Download `EverythingPal_<version>_x64_arm64.msixbundle` (or an architecture-specific `.msix`) from
the [releases page](https://github.com/czz404/EverythingPal/releases).

- Store submission packages are **unsigned**: the Microsoft Store re-signs them, and for local
  testing you must sign them with your own certificate first (see below).
- A signed sideload build installs normally: `Add-AppxPackage .\EverythingPal_..._x64.msix`. The
  signing certificate must be trusted on the machine.

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

- `.github/workflows/ci.yml` — on every push/PR: elevated unit tests (Windows) + `scripts/pack-store.ps1`
  (self-contained x64 and ARM64 publish, per-architecture MSIX, unsigned `.msixbundle`) with the
  packages uploaded as artifacts.
- `.github/workflows/release.yml` — on `v*` tags (or manual dispatch):
  1. resolves the package version from the tag,
  2. `scripts/pack-store.ps1` publishes **self-contained** x64 and ARM64 builds (the .NET runtime is
     inside the package, so clean Store machines do not need a .NET installation), downloads the
     latest `es.exe` for both architectures, stages the layouts and bundles `Tools\es.exe`,
     `THIRD-PARTY-NOTICES.md` and `LICENSE`,
  3. optionally signs the bundle when signing secrets are configured,
  4. creates a **draft GitHub release** with the unsigned `.msixbundle` + per-architecture `.msix`.

### Store packaging

`pwsh -File scripts/pack-store.ps1` produces an **unsigned** x64+ARM64 `.msixbundle` for Partner
Center. Partner Center identity values (Identity Name / Publisher `CN=...` / PublisherDisplayName)
are passed as parameters and injected only into the staged manifest, never into the source
`Package.appxmanifest`. The complete Partner Center checklist (identity values, version rules,
unsigned submission, `.msixupload`, screenshots/listing assets, certification notes) is in
[`docs/store/`](docs/store/).

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
| `everythingPal.FallbackRows` | `1` | Home-search inline rows (0–5). Each row is a separate CmdPal fallback command (one row = one command = one switch under **Fallback commands**); enable the rows you want and tick *Include in the global result* (third-party fallbacks default to *not* included). Changing the count requires reloading the Command Palette extensions. |

## Project layout

```
src/EverythingPal/                 CmdPal extension (MSIX, COM server, UI pages, i18n, diagnostics)
src/EverythingPal.Core/            pure search logic (no WinRT): ES arguments, JSON parsing,
                                   ranking, query normalization, es.exe/Everything discovery — unit tested
tests/EverythingPal.Core.Tests/    67 xUnit tests (incl. resx parity + discovery)
scripts/deploy.ps1                 self-contained publish -> layout -> makeappx -> signtool -> install
scripts/pack-store.ps1             self-contained x64+ARM64 publish -> MSIX -> unsigned Store .msixbundle
scripts/verify-build.ps1           independent release verification (tests, bundle, headless search)
scripts/capture-screenshots.ps1    automated Store screenshots (CmdPal window, >=1366x768)
docs/store/                        Partner Center listing, privacy policy, certification notes, price
docs/research/                     CmdPal API + Store requirement research
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
- **Empty results**: ES is called with `-no-result-error`, so a successful run with an empty export
  means "no matches" and is mapped to the UI's no-results state (not an empty list).
- **Icons**: the shell icon for a file type is extracted once per extension with
  `SHGetFileInfo(SHGFI_USEFILEATTRIBUTES)` on a synthetic name, rendered to a small PNG and cached;
  no disk/thumbnail work happens per result. A missing shell icon falls back to a Segoe glyph.
- **Localization**: user-visible strings live in `Resources/Strings.resx` (en) and
  `Strings.zh-Hans.resx`, resolved through `Localization/Strings.cs` with
  `CultureInfo.CurrentUICulture`; `EVERYTHINGPAL_LANG` forces a culture for tests/screenshots.
- **Packaging**: the MSIX is published **self-contained** (the .NET runtime ships inside), because a
  Store user is not guaranteed to have the matching .NET runtime installed; `PublishTrimmed` keeps
  the per-architecture package ~11 MB.
- **Launch behavior**: the Start-menu tile is not a standalone UI — with no arguments (or
  `--about`/`--diagnostics`) the process shows a Win32 diagnostics dialog instead of exiting silently,
  which also satisfies the Store policy that an incompatible device must be told what is missing.

## Roadmap

- [x] live search page, multi-row fallback, details pane, settings
- [x] bundled `es.exe` (auto-updated from voidtools/ES at build time)
- [x] unit tests + CI + tag-driven release workflow
- [x] real shell file-type icons, bilingual UI, launch diagnostics, self-contained x64+ARM64 bundle
- [x] Store packaging scripts + Partner Center listing/privacy/certification materials (`docs/store/`)
- [ ] reserve the Partner Center product and inject the real Store identity (publish/build is ready;
      Store build version must be `1.0.0.0`-style with a non-zero major)
- [ ] publish to Microsoft Store, then submit to the CmdPal community Gallery
      (`microsoft/CmdPal-Extensions`) so it is discoverable in Command Palette's built-in Gallery;
      WinGet publishing is optional but recommended

## License

MIT — see [LICENSE](LICENSE). Third-party notices in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
