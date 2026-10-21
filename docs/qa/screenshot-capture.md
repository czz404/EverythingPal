# EverythingPal — Store screenshot capture (QA evidence & procedure)

**Tool:** `scripts/capture-screenshots.ps1` (QA-owned; never installs/uninstalls packages)
**Status:** the **active, approved Store set** is in `docs/store/screenshots/` — 5 PNGs at
**1920×1080** (`program files` universal query), approved by the Lead on 2026-10-21. It replaced the
earlier 1366×768 `wantai` set; `docs/store/screenshots-v2/` was folded into the active directory and
removed.
**Captured on:** installed Store-identity package
`EverythingPal.EverythingPal_1.0.0.0_x64__33w78pmn50ga4` (developer-signed for local capture) in
Command Palette `Microsoft.CommandPalette_0.13.12862.0`.
**Machine:** Windows 11 26H1 (build 28000) x64, 2560×1440 screen at 100 %, zh-CN UI.

## Active screenshots (Store PDP ready)

All in `docs/store/screenshots/`, PNG, window-only, **exactly 1920×1080**:

| File | Size | Dimension | Caption (zh) | Caption (en) |
|---|---|---|---|---|
| `01-search-page.png` | 92 KB | 1920×1080 | 实时搜索本机文件：结果列表与真实文件图标 | Live local file search with real file-type icons. |
| `02-home-inline-result.png` | 31 KB | 1920×1080 | 在命令面板主页直接搜索文件名，内联显示 Everything 结果（默认 1 行，0–5 可配） | Search file names right from the Command Palette home screen with an inline Everything result (default 1 row, 0-5 configurable). |
| `03-context-menu.png` | 104 KB | 1920×1080 | 回车打开，右键可定位到资源管理器、复制路径或在 Everything 中打开 | Open with Enter, or reveal in Explorer, copy the path, or open the item in Everything. |
| `04-zero-result.png` | 18 KB | 1920×1080 | 没有匹配结果时给出一行可读提示，而不是空白行 | A readable one-line no-results message instead of blank rows when nothing matches. |
| `05-program-files.png` | 92 KB | 1920×1080 | 在「Everything 搜索」命令页中搜索系统级文件夹（Program Files） | Searching system-level folders (Program Files) on the Everything search page. |

- Each has a `-fullscreen.png` sibling (2560×1440, neutral backdrop + palette). **Do not upload the
  full-screen variants** — they are reference-only.
- `_capture-manifest.json` records the full SHA256, dimensions, zh/en captions, mode/query, sizing
  (captured at 1934×1087 because CmdPal caps its window at ~75 % of the 2560×1440 screen, then cropped
  centered to exactly 1920×1080) and a `personalPathVerification` block.
- **`02-home-inline-result.png` shows exactly ONE inline result** — the default `FallbackRows` is 1
  (0–5 remains configurable); do not retry for 3 rows. Its single row is
  `Downloaded Program Files  C:\Windows （共 3 个结果）` (generic folder, not personal).

## Commands (reproducible)

```powershell
# 01 — command page, universal query "program files"
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries "program files" -SelectFirst -Backdrop -Width 1934 -Height 1087 -WaitMs 2500 -Names 01-search-page -CleanOutput

# 02 — home screen inline fallback result (single-row default; host needs a long settle)
pwsh -File scripts/capture-screenshots.ps1 -Mode home -Queries "program files" -SelectFirst -Backdrop -Width 1934 -Height 1087 -WaitMs 9000 -Names 02-home-inline-result

# 03 — context menu on the first result
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries "program files" -ClickFirstResult -OpenContextMenu -Backdrop -Width 1934 -Height 1087 -WaitMs 2500 -Names 03-context-menu

# 04 — no-results state
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries zzzqqq -Backdrop -Width 1934 -Height 1087 -WaitMs 2500 -Names 04-zero-result

# 05 — program-files page without selection highlight
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries "program files" -Backdrop -Width 1934 -Height 1087 -WaitMs 2500 -Names 05-program-files
```

`-Backdrop` starts a neutral topmost full-screen surface for the duration of the capture, so the
CmdPal window's transparent shadow/rounded-corner margins do not contain the user's desktop. Captures
use `DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)` so the invisible DWM shadow border is not part of
the PNG. Input is `SendInput` (Win+Alt+Space hotkey + Unicode characters); keystrokes are only injected
after the foreground window is verified to belong to `Microsoft.CmdPal.UI`, so a focus failure can
never type into another app. The palette is closed again when the script opened it, and the original
foreground window is restored. The CmdPal **settings** window is explicitly excluded from capture
targets (same process; its title also contains 命令面板).

