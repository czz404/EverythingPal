# EverythingPal 源码审计：对新 CmdPal 宿主（PowerToys 0.102 / 0.13.12934.0）的兼容性与改进点

> 审计日期：2026-10-22（Asia/Shanghai）。审计员：repo-auditor（Team task-2）。
> 范围：`E:\workspace\deepseek\EverythingPal` 只读审计 + 构建验证；不改源码，只写本文件。
> 环境实测：PowerToys (Preview) **0.102.2933.0**（注册表）；CmdPal 包 **`Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`**，`BuildInfo.xml` / 包内 `Microsoft.CommandPalette.Extensions.dll` 的 **host ABI = `0.13.261020004`**（`SemanticVersion`，`BinaryVersion 0.13.2610.20004`）；项目 SDK 引用 `Microsoft.CommandPalette.Extensions` **0.12.260812002**（NuGet 最新稳定）。
> 外部事实（0.102 release notes、PR 编号、宿主 winmd 差异、Gallery feed）见同目录 `powertoys-0.102-cmdpal.md`（release-researcher，task-1），本审计只做源码侧核对与独立复验，不重复其调研。

## 0. TL;DR

- **必须改：0 条。** 现有源码/Store 1.0.2.0 包在新宿主上不需要任何代码修改、不需要重编译/重打包、不需要因 0.102 重新提交 Store 或 Gallery。
- **建议改：7 条**（1 个真实小 bug + 1 个可选图标增强 + 5 处版本/文档/仓库结构同步，见 §7），均**不是** 0.102 强制要求。
- **无需改：14 条**，逐条给出 `文件:行号` 与理由（见 §8）。
- **核心核实结论（task 要求重点）**：`FallbackCommandItem` 的 `Id` **每行唯一**——`src/EverythingPal/Commands/EverythingFallbackItem.cs:167-170` 传的是 `czz404.everythingpal.fallback.{index}`（第 3 个构造参数 = `Id`，已用反射核对 SDK 0.12 Toolkit 的 `FallbackCommandItem(ICommand command, string displayTitle, string id)`）。因此 PowerToys 0.102 修的「共享 Id 关一个全关」（#50047）**与本扩展无关**，多行模式下每行开关互相独立。
- 0.102 对扩展 ABI 是**纯增量**：宿主 winmd 相对 0.12 SDK 只多 `IDetails2`、`IFormContent2`，无删除/改名（见 `powertoys-0.102-cmdpal.md` §1.1）；本扩展只用 0.12 已有 API，属兼容子集。
- 0.102 的宿主改动（图标缓存/队列修复、事件分发/生命周期修复、details 空白修复、设置搜索/深链/shelf 等）**都是宿主侧**，扩展自动受益，无需改代码。

## 1. 对外暴露的 CmdPal API 面（源码清单）

