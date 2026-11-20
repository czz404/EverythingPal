# EverythingPal — 2026-11-20 review 修复验证报告

> 基线：`main` @ `3613454`（工作区干净）。本轮只处理 review 中列出的任务 1、2、3A、3B、4、5、6、7、8。
> 验证机：Windows 11 26H1 build 28000.3231；PowerToys **0.102.3101.0** / CmdPal **0.13.13096.0**；
> Everything **1.5.0.1423**；ES **1.1.1.39**；.NET SDK **10.0.404**。

## 结果总览

| 任务 | 状态 | Core 测试 | CmdPal / Windows 实测 |
|---|---|---|---|
| 1 设置重启后恢复 | 代码 + 进程级验证 PASS | 163/163 | 设置页保存/重启 UI 流程待验收（同一 `EverythingPalSettings` 在全新进程中的读回已验证） |
| 2 首页 1 行多结果回车 | PASS | 163/163（`FallbackResultPlan` 矩阵） | PASS：1 行 + 多匹配 → 回车打开 4 条结果页 |
| 3A 进结果页才完整搜索 | 代码 + UI 行为 PASS | 163/163（宿主回传顺序 4 个回归用例） | PASS（行为）：首页只显示 1 行，回车后页面才出现 4 条结果；ES 调用次数未插桩 |
| 3B 取消传到 ES 进程 | PASS | 163/163 | Windows harness PASS（cancel / timeout / 1.4 retry / 父进程先退出但子进程占管道） |
| 4 用户排序生效 | PASS | 163/163 | 解析器 + 参数测试 PASS |
| 5 高级查询引号 | PASS（第二轮修正） | 163/163 | 真实 ES 短语/OR/带引号路径均 PASS |
| 6 详情面板 | PASS | 163/163 | PASS（截图：文件夹/文件，换选跟随） |
| 7 固定 ES 版本 + 哈希 | PASS | — | 缓存矩阵 + 两个架构包内 `Tools\es.exe` 哈希 PASS |
| 8 日志只写模式名 | PASS | — | `--search-test EP_LOG_CHECK_8347` 两份日志均无标记 PASS |

## 改动文件

- `src/EverythingPal/Services/EverythingPalSettings.cs` — 先注册再 `LoadSettings()`；`DefaultFallbackRows = 1`。
- `src/EverythingPal/Commands/EverythingFallbackItem.cs` — 首页查询取 `N+1`；`FallbackResultPlan` 决定可见条数/回车动作；文案；取消传入 Core。
- `src/EverythingPal/Pages/EverythingPalPage.cs` — `ShowDetails = true`；延迟到首次 `GetItems()` 才执行首页带入的查询；第二轮改用 `PageSearchRequestState` 处理宿主回传；取消传入 Core。
- `src/EverythingPal.Core/EverythingSearch.cs` — `SearchAsync(..., CancellationToken)`；启动前/兼容重试前检查取消；第二轮 `-search*` 原始命令行 + 同一截止时间覆盖进程退出与 stdout/stderr 读取；取消/超时终止本次启动的 `es.exe`；取消不吞成 `QueryFailed`。
- `src/EverythingPal.Core/EsResultParser.cs`、`ResultRanker.cs`、`SearchOptions.cs`、`EsArguments.cs` — 只有默认 `name-ascending` 走相关性重排；显式排序保留 ES 顺序。
- `src/EverythingPal.Core/QueryNormalizer.cs` — 保留短语/多短语/布尔表达式；单个普通 token 与单个带引号路径去引号；幂等。
- `src/EverythingPal.Core/PageSearchRequestState.cs`（第二轮新增）— 宿主预填/回传/用户输入的状态机。
- `src/EverythingPal.Core/EsCommandLine.cs`（第二轮新增）— `-search*` 原始尾部命令行的构造与 Windows 引号规则。
- `src/EverythingPal.Core/EsArguments.cs` — 第二轮的 `-search*` 固定参数与原始表达式分离。
- `src/EverythingPal.Core/FallbackResultPlan.cs`（新增）— 首页返回条数/多匹配/是否触顶的纯逻辑。
- `src/EverythingPal/Program.cs` — 日志只写 `diagnostics` / `search-test` / `com-server` / `unknown`。
- `src/EverythingPal/Localization/Strings.cs`、`Resources/Strings.resx`、`Strings.zh-Hans.resx` — `Fallback.ShowMoreResults`；排序说明。
- `scripts/es-release.ps1`（新增）— 锁定 ES `1.1.1.39`、资产名、x64/ARM64 SHA-256。
- `scripts/pack-store.ps1`、`scripts/deploy.ps1` — 统一读取固定 ES 配置；缓存命中校验哈希；包内 `Tools\es.exe` 再校验。
- `scripts/capture-screenshots.ps1` — 新增 `-PressEnter`（仅 QA 截图脚本）。
- `tests/EverythingPal.Core.Tests/ReviewRegressionTests.cs`（新增）— 设置无关的 Core 回归测试。
- `docs/store/packaging.md` — 固定 ES 版本/哈希说明。

