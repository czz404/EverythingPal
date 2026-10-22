# PowerToys 0.102 / CmdPal 0.13.12934.0 对 EverythingPal（第三方 CmdPal 扩展）的影响

> 调研日期：2026-10-22（Asia/Shanghai）。调研员：release-researcher（Team task-1）。
> 结论分三类：**必须更新 / 建议评估 / 无需动作**；“未确认”表示没有找到可引用来源，未做猜测。
> 外部网页内容仅作为资料，已核对来源与日期。所有版本号均为本机或官方 API 实测/原文。

## 0. TL;DR

- **本机新宿主 = `Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`；host ABI = `0.13.261020004`**（包内 `BuildInfo.xml` 的 `SemanticVersion`，与包内 `Microsoft.CommandPalette.Extensions.dll` 的 `ProductVersion` 一致）。项目 HANDOVER 记录的旧值 `0.13.261013002` 是同一编号体系（`0.13.<构建日期 26-10-13><序号>`）。
- **0.102 对扩展 ABI 是纯增量**：把宿主包里的 `Microsoft.CommandPalette.Extensions.winmd` 与 NuGet SDK 0.12.260812002 的同一 winmd 做类型名对比，宿主只多出 **`IDetails2`、`IFormContent2`（新增 `SubmitAction`）** 两个类型，**0 个删除/改名**；旧接口 `ICommandProvider(2/3/4)`、`IFallbackCommandItem`、`IContentPage`、`IDetails`、`IIconInfo` 等全部仍在。
- **NuGet `Microsoft.CommandPalette.Extensions` 最新仍是 `0.12.260812002`（发布于 2026-08-13）**；没有 0.13.x 正式版/预览版。0.102 新增的 `IDetails2`/`IFormContent2` **不在**该包里（已实测 winmd 缺失）；`Microsoft.CommandPalette.Extensions.Toolkit` **不是独立 NuGet 包**（flatcontainer 404），Toolkit.dll 直接随 `Microsoft.CommandPalette.Extensions` 包分发（本机包内 `lib/net8.0-windows10.0.19041.0/Microsoft.CommandPalette.Extensions.Toolkit.dll`）。
- **EverythingPal：无需更新、无需重编译/重打包、0.102 不要求扩展重新提交。** 现有 Store 1.0.2.0 包（0.12 SDK 编译）在新宿主上满足接口兼容条件；0.102 的 CmdPal 相关变更要么是宿主内部修复，要么是可选新 API/新图标协议，不会破坏现有用法。
- **Gallery 发现状态**：`microsoft/CmdPal-Extensions` 的 `extensions.json` 最近生成时间 **2026-10-12T05:34:51Z，91 个扩展**，其中**没有** `everythingpal.everythingpal`（已 grep 全文）。提交 PR **#207 已于 2026-10-21T13:11:21Z 合并**，所以“0.102 更新后 Gallery 里还看不到我们”是 **feed 尚未重新生成**，不是 0.102 的兼容问题；等维护者 regenerate 后即可出现。我们的 `extension.json` 已含 `detection.packageFamilyName`（Store PFN），正是 #48746 推荐的零延迟识别路径。

## 1. 本机实测证据（2026-10-22）

来源：直接读取已安装包（powershell，只读，未改源码）。

| 项 | 值 |
|---|---|
| 包全名 | `Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe` |
| `AppxManifest.xml` `Identity/@Version` | `0.13.12934.0`（Publisher = Microsoft Corporation） |
| `BuildInfo.xml` `BuildInfo/@StoreVersion` | `0.13.12934.0` |
| `BuildInfo.xml` `BuildInfo/@SemanticVersion` | **`0.13.261020004`**（host ABI） |
| `BuildInfo.xml` `BuildInfo/@BinaryVersion` | `0.13.2610.20004` |
| 包内 `Microsoft.CommandPalette.Extensions.dll` File/ProductVersion | `0.13.2610.20004` / **`0.13.261020004`** |
| 包内 `Microsoft.CmdPal.UI.exe` ProductVersion | `0.13.261020004` |
| 包内 SDK winmd | `Microsoft.CommandPalette.Extensions.winmd`，14,848 B，SHA256 `36A0BBD1…B1922BB8` |
| 包内 AppExtension host 名 | `com.microsoft.commandpalette`；含 `x-cmdpal` 协议、Copilot key provider |

