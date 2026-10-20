# HANDOVER — EverythingPal (Store 上架阶段)

> 这份文档给「下一个 session」用。当前 session 上下文将满，在这里交接全部状态与下一步。
>
> 新 session 的工作区：`E:\workspace\deepseek\EverythingPal`
> 先读本文件 + `README.md`，再 `git log --oneline -10` / `gh run list` 确认状态。

## 这是什么

Everything 搜索扩展，用于 PowerToys Command Palette（CmdPal）。
仓库：<https://github.com/czz404/EverythingPal>（private，用户 czz404）。

- 纯托管扩展（无原生 SDK DLL），通过 **bundled `es.exe`（MIT，来自 voidtools/ES）** 查询本机
  Everything；用户只需安装并运行 Everything。
- 当前状态：**可用版本已完成并本机安装**；CI 全绿；release 工作流已产出 v0.1.0 draft（unsigned）。
- 用户决定：**走 Microsoft Store 上架**，不再考虑自签分发。

## 已实现的功能

- `Everything 搜索` 命令页：实时搜索（150ms 防抖），详情面板（大小/修改时间/可点路径）。
- 主页 fallback：**多行内联结果**（默认 3 行，0–5 可配）。实现方式：一个扩展注册多个
  `FallbackCommandItem`（`czz404.everythingpal.fallback.0/1/2`）。CmdPal 日志确认
  `Loaded 4 command(s)` = 1 命令页 + 3 fallback 行，实测 `fallback result: Ok, 3 item(s)`。
- 动作：打开 / 资源管理器定位 / 复制路径 / 在 Everything 中定位；盘符根路径（`D:\wantai`）正确。
- 错误提示：Everything 未运行 → "Everything 没有运行" + **启动 Everything** / **下载 Everything**；
  es.exe 缺失 → 重装提示；超时 → 索引未就绪提示。
- 设置页：es.exe 路径、结果条数（1–500，默认 50）、排序、主页 fallback 行数（0–5，默认 3）。
- 设置文件：`%LOCALAPPDATA%\EverythingPal\settings.json`。

## 代码结构

```
src/EverythingPal/                 扩展（MSIX、COM server、页面、命令、设置）
src/EverythingPal.Core/            纯逻辑（无 WinRT，单测覆盖）：ES 参数、JSON 解析、
                                   排序、查询规范化、es.exe 定位、进程调用
tests/EverythingPal.Core.Tests/    28 个 xUnit 测试
scripts/deploy.ps1                 构建→下载 ES→组布局→makeappx→signtool→安装
.github/workflows/ci.yml           单测(win) + Release x64 构建 + unsigned MSIX artifact
.github/workflows/release.yml      v* tag：下载最新 ES(x64/ARM64)→构建→打包→可选签名→draft release
Package.appxmanifest               Identity: EverythingPal / Publisher: CN=czz404
```

## 关键环境事实（本机）

- Windows 11 26H1 build 28000.3231（x64），CmdPal 0.13.12862 / PowerToys 0.102.2862。
- 已装：.NET 10 SDK 10.0.402、Windows SDK 10.0.26100.7705、VS Code、GitHub CLI 2.102.0（已 `gh auth login` 为 czz404）。
  ⚠️ DSH 的 pwsh 继承的是安装前的旧 PATH：需要时手动前置
  `C:\Program Files\dotnet`、`C:\Program Files\GitHub CLI`。
- 开发证书 `CN=czz404` 在 `Cert:\CurrentUser\My` 且已信任到 `Cert:\LocalMachine\TrustedPeople`
  （`scripts/deploy.ps1` 会自动处理；**Store 版不需要这个证书**）。
- **必须用签名 MSIX 安装**：`Add-AppxPackage -Register`（松目录）会导致 COM server 启动失败
  `CoResumeClassObjects → 0x80004015 CO_E_WRONG_SERVER_IDENTITY`。这条已实测，别再走松目录。
- MSIX MSBuild 打包 targets 需要 Windows SDK 的 `UAP.props`；装 SDK 后可用，否则只能手工 makeappx。
  `dotnet build -p:WindowsPackageType=None` 用于纯编译（CI 与本地 dev 的打包都由脚本手工 makeappx）。
- `es.exe` 缓存：`artifacts/es/x64/es.exe`（deploy/release 自动下载最新版 voidtools/ES release）。
  包内路径：`Tools\es.exe`（`FindEsExe` 优先使用）。
- 安装后的包：`EverythingPal_0.1.0.0_x64__b32a3jmekh2ja`（PFN `EverythingPal_b32a3jmekh2ja`）。
- 扩展日志：`%LOCALAPPDATA%\EverythingPal\Logs\extension.log`。
- CmdPal 设置：`%LOCALAPPDATA%\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\settings.json`
  里 `ProviderSettings → EverythingPal_b32a3jmekh2ja!App!ID → FallbackCommands` 已把
  `fallback.0/1/2` 设为 `IncludeInGlobalResults: true`（第三方 fallback 默认 false，必须开）。
- DSH 沙箱怪癖（本会话遇到，下个 session 可能也会遇到）：
  - 从 workspace 直接运行构建出来的 `EverythingPal.exe` 时，写 `%TEMP%` 会被拒绝（es.exe 导出失败）。
    把 exe 复制到别的目录运行、或 `dotnet EverythingPal.dll ...` 就正常；CmdPal 拉起的进程不受影响。
  - `dotnet test` 非提权运行时 testhost 报 `Win32Exception(5)`；用
    `Start-Process -Verb RunAs cmd /c "dotnet test ... > log 2>&1"` 跑（UAC 静默）。
  - 有火绒，可能拦截隐藏窗口的 powershell/cmd；被拦时改成交给用户手动执行的一次性命令。

