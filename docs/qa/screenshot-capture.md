# EverythingPal — Store screenshot capture (QA evidence & procedure)

**Tool:** `scripts/capture-screenshots.ps1` (QA-owned; never installs/uninstalls packages)
**Captured:** 2026-10-20 against the installed build `EverythingPal_0.2.0.0_x64__b32a3jmekh2ja`
(developer-signed, self-contained) in Command Palette `Microsoft.CommandPalette_0.13.12862.0`.
**Machine:** Windows 11 26H1 (build 28000) x64, 2560x1440 screen at 100 %, zh-CN UI.

## Final screenshots (ready for the Store listing)

All in `docs/store/screenshots/`, PNG, window-only, **1366x768** (Store desktop minimum is 1366x768):

| File | Size | Caption (zh) | Caption (en) |
|---|---|---|---|
| `01-search-page.png` | 1366x768, 18 KB | 实时搜索本机文件：结果列表与详情面板（大小、修改时间、可点击路径） | Live local file search with a details pane: size, modified time and clickable path. |
| `02-home-inline-rows.png` | 1366x768, 39 KB | 在命令面板主页直接搜索文件名，前 3 条结果即时显示（需在 CmdPal 设置中启用第三方回退命令） | Search file names right from the Command Palette home screen — top 3 results inline (enable third-party fallback rows in Command Palette settings). |
| `03-context-menu.png` | 1366x768, 31 KB | 回车打开，右键可定位到资源管理器、复制路径或在 Everything 中打开 | Open with Enter, or reveal in Explorer, copy the path, or open the item in Everything. |
| `04-zero-result.png` | 1366x768, 22 KB | 没有匹配结果时给出一行可读提示，而不是空白行 | A readable no-results row instead of blank rows when nothing matches. |

- Each has a `-fullscreen.png` sibling (2560x1440, neutral backdrop + palette). **Do not upload the
  full-screen variants** — they exist only as a fallback/reference.
- `_capture-manifest.json` records SHA256, dimensions, captions and the exact capture settings.
- No probe/trial files remain in `docs/store/screenshots/`.

## Commands used (reproducible)

```powershell
# shot #1 — extension command page, real result list (page mode does not depend on fallback toggles)
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -SelectFirst -Backdrop -WaitMs 2500 -Names 01-search-page -CleanOutput

# shot #2 — home screen inline fallback rows (requires 0.2.0.0 + IncludeInGlobalResults enabled)
pwsh -File scripts/capture-screenshots.ps1 -Mode home -Queries wantai -Backdrop -WaitMs 8000 -Names 02-home-inline-rows

# shot #3 — context menu on a result (opens with Ctrl+K after selecting)
pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -SelectFirst -OpenContextMenu -Backdrop -WaitMs 2500 -Names 03-context-menu

# shot #4 — zero-result state AFTER a successful search (preQuery produces the "rows were visible" state
# first; the capture query then replaces it, exactly the path that used to leave blank stale-icon rows)
pwsh -File scripts/capture-screenshots.ps1 -Mode home -PreQuery wantai -PreWaitMs 8000 -Queries zzzqqq -WaitMs 9000 -Backdrop -Names 04-zero-result
```

`-Backdrop` starts a neutral topmost full-screen surface for the duration of the capture, so the
CmdPal window's transparent shadow/rounded-corner margins (and the full-screen reference image) do
not contain the user's desktop. Captures use `DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)` so the
invisible DWM shadow border is not part of the PNG. Input is `SendInput` (Win+Alt+Space hotkey +
Unicode characters); keystrokes are only injected after the foreground window is verified to belong
to `Microsoft.CmdPal.UI`, so a focus failure can never type into another app. The palette is closed
again (Esc ×2 as needed) when the script opened it, and the original foreground window is restored.

## Limitations / host behaviour observed (2026-10-20)

1. **Home fallback rows are rendered asynchronously by the host.** With the 0.2.0.0 fallback fixes
   (50 ms debounce, title before icon) they render with real icons and distinct content, but the
   host needed a long settle (~3–8 s after typing) to show them reliably; at 1.8 s they were absent,
   and some intermediate-settle attempts produced a frame without them. Shot #2 was therefore
   captured with `-WaitMs 8000` after retrying until all three rows were present. If a future host
   update changes this, re-run the command and compare.
2. **Details pane did not render on this host build** — even for built-in commands after keyboard
   selection and mouse click (`artifacts/qa/probe-builtin-details.png`). This is host/environment
   behaviour, not the extension's `Details` metadata (the extension still supplies size/modified/
   path). Shot #1 therefore shows the result list without a details pane.
3. **Zero-result home fallback — FIXED and re-verified.** The earlier 0.2.0.0 build left blank rows with
   stale icons after an unmatched query; the final rebuilt installed package shows exactly ONE
   informative row (app icon, title=query, subtitle `没有匹配结果 · 换个关键词，或检查 Everything 的索引设置。`)
   and no stale icons (`04-zero-result.png`, captured with `-PreQuery wantai`). Old defect evidence:
   `artifacts/qa/zero-result-blank-rows.png`.
4. **Launcher dialog evidence:** `artifacts/qa/launch-dialog.png` (497x499) — no-arg Start-menu
   launch of the installed package shows version `0.2.0.0`, "内置 es.exe：存在", Command Palette
   installed and Everything installed/running; "是(Yes)" opens Command Palette. Captured with an
   ad-hoc Win32 probe (no script needed; the dialog is modal, so a scripted capture must read the
   window text and close it via the Yes/No button).

## Optional / manual shots (not captured; procedure for a human or a future pass)

- **#4 extension settings page** (brand-materials `listing.md` §10 item 4): open Command Palette →
  type `Everything 搜索` → select the command → context menu (Ctrl+K) → Settings. Window must be
  resized to ≥1366x768 before capture (CmdPal settings window defaults to 1280x720 — below the Store
  minimum); `SetWindowPos` works on it. Caption: "可调结果条数、排序与主页显示行数，支持自定义
  es.exe 路径。"
- **#5 Everything-not-running error state**: quit Everything (tray icon → Exit), then run
  `pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -Backdrop -WaitMs 2500 -Names 05-everything-not-running`,
  capture, then start Everything again. Not done here because stopping the user's Everything was out
  of QA scope; the fallback error mapping is unit-tested.
- **#6 fallback settings**: Command Palette Settings (gear) → Extensions → Everything 搜索 →
  回退命令. The three rows should show distinct "第 1/2/3 行" display titles and an
  "包含在全局结果中" checkbox. Settings window is below 1366x768 by default → resize first.
- **#3 context menu** can also be captured manually with Ctrl+K on a selected result (script does it).

## Probe / evidence artifacts (not deliverables)

| Path | What |
|---|---|
| `artifacts/qa/launch-dialog.png` | Launcher diagnostics dialog on installed 0.2.0.0 (version/es.exe evidence). |
| `artifacts/qa/zero-result-blank-rows.png` | Zero-result home fallback defect evidence (blank rows + stale icons). |
| `artifacts/qa/probe-builtin-details.png` | Built-in command selected; no details pane on this host (details-pane TBD evidence). |
| `artifacts/qa/timeline*/`, `artifacts/qa/screen-now.png`, `artifacts/qa/settings-*.png` | Intermediate probes from the capture feasibility work. |

Screenshots must be re-captured after any package repack that changes UI strings/layout (e.g. the
pending zero-result fix does not change these three shots, but verify before submission).