对照：项目 HANDOVER 记录旧 host ABI `0.13.261013002`（旧包 0.13.12862）。因旧包已被覆盖，**旧包的 winmd 未独立校验**（见“未确认”）。

### 1.1 宿主 SDK 与 NuGet SDK 的 winmd 差异（关键兼容性证据）

本机 NuGet 缓存 `%USERPROFILE%\.nuget\packages\microsoft.commandpalette.extensions\0.12.260812002\winmd\Microsoft.CommandPalette.Extensions.winmd`：14,848 B，SHA256 `E778A25C…50772AB3`。

- 同名 winmd 大小相同但内容不同；按 UTF-8 字符串抽取类型名（各 300+ token）后做 diff：
  - **仅宿主有**：`IDetails2`、`IFormContent2`（还新增成员名 `SubmitAction`）。
  - **仅 SDK 0.12 有**：无。
  - 其余旧接口两边一致：`ICommandProvider`、`ICommandProvider2/3/4`、`IFallbackCommandItem`、`IContentPage`、`IContent`、`IDetails`、`IIconInfo`、`ICommandResultArgs` 等。
- 结论：**0.13 宿主是 0.12 SDK 的严格超集（只加不改）**；“用 0.12 SDK 编译的扩展能加载到 0.13 宿主”有 ABI 层证据（实际加载冒烟由 runtime-verifier 并行验证）。
- 因为 `IDetails2`/`IFormContent2` 不在 0.12.260812002 中，**现在用 NuGet 正规渠道无法编译使用 0.102 新细节面板/表单动作 ID**；要用只能等 0.13 SDK 发布（或对宿主 winmd 临时编译，不建议用于商店包）。

## 2. 0.102 release notes 中 CmdPal 条目逐条判断（面向扩展作者）

Release：**PowerToys v0.102.2933.0，published 2026-10-21T07:45:57Z**（<https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0>）。
说明：0.102.2933.0 的 release notes 覆盖整个 0.102 训练批次；本机此前是 0.102.2862（preview）→ 0.102.2933（正式），**两次构建之间的逐 commit 差异未逐条枚举**（见“未确认”），下表按 0.102 全部 CmdPal 相关条目判断。