## 当前 git / CI / release 状态

- 分支 `main`，最新提交 `c37fc7c`（clean，已 push）。
- CI：最近几次全绿（单测 28 + Windows x64 构建 + unsigned MSIX artifact）。
- Release：tag `v0.1.0` → draft release 已生成，资产 `EverythingPal_0.1.0.0_x64.msix`、
  `_ARM64.msix`（各 ~8MB，内含 es.exe，**unsigned**）。
- 注意：draft 是从较早 commit（1744849）打的；正式发布前把 tag 移到最新 commit 重新跑。

## 下一步：Microsoft Store 上架清单

1. **Partner Center**
   - 注册 Microsoft Partner Center 开发者账号（个人约 $19 一次性）。
   - Reserve app name（建议 "Everything Search for Command Palette" 或类似，避免与 Everything 官方混淆）。
   - 拿到 Store 分配的 `Identity Name` 与 `Publisher`（`CN=...`），以及 Store ID。
2. **包身份改造**
   - 把 `src/EverythingPal/Package.appxmanifest` 的 `Identity/@Name`、`Identity/@Publisher`
     改成 Partner Center 的值（Publisher 必须一字不差）。
   - 版本号用 Store 要求（`x.y.z.0`，每次提交需递增）。
   - 不需要改 CLSID / 代码；`AppContext.BaseDirectory\Tools\es.exe` 与 PFN 无关。
   - 本地 dev 安装的 `EverythingPal_..._b32a3jmekh2ja` 与 Store 版 PFN 不同，可共存。
3. **提交包**
   - 需要 **x64 + ARM64 的 `.msixbundle`**（Store 接受 `.msix`/`.msixbundle`/`.msixupload`；
     用 `makeappx bundle /f mapping.txt /p EverythingPal_<ver>_bundle.msixbundle`，
     mapping 里分别指向两个 arch 的 msix；或 VS "Create App Packages" 生成 `.msixupload`）。
   - **上传未签名包即可**，Store 会重新签名（现有 release workflow 的签名步骤可跳过/关掉）。
   - 可能需要 `.msixupload`（= bundle + 资源 的 zip）；Partner Center 会提示。
4. **Store 列表材料**
   - 截图（至少 1 张，建议 1366×768 或更高，展示 CmdPal 里搜索 `wantai` 的结果）。
   - 应用图标：需要 StoreLogo 300×300 等更高规格（当前是脚本生成的占位图，建议做正式图标）。
   - 描述（中英）、分类 Utilities & tools、支持链接（仓库/issue）、隐私政策 URL（不收集数据也要有页面）。
   - 年龄分级问卷、免费定价、市场选择。
5. **能力/合规说明（certification notes 要写清楚）**
   - `runFullTrust` + `unvirtualizedResources`：全信任 Win32 扩展 + 关闭写虚拟化（为了日志/Temp 正常）。
   - 扩展会：调用包内 `Tools\es.exe`（MIT 第三方，已附 THIRD-PARTY-NOTICES）；启动用户自行安装的
     `Everything.exe`（"启动 Everything" 动作）；读写 `%LOCALAPPDATA%\EverythingPal`。
   - **没有网络能力**（不声明 internetClient），无遥测、不收集用户数据。
   - CmdPal 第三方扩展的 fallback 需要在 CmdPal 设置里手动开启（IncludeInGlobalResults），
     这属于 CmdPal 平台行为，可在描述里提示用户。
6. **提交前自检**
   - `dotnet build -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64` 通过。
   - `dotnet test`（提权跑）28 通过。
   - 包内 `Tools\es.exe` 存在、`THIRD-PARTY-NOTICES.md` 存在。
   - 用 Store 身份打包后在干净用户（或删掉 dev 包）上安装，CmdPal 能 `Reload` 出
     `Loaded 4 command(s)`，主页搜 `wantai` 出 3 行、详情面板正常、退出 Everything 后提示可启动。

## 可以顺手继续的改进（非阻塞）

- 文件类型图标（按扩展名映射 Segoe 字形）、正式图标资源。
- 中英 i18n（当前 UI 文案为中文硬编码）。
- fallback 分组的单测（需要把 group 逻辑抽到 Core 或做可注入的搜索桩）。
- `msixbundle` 加入 release workflow 产物。
- Node20 action 版本升级（checkout/setup-dotnet/upload-artifact → v5），仅警告。

## 常用命令

```powershell
$env:PATH = "C:\Program Files\dotnet;C:\Program Files\GitHub CLI;" + $env:PATH
cd E:\workspace\deepseek\EverythingPal

pwsh -File scripts/deploy.ps1 -RestartCmdPal     # 本地构建+打包+签名+安装+重启 CmdPal
dotnet test tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj   # 提权跑
gh run list --limit 5                            # 查看 CI
gh release list                                   # 查看 draft release
```

## 用户偏好 / 决策记录

- 扩展名界面用中文；包名 `EverythingPal`；仓库先 private，确认好用后再考虑 public。
- 明确要求：`es.exe` 由 release 自动从 voidtools/ES 下载并打包；错误提示必须明确可操作。
- 已卸载旧的 VictorLin ECP 插件，只保留本插件。
