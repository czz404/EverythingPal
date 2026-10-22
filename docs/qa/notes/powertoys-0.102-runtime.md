# PowerToys 0.102 / CmdPal 0.13.12934.0 下 EverythingPal 1.0.2.0 运行时验证

采集时间：2026-10-22 17:20–17:26（Asia/Shanghai，本机时刻）
采集者：runtime-verifier（共享任务 task-3）
范围：只读本机运行时证据；未安装/卸载/改系统设置，未改源码；只写本文件。为完成冒烟启动过一次 Everything 客户端（pid 10332），用完后已停止（详见 §6）。

## 结论（先给答案）

- **现有 Store 版 EverythingPal 1.0.2.0 在新宿主（PowerToys 0.102.2933.0 / CmdPal 0.13.12934.0 / host ABI 0.13.261020004）上可正常加载与工作，没有“必须更新”的运行时证据。**
- 宿主 ABI 从旧值 **0.13.261013002** 前进到 **0.13.261020004**（构建日 10-13 → 10-20），但 WinRT API 面是**纯增量**：只多了 `IDetails2`、`IFormContent2`、`SubmitAction`、`actionId` 4 个名字，无删除/改名；因此按 SDK 0.12 编译的扩展仍兼容。
- 运行时硬证据：更新后宿主两次启动都把扩展进程拉起并读到命令：
  - `16:55:25.581 Started extension EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 426 ms`
  - `16:55:25.769 Loaded 2 command(s) ... from EverythingPal... in 85 ms`
  - `17:05:31.199 Started extension ... in 268 ms`；`17:05:31.375 Loaded 2 command(s) ... in 79 ms`
  - 扩展日志在更新后 `17:16:06` 有两次 `fallback result: Ok, 1 item(s)`（pid 15432），无异常。
- Gallery：`extensions.json`（2026-10-12 生成，91 条）**没有 EverythingPal**；0.102 的 #48746 只修 Gallery 的 “installed” 检测，不改变扩展本身能否运行。见 §5。
- 可选冒烟：`--search-test readme` 最终 `status: Ok`，返回真实 FILE/DIR 结果；Everything/es.exe 链路正常（首次失败是 Everything 客户端未运行，非宿主/沙箱问题）。见 §6。

## 1. 新宿主包与 ABI

宿主包目录：`C:\Program Files\WindowsApps\Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe`
目录/主要文件时间戳均为 `2026/10/22 16:55:17`（即本机实际更新落地时间）。

`AppxManifest.xml`：

```
<Identity Name="Microsoft.CommandPalette" Publisher="CN=Microsoft Corporation,..." Version="0.13.12934.0" ProcessorArchitecture="x64" />
...
<uap3:Extension Category="windows.appExtensionHost">
  <uap3:AppExtensionHost><uap3:Name>com.microsoft.commandpalette</uap3:Name></uap3:AppExtensionHost>
</uap3:Extension>
```

`BuildInfo.xml`：

```
<BuildInfo BinaryVersion="0.13.2610.20004"
           SemanticVersion="0.13.261020004"
           StoreVersion="0.13.12934.0"
           BuildDefId="unknown"/>
```

`Microsoft.CommandPalette.Extensions.dll`（FileVersionInfo）：

| 属性 | 新宿主 0.13.12934.0 | 旧宿主 0.13.12862（项目记录） |
|---|---|---|
| FileVersion | `0.13.2610.20004` | `0.13.2610.13002` |
| ProductVersion | `0.13.261020004` | `0.13.261013002`（HANDOVER.md:127） |
| **host ABI** | **`0.13.261020004`** | **`0.13.261013002`** |

结论：**ABI 旧=0.13.261013002，新=0.13.261020004**（ABI 字符串即 ProductVersion/SemanticVersion，FileVersion 为点分形式 0.13.2610.20004）。

重要发现（与任务假设不同，需记录）：**宿主包内的 `Microsoft.CommandPalette.Extensions.dll` 是原生 DLL，不是托管程序集**，因此没有 AssemblyVersion / AssemblyInformationalVersion：