| 位置 | 使用的 API | 用途 | 0.102 影响 |
|---|---|---|---|
| `src/EverythingPal/EverythingPalExtension.cs:13-39` | `IExtension`、`GetProvider(ProviderType.Commands)` | COM server 入口，返回 Commands provider | 无（接口仍在宿主超集内） |
| `src/EverythingPal/Program.cs:15,64-83` | `Shmuelie.WinRTServer` + `RegisterClass<IExtension>`、`-RegisterProcessAsComServer` | 进程外 COM/WinRT server 激活 | 无（激活方式未变） |
| `src/EverythingPal/Program.cs:22-38,47-96` | 自定义 CLI：`--search-test` / `--about` / `--diagnostics` | 诊断/无头搜索（非 CmdPal API） | 无 |
| `src/EverythingPal/EverythingPalCommandsProvider.cs:12-77` | `CommandProvider`、`TopLevelCommands()`、`FallbackCommands()`、`Settings`、`CommandItem`、`CommandContextItem` | 一级命令 + N 行 fallback + 设置页入口 | 无 |
| `src/EverythingPal/Commands/EverythingFallbackItem.cs:23-163,166-284` | `FallbackCommandItem`、`IFallbackCommandItem[]`、`UpdateQuery`、`Title/Subtitle/Icon/Command/MoreCommands` | 主页内联 fallback 行 | 无（`Id` 已唯一，见 §2） |
| `src/EverythingPal/Pages/EverythingPalPage.cs:16-214` | `DynamicListPage`、`GetItems()`、`UpdateSearchText`、`IsLoading`、`RaiseItemsChanged`、`ListItem`、`Details/DetailsElement/DetailsLink/DetailsSeparator` | 搜索页 + 详情面板 | 无（`ListItem.Details` 路径未变） |
| `src/EverythingPal/Commands/ItemCommands.cs:13-113,116-201` | `InvokableCommand`、`CommandResult.Dismiss/ShowToast`、自写 Win32 剪贴板 | 打开/定位/复制路径/Everything 定位 | 无 |
| `src/EverythingPal/Commands/EverythingCommands.cs:12-51` | `InvokableCommand` | 启动 Everything / 打开下载页 | 无 |
| `src/EverythingPal/Services/EverythingPalSettings.cs:15-87` | `JsonSettingsManager`、`TextSetting`、`ChoiceSetSetting`、`Utilities.BaseSettingsPath` | 设置页 + `%LOCALAPPDATA%\EverythingPal\settings.json` | 无（ProviderId 不变则设置保留） |
| `src/EverythingPal/Services/ShellIconProvider.cs:44-258` | `IconInfo.FromStream`、`IRandomAccessStream`（+ GDI+/shell32 P/Invoke） | 结果行真实文件类型图标（PNG 流） | 无（ABI 稳定；宿主缓存修复为净收益） |
| `src/EverythingPal/Localization/Strings.cs:21-282` | `ResourceManager` + `CultureInfo.CurrentUICulture` + `EVERYTHINGPAL_LANG` | 双语 UI | 未确认宿主「应用语言」是否影响进程外扩展（§9） |
| `src/EverythingPal/Commands/EverythingFallbackItem.cs:287-291`、`Commands/ItemCommands.cs:17,39,66,88`、`Commands/EverythingCommands.cs:16,44` | `IconInfo("\uE8B7"/"\uE8A5")` 等 glyph、`IconHelpers.FromRelativePath` | 占位/应用图标 | 无 |
| `src/EverythingPal/Package.appxmanifest:18-21,34-93` | MSIX：`windows.comServer` + `windows.appExtension`（`com.microsoft.commandpalette`）+ `runFullTrust` | 扩展发现与激活 | 无（宿主仍按 AppExtension/CLSID 激活） |

## 2. 重点核实：fallback Id 是否每行唯一（#50047 相关问题）

### 2.1 事实

- 参数位置：SDK 0.12 Toolkit 的 `Microsoft.CommandPalette.Extensions.Toolkit.FallbackCommandItem` 构造函数为
  `(ICommand command, string displayTitle, string id)`，公开属性 `ICommand Command / string DisplayTitle / string Id / ...`（本机对 SDK 0.12 Toolkit 的 `FallbackCommandItem` 类型反射复核：3 参构造 + `Id` 属性）。
  第 3 个实参就是 `Id`。
- 本扩展的调用位置：`src/EverythingPal/Commands/EverythingFallbackItem.cs:167-170`

  ```csharp
  : FallbackCommandItem(
        new NoOpCommand { Id = "czz404.everythingpal.fallback" },   // 命令 Id（仅内部命令标识，不参与 fallback 持久化）
        Strings.FormatFallbackRowTitle(index + 1, totalRows),       // DisplayTitle
        $"czz404.everythingpal.fallback.{index}")                   // ← FallbackCommandItem.Id（唯一）
  ```

  `index` 来自 `EverythingFallbackGroup` 的 `Enumerable.Range(0, rowCount)`（同文件 `:38`）；`rowCount` 被 clamp 到 1..5（`:37`），provider 侧再 clamp 0..5（`EverythingPalCommandsProvider.cs:64`）。因此 Id 取值只可能是 `czz404.everythingpal.fallback.0 .. .4`，**每行唯一、非空、稳定**（`git blame` 显示该行自引入后未改过；行数变化不会改 `.0/.1/.2` 的 Id）。
- 宿主语义（`powertoys-0.102-cmdpal.md` §B.5 引 `FallbackSettingsViewModel.cs` / `ProviderSettingsViewModel.cs`）：fallback 设置按 `IFallbackCommandItem2.Id` 持久化（`ProviderSettings.FallbackCommands[id]`），设置页 `AutomationId` 也是 Id；**改 Id 会静默重置该行开关**。
- #50047 的修复对象是 PowerToys **内置** fallback（它们共用同一个 Id）；本扩展从一开始就是每行独立 Id。

### 2.2 用户可见影响