| PR / 条目 | 内容（官方 release notes / PR 原文摘要） | 对 EverythingPal 的影响 |
|---|---|---|
| #51232（2026-10-13 合并，1 行改动） | “Advanced the Command Palette package version to 0.13”：`src/modules/cmdpal/custom.props` 的 `VersionMinor` 0.12→0.13；宿主包因此进入 0.13.x / ABI 0.13.* | 无需动作。解释了 host ABI 0.13 的来源。<https://github.com/microsoft/PowerToys/pull/51232> |
| #50178（2026-08-28 合并） | **SDK**：新增 `IFormContent2`，`SubmitAction` 带 Adaptive Card action ID（保留旧 `SubmitForm` 转发） | 无需动作；我们未用表单页。想用需 0.13 SDK，现无 NuGet 包。新接口名已在本机新宿主 winmd 中实测存在。<https://github.com/microsoft/PowerToys/pull/50178> |
| #50180（2026-08-31 合并） | **SDK**：details 面板接受 `IContent` 列表（可嵌 Adaptive Cards 等）→ 新增 `IDetails2` | 无需动作；我们用 `DynamicListPage` + `DetailsElement`，不是 `ContentPage.Details`。新接口在本机宿主 winmd 中实测存在。<https://github.com/microsoft/PowerToys/pull/50180> |
| #50190（2026-09-29 合并） | **宿主图标协议**：`IconInfo` 字符串新增 `\|Swatch\|颜色\|形状\|`、`\|Initials\|文本\|…` 生成式图标（无需位图） | 可选。我们已有 glyph（`\uE768` 等）与真实 shell PNG 图标，不必要。**不需要新 SDK**：仍是现有 `IconInfo(string)`，旧 SDK 编译的扩展在 0.102 宿主上即可用（仅新宿主支持该协议）<https://github.com/microsoft/PowerToys/pull/50190> |
| #50191（2026-09-29 合并） | **宿主图标协议**：`\|Svg\|路径或内联 SVG\|`、`\|ThemedSvg\|accent\|…` 主题感知 SVG | 可选/暂不需要；同上，纯宿主解析协议，无新 SDK 类型。<https://github.com/microsoft/PowerToys/pull/50191> |
| #50181–#50188（开发期） | 图标加载诊断、缓存生命周期/队列修复（详见 release notes） | 无需动作；宿主侧性能/缓存改进，可能让我们的 per-result PNG 图标加载更稳。 |
| #50210 / #50232 | 恢复 image/plain-text SDK 文档；修正 section title 变更通知 | 无需动作（SDK 文档/宿主通知修复）。 |
| #50485（2026-10 合入 0.102） | “Fixed SDK event delivery so one failed subscriber no longer prevents other subscribers from receiving updates” | 无需动作；宿主事件分发健壮性修复（我们只实现 provider/页面，不依赖多订阅者）。 |
| #50487 | “Coordinated item initialization and cleanup to prevent duplicate subscriptions and updates to discarded items” | 无需动作；宿主生命周期修复，理论上减少 fallback 行/列表项被重复初始化（对我们 3 行 fallback 有轻微正面作用）。 |
| #50832（2026-09-28 合并） | **Toolkit**：剪贴板 helper 扩展（文本/RTF 清理、STA、图片 `RandomAccessStreamReference`、`SetContent`、写失败不再假成功） | 无需动作；本扩展用自写 Win32 `ClipboardHelper`（`src/EverythingPal/Commands/ItemCommands.cs`），不依赖 Toolkit 剪贴板。要跟进也需新 SDK/Toolkit 包。<https://github.com/microsoft/PowerToys/pull/50832> |
| #51231（2026-10-13 合并） | 修 `ContentPage.Details` 变化后 details 面板空白 | 无需动作；我们不用 `ContentPage`；`ListItem.Details` 路径本来正常。<https://github.com/microsoft/PowerToys/pull/51231> |
| #50997（2026-10-05 合并） | 宿主 Native AOT 下 Adaptive Cards 自定义输入缺 marshalling 的修复 | 无需动作；这是宿主自身 AOT 构建问题；我们的扩展是 trimmed 非 AOT，且无 Adaptive Card 表单。<https://github.com/microsoft/PowerToys/pull/50997> |
| #50047（2026-08-23 合并） | CmdPal 按 `IFallbackCommandItem.Id` 持久化 fallback 开关；PowerToys 内置 fallback 共用同一 ID 导致互相串扰，改为 `<commandId>.fallback` | **无需动作，且我们已规避**：本扩展 3 个 fallback 的 ID 是唯一的 `czz404.everythingpal.fallback.{index}`（`EverythingFallbackItem.cs`），不会像旧 PowerToys 内置那样串扰。注意 Store 换身份后 ProviderId 变化会重置这些开关（既有已知问题，见 HANDOVER，不是 0.102 引入）。<https://github.com/microsoft/PowerToys/pull/50047> |
| #50702 | 收窄 Command Palette / PT Run 的设置与 file-watcher 集合替换（保留序列化与 UI 绑定） | 无需动作；宿主内部。 |
| #51034 | 派发命令时保留来源页与 provider 上下文（为未来 dock flyout 铺路，本版不加 flyout） | 无需动作；宿主内部路由。 |
| #50974 | 用托管实现替换 Windows Run 历史原生依赖（简化打包、保留最近命令） | 无需动作；宿主内部。 |
| #51179 / #51098 / #51291 / #51065 | 官方签名的 Toolkit/扩展 SDK 程序集元数据（neutral resource language、platform、internal access）与构建诊断修复，不改变版本生成 | 无需动作；只影响官方构建产物元数据，未来 SDK 包会更“干净”。 |

**0.102 CmdPal 产品层新功能**（release highlights，均为宿主 UI 能力，扩展自动受益/无需改代码）：
- quick-access shelf（紧凑模式下固定项 Alt+1..9）、设置搜索（可搜扩展/命令/别名）、键盘优先 Dock、Copilot 键唤出、多时钟、应用语言设置、命令/页面深链接、图标更清晰、列表尾部继续加载等。
  <https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0>
