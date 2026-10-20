# EverythingPal — Store screenshot capture (QA evidence & procedure)

**Tool:** `scripts/capture-screenshots.ps1` (QA-owned; never installs/uninstalls packages)
**Captured:** 2026-10-20 against the installed final build `EverythingPal_0.2.0.0_x64__b32a3jmekh2ja`
(developer-signed, self-contained, capability-reduced manifest with `runFullTrust` only, **default
home fallback rows = 1**) in Command Palette `Microsoft.CommandPalette_0.13.12862.0`.
**Machine:** Windows 11 26H1 (build 28000) x64, 2560x1440 screen at 100 %, zh-CN UI.

## Final screenshots (ready for the Store listing)

All in `docs/store/screenshots/`, PNG, window-only, **1366x768** (Store desktop minimum is 1366x768):

| File | Size | Caption (zh) | Caption (en) |
|---|---|---|---|
| `01-search-page.png` | 1366x768, 18 KB | 实时搜索本机文件：结果列表与详情面板（大小、修改时间、可点击路径） | Live local file search with a details pane: size, modified time and clickable path. |
| `02-home-inline-result.png` | 1366x768, 33 KB | 在命令面板主页直接搜索文件名，内联显示 Everything 结果（默认 1 行，0–5 可配；需在 CmdPal 设置中启用第三方回退命令） | Search file names right from the Command Palette home screen with an inline Everything result (default 1 row, 0-5 configurable; enable third-party fallback rows in Command Palette settings). |
| `03-context-menu.png` | 1366x768, 31 KB | 回车打开，右键可定位到资源管理器、复制路径或在 Everything 中打开 | Open with Enter, or reveal in Explorer, copy the path, or open the item in Everything. |
| `04-zero-result.png` | 1366x768, 14 KB | 没有匹配结果时给出一行可读提示，而不是空白行 | A readable one-line no-results message instead of blank rows when nothing matches. |

- Each has a `-fullscreen.png` sibling (2560x1440, neutral backdrop + palette). **Do not upload the
  full-screen variants** — they exist only as a fallback/reference.
- `_capture-manifest.json` records SHA256, dimensions, zh/en captions and the exact capture settings.
- **`02-home-inline-result.png` shows exactly ONE inline result.** The final build changes the default
  `FallbackRows` to 1 (0–5 remains configurable); this is intentional — do not retry for 3 rows.
- No probe/trial files remain in `docs/store/screenshots/`.

## Commands used (reproducible)

```powershell
# shot #1 — extension command page, real result list (page mode does not depend on fallback toggles)
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -SelectFirst -Backdrop -WaitMs 2500 -Names 01-search-page -CleanOutput

# shot #2 — home screen inline fallback result (default = 1 row on the final build)
pwsh -File scripts/capture-screenshots.ps1 -Mode home -Queries wantai -SelectFirst -Backdrop -WaitMs 9000 -Names 02-home-inline-result

# shot #3 — context menu on a result (opens with Ctrl+K after clicking the first result)
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -ClickFirstResult -OpenContextMenu -Backdrop -WaitMs 2500 -Names 03-context-menu

# shot #4 — page no-results state (one informative row, no blank rows)
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries zzzqqq -Backdrop -WaitMs 2500 -Names 04-zero-result
```

`-Backdrop` starts a neutral topmost full-screen surface for the duration of the capture, so the
CmdPal window's transparent shadow/rounded-corner margins (and the full-screen reference image) do
not contain the user's desktop. Captures use `DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)` so the
invisible DWM shadow border is not part of the PNG. Input is `SendInput` (Win+Alt+Space hotkey +
Unicode characters); keystrokes are only injected after the foreground window is verified to belong
to `Microsoft.CmdPal.UI`, so a focus failure can never type into another app. The palette is closed
again (Esc ×2 as needed) when the script opened it, and the original foreground window is restored.

## Limitations / host behaviour observed (2026-10-20, final build)

1. **Home fallback rows are rendered asynchronously by the host.** The final build's single fallback
   row renders with a real icon, but the host needed a long settle (~9 s) to show it reliably after
   typing on this machine. If a future host update changes this, re-run and compare.
2. **Default fallback rows = 1.** `02-home-inline-result.png` therefore shows one inline result
   (`wantai  D:\`). Set "主页搜索内联结果行数" to 3–5 in the extension settings (and reload Command
   Palette extensions) if a multi-row screenshot is ever wanted again.
3. **Details pane did not render on this host build** — even for built-in commands after keyboard
   selection and mouse click (`artifacts/qa/probe-builtin-details.png`). This is host/environment
   behaviour, not the extension's `Details` metadata (the extension still supplies size/modified/
   path). Shot #1 therefore shows the result list without a details pane.
4. **Zero-result state (final build) — PASS.** Command page shows exactly one informative row
   (`没有匹配结果 · 换个关键词，或检查 Everything 的索引设置。`) with no blank/stale rows
   (`04-zero-result.png`). The home-screen equivalent was also probed
   (`artifacts/qa/home-zero-result-1row-new.png`): one row with the query as title and the status as
   subtitle. Old defect evidence (earlier 0.2.0.0 build): `artifacts/qa/zero-result-blank-rows.png`.
5. **Launcher dialog evidence:** `artifacts/qa/launch-dialog.png` (497x499) — no-arg Start-menu
   launch shows version `0.2.0.0`, "内置 es.exe：存在", Command Palette installed and Everything
   installed/running; "是(Yes)" opens Command Palette. Modal dialog: capture its window text and close
   it with the Yes/No button (or WM_CLOSE) rather than trying to script around it.

## Optional / manual shots (not captured; procedure for a human or a future pass)

- **#4 extension settings page** (brand-materials `listing.md` §10): open Command Palette → type
  `Everything 搜索` → select the command → context menu (Ctrl+K) → Settings. Resize the settings
  window to ≥1366x768 before capture (CmdPal settings defaults to 1280x720 — below the Store
  minimum); `SetWindowPos` works on it. Caption: "可调结果条数、排序与主页显示行数，支持自定义
  es.exe 路径。"
- **#5 Everything-not-running error state**: quit Everything (tray icon → Exit), then run
  `pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -Backdrop -WaitMs 2500 -Names 05-everything-not-running`,
  capture, then start Everything again. Not done here because stopping the user's Everything was out
  of QA scope; the fallback error mapping is unit-tested.
- **#6 fallback settings**: Command Palette Settings (gear) → Extensions → Everything 搜索 →
  回退命令/主页结果. The configuration should offer 0–5 home rows (default 1 on the final build) with
  an "包含在全局结果中" checkbox; resize the settings window to ≥1366x768 first.

## Probe / evidence artifacts (not deliverables)

| Path | What |
|---|---|
| `artifacts/qa/launch-dialog.png` | Launcher diagnostics dialog on installed 0.2.0.0 (version/es.exe evidence). |
| `artifacts/qa/zero-result-blank-rows.png` | Old zero-result defect (blank rows + stale icons) on an earlier build. |
| `artifacts/qa/home-zero-result-1row-new.png` | Final-build home zero-result probe (one informative row). |
| `artifacts/qa/probe-builtin-details.png` | Built-in command selected; no details pane on this host (details-pane TBD evidence). |
| `artifacts/qa/timeline*/`, `artifacts/qa/screen-now.png`, `artifacts/qa/settings-*.png` | Intermediate probes from the capture feasibility work. |
