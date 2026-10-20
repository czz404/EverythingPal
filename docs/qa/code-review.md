# EverythingPal — Independent code review (QA)

**Reviewer:** qa-verify (independent QA; no product code changed by this review)
**Date:** 2026-10-20
**Baseline:** worktree at commit `805d2e9` (branch `main`), reviewed after the Lead declared src FROZEN.
**Scope:** full `git diff` of tracked files + all new source/test/script files in the worktree (27 tracked files changed, ~40 new files incl. assets). Excluded: PNG asset pixel content (verified by brand-materials/T4), marketing docs, and my own QA scripts (`scripts/verify-build.ps1`, `scripts/capture-screenshots.ps1`).
**Method:** read every changed source file and new file end-to-end, cross-checked against `docs/research/platform-and-store-research.md` (CmdPal fallback model, 200 ms fallback budget, Store policies), the installed CmdPal host behaviour (0.13.12862) and Microsoft MSIX/SignTool documentation. Findings are limited to concrete bugs, regressions, performance issues and Store/certification risks.

**Legend:** `HIGH` = should fix before Store submission / user-visible breakage; `MED` = concrete risk that should be fixed or consciously accepted; `LOW` = robustness/efficiency nit with a concrete failure mode; `VERIFY` = cannot be confirmed statically, must be checked on the installed 0.2.0.0 build.

---

## Findings

### F1 — RESOLVED (was HIGH Store risk): `unvirtualizedResources` + disabled write virtualization
`src/EverythingPal/Package.appxmanifest:28-29` (`desktop6:FileSystemWriteVirtualization=disabled`, `desktop6:RegistryWriteVirtualization=disabled`) and the `rescap:unvirtualizedResources` capability. Microsoft's own docs (quoted in `docs/research/platform-and-store-research.md` §D.10) say this capability is intended for games / apps with external location, "**not intended to be used for other scenarios**", and it triggers the restricted-capability justification flow. This is the single most likely certification pushback. Options: (a) keep and write a precise justification in Submission options; (b) test removing both properties + capability and verify logging / `%TEMP%` export from `es.exe` still works (packaged full-trust apps can write settings/log via LocalApplicationData without it). Decision is product-level; flagged so it is not forgotten during submission.