```
PE machine=0x8664 (x64)  CLR header RVA=0x00000000  (无 CLR 头 = native, 无托管元数据)
[System.Reflection.AssemblyName]::GetAssemblyName(...) -> "PE image does not have metadata."
[System.Reflection.Assembly]::LoadFrom(...)            -> "拒绝访问。"（WindowsApps ACL）/"Bad IL format"（复制出来后）
```

所以宿主 ABI 只能从 FileVersion/ProductVersion + BuildInfo 读取；**AssemblyInformationalVersion 在宿主侧不存在**。扩展包内的同名 DLL 也是原生（`0.12.2608.12002` / `0.12.260812002`），托管投影由 C#/WinRT 在扩展编译期从 winmd 生成；扩展侧托管的只有 `Microsoft.CommandPalette.Extensions.Toolkit.dll` = `0.12.0.0` / `0.12.2608.12002` / Informational `0.12.260812002`。

## 2. 新旧 API 面（winmd）对比：纯增量

- 宿主 `Microsoft.CommandPalette.Extensions.winmd`：14,848 B，SHA256 `36A0BBD1357F2DEB1EBA5DF34ADA8A71DB0C67C3897F877868F6DC06B1922BB8`
- 扩展包内 SDK 0.12 winmd：14,848 B，SHA256 `E778A25C5FEA3D3A0258B54396F3D9DC115DA2AA26BE951EB6E8D7DF50772AB3`
- 方法：读取两文件全部可打印 ASCII 元数据名并按集合求差（脚本见 §8）。结果：
  - 仅宿主有（4 个）：`IDetails2`、`IFormContent2`、`SubmitAction`、`actionId`
  - 仅 SDK 0.12 有（1 个）：`[QG$`（二进制噪声，非元数据名）
  - 其余全部相同（`IFallbackCommandItem`/`IFallbackCommandItem2`/`IFallbackHandler`、`IDetails`、`IFormContent`、`IExtension` 等既有接口一个不少）。
- 解释：宿主只**新增** WinRT 版本化接口/枚举成员（与 0.102 中 details pane 内嵌 Adaptive Cards、表单动作 ID 等 SDK 增强一致），没有破坏性改动。旧 SDK 编译的扩展不需要为新宿主重编译。
- 旁证（宿主主二进制 `Microsoft.CmdPal.UI.exe` 的字符串扫描，UTF-16）：包含 `GetStorePackagesByIdAsync`、`ExtensionGalleryViewModel`、`CompositeSearchBehavior`、`IDetails2`、`IFormContent2`、`SubmitAction`、`FallbackRanks` —— 说明新宿主确实带 #48746 的 Store 安装检测代码路径、新 ABI 接口，以及按 ID 持久化 fallback 的 `FallbackRanks`。

## 3. 运行时日志（更新后）

### 3.1 CmdPal 主日志
`C:\Users\Lenovo\AppData\Local\Microsoft\PowerToys\CmdPal\Logs\0.102.2933.0\Log_2026-10-22.log`
（注意日志目录名 = PowerToys 版本 0.102.2933.0；16:55:21 启动时删除了旧目录 `Logs\0.102.2862.0`。）

```
[16:55:21.876] Deleted old log directory: ...\CmdPal\Logs\0.102.2862.0
[16:55:21.899] Hello World! Command Palette is starting.
  App version: 0.13.12934.0   Packaging flavor: Packaged   Is elevated: no
  OS version: Microsoft Windows 10.0.28000   Framework: .NET 10.0.11
  Config directory: C:\Users\Lenovo\AppData\Local\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState
[16:55:25.512] Started extension Microsoft.PowerToys.SparseApp_0.102.2933.0_neutral__8wekyb3d8bbwe in 357 ms
[16:55:25.581] Started extension EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 426 ms
[16:55:25.683] WinRTExtensionService: Started 2 extension(s) in 528 ms
[16:55:25.769] Loaded 2 command(s) and 0 band(s) from EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 85 ms
...（17:05:27 第二次宿主启动，同样成功）
[17:05:31.199] Started extension EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 268 ms
[17:05:31.375] Loaded 2 command(s) and 0 band(s) from EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 in 79 ms
[17:22:01.366] [Warning] WinGetPackageManagerService.cs::GetStorePackagesByIdAsync::280
    Microsoft Store package lookup canceled: The operation was canceled.
```

