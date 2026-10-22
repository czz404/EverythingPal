# PowerToys 0.102 更新评估 — EverythingPal 是否需要更新

> 评估日期：2026-10-22（Asia/Shanghai）
> 结论：**PowerToys 0.102（CmdPal 0.13.12934.0）是宿主侧更新；EverythingPal 不需要更新。**
> 现有 Store 版 **1.0.2.0**（SDK 0.12.260812002 编译）在新宿主上实测可正常加载与工作；无需改代码、无需重编译/重打包、无需重新提交 Store 或 Gallery。
> 证据笔记：`docs/research/notes/powertoys-0.102-cmdpal.md`（官方 release notes/PR/NuGet/ABI 调研）、`docs/research/notes/everythingpal-api-audit.md`（源码审计/构建）、`docs/qa/notes/powertoys-0.102-runtime.md`（本机运行时验证）。

## 1. 事实核对（本机 + 官方）

| 项 | 更新前（项目记录） | 现在（本机实测） |
|---|---|---|
| PowerToys | 0.102.2862（preview） | **0.102.2933.0**（正式版，官方 2026-10-21 发布；本机 2026-10-22 16:55 更新落地） |
| CmdPal 宿主包 | `Microsoft.CommandPalette_0.13.12862.0_x64__8wekyb3d8bbwe` | **`Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`** |
| host ABI | `0.13.261013002` | **`0.13.261020004`**（`BuildInfo.xml` 的 `SemanticVersion`，包内 `Microsoft.CommandPalette.Extensions.dll` ProductVersion 一致） |
| 扩展 SDK | `Microsoft.CommandPalette.Extensions` 0.12.260812002 | 不变；**仍是 NuGet 最新稳定**（截至 2026-10-22 无 0.13.x 包） |
| EverythingPal | Store 1.0.2.0 | 不变（PFN `EverythingPal.EverythingPal_33w78pmn50ga4`） |

