# Everything Search for Command Palette — Microsoft Store 上架文案

> 状态：**v1.1（2026-10-20 修正版）**，供 Partner Center 提交时逐字复制。修正：Gallery 可发现性、个人开发者免费注册、主页 fallback 默认 1 行、能力只剩 `runFullTrust`、公开 URL/截图状态。
> 负责：brand-materials（task-4）；包身份 / 打包由 store-packaging（task-3）负责，见 [packaging.md](packaging.md)。
> 事实核对来源：Microsoft Learn（见文末「引用与核实」）；未经核实的字段用 **TBD** 标出，提交前必须逐项确认。

---

## 0. 一页速览（提交时填什么）

| Partner Center 字段 | 建议值 | 状态 |
| --- | --- | --- |
| 产品名称（Product name） | `Everything Search for Command Palette`（候选见 §1） | 推荐；商标风险见 §7 |
| 类别 / 子类别 | Utilities & tools（子类别 TBD，按 Partner Center 列表选最接近“文件/搜索工具”） | Learn 建议按功能选最合适类别 |
| 定价 | 免费，无应用内购买 | 建议 |
| 市场 | 全部市场（All markets） | 建议 |
| 语言（商店列表） | 中文（简体，zh-CN/zh-Hans）为主 + English (United States) | 建议；每个语言要单独上传一次截图。UI 已双语（跟随系统显示语言） |
| 支持的体系结构 | x64 + ARM64（`.msixbundle`） | 由 packaging 提供 |
| 包内身份 | 用 Partner Center「Product identity」的值逐字替换（Name / Publisher / PublisherDisplayName） | 由 store-packaging 执行；**提交前必须核对** |
| 商店图标 | `docs/store/assets/store-icon-300.png`（300×300 PNG） | 已生成 |
| 截图 | `docs/store/screenshots/`（QA 已采集 4 张，均 1366×768） | ✅ `01-search-page` / `02-home-inline-result` / `03-context-menu` / `04-zero-result`；可选补设置页/错误态（见 §10） |
| 年龄分级 | IARC 问卷；无用户生成内容、无网络、无广告 → 预计 3+ | 问卷在 Partner Center 填 |
| 隐私政策 URL | https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md | ✅ 仓库已 public，可直接使用 |
| 支持 URL | https://github.com/czz404/EverythingPal/issues | ✅ 仓库已 public |
| 认证备注（Notes for certification） | 见 `certification-notes.md` 的英文粘贴版 | 已准备，需随最终行为复核 |

> 截图、应用图标、描述、隐私/支持 URL 是 Store 列表的核心资产；其中**隐私政策 URL 与支持 URL 必须真实可访问**，否则提交可能被拒。

---

## 1. 应用名称候选（Store display name）

规则：Store 列表名 = 在 Partner Center 预留的产品名；它**不要求**等于包身份 `Identity/@Name`，但提交后不轻易改（改名通常要重新提交、且影响已有评价/链接）。名称上限以 Partner Center 输入框提示为准（**TBD**；下列候选均远低于常见 256 字符限制）。

| 优先级 | 候选名称 | 字符数 | 优点 | 风险 |
| --- | --- | --- | --- | --- |
| ★ 首选 | `Everything Search for Command Palette` | 36 | 一眼说明“给命令面板用的 Everything 搜索”，利于搜索发现 | 以第三方商标 “Everything” 开头；需确保描述中声明非官方（§7） |
| 备选 1 | `EverythingPal for Command Palette` | 34 | 自有品牌词 `EverythingPal` 在前，商标风险更低 | 新品牌认知度低；`Everything` 仍出现在词内 |
| 备选 2 | `EverythingPal — File Search for Command Palette` | 46 | 品牌 + 功能最完整；破折号在 Store 列表里可读 | 名称偏长，小屏可能截断 |
| 备选 3 | `CmdPal Everything Search` | 23 | 短；明确是 CmdPal 扩展 | “CmdPal” 是 PowerToys 生态社区简称，非官方名；仍含 Everything |
| 备选 4 | `EverythingPal` | 11 | 最短、纯自有品牌 | 完全不说明用途，Discoverability 差 |

**建议流程**：先 reserve 首选名；若 Partner Center 因商标/重名拒绝，依次尝试备选 1 → 备选 2。无论用哪个，描述第一行都写明 “for PowerToys Command Palette”。