## 任务细节与实测输出

### 任务 1 — 重启后设置恢复

触发输入：构造完整 `settings.json`（`MaxResults=17`、`Sort=date-modified-descending`、`FallbackRows=4`），启动一个新的扩展进程（同一 `EverythingPalSettings` 单例在该进程中首次构造）。

修复后实际输出（Windows harness，解包进程，真实 Toolkit + 真实扩展 DLL）：

```json
{"maxResults":17,"fallbackRows":4,"sort":"date-modified-descending"}
```

再用全新进程读取同一文件，结果相同。部分键、显式 0、非法值、无文件：

```text
partial  : {"maxResults":33,"fallbackRows":1,"sort":"name-ascending"}
zero     : {"maxResults":50,"fallbackRows":0,"sort":"name-ascending"}
invalid  : {"maxResults":50,"fallbackRows":1,"sort":"name-ascending"}
no file  : {"maxResults":50,"fallbackRows":1,"sort":"name-ascending"}
```

说明：harness 覆盖了 `EverythingPalSettings` 的真实构造函数与 Toolkit `JsonSettingsManager`。设置页表单的“保存 → 重启扩展 → 再开设置页”仍标为 **待验收**（表单提交未做 UI 自动化）。

### 任务 2 — 首页多结果回车

- 首页查询改为 `MaxResults = 首页行数 + 1`，多取的一条只用于判断。
- `FallbackResultPlan`：`VisibleCount = min(N, returned)`、`HasMultipleMatches = returned > 1`、`HitFetchLimit = returned == N+1`。
- 首行文案：触顶时 `查看更多结果`；未触顶时 `（共 N 个结果）`。多匹配时主动作是结果页，右键仍保留“打开此项”。

Core 矩阵（163 个测试中）：

| 首页行数 | ES 返回 | Visible | 多匹配 | 触顶 |
|---:|---:|---:|---|---|
| 1 | 0 | 0 | false | false |
| 1 | 1 | 1 | false | false |
| 1 | 2 | 1 | true | true |
| 3 | 2 | 2 | true | false |
| 3 | 3 | 3 | true | false |
| 3 | 4 | 3 | true | true |

CmdPal 实测（临时禁用同时安装的 Store 1.0.2.0 扩展以隔离 dev 包，之后已恢复）：
`wantai` 首页仅 1 行：`wantai  E:\ · 查看更多结果`；回车后打开结果页，页内 4 条：
`E:\wantai`、`03-home-wantai.png`、`out-ext_lnk_wantai.txt`、`03-home-wantai-fullscreen.png`。
截图：`artifacts/qa/review-2026-11-20/ui/05-home-store-disabled.png`（首页）、
`06-home-fallback-enter.png`（回车后结果页）。

### 任务 3A — 进结果页再完整搜索（第二轮修正）

- 首页生成结果行时只 `SetSearchNoUpdate(query)` 并记录待执行查询；不触发 `SearchText` setter。
- 页面首次 `GetItems()` 先清标记，再启动原有的防抖搜索；`RaiseItemsChanged()` 后的再次 `GetItems()` 不会重复启动。
- **第二轮修复**：宿主可能在 `GetItems()` 之前用 `UpdateSearchText("", 预填文本)` 回传同一个文本。
  第一轮代码会先清空 `_pendingQuery`，再因为文本等于 `_query` 而返回，导致页面一次搜索都不执行。
  现在状态机 [PageSearchRequestState.cs](src/EverythingPal.Core/PageSearchRequestState.cs) 的规则是：
  与待执行查询相同的回传保留标记；真正不同的新文本才清标记并启动；标记被消费后的相同回传视为重发。
- 新增 4 个回归用例：预填 A → 回传 A → 两次 GetItems 只执行 1 次；预填 A → GetItems → 回传 A 只执行 1 次；
  预填 A → 用户改成 B → 只执行 B；激活时空文本不擦除待执行查询。
- 共享页每次首页搜索先 `ClearPendingSearch()`，避免打开后看到上一个查询。

实测：首页停留 9 秒只看到 1 行；回车后结果页出现 4 条（结果页使用 `MaxResults`，默认 50）。
ES 调用次数没有插桩（避免在日志里记录查询），因此“恰好 1 次首页查询 + 1 次页面查询”的计数项标为
**代码/行为验证，计数待验收**。