0.102 更新后本机 CmdPal 主日志（`%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\Logs\0.102.2933.0\`）明确记录：

```
16:55:25.581 Started extension EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 426 ms
16:55:25.769 Loaded 2 command(s) and 0 band(s) from EverythingPal... in 85 ms
17:05:31.199 Started extension EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 268 ms
17:05:31.375 Loaded 2 command(s) ... in 79 ms
```

扩展日志 `%LOCALAPPDATA%\EverythingPal\Logs\extension.log` 在更新后（10-22 17:16）有 `fallback result: Ok, 1 item(s)`，无更新后异常。

## 2. PowerToys 0.102 更新了什么

官方 release notes：<https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0>

### 2.1 与插件无直接关系的部分（一句话带过）

两个新鼠标工具（Mouse Button Lock、CursorHide）、ZoomIt 截图听写、Advanced Paste/Light Switch/Settings/Peek 等 CLI、Keyboard Manager 单击重映射、Color Picker/Text Extractor 迁 WinUI 3、FancyZones 滚轮换布局、Peek SQLite 预览、Screen Ruler DIP 单位、环境变量去重、Quick Accent 字符表、Grab and Move、Shortcut Guide、Workspaces、Awake、New+、Settings 动画等。

### 2.2 Command Palette（CmdPal）本体

产品层（用户可见，扩展自动受益）：

- **Quick-access shelf（快速访问栏）**：紧凑模式下把固定命令放在搜索框下方，`Alt+1..9` 启动、`Alt+Shift+数字` 选中不运行，可拖拽排序/右键操作；Home 与 shelf 可选显示“最近命令”。
- **设置搜索**：模糊搜索设置项、扩展、命令、别名、快捷键，并直接跳转。
- **键盘优先 Dock**：新增 `Win+Alt+J` 聚焦/切换 Dock 项；Dock 增加右键菜单（设置/日志/重载扩展/退出）。
- **Copilot 键支持、多时钟 Dock、应用语言选择、命令/页面可复制深链**。
- **打磨**：更清晰的图标、更快网格/列表、列表尾部继续加载、嵌套菜单可返回、`Alt+数字` 直接运行列表结果等。

扩展相关修复/新增（逐条与本插件的关系见第 3 节）：

| PR | 内容 | 对扩展的意义 |
|---|---|---|
| #51232 | CmdPal 包版本推进到 0.13（`VersionMinor` 0.12→0.13） | 解释 host ABI 0.13 的来源；扩展无需跟随 |
| #50047 | fallback 设置按 `IFallbackCommandItem.Id` 持久化；修复 PowerToys 内置 fallback 共用同一 Id 导致“关一个全关” | 我们每行 Id 已唯一，不受影响 |
| #48746 | Extension Gallery installed 检测新增 Pass 3：Store-only 扩展按 Store ID 查 WinGet composite catalog；长期正解是 feed 加 `detection.packageFamilyName` | 我们是 Store-only，未来进 Gallery 后能正确显示“已安装” |
| #50178 | SDK 新增 `IFormContent2` / `SubmitAction`（表单动作 ID） | 我们不用表单；需 0.13 SDK |
| #50180 | SDK 新增 `IDetails2`（details 面板可嵌 Adaptive Cards/`IContent`） | 我们不用 `ContentPage.Details`；需 0.13 SDK |
| #50190 / #50191 | 宿主新图标协议：`\|Swatch\|`、`\|Initials\|`、`\|Svg\|`、`\|ThemedSvg\|`（旧 SDK 的 `IconInfo(string)` 即可用，仅新宿主解析） | 可选，不必现在用（旧宿主会当普通字符串） |
| #50832 | Toolkit 剪贴板 helper 增强（图片/自定义格式、写失败不再假成功） | 我们自写 Win32 剪贴板，不依赖；需新 Toolkit |
| #51231 | 修复 `ContentPage.Details` 被扩展替换后详情面板空白 | 我们用 `ListItem.Details`，不在该路径 |
| #50997 | 修复 Native AOT 下扩展表单/Adaptive Cards 的 marshalling | 我们 trimmed 非 AOT，且无表单 |
| #50938 | 动态刷新子页面上按 Enter 跟随更新后的首项，不覆盖用户后来选择 | 我们的结果页是动态列表，净收益 |
| #50996 | 双击列表项运行被点中的项，而不是上一次选择 | 搜索结果页操作更可靠 |
| #50994 / #50651 | 启动应用后 CmdPal 不再抢回焦点；重复扩展命令激活后窗口可重新获得焦点 | 通过本插件打开文件/文件夹后体验更好 |
| #51285 / #51287 | 首屏不满时继续加载批次；键盘导航到列表末尾时加载下一批 | 我们一次返回全部结果，暂不涉及；未来分页可用 |
| #51290 | 修复上下文菜单子菜单中 Ctrl+Enter 指向错误命令 | 我们的 `MoreCommands` 右键菜单受益 |
| #50981 / #51034 / #51038 等 | 宿主键盘/命令路由内部重构 | 扩展无破坏性变化 |

### 2.3 扩展 SDK/ABI

- 宿主 winmd 与 NuGet SDK 0.12.260812002 的 winmd 同为 14,848 B；类型名差集显示**宿主只多 `IDetails2`、`IFormContent2`（+`SubmitAction`），无删除/改名**，旧接口（`ICommandProvider`、`IFallbackCommandItem`、`IContentPage`、`IDetails`、`IIconInfo` 等）全部保留。
- 结论：**0.13 宿主是 0.12 SDK 的严格超集**，按 0.12 SDK 编译的扩展二进制可直接加载；无需重编译。
- 0.13 新接口尚未发布 NuGet 包，现在也无法“正规升级”SDK；旧包继续用是正确的。

## 3. 对本插件的实际影响

| 主题 | 我们的实现 | 0.102 影响 | 需要改吗 |
|---|---|---|---|
| ABI/激活 | `IExtension` + `CommandProvider`，MSIX `windows.comServer` + `windows.appExtension`（`Package.appxmanifest:60-85`） | 0.13 宿主接口超集；激活方式未变 | ❌ 不需要 |
| fallback 行 | 每行 `FallbackCommandItem`，Id = `czz404.everythingpal.fallback.{index}`（`Commands/EverythingFallbackItem.cs:167-170`） | #50047 修的是内置 fallback 共享 Id；我们每行已唯一 | ❌ 不需要 |
| 真实文件图标 | `ShellIconProvider` 每扩展名缓存 32px PNG，经 `IconInfo.FromStream` 传给宿主（`Services/ShellIconProvider.cs:218-232`） | 宿主图标缓存/队列/回收修复（#50181–#50192）让图标加载更快、更少闪烁/错位；旧 PNG 通道不变 | ❌ 不需要；自动受益 |
| 搜索结果页 | `DynamicListPage` + `ListItem` + `Details`（`Pages/EverythingPalPage.cs:16-214`） | #50938/#50996/#51290 修复动态列表选择/双击/上下文菜单；#51231 修的是 `ContentPage.Details`，我们走 `ListItem.Details` | ❌ 不需要；自动受益 |
| 详情面板 | 提供名称/路径/类型/大小/修改时间（`EverythingPalPage.cs:165-186`） | 0.102 若顺带修好详情面板渲染，现有元数据会自动出现；否则仍取决于宿主 | ❌ 不需要；可实机复查 |
| 打开文件后的焦点 | `OpenItemCommand` / `RevealInExplorerCommand` 等 | #50994/#50651 让 CmdPal 不再抢焦点、已启动窗口可重新获得焦点 | ❌ 不需要；体验更好 |
| 设置页/别名 | `JsonSettingsManager`，ProviderId = `PFN!ExtensionId` | 宿主“设置搜索/深链”能搜到我们的设置与命令；ProviderId 未变，fallback/pin/alias 保留 | ❌ 不需要 |
| 剪贴板 | 自写 Win32 `OpenClipboard/SetClipboardData`（`Commands/ItemCommands.cs:116-201`） | #50832 只改 Toolkit 剪贴板 helper；我们不受影响 | ❌ 不需要 |
| 表单/Adaptive Cards/Dock | 未使用 | 新 API 需 0.13 SDK，且产品不需要 | ❌ 不需要 |
| 本地化 | `CurrentUICulture` + `EVERYTHINGPAL_LANG`，清单声明 en-US/zh-Hans | 0.102 新增宿主“应用语言”；是否影响进程外扩展未确认（无扩展本地化 API 变化） | ❌ 不需要；观察 |
| Gallery 可见性 | PR #207 已合并；`extension.json` 含 Store ID + `detection.packageFamilyName` | 当前 feed（2026-10-12，91 条）还没 regenerate，所以 Gallery 仍看不到我们；#48746 修的是“收录后 installed 检测” | ❌ 插件不改；等 feed 刷新 |
| SDK 升级 | 引用 0.12.260812002（NuGet 最新） | 0.13 新接口未发布；现无升级路径 | ❌ 不需要（等 SDK 发布再评估） |

## 4. 结论：是否需要更新插件？

**不需要。** 具体理由：

1. **兼容性**：0.13 宿主 ABI 相对 0.12 SDK 是纯增量，现有 1.0.2.0 二进制可直接运行；本机宿主日志已证明加载、命令与 fallback 均正常。
2. **没有可升的 SDK**：NuGet 上 `Microsoft.CommandPalette.Extensions` 仍只有 0.12.260812002；0.102 新增的 `IDetails2`/`IFormContent2` 不在包内，无法也不应现在升级。
3. **没有必须改的代码**：本插件未使用 0.102 改动到的 `ContentPage.Details`、Toolkit 剪贴板、Adaptive Cards/表单、Dock/band、命令参数等路径；fallback Id 已唯一，不需要 #50047 的适配。
4. **不需要重新上架**：CmdPal 与扩展是两个独立 MSIX 包；0.102 没有要求扩展重新提交 Store，也没有提升扩展清单/权限要求。Store 包不依赖宿主版本。
5. **Gallery 看不到 ≠ 需要更新**：PR #207（2026-10-21 合并）后只差维护者重新生成 `extensions.json`；这是上游 feed 刷新问题，与本插件或 0.102 无关。我们的条目已填 `detection.packageFamilyName`（Pass 1 秒判），正好利用 #48746 推荐的识别路径。

## 5. 验证证据摘要

- **运行时**：更新后宿主两次启动都成功拉起扩展并读到命令（`Started extension … 426 ms / 268 ms`，`Loaded 2 command(s) … 85 ms / 79 ms`）；扩展日志 `fallback result: Ok, 1 item(s)`；更新后无 Error/Exception/COM 失败。
- **ABI**：宿主 winmd 相对 SDK 0.12 仅多 `IDetails2`/`IFormContent2`/`SubmitAction`，0 删除；宿主二进制含 `GetStorePackagesByIdAsync`、`FallbackRanks`、`IDetails2` 等符号，证明 #48746/#50047 代码路径存在。
- **搜索冒烟**：把已安装包载荷复制到 workspace 外运行 `dotnet EverythingPal.dll --search-test readme` → `status: Ok`，返回真实 FILE/DIR（首次失败仅因本机 Everything 客户端未启动，启动后正常）。
- **构建**：workspace 外纯净副本 `dotnet build src\EverythingPal\EverythingPal.csproj -c Debug` → **0 警告 0 错误**（44.8s）；测试项目也可编译。仓库内 `.slnx` 构建失败是本机 SDK 缺 workload locator 目录 + 10/21 提权构建遗留 obj ACL 的环境问题，与仓库无关；非提权 `dotnet test` 仍会 `Win32Exception(5)`（需提权，历史记录 118/118）。

## 6. 建议的后续（均非必须）

1. **等 Gallery feed 刷新后验证**：在 CmdPal Gallery 点一次 Refresh，确认 EverythingPal 条目出现且显示“已安装”。若长时间未刷新，可在 `microsoft/CmdPal-Extensions` 提醒维护者 regenerate（PR #207 已合并）。
2. **实机看一眼详情面板**：旧记录（0.13.12862）连内置项都不渲染详情；0.102 有相关修复。若现在能显示，可考虑更新商店截图/文案里“详情面板取决于宿主版本”的免责声明（`docs/store/listing.md:243,273`）。
3. **等 0.13 SDK 发布再评估新能力**：`IDetails2`（details 内嵌 Adaptive Cards）、新图标协议（`|Svg|`/`|ThemedSvg|`/`|Swatch|`）、表单动作 ID。届时若要改用，需要升 SDK → 重编译 → 重新打包/提交；且图标协议要做宿主能力探测 + PNG 回退，避免旧宿主异常。
4. **如果将来发维护版（如 1.0.3.0）**，审计发现两处与 0.102 无关的小改进可顺手做：
   - `src/EverythingPal/Services/EverythingPalSettings.cs:76`：`FallbackRows` 解析失败时回退 **3**，与默认值/文档的 **1** 不一致（一行修复；仅在设置文件损坏/误输入时可见）。
   - 可选：图标从 32px 升级到 `ThumbnailHelper.GetThumbnail(jumbo: true)` 256px，或宿主 `|ShellItemIcon|` 协议（后者需回退）。
5. **文档同步**：`docs/store/listing.md`、`docs/store/support.md`、`docs/store/README.md` 里 “Store live 1.0.1.0 / 开发环境 0.13.12862” 的提交时快照已过时；`HANDOVER.md` 已在本 session 更新。历史验证记录（`docs/qa/*`、`docs/research/platform-and-store-research.md`）保留不动。

## 7. 来源

- PowerToys 0.102.2933.0 release：<https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0>
- 关键 PR：<https://github.com/microsoft/PowerToys/pull/51232>、<https://github.com/microsoft/PowerToys/pull/50047>、<https://github.com/microsoft/PowerToys/pull/48746>、<https://github.com/microsoft/PowerToys/pull/50178>、<https://github.com/microsoft/PowerToys/pull/50180>、<https://github.com/microsoft/PowerToys/pull/50190>、<https://github.com/microsoft/PowerToys/pull/50191>、<https://github.com/microsoft/PowerToys/pull/50832>、<https://github.com/microsoft/PowerToys/pull/51231>、<https://github.com/microsoft/PowerToys/pull/50997>
- NuGet SDK 版本：<https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json>
- CmdPal Gallery feed：<https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/extensions.json>；提交 PR：<https://github.com/microsoft/CmdPal-Extensions/pull/207>
- 本机宿主包：`C:\Program Files\WindowsApps\Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`（`BuildInfo.xml`、`Microsoft.CommandPalette.Extensions.{dll,winmd}`）
- 详细证据：`docs/research/notes/powertoys-0.102-cmdpal.md`、`docs/research/notes/everythingpal-api-audit.md`、`docs/qa/notes/powertoys-0.102-runtime.md`
