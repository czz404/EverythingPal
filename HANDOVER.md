# HANDOVER — EverythingPal (Store 上架准备完成，待 Partner Center 身份)

> 给下一个 session 的交接。工作区：`E:\workspace\deepseek\EverythingPal`
> 先读本文件 + `README.md`；`git status` 当前应干净（本 session 末尾会提交）。
> 上架材料入口：`docs/store/README.md`（提交清单）、`docs/store/packaging.md`（打包/身份）、
> `docs/store/listing.md`（商店文案）、`docs/store/certification-notes.md`（审核备注）、
> `docs/research/platform-and-store-research.md`（CmdPal API + Store 规则的实证研究）。

## 当前状态（2026-10-20 收尾）

- 版本 **0.2.0.0**，`main` 干净、CI 预期全绿；单元测试 **67/67**（提权跑）。
- 已完成一轮“发布前打磨”（用户明确要求不要急着上架）：
  - **真实文件类型图标**（SHGetFileInfo + 每扩展名缓存 PNG；页面与主页 fallback 行都有），
    不再用笼统的文件夹/文件字形。图标类：`src/EverythingPal/Services/ShellIconProvider.cs`。
  - **中英双语**（跟随系统 UI 语言，`EVERYTHINGPAL_LANG` 可强制）：`Resources/Strings*.resx` +
    `Localization/Strings.cs`，95 个键，en/zh 键集一致（有测试）。
  - **About 页不再出现 GitHub 用户名**：`Package.appxmanifest` 的
    `<Properties><PublisherDisplayName>` 改为 `EverythingPal`（CmdPal 的 About 卡只读这个字段；
    `Identity/@Publisher` 不显示）。
  - **回退命令三行可辨认**：每行 `DisplayTitle` 为「Everything 搜索（第 N 行）」；三行=三个
    `FallbackCommandItem`（CmdPal 的 fallback 模型就是一行一个命令，没有多行 API）。主页 fallback
    延迟改为 50 ms（宿主对 inline fallback 有约 200 ms 预算），并先写 Title 再异步升级图标。
  - **无参启动诊断对话框**（`Ui/LaunchDialog.cs`，Win32 MessageBox）：开始菜单点开不再“死掉”，
    显示 PowerToys/Everything/es.exe 检测结果、版本、日志路径，可一键打开 Command Palette。
  - **Core 正确性**：空结果不再显示空白列表（`SearchOutcomeMapper`）；盘符根 `D:` 有标题；
    Everything.exe 发现支持自定义安装目录（注册表 App Paths/Uninstall、运行中进程、PATH——本机
    `D:\software\Everything` 已验证）；es.exe 路径按配置缓存。
  - **自包含打包**：`scripts/pack-store.ps1` 用 `dotnet publish --self-contained -p:PublishTrimmed=true`
    产 x64+ARM64 未签名 `.msixbundle`（干净 Store 机器无需装 .NET）。`deploy.ps1` 也改为自包含，
    用于本地签名安装/E2E。
  - 图标资源重做为多 scale（`scripts/generate-icons.py`，37 个 PNG，含 300×300 商店图标）。

## 产物 / 已核实

- **Store 上传包（未签名，最终）**：
  `artifacts/store/EverythingPal_0.2.0.0_x64_arm64.msixbundle`
  size 22,348,532 B，SHA256 `512B9998E3A5BFF0240F41DCD573DF74DECE159A8B3704727EF90B98C9A4D8DB`
  （x64/ARM64 各 ~10.8/10.5 MB；`artifacts/store/` 下还有两个 per-arch .msix 与 manifests）。
  该 bundle 由 `scripts/pack-store.ps1` 生成，未签名（Store 重签）；本地安装验证用
  `scripts/deploy.ps1 -Configuration Release` 出来的自包含签名包。