- 没有 EverythingPal 相关的 Error/异常/COM 失败；宿主报的 2 条 Error 是 `UWPApplication.TryLoadIndirectString` 读不了 Windows 自带 `MicrosoftWindows.Client.CoreAI`（ClickToDo）资源的宿主自身问题，与扩展无关；`Skipping duplicate pinned dock band command`/`DockDrop ... failed to find band command` 是内置 provider 的既有告警。
- `17:22:01` 的 `GetStorePackagesByIdAsync` 取消告警是 **0.102 新 #48746 代码路径**在 Gallery 做 Store 包检测时的日志（宿主正常跟踪日志，不是崩溃）。
- 另外两个 988 B 的 GUID 前缀日志（`75bdb150...` 17:06:33、`98cfd5d9...` 17:14:14）只有启动头 24 行、没有 provider/扩展加载记录：属于第二实例/交接式启动，未见错误。

### 3.2 扩展日志
`C:\Users\Lenovo\AppData\Local\EverythingPal\Logs\extension.log`（407 行，最后写入 17:16:06）

```
2026-10-22 16:55:25.278 [pid 17656] Main entry. args=[-RegisterProcessAsComServer | -Embedding]
2026-10-22 16:55:25.519 [pid 17656] EverythingPalExtension constructed
2026-10-22 16:55:25.585 [pid 17656] GetProvider requested: Commands
2026-10-22 17:05:31.027 [pid 15432] Main entry. args=[-RegisterProcessAsComServer | -Embedding]
2026-10-22 17:05:31.158 [pid 15432] EverythingPalExtension constructed
2026-10-22 17:05:31.202 [pid 15432] GetProvider requested: Commands
2026-10-22 17:16:06.474 [pid 15432] fallback result: Ok, 1 item(s)
2026-10-22 17:16:06.666 [pid 15432] fallback result: Ok, 1 item(s)
```

- 全文件异常关键词扫描：仅 1 条历史异常，`2026-10-20 14:44:23.778 [pid 23376] EverythingFallbackGroup failed: System.NullReferenceException`（`EverythingFallbackItem.Clear()` -> `CommandItem.set_Command`，见 `src/EverythingPal/Commands/EverythingFallbackItem.cs:163`）。该条落在 **更新前的旧宿主会话**（10-20），之后 10-21/10-22 的多次会话（含 10-22 16:55、17:05 两次）均无异常。
- 更新后（10-22 16:55 之后）没有新的 Error/Exception/COM 错误；GetProvider、fallback 都正常返回。

## 4. 扩展注册与 CmdPal 设置

因沙箱内 `Get-AppxPackage` 被拒（未再尝试），改用注册表：
`HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages`

```
=== EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4 ===
  PackageRootFolder = C:\Program Files\WindowsApps\EverythingPal.EverythingPal_1.0.2.0_x64__33w78pmn50ga4
  DisplayName       = EverythingPal
```