**不要使用**（会被商标/政策盯上）：`Everything`（单独）、`voidtools Anything`、任何含 “official/官方/微软/PowerToys 官方” 或冒充 Microsoft/PowerToys 团队的表述。

---

## 2. 简短描述（Short description）

> 用途：搜索结果里的单行摘要。任务要求 ≤270 字符；Partner Center 对该字段的硬上限 **TBD**（常见说法 1,000 字符、建议 <270），本稿按 ≤270 控制。

**中文（zh-CN）**
```
EverythingPal 是 PowerToys 命令面板的 Everything 搜索扩展：在命令面板里直接搜索本机文件和文件夹，回车打开、定位或复制路径。需已安装并运行 Everything 1.4/1.5；内置 es.exe，无需另行下载。
```
字符数：**124**（含标点，远低于 270）。

**English (en-US)**
```
Search your PC's files from PowerToys Command Palette. Type a file name, then open, reveal, or copy its path — powered by your local Everything index. Requires Everything 1.4/1.5 running; es.exe is bundled.
```
字符数：**206**（含标点，远低于 270）。

---

## 3. 长描述（Description）

> Partner Center 的 Description 上限常见为 10,000 字符（**TBD**，以 Partner Center 字段提示为准）；以下两版都远低于该值。Store 政策 10.1.1 要求描述准确、必须写明重要限制（本产品必须写明 PowerToys Command Palette 与 Everything 两个前提）。

### 3.1 中文（zh-CN，推荐主语言）

```text
EverythingPal 把 Everything 的本地文件搜索带进 PowerToys 命令面板（Command Palette）。在命令面板主页直接用文件名搜索，或在「Everything 搜索」命令页里实时筛选，结果就是磁盘上的真实文件与文件夹——不是最近使用记录、也不是快捷方式。

【前提条件】
• Windows 11，或 Windows 10 版本 2004（build 19041）及以上（x64 或 ARM64）
• 已安装 PowerToys，并启用其中的 Command Palette（默认快捷键 Win + Alt + Space）
• 已安装并正在运行 voidtools 的 Everything 1.4 或 1.5a（本扩展通过 Everything 的索引查询，不替代 Everything）

【功能】
• 主页内联结果：在命令面板主页输入文件名，默认显示 1 行（可在设置中改为 0–5 行）Everything 结果；回车打开，右键菜单可定位、复制路径或在 Everything 中打开。
• 实时搜索页：进入「Everything 搜索」命令，输入即搜（150 ms 防抖），结果按名称精确度、前缀、包含关系、文件夹/长度排序。
• 详情面板：显示文件大小、修改时间与可点击的完整路径。
• 可操作错误：Everything 未运行时可一键「启动 Everything」或「下载 Everything」；内置 es.exe 缺失、Everything 索引未就绪（超时）都有明确提示。
• 设置页：结果条数（1–500）、排序方式、主页 fallback 行数（0–5）、es.exe 路径覆盖。
• 自带 MIT 许可的 es.exe（来自 voidtools/ES），你只需安装并运行 Everything 本体，无需再单独下载命令行工具或配置 PATH。
• 隐私友好：不收集任何数据、不发送任何遥测、不申请网络能力；查询只在本机内存中交给 es.exe，结果不出设备。

【重要说明：如何看到主页内联结果】
Command Palette 默认不把第三方扩展的 fallback 命令加入全局结果。安装后请打开 command palette 设置 → 扩展 → Everything Search → 回退命令，启用需要的行，并勾选「Include in the Global result / 在全局结果中显示」；否则只有「Everything 搜索」命令页可用，主页输入文件名不会出现内联结果。这属于 Command Palette 平台行为，不是扩展的开关错误。

【与官方产品的关系】
本扩展是第三方个人开发的 PowerToys Command Palette 扩展，与 voidtools（Everything）及 Microsoft（PowerToys/Command Palette）均无隶属、赞助或背书关系。Everything 是 voidtools 的商标；PowerToys 与 Command Palette 是 Microsoft 的商标。应用图标为本项目原创，不含上述产品的商标或标志。

【支持与隐私】
支持：https://github.com/czz404/EverythingPal/issues
隐私：见 Store 列表中的隐私政策链接（本应用不收集、不传输任何个人数据）。
```

### 3.2 English (en-US)