- 多行模式（`everythingPal.FallbackRows` = 2..5）下，在 CmdPal「设置 → 扩展 → EverythingPal → 回退命令」里关掉「主页结果 2/3」只关第 2 行，**不会**像旧宿主内置 fallback 那样关一个全关。0.102 的修复对本扩展**无行为变化、无需要发的更新**。
- 注意点（非缺陷）：`DisplayTitle` 带「第 N/M 行」（`Strings.cs:230-233`），改行数时显示文案会变，但设置按 Id 存储，**不会**重置开关。若将来改动 Id 命名或重排行序，会导致老用户开关失效——改 Id 时请保留 `czz404.everythingpal.fallback.` 前缀并避免换序（代码注释可补一句）。

## 3. 详情面板 / Adaptive Cards / 剪贴板 / 表单 / 图标 / 命令参数 / 上下文菜单 / 本地化 / 设置页

| 主题 | 现状（文件:行号） | 0.102 宿主变化 | 结论 |
|---|---|---|---|
| 详情面板 `Details` | `Pages/EverythingPalPage.cs:155,165-186`：`ListItem.Details` + `DetailsElement/DetailsLink/DetailsSeparator`；`DetailsLink.Link = new Uri(result.FullPath)`（`:169-170`，已实测 `#`/`%`/空格/UNC 路径都会被 `Uri` 正确转义，不会截断） | 0.102 的 #51231 修的是 `ContentPage.Details` 变更后空白；本扩展用 `ListItem.Details`，不在其影响面。宿主要是修好了详情面板，现有元数据**自动开始显示**，无需改代码；若仍不渲染属宿主/设置问题（旧记录：0.13.12862 上内置项也不渲染） | 无需改 |
| Adaptive Cards | 未使用（全仓库无 `AdaptiveCard`/`IContent` 引用） | #50180 新增 `IDetails2`（details 接受 `IContent` 列表）、#50997 AOT forms 修复，均不在 0.12 SDK | 无需改（要用需等 0.13 SDK） |
| 剪贴板 | `Commands/ItemCommands.cs:116-201` 自写 Win32 `OpenClipboard/SetClipboardData`（MTA 安全，注释 `:115`） | #50832 是 Toolkit 剪贴板 helper 增强（需新 SDK/Toolkit） | 无需改：现实现与宿主更新无关，继续用 |
| 表单 | 未使用（无 `IFormContent`/表单页） | #50178 `IFormContent2.SubmitAction` 不在 0.12 SDK | 无需改 |
| 图标格式 / PNG 流 | `Services/ShellIconProvider.cs:218-232`：`InMemoryRandomAccessStream` + `IconInfo.FromStream`（走 `IIconData.Data` 通道，ABI 稳定）；`IconHelpers.FromRelativePath("Assets\\StoreLogo.png")`（`EverythingPalCommandsProvider.cs:23`、`EverythingPalPage.cs:25`、`EverythingFallbackItem.cs:172`） | 0.102 图标缓存/队列修复（#50181–#50188）是宿主侧净收益；新协议 `\|ShellItemIcon\|`、`\|Svg\|`、`\|ThemedSvg\|`、`\|Swatch\|`、`\|Initials\|` 是**仅新宿主**支持的 `IconInfo(string)` 协议，不需要新 SDK | 无需改（可选增强见 S5）；**不建议**现在改用新协议/SVG：旧宿主会当普通字符串处理，跨版本不一致 |
| 命令参数 / 深链 / Dock | 无命令参数、无 dock band、无深链 | 0.102 新增命令/页面深链、shelf、Dock（宿主 UI 能力）；`ICommandProvider3.GetDockBands` 不在 0.12 SDK | 无需改 |
| 上下文菜单 | `EverythingPalPage.cs:156-161`、`EverythingFallbackItem.cs:193-211,250-254`、`EverythingPalCommandsProvider.cs:32`：`CommandContextItem` + `MoreCommands` | 0.102 无破坏性变化 | 无需改 |
| 本地化 | `Localization/Strings.cs:240-282`：`CurrentUICulture` + `EVERYTHINGPAL_LANG`；`Package.appxmanifest:38-43` 声明 en-US + zh-Hans | 0.102 新增宿主「应用语言」设置；是否影响进程外 COM server 的 `CurrentUICulture` **未确认**（无扩展本地化 API 变化证据） | 无需改；观察即可（§9） |
| 设置页 | `Services/EverythingPalSettings.cs:15-69`：`JsonSettingsManager`、`Settings.SettingsPage`、`%LOCALAPPDATA%\EverythingPal\settings.json` | 0.102 的「复制设置链接」「设置搜索」等是宿主 UI；ProviderId（`PFN!ExtensionId`）未因宿主更新变化 | 无需改：fallback 开关/pin/alias 保留（只有换 Store 身份才重置，属既有已知问题） |