## Query rationale

- **`program files`** is present on every Windows PC, the ranker puts `C:\Program Files` /
  `C:\Program Files (x86)` at the top, and the captured rows contain **no `C:\Users\<username>`,
  `C:\ProgramData` or `C:\Windows\ServiceProfiles` paths** (verified by visual inspection). This is
  why it replaced the machine-specific `wantai` and the noisy `downloads` query (the latter surfaced
  this machine's ProgramData/cache and user-profile paths).
- Lower rows of 01/03/05 still contain generic `C:\Windows\WinSxS\…` noise (cosmetic, never personal);
  a future host smart-match update may push it off the visible first screen.
- `notepad` was evaluated as an alternative and rejected (its results include
  `C:\Users\<name>\AppData\Local\…` paths).

## Personal-path check (required)

**PASS** for 01/02/03: no `C:\Users\<username>`, no `C:\ProgramData`, no
`C:\Windows\ServiceProfiles`; only generic `C:\Program Files*`, `C:\Windows\Downloaded Program Files`,
Office `vfs` and generic WinSxS paths. The manifest stores this as `personalPathVerification`.

## Smart-match internal evidence (dev build only, NOT a Store asset)

- `artifacts/qa/term1-evidence/term1-evidence.png` (1366×768, captured by the Lead on the dev
  smart-match build): query `term1` surfaces `Term 1  E:\BaiduNetdiskDownload` as the **first** row,
  followed by other generic `Term 1` workspace matches.
- Regeneration was attempted on the currently installed Store 1.0.0.0 package and correctly does not
  show the smart-match ordering (that fix ships in 1.0.1.0), so the Lead-captured file is referenced.
- Referenced in `docs/qa/verification.md` (E2E-12); **not** part of the Store screenshot upload.

## Other limitations / host behaviour

1. **Home fallback rows are rendered asynchronously by the host** — the single row needed a ~9 s settle
   after typing on this machine. Re-run and compare if a host update changes this.
2. **Details pane does not render on CmdPal 0.13.12862** even for built-in commands after keyboard
   selection and mouse click (`artifacts/qa/probe-builtin-details.png`). This is host/environment
   behaviour, not the extension's `Details` metadata (size/modified/path are supplied), so the
   screenshots show the result list only.
3. **Launcher dialog evidence:** `artifacts/qa/launch-dialog.png` (497×499) — no-arg Start-menu launch
   shows the package version, bundled `es.exe` present, Command Palette installed and Everything
   installed/running; Yes opens Command Palette. Modal dialog: read its window text and close it via the
   Yes/No button (or WM_CLOSE).

## Optional / manual shots (not captured)

- **Extension settings page**: open Command Palette → type `Everything 搜索` → select the command →
  context menu (Ctrl+K) → Settings. Resize the settings window to ≥1366×768 before capture (default
  1280×720 is below the Store minimum); `SetWindowPos` works on it.
- **Everything-not-running error state**: quit Everything (tray → Exit), run
  `pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries "program files" -Backdrop -Width 1934 -Height 1087 -WaitMs 2500 -Names 06-everything-not-running`,
  then start Everything again. Not done here (stopping the user's Everything was out of QA scope); the
  error mapping is unit-tested.
- **CmdPal fallback settings screenshot**: Settings (gear) → Extensions → Everything 搜索 → fallback
  commands (0–5 home rows, default 1); resize the settings window to ≥1366×768 first.

## Evidence / probe artifacts (not deliverables)

| Path | What |
|---|---|
| `artifacts/qa/term1-evidence/` | Dev-build smart-match evidence (`term1` → `Term 1 E:\BaiduNetdiskDownload`). |
| `artifacts/qa/launch-dialog.png` | Launcher diagnostics dialog. |
| `artifacts/qa/home-zero-result-1row-new.png` | Home-screen zero-result probe (one informative row). |
| `artifacts/qa/zero-result-blank-rows.png` | Old zero-result defect (earlier build) used as regression evidence. |
| `artifacts/qa/probe-builtin-details.png` | Built-in item selected; no details pane on this host. |
| `artifacts/qa/v2-probes/` | Query-comparison probes (`downloads`, `folder:downloads`, `notepad`, …) from the v2 evaluation. |