- 包根目录存在，`AppxManifest.xml` Identity：`Name="EverythingPal.EverythingPal" Publisher="CN=A00B5A3A-..." Version="1.0.2.0" ProcessorArchitecture="x64"`；Stack 版 PFN = **`EverythingPal.EverythingPal_33w78pmn50ga4`**。
- 清单含：`windows.comServer`（ExeServer `EverythingPal.exe -RegisterProcessAsComServer`，ClassId `a2b723c3-d9d7-4a91-941f-2314f448299d`）+ `windows.appExtension Name=com.microsoft.commandpalette`（`CmdPalProvider/CreateInstance`、`SupportedInterfaces/Commands`）。
- 载荷：`Tools\es.exe` 262,824 B，FileVersion **1.1.1.39**；`EverythingPal.dll` AssemblyVersion 1.0.0.0 / Informational `1.0.0+34645dc5...`（包版本 1.0.2.0，程序集版本 1.0.0.0，属项目既有约定）。
- CmdPal 设置（只读）`C:\Users\Lenovo\AppData\Local\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\settings.json`（23,233 B，10-22 17:17:01）：
  - `ProviderSettings["EverythingPal.EverythingPal_33w78pmn50ga4!App!ID"]`：`IsEnabled=true`、`SearchWeight=Normal`，有 6 个 fallback 条目 + `czz404.everythingpal.fallback`，`PinnedCommandIds=[]`。
  - `Aliases["e "]` -> `CommandId: EverythingPal.EverythingPal_33w78pmn50ga4!App!ID16904776974876113808`。
  - `FallbackRanks` 43 项中含 `EverythingPal.EverythingPal_33w78pmn50ga4!App!ID8365361634605052759`（fallback 设置参与持久化，与 #50047 的按 ID 持久化模型一致）。
  - `GalleryFeedUrl` 为空（使用内置默认 feed）。
  - `commandProviderCache.json`：`"EverythingPal.EverythingPal_33w78pmn50ga4!App!ID": {"DisplayName": "Everything 搜索"}`。
  - 另有旧开发身份 `EverythingPal_b32a3jmekh2ja!App!ID`（及 `czz404.everythingpal.fallback.*`）的历史条目，属旧开发包残留，未影响 Store 包加载。
- 结论：扩展在 CmdPal 设置里存在且已启用；身份 = Store 1.0.2.0，PFN 正确。

## 5. Gallery 状态与 #48746 的含义

- 拉取 `https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/extensions.json`（HTTP 200）：
  - `"generatedAt": "2026-10-12T05:34:51Z"`，`"extensionCount": 91`
  - 全文搜索：`EverythingPal` / `czz404` / `33w78pmn50ga4` **均无匹配**。
  - 其中确有两个别的 Everything 扩展条目：`goucey.everythingextension`、`linycv.everythingcmdpal`；**EverythingPal 尚未提交到社区目录**（与 `docs/store/README.md` R6 “提交完成前 Gallery 可能搜不到，属正常”一致）。
- #48746（已合并，标题 “Fix CmdPal extension installed state detection for Store-only package…”）实际改动：Gallery 的 installed 检测新增 Pass 3 —— 用 WinGet composite catalog 按 Microsoft Store ID 查询本地安装（`IWinGetPackageManagerService.GetStorePackagesByIdAsync`，`CompositeSearchBehavior.AllCatalogs`，并发限流 4），仅对前两轮（PFN、WinGet package ID）都未解析的条目兜底；PR 描述同时指出长期正解是给 extensions.json 条目加 `"detection": { "packageFamilyName": "<PFN>" }`（Pass 1 直接命中）。
- 对本插件的意义：EverythingPal 是 **Store-only（msstore）** 扩展（无 winget id），#48746 让它“将来进入 Gallery 后”能被正确标成已安装：
  - 若在 feed 条目里加 `"detection": {"packageFamilyName": "EverythingPal.EverythingPal_33w78pmn50ga4"}`，新宿主 Pass 1 直接识别；
  - 否则 0.102 的 Pass 3 也能按 Store ID 识别（旧宿主 0.13.12862 没有 Pass 3，会一直显示未安装）。
  - 但 #48746 **不解决“条目不存在”**：在提交 Gallery（含 Store ID/PFN）之前，该修复对本插件无可见影响，也不影响扩展运行。
- 本机证据：主日志 `17:22:01` 已出现 `GetStorePackagesByIdAsync` 的调用/取消记录，宿主二进制也含该符号 —— 说明本机 0.13.12934.0 确实带 #48746。

## 6. 冒烟（Everything / es.exe 链路）

方法（参考 `scripts/verify-build.ps1` 的 smoke 段）：把**已安装 Store 包载荷**整体复制到 workspace 外的 `%TEMP%\...\epwt-smoke-*`，再执行：

```
dotnet EverythingPal.dll --search-test readme
```