```text
EverythingPal brings Everything's local file search into PowerToys Command Palette. Search by file name from the Command Palette home screen, or use the "Everything Search" page for live filtering. Results are the real files and folders on your disk — not recent-item shortcuts.

REQUIREMENTS
• Windows 11, or Windows 10 version 2004 (build 19041) or later (x64 or ARM64)
• PowerToys installed with Command Palette enabled (default hotkey: Win + Alt + Space)
• voidtools Everything 1.4 or 1.5a installed and running (this extension queries Everything's index; it does not replace Everything)

FEATURES
• Inline results on the Command Palette home screen: type a file name and get one row of Everything results by default (0–5 configurable). Enter opens the item; the context menu can reveal it, copy the path, or open it in Everything.
• Live search page: open the "Everything Search" command and type to search with a 150 ms debounce, ranked by exact name, prefix, substring, folders and shorter names.
• Details pane: size, modified time and a clickable full path.
• Actionable errors: if Everything is not running, use "Start Everything" or "Download Everything"; a missing bundled es.exe or a still-loading index are explained clearly.
• Settings page: result count (1–500), sort order, home-screen fallback rows (0–5) and an optional es.exe override.
• The MIT-licensed es.exe (from voidtools/ES) is bundled: you only need Everything itself — no separate CLI download, no PATH setup.
• Privacy friendly: no data collection, no telemetry, no network capability. Queries are passed to es.exe in memory on your device; results never leave it.

IMPORTANT: ENABLING INLINE RESULTS
Command Palette does not include third-party fallback commands in global results by default. After installing, open Command Palette settings → Extensions → Everything Search → Fallback commands, enable the rows you want and check "Include in the Global result". Until then only the "Everything Search" command page is available; typing on the home screen will not show inline results. This is Command Palette platform behavior, not a bug in the extension.

RELATIONSHIP TO OFFICIAL PRODUCTS
This is a third-party extension developed by an individual. It is not affiliated with, sponsored by, or endorsed by voidtools (Everything) or Microsoft (PowerToys / Command Palette). Everything is a trademark of voidtools; PowerToys and Command Palette are trademarks of Microsoft. The app icon is original artwork and contains no third-party logos.

SUPPORT & PRIVACY
Support: https://github.com/czz404/EverythingPal/issues
Privacy: see the privacy policy link on the Store listing (this app does not collect or transmit personal data).
```

---

## 4. 功能列表（Product features / 短 bullet）

> Partner Center 的 “Product features” 一般最多 20 条、每条长度受限（**TBD**）；以下 8 条 zh + 8 条 en 可直接逐条粘贴。

**中文**
1. 在 PowerToys 命令面板内实时搜索本机文件与文件夹
2. 主页默认显示 1 行内联结果（0–5 行可调），回车即打开
3. 详情面板显示大小、修改时间与完整路径
4. 资源管理器定位、复制路径、在 Everything 中打开
5. Everything 未运行时一键启动或下载
6. 自带 MIT 许可的 es.exe，免配置
7. 无网络能力、无遥测、不收集数据
8. 支持 x64 与 ARM64

**English**
1. Live local file search inside PowerToys Command Palette
2. One inline result on the Command Palette home screen by default (0–5 configurable)
3. Details pane with size, modified time and full path
4. Reveal in Explorer, copy path, or open in Everything
5. Start or download Everything when it is not running
6. Bundled MIT-licensed es.exe, zero configuration
7. No network capability, no telemetry, no data collection
8. x64 and ARM64 support

---

## 5. 类别与搜索词（Category & search terms）

- **类别**：`Utilities & tools`（Store 政策要求选择最符合功能的类别；CmdPal 社区目录也把扩展归在 Utilities & tools）。
- **搜索词（Search terms）**：Partner Center 一般最多 7 个、单个长度有限（**TBD**）。建议 7 个：
  `Everything`、`Command Palette`、`PowerToys`、`file search`、`文件搜索`、`本地搜索`、`快速搜索`。
  备选：`CmdPal`、`search`、`Everything 搜索`、`everythingpal`。
- **子类别**：TBD（Partner Center 的 Utilities 下子分类以实际列表为准，选 File Management / Search 类）。

---

## 6. 系统要求与先决条件（Requirements）

| 项 | 要求 |
| --- | --- |
| OS | Windows 11（10.0.19041+ 起可安装；实际功能面向 Win11） |
| 体系结构 | x64、ARM64 |
| PowerToys | 已安装且启用 Command Palette（CmdPal；当前开发环境 0.13.12862） |
| Everything | voidtools Everything 1.4 或 1.5a **已安装并正在运行**（扩展只查询其索引） |
| 网络 | 不需要；应用不声明网络能力 |
| 磁盘 | 约 10 MB（含 .NET 运行时与 es.exe，最终以包为准） |