## 4. 版本一致性核对

| 位置 | 当前值 | 一致性判断 |
|---|---|---|
| `src/EverythingPal/Package.appxmanifest:18-21` | `Identity Name="EverythingPal"`、`Publisher="CN=czz404"`、`Version="0.2.0.0"` | ✅ 与设计一致：源码 manifest 是 **dev identity**（注释 `:11-17` 已说明）；Store 提交包由 `scripts/pack-store.ps1 -IdentityName/-Publisher/-Version` 只写 staged manifest（`docs/store/packaging.md:96,102,122-133,238-242`）。0.2.0.0 与 Store 1.0.2.0 是两条线，不是不一致 |
| `Directory.Packages.props:6-7` | SDK `0.12.260812002`；注释「host = 0.13.261013002」 | ⚠️ SDK 版本正确（仍是 NuGet 最新）；**注释里的 host ABI 过时**：现宿主是 `0.13.261020004`（`BuildInfo.xml` 实测）→ S2 |
| `docs/store/gallery-submission/extension.json:24-31` | `installSources: msstore id 9NFD76MCDRJZ`；`detection.packageFamilyName: EverythingPal.EverythingPal_33w78pmn50ga4` | ✅ 与 Store 身份一致（`docs/store/packaging.md:126`、`HANDOVER.md:46`）；`$schema` 路径无效 → S6 |
| `HANDOVER.md:6,9` vs `:17` | 顶部「Store live **1.0.2.0**」；第 17 行「已上架 **1.0.0.0**，待提交更新 **1.0.1.0**」 | ⚠️ 同一文件自相矛盾、旧状态未清 → S3 |
| `HANDOVER.md:127-128` | 「CmdPal **0.13.12862**（host ABI **0.13.261013002**）；SDK 0.12.260812002…」 | ⚠️ 已过时（现 0.13.12934.0 / ABI 0.13.261020004）→ S3 |
| `docs/store/listing.md:3,180,243,273,279-292`、`docs/store/support.md:63`、`docs/store/README.md:166` | 「当前 Store live **1.0.1.0**，本次提交 1.0.2.0」「当前开发环境 **0.13.12862**」 | ⚠️ 1.0.2.0 已上线（HANDOVER 顶部），这些是提交时快照，需要标注 live=1.0.2.0 / host=0.13.12934.0 → S4 |
| `docs/qa/verification.md`、`docs/qa/code-review.md`、`docs/research/platform-and-store-research.md` | 记录 0.2.0.0 / 0.13.12862 的历史快照 | ✅ 属**历史验证记录**，不应改（保留证据链）；只在新笔记里注明现状 |
| `EverythingPal.slnx:3-7` | 只含 `EverythingPal.Core` + `EverythingPal.Core.Tests`，**不含** `src/EverythingPal/EverythingPal.csproj` | ⚠️ solution 构建覆盖不到真正发布的扩展项目 → S7（低） |

## 5. 基线构建 / 测试（本机实测）

环境：Windows 11 26H1 build 28000.3231 x64；只装了 **.NET SDK 10.0.402**（`dotnet --list-sdks`）；PATH 前置 `C:\Program Files\dotnet`。所有命令不修改源码，构建产物只落在 `obj/bin` 与 `%TEMP%`。