- 本机已安装验证：`EverythingPal_0.2.0.0_x64__b32a3jmekh2ja`（Developer 签名，自包含）。
  CmdPal 中：命令页/主页 3 行 fallback 均可见，真实图标（.lnk 箭头、文件夹、zip 图标）已确认。
- 打包校验（脚本自动）：自包含 runtime、PE arch、zh-Hans satellite、GDI/System.Drawing、
  Tools\es.exe、LICENSE + THIRD-PARTY-NOTICES、无 PDB、无 AppxSignature（unsigned）、
  bundle 两架构同 identity、Windows.Desktop-only、`en-US`+`zh-Hans` 资源语言。
- `docs/store/screenshots/` 下为最终商店截图（QA 采集，≥1366×768；提交前按 listing.md 复核）。

## 上架还差什么（按顺序）

1. **Partner Center 注册 + 预留应用名**（用户个人账号 $19）：拿到 `Package/Identity/Name`、
   `Package/Identity/Publisher`（`CN=<GUID>`）、`Package/Properties/PublisherDisplayName`、Store ID。
   当前源码里的 `Identity Name=EverythingPal / Publisher=CN=czz404` 只是 dev 身份，**不要**直接提交。
2. **用 Store 身份重打**（不改进源码 manifest）：
   ```powershell
   pwsh -File scripts/pack-store.ps1 -Version 1.0.0.0 `
     -IdentityName "<PartnerCenter Name>" -Publisher "CN=<GUID>" `
     -PublisherDisplayName "<Partner Center 的 PublisherDisplayName>"
   ```
   注意 Partner Center 版本规则：**第一段不能为 0**（Store 提交用 1.0.0.0；之后每次递增第三段），
   第四段必须 0。脚本对 major=0 + Store 身份会硬报错。
3. **公开支持/隐私 URL**：仓库现在是 private → `docs/store/privacy-policy.md` 需要托管到可公开访问
   的 URL；支持链接同理（或把仓库转 public）。这是提交阻塞项。
4. **决定 `unvirtualizedResources`**（认证风险 R2）：manifest 里保留了两条 `desktop6:*WriteVirtualization
   disabled` + `unvirtualizedResources`。微软文档说该能力面向游戏/external-location，“不面向其他场景”，
   审核可能要求 justification 或拒绝。建议做一次移除实验（设置/日志会落到包 LocalCache；es.exe 的
   `%TEMP%` 导出仍需回归）——`docs/store/certification-notes.md` 已写了两套说法。
5. **提交认证**：上传未签名 bundle（Store 重签）；Submission options 解释 `runFullTrust`
   （打包 COM server 必需）；列表按 `docs/store/listing.md`；Notes for certification 用
   `docs/store/certification-notes.md` 的英文短版（PowerToys + Everything 前提、Win+Alt+Space 测试步骤、
   fallback 开启方法、无网络）。审核通常 1–3 个工作日。
6. **发布后**：PFN 会从 `EverythingPal_b32a3jmekh2ja` 变 Partner Center 的 PFN → CmdPal 里该扩展的
   per-fallback 开关/pin/alias 等设置会丢（settings 以 ProviderId=PFN!ExtensionId 为键），发版说明里写清。
   另外 **Store-only 的 CmdPal 扩展在命令面板里不可发现**，建议同时发 WinGet + 商店 assoc tag。

## 重跑验证

```powershell
$env:PATH = "C:\Program Files\dotnet;C:\Program Files\GitHub CLI;" + $env:PATH
cd E:\workspace\deepseek\EverythingPal

# 单测（必须提权；非提权 testhost 会 Win32Exception(5)）
#   Start-Process -Verb RunAs cmd /c "dotnet test ... > log 2>&1"
dotnet test tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj

pwsh -File scripts\pack-store.ps1                    # ~20s（冷）/ 8s（热），产 artifacts/store/*.msixbundle
pwsh -File scripts\verify-build.ps1                  # 独立校验（测试/包/无头搜索），报告 docs/qa/verification.md
pwsh -File scripts\deploy.ps1 -Configuration Release -RestartCmdPal   # 本机自包含签名安装 + 重启 CmdPal
pwsh -File scripts\capture-screenshots.ps1 -Mode page -Queries wantai -SelectFirst -Backdrop -Names 01-search-page
pwsh -File scripts\capture-screenshots.ps1 -Mode home -Queries wantai -SelectFirst -Backdrop -Names 02-home-inline
```