> 描述和认证备注里必须写明“需要 PowerToys Command Palette + Everything 正在运行”，这是 Store 政策 10.1.1（准确描述限制）与兼容性条款的要求。

---

## 7. 版权 / 商标声明（Copyright & trademark）

- Copyright © 2026 EverythingPal（与包内 `Properties/PublisherDisplayName` 一致；最终以 Partner Center 值为准）。
- 本产品为第三方独立开发的 PowerToys Command Palette 扩展，**与 voidtools 及 Microsoft 无隶属、赞助或背书关系**。
- “Everything” 是 voidtools 的商标；“PowerToys”“Command Palette” 是 Microsoft 的商标。本项目仅在描述性语境中使用这些名称。
- 应用图标为本项目原创（`scripts/generate-icons.py` 生成，见 `assets/icon-inventory.md`），不含 voidtools / Microsoft 的商标或标志。
- 内置的 `es.exe` 来自 <https://github.com/voidtools/ES>（MIT 许可），版权归 voidtools 所有；包内 `THIRD-PARTY-NOTICES.md` 保留完整许可文本。
- 上架前需确认：voidtools 是否允许在 Store 包内再分发 `es.exe`（研究文档标注 **未验证**；MIT 许可允许再分发，但 Everything 本体许可不同，需只依赖 es.exe 的许可条款）。

---

## 8. 支持 URL 与隐私政策 URL

| 项 | 值 | 状态 |
| --- | --- | --- |
| 支持 URL | https://github.com/czz404/EverythingPal/issues | ✅ 仓库已 public，可直接访问 |
| 隐私政策 URL | https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md | ✅ 仓库已 public，可直接访问 |
| 支持邮箱（可选） | TBD（建议公开一个 GitHub Discussions/Issues 之外的邮箱，若无则只留 Issues） | TBD |

> Learn 的 [Privacy policy and support info](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info) 要求：若应用“访问、收集或传输个人信息”或能力声明可能触及个人信息，则必须提供隐私政策 URL；本应用实际不收集数据，但仍建议提供，且不要留空支持信息。

---

## 9. 定价、市场与发布设置

- **定价**：Free（免费），无应用内购买、无订阅、无广告。
- **市场**：全部市场（All markets）。技术前提是 Windows 11 桌面；商店会按设备族过滤，无需手动排除 Xbox/Holo（`TargetDeviceFamily` 声明见 packaging 的 TBD 建议）。
- **发布节奏**：手动发布（不设定时发布），先内部试投，认证通过后手动“Publish now”。
- **年龄分级**：IARC 问卷 → 无内容 → 预计 3+ / Everyone；无账号、无社交、无广告。
- **隐私问卷**：应用不收集/传输数据，如实回答“No”；同时仍填隐私政策 URL。
- **开发者注册**：个人开发者走新的 Store onboarding 流程**免注册费**（需要 Microsoft 账户 + 政府证件/自拍身份验证；必须从 storedeveloper.microsoft.com 进入新流程；公司账号为 $99 一次性；本项目按个人开发者）。参考 [Learn: Free developer registration for individual developers](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer)。
- **可发现性（发布后必做）**：CmdPal 0.13+ 的设置里有内置 **Gallery/库**（Settings → Extensions → Gallery），内容来自 `microsoft/CmdPal-Extensions` 社区目录，并且**链接到该扩展的安装来源（WinGet 或 Microsoft Store）**。Store 上架后请把本扩展提交到 <https://github.com/microsoft/CmdPal-Extensions>（按其 Contributing Guide 提 PR），用户即可在 Gallery 中浏览/一键安装；**WinGet 发布可选但推荐**（Gallery 的安装/更新可走 WinGet）。参考 [Learn: Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery) 与 [Finding and installing Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/finding-and-installing-extensions)。

---

## 10. 截图清单（Screenshots shot list）

要求（已核实）：至少 1 张、最多 10 张桌面截图；`.png`，横向或纵向，≤50 MB；桌面要求 **≥1366×768**（支持 4K）；每张可加 ≤200 字符说明文字。建议提供 4–6 张。截图里的产品文案如果是中文，提交 zh-CN 列表；en-US 列表需要单独上传一份（可同图，说明文字用英文）。UI 已支持中/英双语，若时间允许可为 en-US 列表补拍英文 UI 截图（非必须）。