| 命令 | 结果 | 说明 |
|---|---|---|
| `dotnet build EverythingPal.slnx -c Debug`（仓库内） | ❌ exit 1，输出「生成失败 / 0 个警告 / 0 个错误」，2.7–3.2s | 诊断日志：`MSB4276` 默认 SDK 解析器找不到 `C:\Program Files\dotnet\sdk\10.0.402\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk` 与 `...WorkloadManifestTargetsLocator\Sdk`，导致 NuGet 目标 `_FilterRestoreGraphProjectInputItems` 失败。**本机 SDK 安装缺 workload locator 目录**，与仓库无关 |
| 同上，在纯净副本 `%TEMP%\epaudit\repo`（复制 src/tests/Directory.*/nuget.config/slnx，删掉 bin/obj） | ❌ 同样失败（1.6s，0 警告 0 错误） | 证明与仓库内容、obj ACL 无关；`.slnx` 的 restore 图过滤在本机 SDK 上必挂 |
| `dotnet build src\EverythingPal\EverythingPal.csproj -c Debug -p:NuGetAudit=false`（纯净副本） | ✅ **成功，0 个警告，0 个错误**，44.78s；产出 `EverythingPal.Core.dll` + `EverythingPal.dll`（`net10.0-windows10.0.26100.0`），源文件未被改动 | 扩展项目本身在新 SDK 下可编译；NuGet 警告仅 `NU1900`（本机无网络，漏洞数据源不可达，非错误） |
| `dotnet build tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj -c Debug`（纯净副本） | ✅ 成功，0 警告 0 错误，2.13s | 测试项目也可编译 |
| `dotnet test tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj -c Debug --no-build`（非提权） | ❌ testhost 退出：`System.ComponentModel.Win32Exception (5): 拒绝访问`，栈在 `ProcessManager.OpenProcess`（testhost 给父进程设 ExitCallback 时） | 与 `HANDOVER.md:114,134-135` 记录一致：**单测必须提权**。按任务要求**只尝试一次即记录并跳过**，未反复重试 |
| 仓库原位 `dotnet build src\...\EverythingPal.csproj`（另一次尝试） | ❌ `error MSB3491: 未能向文件 "obj\Debug\net10.0\EverythingPal.Core.AssemblyInfoInputs.cache" 写入行。Access to the path is denied.` | 原因是 **10/21 提权构建留下的旧 obj 文件**：`obj\Debug\net10.0\EverythingPal.Core.AssemblyInfoInputs.cache` 的 owner = `BUILTIN\Administrators`、mtime 2026-10-21 12:18，且缺少当前用户显式 ACE（同目录新文件 `*.assets.cache` 可写）。属**本机文件权限/沙箱环境问题**，不是代码问题；未改任何 ACL，改用 workspace 外纯净副本完成验证 |

**结论**：源码在 SDK 10.0.402 + 0.12 SDK 下**可编译（0 警告 0 错误）**；本机两点构建失败（`.slnx` restore 的 workload locator、旧 obj 提权文件 ACL）都是环境问题。若要修 `.slnx`，可修复/重装本机 .NET SDK（`dotnet workload` 相关目录缺失），或按项目构建；CI（`.github/workflows/ci.yml`）本身用 `dotnet restore/test` 单项目 + `scripts/pack-store.ps1`，不经过 `.slnx`。

## 6. 必须改（0 条）

**没有必须修改的代码点。** 理由（逐项已核对）：

1. ABI：宿主 0.13 是 0.12 SDK 的**严格超集**（只多 `IDetails2`、`IFormContent2`，旧接口全在，见 `powertoys-0.102-cmdpal.md` §1.1）；本扩展只用 0.12 已有类型（§1 表），不存在加载/调用断裂。
2. fallback Id 每行唯一（§2），#50047 的宿主修复不改变本扩展行为。
3. 详情/图标/剪贴板/上下文菜单/设置页路径都未使用 0.102 才有的 API，无需适配。
4. 因此 **Store 1.0.2.0 可直接继续在新宿主运行**，无需因 0.102 发 1.0.3.0。

> 若 Lead 想借这次更新发一版，**唯一有实际收益的代码项是 S1（真实小 bug）和 S5（图标质量增强，可选）**，但两者都不是 0.102 引起的，不构成「必须改」。

## 7. 建议改（7 条，按优先级；均非 0.102 强制）

### S1（建议，真实小 bug，非宿主相关）：fallback 行数设置解析失败时默认值不一致
- 位置：`src/EverythingPal/Services/EverythingPalSettings.cs:45`（`TextSetting` 默认 `"1"`）、`:76`（`int.TryParse` 失败时回退 **3**）、`README.md:134`（文档写明默认 `1`）。
- 影响：用户在设置里把 `everythingPal.FallbackRows` 写成非数字（如误输入），主页会**多出 2 行**内联 fallback（1 → 3），与文档/初始值不符；这也是「默认 1 行」改造（commit a421b77）后的残留。
- 建议改法：加常量并让两处一致，例如
  ```csharp
  private const int DefaultFallbackRows = 1;            // 与 _fallbackRows 初始值 "1" 一致
  private readonly TextSetting _fallbackRows = new(..., DefaultFallbackRows.ToString(...));
  internal int FallbackRows => int.TryParse(_fallbackRows.Value, out var v) ? Math.Clamp(v, 0, 5) : DefaultFallbackRows;
  ```