### 任务 3B — 取消传到 ES 进程（第二轮修正）

用可控的 `FakeEs.exe`（可 sleep、可模拟 1.4 返回 9、可写 JSON 后启动继承 stdout 的子进程再退出）验证：

```text
cancel : OperationCanceledException, cancelElapsedMs=16, childExited=true, leftoverTempFiles=[]
timeout: Timeout, elapsedMs=1154, childExited=true, leftoverTempFiles=[]
retry  : Ok, resultCount=1, invocations=[nocase:ignorews:term1, term1]
normal : Ok, resultCount=1, invocations=[nocase:ignorews:term1]
orphan : Timeout, elapsedMs=1136, orphanChildPid=..., orphanChildKilled=true, leftoverTempFiles=[]
```

**第二轮修复**：同一个截止时间现在同时覆盖 `WaitForExitAsync()` 和 stdout/stderr 读取。
第一轮只约束了进程退出；如果自定义 ES 包装程序写完 JSON、启动一个继承 stdout 的子进程后自己先退出，
等待管道 EOF 会突破 1,100 ms 的预算（复审实测 3,653 ms 后才返回 Ok）。现在读取通过
`WaitForReadAsync` 与进程等待共享同一个 deadline；超时后关闭本次管道读取、返回 `Timeout`，
调用方取消仍抛 `OperationCanceledException`。实现见
[EverythingSearch.cs](src/EverythingPal.Core/EverythingSearch.cs)。

### 任务 4 — 用户排序

- `SearchOptions.DefaultSort = "name-ascending"`，只有它（或空）走 `ResultRanker.Rank`。
- 解析器新增可选 `sort` 参数；`EverythingSearch` 把 `options.Sort` 传入解析器。

新增测试（Core）：

```text
Date_modified_descending_keeps_the_order_es_returned
Run_count_descending_keeps_the_order_es_returned
Default_sort_still_re_ranks_by_relevance
Relevance_ranking_is_only_used_for_the_default_sort
```

设置说明中英文均已改为“默认名称升序会再按相关性调整；其他排序保留 Everything 顺序”。

### 任务 5 — 高级查询引号（第二轮修正）

规范化结果（Core 测试）：

```text
"wantai"                              -> wantai
"Program Files"                       -> "Program Files"
"Program Files" | "Windows Kits"      -> "Program Files" | "Windows Kits"
ext:pdf "Annual Report"               -> ext:pdf "Annual Report"
D:\wantai\                            -> D:\wantai
"C:\...\Annual Report.pdf"            -> C:\...\Annual Report.pdf
```

**第一轮报告的错误归因已撤回。** 真正原因在 ES 的参数解析层：ES 1.1.1.39 的 `-search` / `-q`
调用 `_es_expect_command_argv()`，该函数明确**删除引号**（上游 `src/es.c:8095-8107`）。所以即使
`QueryNormalizer` 保住了引号，`-search` 也会在 Everything 求值前破坏
`ext:pdf "Annual Report"`。FakeEs 之前只验证了 .NET 已经还原的 `string[] args`，覆盖不到这一点。

第二轮修复：固定选项仍由 Core 构造，搜索表达式改为 `-search*` 的**原始命令行尾部**：
`-search*` 在 ES 源码中是“eat the rest”，不删除引号（`src/es.c:8110-8129`）。
`ProcessStartInfo` 使用 `Arguments` 字符串而不是 `ArgumentList`，表达式逐字符附加在
`-search* ` 之后；固定路径/选项按 Windows 引号规则单独转义。实现见
[EsCommandLine.cs](src/EverythingPal.Core/EsCommandLine.cs) 与
[EsArguments.cs](src/EverythingPal.Core/EsArguments.cs)。

真实 ES（ES 1.1.1.39 + Everything 1.5.0.1423）验收，查询限定到测试目录：

```text
path:C:\Users\...\ep-review-5 ext:pdf "Annual Report"
  -> Ok, 1 条: Annual Report.pdf（Annual revised Report.pdf 未命中）
"Program Files"
  -> Ok, 4 条（C:\Program Files、C:\Program Files (x86) 等）
"Program Files" | "Windows Kits"
  -> Ok, 8 条
"C:\Users\...\ep-review-5\Annual Report.pdf"
  -> 规范化去引号后 Ok，首个结果即目标文件
```

`--search-test` 走同一 Core 路径也再次验证：

```text
status: Ok
FILE C:\Users\Lenovo\AppData\Local\Temp\ep-review-5\Annual Report.pdf
```