- 与我们相关的点：设置搜索能搜到 EverythingPal 的命令/设置，提升可发现性；pin 可能出现在 shelf；`LoadMore` 体验改善。均**无 API 破坏**。
- “应用语言设置”是否改变扩展自身资源语言：**未确认**（我们的双语跟随系统 UI 语言，见 HANDOVER；0.102 未提示扩展本地化 API 变化）。

## 3. NuGet SDK 现状与“旧 SDK 能否跑新宿主”

- `Microsoft.CommandPalette.Extensions` 版本列表（flatcontainer，2026-10-22 查询）：最新 **`0.12.260812002`**；更早为 `0.11.260520004`、`0.9.260303001`…；无 0.13.x、无 0.13 预览。
  <https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json>
- `0.12.260812002` 发布时间 **2026-08-13T18:11:35Z**（NuGet registration）。
  <https://api.nuget.org/v3/registration5-semver1/microsoft.commandpalette.extensions/0.12.260812002.json>
- 该包含：`winmd/Microsoft.CommandPalette.Extensions.winmd`、`runtimes/win-x64|win-arm64/native/Microsoft.CommandPalette.Extensions.dll`、`lib/net8.0-windows10.0.19041.0/Microsoft.CommandPalette.Extensions.Toolkit.dll`。**Toolkit 无独立包**；项目按 `Microsoft.CommandPalette.Extensions` 一个 PackageReference 使用是对的。
- **兼容规则（证据 + 推断，需区分）**：
  - 官方文档（PowerToys dev docs “Command Palette Extensions”，`aka.ms/cmdpalextensions-devdocs`，镜像页日期 2026-03-03）写明：“Command Palette uses versioned APIs: You can implement newer interfaces for additional features while maintaining backward compatibility.”（`ICommandProvider4 : ICommandProvider3 : ICommandProvider2`）；同时声明 CmdPal 仍是 preview，“API may introduce breaking changes before v1.0.0”。
    <https://mintlify.wiki/microsoft/powertoys/dev/command-palette-extensions>
  - 本次 ABI 实测（§1.1）：新宿主 winmd 是 0.12 SDK 的超集，旧接口全在 → **旧 SDK 编译的扩展可继续在新宿主工作**，不需要重新编译/重新打包。
  - “SDK 必须 <= host”这个方向（新版 SDK 编的扩展不能跑旧宿主）是从“宿主只保证提供自己及更早的接口”推断出来的工程规则，**未找到官方明文数值规则**（见“未确认”）。
  - 是否需要为 0.102 重新提交 Store 扩展：**否**。CmdPal 与扩展是两个独立 MSIX 包，扩展由宿主按 AppExtension/COM 激活；0.102 没要求扩展重新提交，也没提升 Store 侧要求。只有当我们**改用 0.13 新接口**时才必须“升 SDK → 重编译 → 重新打包/提交”；现在不涉及。
  - 项目 `Directory.Packages.props` 注释“Extension SDK must be <= host SDK（host = 0.13.261013002，NuGet max stable = 0.12.x）”与实测一致，只是 host 值应更新为 **0.13.261020004**（0.102.2933.0 之后）。

## 4. Gallery / 扩展发现机制在 0.102 的变化

官方文档 `src/modules/cmdpal/doc/extension-gallery.md`（v0.102.2933.0）说明：
- Gallery 只读取一个远端 `extensions.json`（默认 `https://aka.ms/CmdPal-ExtensionsJson`，即 `microsoft/CmdPal-Extensions` 仓库），无 WinGet 发现、无逐扩展 manifest 拉取；feed 与图标有磁盘缓存，**feed TTL 4 小时**，支持用户手动 Refresh（forceRefresh）与离线 stale 提示。
- 条目字段：`installSources[]`（`winget`/`msstore`/`url`）+ 可选 `detection.packageFamilyName`；`detection.packageFamilyName` 让 Gallery 在 WinGet 元数据就绪前直接识别已安装。
  <https://raw.githubusercontent.com/microsoft/PowerToys/v0.102.2933.0/src/modules/cmdpal/doc/extension-gallery.md>
- `extensions.json` 实际内容（2026-10-22 抓取 main 分支）：`generatedAt: 2026-10-12T05:34:51Z`、`extensionCount: 91`，**无 `everythingpal`**。
  <https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/extensions.json>