### S2（建议，注释/文档同步）：SDK 注释里的 host ABI 过时
- 位置：`Directory.Packages.props:6`：「host = `0.13.261013002`」。
- 实测：0.102.2933.0 的 CmdPal 0.13.12934.0 的 ABI = **`0.13.261020004`**（`BuildInfo.xml` 的 `SemanticVersion`；包内 `Microsoft.CommandPalette.Extensions.dll` ProductVersion 一致）。
- 建议改法：只改注释为 `host = 0.13.261020004`（「Extension SDK 必须 ≤ host SDK，NuGet 最新稳定 = 0.12.x」规则本身仍成立）；**不要**改 `Version="0.12.260812002"`。

### S3（建议，文档同步）：HANDOVER 版本记录过时且自相矛盾
- 位置：`HANDOVER.md:17`（「已上架 1.0.0.0，待提交 1.0.1.0」）与 `:6,9`（「Store live **1.0.2.0**」）矛盾；`:127-128`（CmdPal 0.13.12862 / ABI 0.13.261013002）。
- 建议改法：把环境行更新为 PowerToys **0.102.2933.0** / CmdPal **0.13.12934.0** / host ABI **0.13.261020004**，Store live 统一为 **1.0.2.0**（历史提交记录保留在「历史」段），并注明本次更新**不需要扩展发版**。

### S4（建议，文档同步）：Store/Gallery 材料里的「当前状态」快照过期
- 位置：`docs/store/listing.md:3`（「当前 Store live 1.0.1.0，本次提交 1.0.2.0」）、`:180,243,273`（开发环境 0.13.12862）、`:279-292`（1.0.2.0 What's new 段）；`docs/store/support.md:63`；`docs/store/README.md:166`。
- 建议改法：把「live」更新为 1.0.2.0（1.0.2.0 段保留为历史）、把「当前开发环境 0.13.12862」改为「0.13.12934.0（PowerToys 0.102）」；**详情面板措辞暂不改**（`listing.md:243,273` 已写「取决于宿主版本」，等 runtime-verifier 在新宿主上确认后再决定是否去掉该免责声明）。`docs/qa/*` 与 `platform-and-store-research.md` 是历史验证证据，**不要改**。