最终源码构建重新部署后，在 CmdPal 结果页输入 `ext:pdf "Annual Report"` 只显示
`Annual Report.pdf`，`Annual revised Report.pdf` 不在列表中：
`artifacts/qa/review-2026-11-20/ui/11-final-quoted-phrase.png`。

因此 review 要求的“`Annual Report.pdf` 命中、`Annual revised Report.pdf` 不命中”现已 PASS，
不再有环境限制待验收。

### 任务 6 — 详情面板

`EverythingPalPage` 构造函数设置 `ShowDetails = true`。CmdPal 实测（PowerToys 0.102.3101.0 /
CmdPal 0.13.13096.0）：
- 选中文件夹：名称、完整路径、类型“文件夹”、修改时间显示。
- 换选 `.txt` 文件：详情跟随为文件、105 B、修改时间。
截图：`artifacts/qa/review-2026-11-20/ui/01-details-page.png`、
`02-details-second.png`。

### 任务 7 — 固定 ES 版本

`scripts/es-release.ps1`：

| 架构 | 资产 | SHA-256 |
|---|---|---|
| x64 | `ES-1.1.1.39.x64.zip` | `23DCD7158947B660DBABF52DC983D42840CF34CFF96859F9608BF8A7A3BA8ADE` |
| ARM64 | `ES-1.1.1.39.ARM64.zip` | `1F4F79B6B1723096A0437CD02DBEEF53E0BCC14B3B2C300A5E1AF9BE93BA3CF6` |

临时缓存实测：空缓存下载并校验；正确缓存直接复用；把 ARM64 字节放进 x64 缓存后检测到哈希不符并从
固定 tag 重新下载。最后解包两个架构的 `.msix`：

```text
x64   Tools\es.exe 262824 bytes 23DCD715...8A7A3BA8ADE
arm64 Tools\es.exe 270504 bytes 1F4F79B6...3BA3CF6
```

完整打包（x64+arm64，第二轮修复后重打）通过脚本内置校验：
`artifacts/qa/review-2026-11-20/store/EverythingPal_0.2.0.0_x64_arm64.msixbundle`
（21.35 MB，SHA256 `2180DB00129E5158EE1D2BB09B93D18730E9BDD90E7FDAC98EFFAA616C3C2FF8`，未签名）。

### 任务 8 — 日志隐私

`EverythingPal.exe --search-test EP_LOG_CHECK_8347`（从 workspace 外运行）：

- 两份日志新增行只有 `Main entry. mode=search-test`；
- `%LOCALAPPDATA%\EverythingPal\Logs\extension.log` 与 `%TEMP%\EverythingPal.log`
  均找不到 `EP_LOG_CHECK_8347`；
- `--search-test` 控制台输出保留（本次 `status: NoResults`）。

## 实际运行的测试

```powershell
# 非提权 testhost 会 Win32Exception(5)，按仓库约定提权运行；以下为提权结果
dotnet test .\tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj -c Release -p:Platform=x64
# 第二轮：163 passed, 0 failed, 0 skipped

dotnet build src\EverythingPal\EverythingPal.csproj -c Release -p:Platform=x64 `
  -p:EnableMsixTooling=false -p:WindowsPackageType=None -p:EnableMsixPackaging=false
# 0 warnings, 0 errors

pwsh -File scripts\pack-store.ps1 -OutputDir artifacts\qa\review-2026-11-20\store
# x64 + arm64 publish/pack/bundle + Tools\es.exe SHA-256 validation: PASS

pwsh -File scripts\deploy.ps1 -Configuration Release -RestartCmdPal
# dev package installed and CmdPal restarted: PASS
```

附带的 Windows harness（均在 `artifacts/qa/review-2026-11-20/`，未入库）：
`settings-harness`、`FakeEs`、`CancelHarness`。第二轮 `CancelHarness` 覆盖
`cancel` / `timeout` / `retry` / `normal` / `orphan` / `passthrough` / `reales` 七种模式，
其中 `orphan` 就是“父进程先退出、子进程继续持有管道”的新回归用例。

## 待验收 / 环境限制

1. 设置页 UI 的“保存 → 重启扩展 → 再开设置页”表单流程未自动化；同一设置类的全新进程读回已验证。
2. 任务 3A 的 ES 进程调用次数未插桩；首页/结果页行为、宿主三种回传顺序的状态机与代码路径已验证。
3. 首页 1 行/1 匹配直接打开文件、3 行/2 匹配精确计数等矩阵项由 `FallbackResultPlan` 单测覆盖，
   未逐项做 UI 点击验收。