- 我们的提交 PR **#207 “Add EverythingPal extension” 于 2026-10-21T13:11:21Z 合并**（晚于 feed 生成时间），提交内容已含 `installSources: [{type: msstore, id: 9NFD76MCDRJZ}]` 与 `detection.packageFamilyName: EverythingPal.EverythingPal_33w78pmn50ga4`。
  <https://github.com/microsoft/CmdPal-Extensions/pull/207>
- **#48746（2026-09-02 合并）**：“Fix CmdPal extension installed state detection for Store-only package…”：Store-only 扩展既无 WinGet ID 也无（feed 里的）PFN 时此前永远显示未安装；新增 **Pass 3：用 Store ID 查 WinGet composite catalog（`AllCatalogs`，并发 4）**，根据 `InstalledVersion` 判定已安装。检测顺序变为 Pass1 PFN → Pass2 WinGet ID → Pass3 Store ID。PR 正文明确：**长期正解是给 feed 条目加 `detection.packageFamilyName`**（Pass 1 秒判），Pass 3 只是兜底；另有 PackageManager 缓存导致“刚装完不刷新，需重启 CmdPal 或等 WinGet 索引刷新”的已知延迟。
  <https://github.com/microsoft/PowerToys/pull/48746>
- **对 EverythingPal（Store-only）意味着**：
  1. 我们已按推荐填了 PFN，所以 feed 一旦包含我们，Pass 1 即可显示“已安装”，不依赖 #48746 的 Pass 3；
  2. 但 **feed 尚未 regenerate → 当前 Gallery 看不到我们**，与 0.102 无关；也不存在“0.102 要求重新提交 Gallery”；
  3. 用户端 feed 缓存 TTL 4h，且需 CmdPal 已更新到含 #48746 的 0.102 才有 Pass 3 兜底；建议发布后在 Gallery 里点一次 Refresh 验证“已安装”状态。
  4. 若之后 PFN 有变（Partner Center 身份变化），务必同步更新 feed 条目的 `detection.packageFamilyName`，否则会退回 Pass 3（Store ID 查询）且可能有索引延迟。

## 5. SDK 新 API / 新能力：值不值得跟进