结果：
- 首次运行：`exit code: 0`，`status: QueryFailed`，`error: es.exe exited with 7: Error 7: Unable to send IPC message or bad IPC reply.`
  - 原因排查：包内 `Tools\es.exe -version` 正常（exit 0，`1.1.1.39`）；Everything **服务**在跑（`sc.exe qc Everything` -> `"D:\software\Everything\Everything.exe" -svc`，StartType AUTO，状态 Running），但 **Everything GUI 客户端未运行**（只开服务时 es.exe IPC 返回 7）。属文档中已知环境限制，不是沙箱或宿主问题。
- 为完成链路验证，临时启动 `D:\software\Everything\Everything.exe`（Everything 1.5.0.1423b x64，pid 10332，`-minimized`），重跑同一命令：
  - `exit code: 0`，`status: Ok`，输出包含 `DIR/FILE` 真实命中（例如 `FILE C:\Program Files\Git\mingw64\lib\tk8.6\demos\README`、`FILE C:\Windows\System32\drivers\gmreadme.txt` 等）。
  - 验证后已 `Stop-Process` 停止该 pid（启动前确认它未运行，结束后恢复原状）。
- 结论：EverythingPal 的 Everything/es.exe 搜索链路在新宿主环境下正常工作。

## 7. 异常、限制与未决点

- DSH 沙箱内 `Get-AppxPackage` 被拒：改用 HKCU AppModel Repository（成功）。
- 本会话中 `Get-Process` 未能列出 CmdPal/EverythingPal 进程（临时启动的 Everything 可见），但主日志在 17:22:01 仍有追加、扩展进程曾正常启动，因此**进程枚举在沙箱下可能不完整**；“当前是否有 CmdPal 进程”未据此下结论。
- 扩展包内 `Tools\es.exe` 与扩展搜索依赖本机 Everything 客户端运行；若用户关闭 Everything，扩展会报告 QueryFailed（Error 7），这是既有行为，与本次宿主更新无关。
- 未运行 `dotnet test`（非提权会 Win32Exception(5)）；未做 CmdPal UI 截图/详情面板验证（属于插件 API/UI 审计范围，见 task-2）；未打开 CmdPal UI（仅读取其日志/设置）。
- 10-20 的 `EverythingFallbackGroup NullReferenceException` 是更新前旧宿主会话的历史异常，更新后 10-21/10-22 未复现（是否已修复需源码审计确认，不在本任务范围）。

## 8. 证据命令（可复现）

```powershell
# 宿主版本/ABI
$pkg='C:\Program Files\WindowsApps\Microsoft.CommandPalette_0.13.12934.0_x64__8wekyb3d8bbwe'
Get-Content (Join-Path $pkg 'BuildInfo.xml') -Raw
[System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $pkg 'Microsoft.CommandPalette.Extensions.dll')) |
  Select-Object FileVersion,ProductVersion

# 注册（Get-AppxPackage 在沙箱被拒时的替代）
Get-ChildItem 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages' |
  Where-Object PSChildName -match 'EverythingPal'    # -> ..._1.0.2.0_x64__33w78pmn50ga4

# 日志
Get-Content 'C:\Users\Lenovo\AppData\Local\Microsoft\PowerToys\CmdPal\Logs\0.102.2933.0\Log_2026-10-22.log' -Raw
Get-Content 'C:\Users\Lenovo\AppData\Local\EverythingPal\Logs\extension.log' -Tail 60
Select-String -Path 'C:\Users\Lenovo\AppData\Local\EverythingPal\Logs\extension.log' -Pattern 'error|exception|fail|warn'

# winmd 差异（ASCII 元数据名集合求差）
# 宿主 winmd 独有 -> IDetails2 / IFormContent2 / SubmitAction / actionId；SDK 0.12 无缺失
# 宿主主二进制包含 #48746/#50047 相关符号
$b=[System.IO.File]::ReadAllBytes((Join-Path $pkg 'Microsoft.CmdPal.UI.exe'))
$u16=[System.Text.Encoding]::Unicode.GetString($b)
'GetStorePackagesByIdAsync' | ForEach-Object { $u16.Contains($_) }   # True

# 冒烟（在 workspace 外）
#   Copy-Item <安装包载荷> $env:TEMP\epwt-smoke-* -Recurse
#   dotnet EverythingPal.dll --search-test readme
```