| # | 画面（文件） | 中文说明（≤200 字符） | English caption |
| --- | --- | --- | --- |
| 1 | 「Everything 搜索」命令页：查询 `wantai` 的真实文件/文件夹结果（`01-search-page.png`，已交付） | 「实时搜索本机文件：结果就是磁盘上的真实文件与文件夹，不是快捷方式」 | "Live local file search: results are the real files and folders on disk, not shortcuts." |
| 2 | CmdPal 主页回退区：默认 **1 行** Everything 结果（`02-home-inline-result.png`，已交付；0–5 可调） | 「在命令面板主页直接搜索文件名，默认显示 1 条 Everything 结果（0–5 行可调；需在 CmdPal 设置中启用回退命令）」 | "Search from the Command Palette home screen — one Everything result by default (0–5 configurable; enable the fallback row in Command Palette settings)." |
| 3 | 结果右键菜单：打开文件夹、资源管理器定位、复制路径、在 Everything 中定位、显示详细信息（`03-context-menu.png`，已交付） | 「回车打开；右键可打开文件夹、资源管理器定位、复制路径，或在 Everything 中定位」 | "Open with Enter, or open the folder, reveal in Explorer, copy the path, or locate it in Everything." |
| 4 | 无匹配结果状态：`没有匹配结果 换个关键词，或检查 Everything 的索引设置。`（`04-zero-result.png`，已交付） | 「没有匹配结果时给出明确提示，而不是空白列表」 | "A clear empty-state message when nothing matches, instead of a blank list." |
| 5（可选） | 扩展设置页：结果条数、排序、主页 fallback 行数（默认 1，0–5 可调）、es.exe 路径 | 「可调结果条数、排序与主页显示行数（默认 1 行，可 0–5），支持自定义 es.exe 路径」 | "Configure result count, sort order, home-screen rows (default 1; 0–5) and an optional es.exe path." |
| 6（可选） | CmdPal 设置 → 扩展 → Everything Search → 回退命令开关（含 Include in Global result） | 「在 Command Palette 设置中启用第三方回退命令，即可在主页看到内联结果」 | "Enable the extension's fallback rows in Command Palette settings to get inline home-screen results." |
| 7（可选） | Everything 未运行时的错误提示 + 「启动 Everything」「下载 Everything」按钮 | 「Everything 未运行时给出可操作提示：一键启动或前往下载」 | "Actionable error when Everything is not running: start it, or go download it." |

已交付截图（QA，2026-10-20，均 1366×768）：`01-search-page.png`、`02-home-inline-result.png`、`03-context-menu.png`、`04-zero-result.png`（另有同名 `-fullscreen` 2560×1440 备份；提交用 1366×768 版本即可）。可选的第 5–7 张若需补拍：

| 文件 | 画面 | 命令 / 方式 |
| --- | --- | --- |
| `05-extension-settings.png` | 设置页 | 手工：命令 → 右键 → 设置（设置页）后截取 |
| `06-fallback-settings.png` | CmdPal 回退命令开关 | 手工：CmdPal 设置 → 扩展 → Everything Search → 回退命令 |
| `07-everything-not-running.png` | Everything 未运行错误态 | 手工：退出 Everything 后跑 `-Mode page` 截取（完成后重新启动 Everything） |

拍摄注意：
- **主页截图按默认 1 行**：`02-home-inline-result.png` 已由 QA 用 0.2.0.0 包 + `-Mode home -SelectFirst -Backdrop -Names 02-home-inline-result` 生成；如需重拍，勿再描述成“3 行”。行数可在扩展设置页调为 0–5，默认 1。
- 首图（`01-search-page.png`）用 `-Mode page`，不依赖 fallback 开关；`-Backdrop` 用中性背景避免桌面隐私入镜（CmdPal 窗口有透明阴影边距），`-SelectFirst` 选中首项。
- 截图脚本会校验 ≥1366×768 并写 `_capture-manifest.json`；最终 PNG 只保留在 `docs/store/screenshots/`。
- 关键内容放在画面上 2/3（Store 可能在底部叠加文字）；不要加第三方 logo/水印/营销文字；避免过亮/过暗或高对比条纹。
- 建议 100% 缩放、窗口 1366×768 或更大；截图需能看清中文（若最终 UI 为中文，zh-CN 列表优先）。
- 截图里不要出现作者个人路径/用户名等隐私信息（如 `C:\Users\<真实姓名>`）。**注意：已交付的 `01-search-page.png` / `03-context-menu.png` 含本机路径 `C:\Users\Lenovo\...`（来自 Everything 索引结果）；Store 审核通常可接受，若想更干净，可重拍时用测试账户或换不含用户目录的查询。**