## 环境 / 沙箱怪癖（本机）

- Windows 11 26H1 build 28000.3231 (x64)；CmdPal 0.13.12862（host ABI 0.13.261013002）；
  SDK 引用 `Microsoft.CommandPalette.Extensions` 0.12.260812002（NuGet 最新稳定，和 host ABI 兼容）。
- PATH 里要手动前置 `C:\Program Files\dotnet`、`C:\Program Files\GitHub CLI`（DSH 继承的是安装前 PATH）。
- DSH 沙箱：**从 workspace 路径直接跑发布产物时 es.exe 会 `Error 5: Failed to create output file`**
  （临时文件写不进去的沙箱怪癖）；把 self-contained publish 复制到 workspace 之外
  （例如 `E:\workspace\deepseek\_epwt-publish`）再跑 `dotnet EverythingPal.dll --search-test readme`
  就正常（本 session 已验证 status Ok）。CmdPal 拉起的已安装扩展不受影响。
- 非提权 `dotnet test` 的 testhost 会 `Win32Exception(5)`；用
  `Start-Process -Verb RunAs cmd /c "dotnet test ... > log 2>&1"`。
- 有火绒，可能拦截隐藏窗口的 powershell/cmd；被拦时交给用户手动执行。
- `dotnet publish` 必须加 `-p:EnableMsixTooling=false`（否则 MSB3030 缺 EverythingPal.pri）和
  `-p:PublishProfile=`（否则 NETSDK1198）；脚本已处理。
- 安装/本地签名：dev 证书 `CN=czz404` 在 `Cert:\LocalMachine\TrustedPeople`；Store 版不需要。
  仍是 `Add-AppxPackage` 安装（不要用 `-Register` 松目录，COM server 会 CO_E_WRONG_SERVER_IDENTITY）。

## 用户偏好 / 决策

- **不要署名**：CmdPal 设置页 About 不显示个人 GitHub 用户名；发布者显示名 = `EverythingPal`。
- 界面中英双语、跟随系统；商店列表中文为主 + 英文。
- 上架前先把本体打磨好（本 session 的主线）；Store 身份/隐私 URL 等留到 Partner Center 阶段。
- 扩展名界面用中文；包名 `EverythingPal`；`es.exe` 由构建自动从 voidtools/ES 下载并打包。
- 之前已卸载 VictorLin ECP 插件，只保留本插件。

## 代码结构

```
src/EverythingPal/                 扩展（MSIX、COM server、页面/命令/设置、图标、i18n、启动对话框）
src/EverythingPal.Core/            纯逻辑（无 WinRT，单测覆盖）：ES 参数、JSON 解析、排序、
                                   查询规范化、es.exe/Everything 发现、退出码映射
tests/EverythingPal.Core.Tests/    67 xUnit 测试
scripts/deploy.ps1                 自包含 publish -> layout -> makeappx -> signtool -> 安装
scripts/pack-store.ps1             自包含 x64+ARM64 -> MSIX -> 未签名 Store bundle（身份参数化）
scripts/verify-build.ps1           QA 独立验证（含从 workspace 外跑无头搜索）
scripts/capture-screenshots.ps1    自动截 CmdPal 窗口（商店截图，≥1366×768）
scripts/generate-icons.py          多 scale 图标生成（Pillow）
docs/store/                        商店材料；docs/qa/ QA 报告；docs/research/ 平台研究
```