| 能力 | 现在能用吗 | 值得跟进吗（EverythingPal） |
|---|---|---|
| `IFormContent2` 表单动作 ID (#50178) | 否（NuGet 0.12 无此类型，宿主有） | 不需要：无表单页 |
| `IDetails2` details 嵌 Adaptive Cards (#50180) | 否（同上） | 低优先：若以后想在 details 面板展示更丰富信息（预览/缩略图/操作卡）可考虑，但当前 `DetailsElement` 已够用 |
| `\|Swatch\|` / `\|Initials\|` 图标 (#50190) | **能**（旧 SDK 的 IconInfo 字符串，**仅新宿主**支持） | 不必：现有 glyph + 真实 shell 图标更合适；可用于占位/无图标场景 |
| `\|Svg\|` / `\|ThemedSvg\|` 图标 (#50191) | **能**（同上） | 不必：需引入 SVG 资源，现有 PNG/glyph 已满足；且旧宿主会当普通字符串处理，跨版本一致性差 |
| Toolkit 剪贴板扩展 (#50832) | 否（新 Toolkit 未发布） | 不需要：已有自写 Win32 剪贴板 helper（MTA 兼容） |
| 事件分发/生命周期修复 (#50485/#50487) | 自动生效（宿主修复） | 无需动作，属净收益 |
| 空白 details 修复 (#51231) | 自动生效 | 无需动作：不使用 ContentPage.Details |
| Native AOT forms 修复 (#50997) | 宿主自身 | 无需动作 |

## 6. 最终判断

- **必须更新：无。** 无需改 SDK 版本、无需重编译/重打包、无需因 0.102 重新提交 Store 或 Gallery。
- **建议评估（都不紧急，且非 0.102 强制）**：
  1. 等 `CmdPal-Extensions` feed regenerate 后，在 0.102 Gallery 里验证 EverythingPal 显示“Installed”（依赖已填的 PFN；若失败再看 #48746 Pass 3 的索引延迟）。
  2. 可选：在新宿主上人工回归 3 行 fallback、命令页、真实图标（runtime-verifier 正在做；本研究的 ABI 分析预测兼容）。
  3. 可选：观察宿主新图标协议与 details 新 API，仅当产品需要时再升 0.13 SDK（发布后再跟）。
- **无需动作**：`#50047`（我们 fallback ID 已唯一）、`#51231`、`#50832`、`#50997`、`#51034`、`#50702`、`#50181-#50188` 等宿主内部项。
- **注意（非 0.102 引入）**：Store 身份生效后 ProviderId（PFN!ExtensionId）变化会让已存的 per-fallback 开关/pin/alias 重置（HANDOVER 已有记录）。

## 7. 未确认

1. **0.102.2862 → 0.102.2933 的逐 commit 增量**未枚举（只核对了 0.102 整批 release notes；本机旧包已被覆盖，无法逐文件对比两次构建）。若需要精确 delta，应拉 GitHub compare `v0.102.2862.0...v0.102.2933.0`（tag 是否存在待查）。
2. **旧 host ABI `0.13.261013002` 的接口集合**未独立验证（旧包已不在本机；仅对比了当前 `0.13.261020004` 宿主与 NuGet 0.12 SDK）。因此“0.13.261013002 → 0.13.261020004 期间 ABI 是否也加了 `IDetails2`/`IFormContent2`”未确认；但 0.102 正式版宿主对 0.12 SDK 是超集（已实测）这一点不受影响。
3. 官方是否有**明文**“SDK <= host”版本规则：未找到；只有官方文档“versioned APIs / backward compatibility”表述 + 本次 ABI 超集实测，以及项目既有假设。
4. 0.102 新增的“应用语言（app language）”设置是否影响扩展自身本地化：未确认。
5. Gallery feed 何时重新生成：未确认（取决于 microsoft/CmdPal-Extensions 维护者；PR #207 已合并）。
6. 0.13 SDK NuGet 何时发布：未确认（截至 2026-10-22 17:30 未发布）。

## 8. 来源清单（访问日期均为 2026-10-22）

- PowerToys 0.102.2933.0 release（2026-10-21 发布）：<https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0>
- 本机已安装包：`C:\Program Files\WindowsApps\Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`（`AppxManifest.xml`、`BuildInfo.xml`、`Microsoft.CommandPalette.Extensions.{dll,winmd}`）
- 本机 NuGet 缓存：`%USERPROFILE%\.nuget\packages\microsoft.commandpalette.extensions\0.12.260812002`（nuspec/winmd/Toolkit.dll）
- 项目侧：`HANDOVER.md`、`Directory.Packages.props`、`src/EverythingPal/Package.appxmanifest`、`src/EverythingPal/Commands/EverythingFallbackItem.cs`、`src/EverythingPal/Commands/ItemCommands.cs`、`docs/store/gallery-submission/extension.json`
- PowerToys 源码（tag v0.102.2933.0）：`src/modules/cmdpal/custom.props`（VersionMinor=13）、`src/modules/cmdpal/doc/extension-gallery.md`、`src/modules/cmdpal/extensionsdk/README.md`
- PR：#51232（Bump cmdpal to 0.13，2026-10-13 合并）、#50047（fallback ID，2026-08-23）、#48746（Store-only 检测，2026-09-02）、#50178（IFormContent2，2026-08-28）、#50180（IDetails2/IContent，2026-08-31）、#50190/#50191（图标协议，2026-09-29）、#50832（剪贴板，2026-09-28）、#51231（ContentPage details，2026-10-13）、#50997（AOT cards，2026-10-05）——均 `https://github.com/microsoft/PowerToys/pull/<编号>`
- NuGet：<https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json>、<https://api.nuget.org/v3/registration5-semver1/microsoft.commandpalette.extensions/0.12.260812002.json>
- CmdPal 扩展官方开发文档（镜像，2026-03-03）：<https://mintlify.wiki/microsoft/powertoys/dev/command-palette-extensions>
- CmdPal-Extensions：#207（2026-10-21T13:11:21Z 合并）<https://github.com/microsoft/CmdPal-Extensions/pull/207>；feed <https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/extensions.json>（generatedAt 2026-10-12，91 条）