### S5（可选增强，非 0.102 必需）：图标清晰度 / 每文件图标
- 现状：`Services/ShellIconProvider.cs:13-42` 有意只在**扩展名**级别取图标（`SHGetFileInfo` + `SHGFI_USEFILEATTRIBUTES`，见 `:183-215`），渲染成 **32×32 PNG**（`:205-209` 的 `Icon.FromHandle` + `ToBitmap` 默认小图标尺寸）后 `IconInfo.FromStream`（`:218-232`）。在高 DPI 下宿主会放大这张 32px 图，且同一扩展名的文件都显示同一个图标（`.exe`/`.lnk` 不显示各自图标）。
- 可做（SDK 0.12 内、无 ABI 风险）：页面结果改用 Toolkit 的 `ThumbnailHelper.GetThumbnail(path, jumbo: true)`（256px，PNG 流；图片文件先缩略图，其余回退 shell 文件图标；0.12 包 DLL 中确认存在该类型，签名见 `docs/research/platform-and-store-research.md` A.1 `:100-107`），fallback 行仍用现有扩展名级缓存（守住宿主 200ms 预算，见 `EverythingFallbackItem.cs:25-29` 注释）。注意：本项目 Release 的 `obj\...\linked\` 输出是 **ILLink 裁剪后** 的副本，未使用成员（如 ThumbnailHelper）会被裁掉，判断 API 可用性要看 NuGet 包里的原始 DLL。
- 也可做（**仅新宿主**，不建议现在上）：`IconInfo("|ShellItemIcon|v1;<len>:<path>")` 协议（wire 格式与证据见 `docs/research/platform-and-store-research.md` A.2 `:123-146`），让宿主自己按文件取图标并命中它的新图标缓存。风险：旧宿主不认该协议（会当 glyph），跨版本不一致——**要上必须做宿主能力探测 + PNG 流回退**。
- 结论：属「以后值得做」的增强，不进 1.0.3.0 也完全没问题。

### S6（建议，低）：Gallery 提交文件的 `$schema` 指向不存在的路径
- 位置：`docs/store/gallery-submission/extension.json:2` → `"$schema": "../../../.github/schemas/extension.schema.json"`；本仓库 `.github/` 下只有 `workflows/`（无 `schemas/`），本地校验会失败。
- 建议改法：改成上游 `microsoft/CmdPal-Extensions` 仓库里的实际路径，或直接删掉 `$schema` 行（该文件已随 PR #207 合入上游，本地这份是存档）。`detection.packageFamilyName`（`:30`）与 Store PFN 一致，**不要动**。

### S7（建议，低）：`.slnx` 不包含真正发布的扩展项目
- 位置：`EverythingPal.slnx:3-7` 只有 Core + Tests。
- 影响：`dotnet build EverythingPal.slnx` 即使成功也**覆盖不到** `src/EverythingPal/EverythingPal.csproj`（CI 真正构建扩展走 `scripts/pack-store.ps1`，README 的开发循环走 `scripts/deploy.ps1`，所以 CI 不受影响）；新贡献者用 solution 构建会漏掉扩展编译错误。
- 建议改法：在 slnx 加 `<Project Path="src/EverythingPal/EverythingPal.csproj" />`（`EnableMsixPackaging` 默认 false，dev 构建不需 Windows SDK 打包），并在 README「Build from source」补一句「slnx 含扩展项目；MSIX 打包走 scripts」。注意：本机 `.slnx` restore 因 SDK 安装缺 workload locator 目录会失败（§5），那是环境问题，不是本项的理由。

## 8. 无需改（14 条）

| # | 项 | 文件:行号 | 理由 |
|---|---|---|---|
| N1 | fallback Id 唯一性（#50047） | `Commands/EverythingFallbackItem.cs:167-170`（Id 构造参数）、`:38`（index）、`EverythingPalCommandsProvider.cs:62-77` | 每行 `...fallback.{index}`，0..4 唯一；改行数不改已有 Id；设置按 Id 存储，不会串扰/重置。结论：0.102 的修复与本次无需动作 |
| N2 | ABI/激活兼容 | `Directory.Packages.props:7`；`Package.appxmanifest:60-85`；`EverythingPalExtension.cs:13`（GUID 与 manifest `:63,77` 一致） | 宿主 ABI 0.13.261020004 是 0.12 SDK 超集，旧接口全在；扩展激活方式（AppExtension + COM ExeServer）未变 |
| N3 | 详情面板 `Details` | `Pages/EverythingPalPage.cs:155,165-186` | 用 `ListItem.Details`（0.12 已有），#51231 修的是 `ContentPage.Details`；宿主要是修好面板则自动显示，无需改 |
| N4 | PNG 图标流 | `Services/ShellIconProvider.cs:218-232` | 走 `IIconData.Data`（`IconInfo.FromStream`）稳定通道；宿主图标缓存/队列修复（#50181–#50188）自动受益 |
| N5 | 剪贴板 | `Commands/ItemCommands.cs:116-201` | 自写 Win32 剪贴板（MTA 安全）；#50832 只改 Toolkit，且新 Toolkit 未发布，无需跟进 |
| N6 | Adaptive Cards / 表单 | 全仓库未使用 | `IFormContent2`(#50178)/AOT forms(#50997)/`IDetails2`(#50180) 都不在影响面；要用需 0.13 SDK |
| N7 | 命令参数/深链/Dock | 无相关代码 | 0.102 的深链/shelf/Dock 是宿主功能；`ICommandProvider3.GetDockBands` 不在 0.12 SDK，且产品不需要 |
| N8 | 上下文菜单 | `Pages/EverythingPalPage.cs:156-161`、`Commands/EverythingFallbackItem.cs:193-211,250-254`、`EverythingPalCommandsProvider.cs:32` | `CommandContextItem`/`MoreCommands` 在 0.13 宿主 API 未变 |
| N9 | 设置/ProviderId | `Services/EverythingPalSettings.cs:15-69` | 宿主更新不改 `PFN!ExtensionId` → fallback 开关、pin、alias 保留；只有换 Store 身份才重置（既有已知问题，非 0.102 引入） |
| N10 | 本地化 | `Localization/Strings.cs:240-282`；`Package.appxmanifest:38-43` | 跟随 `CurrentUICulture` + `EVERYTHINGPAL_LANG`；0.102「应用语言」是否影响进程外扩展未确认，但 0.13 宿主无扩展本地化 API 变化，无需改 |
| N11 | 版本号体系 | `Package.appxmanifest:18-21`、`docs/store/packaging.md:96,122-133,238-242`、`docs/store/gallery-submission/extension.json:24-31` | 源码 manifest 0.2.0.0 = dev identity；Store 1.0.2.0 由 `pack-store.ps1` 注入 staged manifest；Gallery PFN 与 Store 身份一致——设计正确，不是不一致 |
| N12 | SDK 版本 | `Directory.Packages.props:7` | `0.12.260812002` 仍是 NuGet 最新稳定（无 0.13 包）；无升级路径也不需要升级 |
| N13 | fallback 多行模型 | `EverythingFallbackItem.cs:23-163`、`EverythingPalCommandsProvider.cs:53-60` | 0.102 没有新增「一个 item 返回多行」的 API；N 个 `FallbackCommandItem` = N 行的模型仍是唯一正解 |
| N14 | 新图标协议（`\|Swatch\|` / `\|Initials\|` / `\|Svg\|` / `\|ThemedSvg\|`） | — | 旧 SDK 的 `IconInfo(string)` 即可用（仅新宿主解析），但跨版本会退化成字符串/无效图标；现有 glyph + 真实 shell PNG 已够，**不采用**（如产品需要再按 §S5 方式做能力探测+回退） |

另：0.102 的宿主 UI 新功能（设置搜索、shelf/pin、命令与页面深链、LoadMore、图标更清晰、事件分发/生命周期修复 #50485/#50487 等）对扩展**自动生效**，无需改代码；可发现在新宿主上反而更好。

## 9. 未确认 / 后续

1. 0.102 宿主的「应用语言」设置是否改变进程外 COM server 的 `CultureInfo.CurrentUICulture`（影响双语 UI 跟随）——未确认；可用 runtime-verifier 的截图/实际语言对比确认。
2. 0.102 是否让 `ListItem.Details` 详情面板真正渲染（旧宿主 0.13.12862 连内置项也不渲染）——属宿主行为，需实机验证；本审计确认扩展侧元数据已就绪（`EverythingPalPage.cs:165-186`）。
3. 本机 `.slnx` restore 失败（SDK 安装缺 workload locator 目录）与旧 obj 文件 ACL 属**本机环境**；未做 ACL 修复（沙箱权限内不可行），已用 workspace 外纯净副本完成构建验证。
4. 单测未运行（非提权 testhost `Win32Exception(5)`，与 HANDOVER 记录一致）；历史记录为提权 118/118 通过，本次未复跑。

## 10. 参考

- 源码：`src/EverythingPal/**`（§1 表格逐文件核对）；`EverythingPal.slnx`；`Directory.Packages.props`；`Package.appxmanifest`；`README.md`；`HANDOVER.md`；`docs/store/**`。
- SDK 0.12 程序集核对：NuGet 包 `%USERPROFILE%\.nuget\packages\microsoft.commandpalette.extensions\0.12.260812002\lib\net8.0-windows10.0.19041.0\Microsoft.CommandPalette.Extensions.Toolkit.dll`（未裁剪，484,864 B；二进制扫描确认含 `ThumbnailHelper`/`ClipboardHelper`/`IconHelpers`/`FromStream`/`DetailsLink`/`FallbackCommandItem`）；对 `FallbackCommandItem` 构造签名 `(ICommand, string, string)`、`DetailsLink.Link: Uri` 用反射复核。注意 `obj\x64\Release\...\win-x64\linked\` 里的同名 DLL 是 ILLink **裁剪后**的 364,544 B 副本，未使用成员会被裁掉，不能作为 API 可用性依据。
- 宿主实测：`C:\Program Files\WindowsApps\Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe\BuildInfo.xml`（`SemanticVersion=0.13.261020004`，`StoreVersion=0.13.12934.0`）。
- 团队并行调研：`docs/research/notes/powertoys-0.102-cmdpal.md`（0.102 release notes / PR / winmd diff / Gallery feed，2026-10-22）。
- 历史研究（不改）：`docs/research/platform-and-store-research.md`（A.2 图标协议 wire 格式 `:123-146`；B.5 fallback 设置键 `:233-273`）。