---

## 11. 商店图标（Store listing icon）

- 文件：`docs/store/assets/store-icon-300.png`（300×300 PNG，透明圆角，1:1）。
- 已核实：微软建议提供 1:1 App tile icon（300×300），且 Store 会**优先使用上传图**而不是包内图标；PNG、≤50 MB。
- 包内图标仍必须有效（`StoreLogo.png` 50×50 等），见 `assets/icon-inventory.md`。

---

## 12. 待确认（TBD）清单

| # | 项 | 说明 / 负责人 |
| --- | --- | --- |
| 1 | 产品名最终选择、是否被 Partner Center 拒绝 | Lead / 用户 |
| 2 | Short description 硬上限 | 提交时看 Partner Center 提示 |
| 3 | Product features 条数与单条长度上限 | 提交时看字段提示 |
| 4 | 搜索词数量/长度上限 | 提交时看字段提示 |
| 5 | ~~隐私政策 URL、支持 URL~~（已解决：仓库已 public） | — |
| 6 | Publisher display name / 作者署名 | 用户（与包内 `PublisherDisplayName` 一致） |
| 7 | 最终截图 | 已交付 4 张（`01-search-page` / `02-home-inline-result` / `03-context-menu` / `04-zero-result`，均 1366×768）；可选补设置页/错误态 |
| 8 | 上架语言组合（zh-CN + en-US 或仅其一） | 用户 |
| 9 | voidtools 对再分发 es.exe 的书面确认 | 用户 / Lead |
| 10 | 子类别选择 | 提交时决定 |
| 11 | Store 上架后提交 CmdPal 社区 Gallery（`microsoft/CmdPal-Extensions`） | 发布后必做；决定是否同时发 WinGet（Lead / 用户） |
| 12 | 个人开发者注册（新流程免费）与身份验证 | 从 storedeveloper.microsoft.com 进入；提交前（用户） |

---

## 13. 引用与核实（References）

已通过 web 抓取/搜索核实的 Microsoft Learn 页面：

- [Add app screenshots, images, and trailers for MSIX apps](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images) — 截图 ≥1、≤10 桌面、≥1366×768、PNG ≤50 MB、说明文字 ≤200 字符；1:1 App tile icon 300×300 推荐且优先于包内图标；Store logo 必填。
- [App package requirements for MSIX app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements) — 接受的包格式（.msix/.msixbundle/.msixupload 等）；**未签名包提交后由 Store 重新签名**；版本号四段、第四段提交时置 0；bundle 内各包除 ProcessorArchitecture 外 manifest 必须一致；支持语言清单（zh-Hans、en-US 均支持）。
- [Publish to Microsoft Store（PowerToys Command Palette）](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-store) — 官方说明 Command Palette 扩展可发布到 Microsoft Store；包内图标尺寸清单；建议在描述里写 “integrates with the Windows Command Palette to …”，并在 Additional testing information 写 PowerToys 前提与测试步骤。
- [App capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations) — 包当前只声明 `runFullTrust`（打包 out-of-process COM server 必需）；`unvirtualizedResources` 与 `desktop6:*WriteVirtualization` 已移除并实测通过，提交时只需解释 `runFullTrust`。
- [Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery) — CmdPal 内置 Extension Gallery（Settings → Extensions → Gallery），目录由 `microsoft/CmdPal-Extensions` 驱动，安装来源为 WinGet 或 Microsoft Store；提交 PR 即可上架 Gallery。
- [Finding and installing Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/finding-and-installing-extensions) — Gallery 浏览/安装流程（安装、更新、卸载走 WinGet）。
- [Free developer registration for individual developers](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer) — 个人开发者 onboarding 免注册费（原注册费已取消），需身份证明 + 自拍验证；公司账号 $99 一次性。
- [Microsoft Store Policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies) — 10.1.1 描述准确、10.1.2 完全可用/兼容性要求。

**未核实 / 需现场确认（TBD）**：Short description、Product features、搜索词的硬上限；Partner Center 子类别 id；voidtools 再分发许可；headless 扩展的实际认证结果（见 `README.md` 风险与 `docs/research/platform-and-store-research.md`）。