### F2 — WITHDRAWN (not a bug): `MinVersion` 10.0.19041.0 is intentional
Originally raised as a Store-compatibility concern (`Windows.Desktop MinVersion="10.0.19041.0"` at `src/EverythingPal/Package.appxmanifest:38`). The Lead verified that PowerToys Command Palette officially supports **Windows 10 2004 (build 19041)+ as well as Windows 11** (Microsoft Learn install requirements and the host package's own manifest), so 19041 matches the host baseline and is **not** a compatibility bug. README wording is being aligned to "Windows 11, or Windows 10 2004 (19041)+". **No action needed.**

### F3 — MED (bug): stale async search results can overwrite a newer query (page and fallback)
- Page: `src/EverythingPal/Pages/EverythingPalPage.cs:57-84`. After the cancellation token check at :65-66, the task does `_items = await BuildItemsAsync(outcome)` (:68) and then sets `IsLoading = false`, `RaiseItemsChanged` (:69-70) **without re-checking the token**. Since `BuildItemsAsync` now awaits shell-icon extraction, a superseded (old) query can finish after the new one and replace the list / clear the loading state of the newer query. Race window used to be microseconds; icon awaits widened it to milliseconds.
- Fallback: `src/EverythingPal/Commands/EverythingFallbackItem.cs:89-115`. `DistributeAsync` is called after the token check, but itself awaits icon extraction (`Task.WhenAll`) and then assigns `Title/Subtitle/Icon/Command` (:151-160) with no cancellation check. If the user clears or retypes the query while icons are resolving, rows repopulate with results for the old query (the group's `ClearAll()` has already run for the new query).
Fix: pass the `CancellationToken` into `BuildItemsAsync`/`DistributeAsync` (or capture the current `_cts`), and before every UI-visible mutation check `token.IsCancellationRequested` (and for the page, do not reset `IsLoading`/`RaiseItemsChanged` for a stale task).

### F4 — MED (perf/render): fallback row title is set only AFTER the await of shell-icon extraction
`src/EverythingPal/Commands/EverythingFallbackItem.cs:145-153`: `var icon = await ShellIconProvider.GetIconAsync(...)` is awaited **before** `Title = result.Name` (:151). The host gives inline fallback rows a **200 ms** budget (`FallbackItemSlowTimeout`, research §A.2) before it detaches the row and continues; the first sighting of a file type can load a shell icon handler and take tens of ms. On a cold cache (first home-screen search after restart, an unusual extension, slow AV) the row title can arrive too late and the rows do not render at all. Fix: set `Title`, `Subtitle`, `Command`, `MoreCommands` synchronously first, then update `Icon` after the await (icon update on an already-rendered row is benign; the title is what the host scores/renders). Same pattern exists on the page (`EverythingPalPage.cs:122-140`) but the page has its own loading state, so it is only a latency issue there.

### F5 — MED (UX/expectation): changing "主页搜索内联结果行数" will not take effect without a CmdPal extension reload
`src/EverythingPal/EverythingPalCommandsProvider.cs:42-47` claims the new row count "applies without an extension reload". CmdPal caches `FallbackCommands()` for the loaded provider and only re-reads it after its own `ReloadCommandsMessage` (triggered by CmdPal's fallback-settings UI), not by our JSON `SettingsChanged` event. `RebuildFallbackGroup` therefore updates a cache the host does not re-query, and the old group's items keep receiving `UpdateQuery` (the old group is intentionally never disposed, :71-73). Result: user changes 3→5 rows, sees no change until "Reload Command Palette extensions". Either document that in the setting description (and fix the comment), or push users to the reload action. Also note that any settings change (e.g. sort order) creates a new fallback group while old host-held items keep searching through the old group → extra `es.exe` process per keystroke until reload. Lead's planned row-count E2E test will confirm the user-visible behaviour.

### F6 — MED (VERIFY): fallback status rows may never render ("没有匹配结果", "Everything 没有运行")
`src/EverythingPal/Commands/EverythingFallbackItem.cs:163-196` puts the status text in the row **Title**. For third-party fallbacks with `IncludeInGlobalResults=true`, the host re-scores fallback rows on render against their live title and the user's query; a row titled "没有匹配结果" / "Everything 没有运行" does not match a file-name query and is likely filtered out, leaving the home screen blank instead of showing the status (the command page status item is unaffected). This is the same mechanism that makes real result rows (titles = file names) surface. Must be verified on 0.2.0.0: search for a nonexistent file name on the home screen and with Everything stopped. If the row does not appear, T1 item 6 (zero-result message) is not actually achieved for the home fallback and the status mechanism needs a different approach (or is accepted as page-only).

### F7 — LOW (robustness): `EverythingLocator.Find()` re-scans the whole uninstall registry on every action
`src/EverythingPal/Commands/EverythingLocator.cs:322-354` → `UninstallLocations()` iterates every `...\CurrentVersion\Uninstall` subkey (HKLM/HKCU x64/x86) on each call. It is called from `StartEverythingCommand`, `OpenInEverythingCommand` and the launch dialog (all UI paths), so on machines with hundreds of uninstall entries this can block the UI for tens of ms. Discovery results are stable for a session; cache the successful result (e.g. `Lazy<string?>`) and invalidate on user action failure.

### F8 — LOW (robustness): path normalization edge cases in Everything GUI discovery
`src/EverythingPal/Core/EverythingGuiDiscovery.cs:139-191` treats "path ends with `.exe`" as a full path; a bare `Everything.exe` App Paths value would be probed relative to the process CWD instead of converted to a full path. It also strips anything after the last comma when the suffix is all digits — a directory legitimately containing `,<digits>` would be truncated. Both are unlikely on real installs; a `Path.GetFullPath`/`Path.IsPathFullyQualified` check and a stricter `",<digits>"`-at-end match would harden it.

### F9 — LOW (i18n/UX): extension name in CmdPal's extension list stays English on zh systems
`Package.appxmanifest:25,50,72` keep English display names (deliberate per research §C.7 "keep manifest English"); the in-app UI is now localized. Result: on a Chinese Windows, CmdPal → Extensions shows "Everything Search" while the commands/pages are Chinese. Not a bug, but a visible inconsistency; localized manifest names require `ms-resource:` + `.resw` (explicitly out of scope in the research doc) — record as accepted.

### F10 — LOW (perf): localization lookups are uncached on hot UI property reads
`src/EverythingPal/Localization/Strings.cs:236-278`: every `Get(key)` calls `Environment.GetEnvironmentVariable("EVERYTHINGPAL_LANG")` and `CultureInfo.GetCultureInfo(...)` and a `ResourceManager.GetString`. These accessors are used in `Name`/`Title` properties that the host may read per row while rendering a 50-item page. `CultureInfo.GetCultureInfo` is cached by .NET and `ResourceManager` caches strings, so this is micro-cost, but caching the resolved culture per process (and reading the env var once) removes the repeated env-variable/IO-free lookups from the render path.

### F11 — LOW (perf): shell icon is a 32×32 PNG rendered at up to 48 px on high DPI
`src/EverythingPal/Services/ShellIconProvider.cs:182-215` extracts the shell's default (large/32×32) icon and encodes it once per extension. On 150–200 % DPI the host may request 24–32 logical px (36–64 physical), so the cached PNG can be slightly upscaled/blurry. Acceptable for a first Store release; if it looks soft in the screenshots, request a larger source icon (e.g. `SHIL_JUMBO`/`IImageList` path used by the Toolkit `ThumbnailHelper`) at warm-up time.

### F12 — LOW (cache efficiency): `ShellIconProvider` insertion-order queue can misorder/duplicate
`src/EverythingPal/Services/ShellIconProvider.cs:130-155`: detection of "was a new key added" via `PngCache.Count` before/after `GetOrAdd` can enqueue the same key twice under concurrency; eviction is then not strict FIFO (a live key may be evicted while a stale queue entry is discarded). The dictionary stays bounded (`MaxCachedIcons`), so this only affects hit-rate, not memory safety.

---

## Verified good (no action needed)

- **Core search semantics** (`SearchOutcomeMapper.cs`, `EverythingSearch.cs`, `EsResultParser.cs`): exit 0 + zero parsed results → `SearchStatus.NoResults` (so the UI shows "没有匹配结果" instead of a blank list); exit 9 → NoResults; exit 8 → EverythingNotRunning; missing export file tolerated (`-no-result-error` case); non-zero codes never parse the help dump. Unit-tested (`SearchSemanticsTests`).
- **Drive-root titles** (`EverythingResult.cs`): `D:\` now yields title `D:` and empty directory; normal files/folders unchanged; ranker regression test included. Root `Directory` = `""` is intentional/asserted.
- **es.exe discovery cache** (`EsExePathCache.cs` + `EverythingSearch.FindEsExe`): keyed by effective configured path (incl. `EVERYTHINGPAL_ES`), only success cached, stale cache invalidated when the file disappears; tests cover changed setting + deleted file. PATH is no longer rescanned per keystroke.
- **Everything GUI discovery** (`EverythingGuiDiscovery.cs` + `EverythingLocator.cs`): process path → App Paths (HKCU/HKLM ×64/x86) → uninstall InstallLocation/DisplayIcon → common dirs → PATH, normalized/quoted/`,0`-suffix handling, dedupe, injectable probing; both "start Everything" and "locate in Everything" now share it (duplicate old code removed). Tests cover ordering, dedupe, normalization, misses.
- **Localization**: neutral English + `zh-Hans` `.resx`; typed accessor layer with `EVERYTHINGPAL_LANG` override (`zh-TW/Hant` correctly falls back to English rather than Simplified); parity tests enforce identical key sets, non-empty values, typed-accessor coverage and that zh values actually differ.
- **Launch diagnostics dialog** (`Ui/LaunchDialog.cs`, `Program.cs:40-66`): Win32 `MessageBoxW` only (trim-safe), STA thread + join so the `[MTAThread]` COM-server path is untouched; shows version from `GetCurrentPackageFullName` (falls back to assembly version for loose builds), Command Palette presence via `SHParseDisplayName` AUMID with package-folder fallback, Everything path/running state, bundled `es.exe` presence and log path; offers "open Command Palette"/prerequisites; every failure is logged and the launch path never throws. This closes the "Start-menu tile does nothing" Store 10.1.2 risk.
- **Manifest** (`Package.appxmanifest`): `Windows.Universal` removed (desktop-only), `PublisherDisplayName` now "EverythingPal" (CmdPal About card), version 0.2.0.0, COM `ExeServer`/`Class` + `windows.appExtension` (com.microsoft.commandpalette) + `CreateInstance` intact and consistent with `EverythingPalExtension`'s GUID; `zh-Hans` resource language added for the satellite assembly.
- **Fallback model**: one `FallbackCommandItem` per row with stable IDs `czz404.everythingpal.fallback.0/1/2` preserved (user settings survive) and distinct localized `DisplayTitle`s (settings list no longer shows three identical entries); identical-query dedupe stops 3× searches per keystroke; grouped rows get NoResults handling for both old (Ok/0) and new (NoResults) Core behaviour; empty rows are cleared with a valid `NoOpCommand` (the old `Command = null` NRE path is gone).
- **Packaging/release pipeline**: `scripts/pack-store.ps1` is the single source of truth; self-contained publish flags are correct for a trimmed publish (`EnableMsixTooling=false`, `PublishProfile=`, `PublishProfile`, PDB stripping); LICENSE + THIRD-PARTY-NOTICES.md are bundled; per-arch identity/version/architecture, unsigned assertion and both-arch bundle validation are built into the packager; `deploy.ps1` now publishes self-contained, asserts the installed version matches the packed manifest and waits for CmdPal after restart. **Signing order is correct**: Microsoft documents that signing a `.msixbundle` recursively signs the inner packages ([Learn: Sign an app package using SignTool](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool)), so `release.yml` signing the bundle first is not a bug.
- **Tests**: baseline 28 tests kept; new tests for outcome mapping, drive roots, es.exe cache, GUI discovery and i18n parity. No machine-dependent assertions that would fail in CI (the one `FindEsExe` cache test uses `NotEqual`, safe either way).

## Needs on-device verification (0.2.0.0 install) — not statically decidable
1. F6 zero-result/Everything-not-running status rows on the **home fallback** (and the dedicated command page).
2. F5 row-count change (3→5→0) without extension reload; whether CmdPal picks it up.
3. Fallback cold-start: the default single row renders with real shell icons within the host budget after `Reload` (verified on 0.2.0.0; optional 3-row mode also verified earlier).
4. Details pane: my capture against the stale 0.1.0.0 build showed 5 real results but an empty details pane even with the first row selected. Confirm on 0.2.0.0; if still empty, check `ListItem.Details` wiring against host settings (details pane enabled?).
5. No-arg launch dialog: version shows `0.2.0.0`, Command Palette detected, es.exe present, "open Command Palette" works; `--about`/`--diagnostics` show the same dialog.
6. Trimmed self-contained payload: localized satellite `zh-Hans/EverythingPal.resources.dll` present and used (`EVERYTHINGPAL_LANG=en` vs `zh-Hans`), `System.Drawing.Common`/GDI+ payload present, shell icons render (verification script asserts payload; screenshots assert rendering).

## QA scope note
This review is explicitly about the code paths being shipped. Content/marketing risks already tracked elsewhere: Partner Center identity/version rules, privacy-policy URL and support URL (both public now), `Everything` trademark wording, runFullTrust justification, CmdPal Gallery submission — see `docs/store/`.

---

## Post-fix status after the 0.2.0.0 reinstall (2026-10-20, src frozen)

Verified against the installed `EverythingPal_0.2.0.0_x64__b32a3jmekh2ja` build (developer-signed,
self-contained) in Command Palette 0.13.12862 — see `docs/qa/verification.md` "Installed-package
end-to-end evidence" for the evidence table and screenshots.

| Finding | Status after reinstall | Notes |
|---|---|---|
| F1 unvirtualizedResources | **RESOLVED** | Capability + desktop6 write-virtualization props removed from the manifest and E2E-tested (page search/es.exe temp export, home row, real icons, log writes all OK). Final packages declare `runFullTrust` only; `verify-build.ps1` guards against regression. |
| F2 MinVersion 19041 | **WITHDRAWN — not a bug** | Lead verified Command Palette officially supports Windows 10 2004 (19041)+ and Windows 11; README aligned. |
| F3 stale async results (page + fallback) | **FIXED — verified in frozen src** | `EverythingFallbackItem.cs:98-99/126-129/183` check the token before/after distribution and icon upgrade; `EverythingPalPage.cs:68-75/84-88` re-check the token after `BuildItemsAsync` and never reset `IsLoading` for a stale task. |
| F4 fallback title delayed by icon await | **FIXED — verified on-device** | `EverythingFallbackItem.cs:171-182` fills Title/Subtitle/Command synchronously and only then awaits the icon. Home rows render with real icons and content on 0.2.0.0 (`02-home-inline-result.png`, now the default single row). Residual caveat is host-side timing: CmdPal shows these rows asynchronously and they were only reliably visible with a long settle after typing. That is host render cadence, not the extension's title ordering. |
| F5 row-count setting needs extension reload | **OPEN (MED, TBD)** | Code still claims otherwise in `EverythingPalCommandsProvider.cs:42-47`; CmdPal caches `FallbackCommands()` and re-reads it only on its own reload message. Not user-tested in this pass (settings navigation not completed) — recommend wording the setting as "reload Command Palette extensions to apply" unless host testing shows otherwise. |
| F6 fallback status rows / title scoring | **REPLACED by F13 (fix in progress)** | The frozen build intentionally shows NO status row for NoResults (`EverythingFallbackItem.cs:109-113`) — but that leaves blank rows (see F13). Other statuses (`EverythingNotRunning`, `EsExeNotFound`, `Timeout`, `QueryFailed`) go through `ShowStatus`, which sets Title=query + status in the subtitle (`:192-224`); not E2E-verified (would require stopping Everything / breaking es.exe). |
| F13 (NEW, HIGH user-visible) — zero-result home fallback leaves blank rows with stale icons | **FIX IN PROGRESS by icon-ui (Lead repacking)** | On 0.2.0.0, after a successful search an unmatched word (`zzzqqq`) leaves three blank 回退 rows with the previous file/folder/zip icons and no text. Root cause: `EverythingFallbackItem.cs:109-113` returns after `ClearAll()` for NoResults without a status row, and `Clear()` (`:226-232`) does not reset `Icon`, so empty rows keep stale icons. Evidence: `artifacts/qa/zero-result-blank-rows.png`. Intended fix (agreed with Lead): `Clear()` resets `Icon`; NoResults shows exactly one informative row (Title=query, Subtitle="没有匹配结果 · …", app icon). Re-verification required after the repack. |
| Details pane empty | **TBD (host-side, not a bug)** | CmdPal 0.13.12862 did not render the details pane even for built-in items after keyboard selection and mouse click (`artifacts/qa/probe-builtin-details.png`). The extension supplies `Details` metadata; whether the pane appears depends on the host/environment. |
| Home fallback render timing | **OBSERVED HOST BEHAVIOUR (no code action)** | Rows are host-rendered asynchronously; with the default single row they are reliably visible after a few seconds. Earlier 3-row captures needed an 8 s settle after retrying; the final Store shot 02 uses the 1-row default with a 9 s settle. |

Bottom line: no open functional bug in the extension code after the final reinstall and the zero-result
fix; F1 was resolved by removing the restricted capability, and the remaining work is Partner Center
admin (identity/version, runFullTrust justification, listing) plus the F5 row-count wording note.

---

## FINAL closure (after the Lead's final reinstall with the zero-result fix + capability reduction)

**Final verdict: PASS for the QA scope of task-6; no open code/Store blockers remain in this review.**

- **F1 (restricted capability) — RESOLVED.** The final manifest is capability-reduced to
  `runFullTrust` only: `unvirtualizedResources` and the `desktop6:FileSystemWriteVirtualization` /
  `RegistryWriteVirtualization` overrides were removed (`src/EverythingPal/Package.appxmanifest`; the
  installed package manifest was inspected and lists `runFullTrust` only). `scripts/verify-build.ps1`
  now asserts this posture in every bundled package (runFullTrust present, unvirtualizedResources and
  desktop6 virtualization absent), so a regression fails the verification run. The only remaining
  Store-submission action is the normal "restricted capability: runFullTrust" justification in
  Partner Center Submission options — expected for a packaged out-of-process COM server.
- **F13 (zero-result blank rows) — FIXED and re-verified.** On the final installed build:
  command page with unmatched `zzzqqq` shows exactly ONE informative row (app icon,
  `没有匹配结果 · 换个关键词，或检查 Everything 的索引设置。`) and no blank/stale-icon rows
  (`docs/store/screenshots/04-zero-result.png`); the home-screen equivalent was also probed
  (`artifacts/qa/home-zero-result-1row-new.png`: one row, query as title + status as subtitle).
  Fix: `src/EverythingPal/Commands/EverythingFallbackItem.cs` — NoResults calls `ShowNoResults(query)`
  (`:109-114`, `:233-241`) and `Clear()` resets the icon (`:243+`).
- **Default home fallback rows = 1 (new final default).** `docs/store/screenshots/02-home-inline-result.png`
  intentionally shows exactly ONE inline result (`wantai  D:\`); the setting remains 0–5. The earlier
  3-row screenshot was removed (old default 3).
- **Matched-query regression check — PASS.** Home screen shows the one configured inline result with a
  real folder icon; page mode still shows 3 real results with real .lnk/.zip icons
  (`01-search-page.png`); context menu unchanged (`03-context-menu.png`).
- **Packaging re-verified on the final source tree:** `scripts/verify-build.ps1` → see
  `docs/qa/verification.md`; final unsigned Store bundle for upload sha256
  `73B71AF9B1570FC592DFFAB1522F177F1CCACD65878BBF0E8187B0DFEAAAF8E4` (22,349,272 bytes), per
  `artifacts/store/EverythingPal_0.2.0.0_x64_arm64.msixbundle`.
- Remaining non-blocking notes: F5 (row-count change wording/reload expectation) stays a docs/UX
  note; details-pane and Everything-not-running status are host-side/unverified TBDs (see
  `docs/qa/verification.md`).



