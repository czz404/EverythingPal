# Microsoft Store 打包指南（EverythingPal / Command Palette 扩展）

> 目标：产出一个 **UNSIGNED（未签名）的 x64 + ARM64 `.msixbundle`**，直接上传到 Microsoft Partner Center。
> Store 会用自有证书重新签名；本地**不要**给 Store 包做 `signtool`。
>
> 一键入口：`scripts/pack-store.ps1`（本地、CI、release 共用同一条打包路径）。
> 本文所有命令都在仓库根目录 `E:\workspace\deepseek\EverythingPal` 下执行。

---

## 0. TL;DR

```powershell
# 本地/CI 冒烟包（dev identity：Identity Name=EverythingPal，Publisher=CN=czz404，版本 0.2.0.0）
# 产物：artifacts/store/EverythingPal_0.2.0.0_x64_arm64.msixbundle
pwsh -File scripts/pack-store.ps1

# 真实 Store 包：身份和版本都来自 Partner Center（示例值，务必替换）
pwsh -File scripts/pack-store.ps1 `
    -Version 1.0.0.0 `
    -IdentityName "12345ABCDE6789.5F6E7D8C9B0A1F2E" `
    -Publisher "CN=01234567-89AB-CDEF-0123-456789ABCDEF" `
    -PublisherDisplayName "EverythingPal"
```

产物位置（脚本最后会打印完整路径、大小和 SHA256）：

```
artifacts/store/
├── EverythingPal_<version>_x64_arm64.msixbundle   ← 上传 Partner Center 的文件（UNSIGNED）
├── EverythingPal_<version>_x64.msix               ← 单架构包（sideload / 调试用）
├── EverythingPal_<version>_arm64.msix
├── publish-x64/  publish-arm64/                   ← self-contained publish 输出
├── layout-x64/   layout-arm64/                    ← 组好的 MSIX 布局（含注入后的 AppxManifest.xml）
└── manifests/                                     ← 校验证据：解包后的 AppxManifest / AppxBundleManifest
```

> `artifacts/` 已在 `.gitignore` 中，不会误提交。

---

## 1. 为什么必须 **self-contained**

旧流程（`scripts/deploy.ps1`、旧 CI）用 `dotnet build`，产出的是 **framework-dependent** 包：
`EverythingPal.runtimeconfig.json` 要求目标机器安装 `Microsoft.NETCore.App 10.0`。
干净的用户/Store 审阅机器**没有 .NET 10 runtime**，扩展会启动失败（COM server 拉不起来，CmdPal 里表现为"扩展消失"）。

所以 `pack-store.ps1` 改用 **`dotnet publish --self-contained true`**，把 .NET runtime 打进包里，并**强制校验**：

- `hostfxr.dll`、`hostpolicy.dll`、`coreclr.dll`、`EverythingPal.runtimeconfig.json` 都在 publish 输出里；
- `runtimeconfig.json` 含 `"includedFrameworks"`（framework-dependent 时没有）；
- `EverythingPal.exe` / `coreclr.dll` 的 PE machine 与架构一致：x64 = `0x8664`，ARM64 = `0xAA64`。

实测：`PublishTrimmed=true` 约 **25–27 MB/架构**（0.2.0.0，含正式图标和 zh-Hans 资源），`dotnet EverythingPal.dll --search-test readme` 能正常返回结果。
Debug symbols（`*.pdb`）在打包前被剔除，不进入 MSIX。

> 文件类型图标依赖 `System.Drawing.Common`（GDI+）：脚本会断言 publish 输出和包内都有
> `System.Drawing.Common.dll` 与 `System.Private.Windows.GdiPlus.dll`。trimming 若把它们裁掉会直接报错，
> 避免上线后只剩通用字形图标。

> ⚠️ **Release blocker**：任何"用 `dotnet build` 产物打包"的做法都不能用于 Store 提交。
> `scripts/deploy.ps1`（本地 dev 安装）现在也改为 self-contained publish，用来在 CmdPal 里验证**与 Store 包相同的 payload 形态**；
> 它仍使用 dev 身份和自签证书，产物不能提交 Store。

**本次实测**（0.2.0.0 dev identity，2026-10-20，x64 主机、warm NuGet/SDK 缓存，最终构建）：

| 产物 | 大小 | SHA256 |
| --- | --- | --- |
| `publish-x64`（self-contained, trimmed） | 25.3 MB | — |
| `publish-arm64`（self-contained, trimmed） | 26.7 MB | — |
| `EverythingPal_0.2.0.0_x64.msix` | 10.81 MB（11,337,491 B） | `BDA70B17F7C2FCAE14F4AD8F15608D5518CA4624944A9EF1DF126C04A0F5269C` |
| `EverythingPal_0.2.0.0_arm64.msix` | 10.5 MB（11,009,811 B） | `9726E55F884CA58CF0F2E508EA9C9B10694DE8B98DFFEED3A7BAE91E5C63F236` |
| `EverythingPal_0.2.0.0_x64_arm64.msixbundle`（实际提交物） | **21.31 MB（22,349,272 B）** | `73B71AF9B1570FC592DFFAB1522F177F1CCACD65878BBF0E8187B0DFEAAAF8E4` |

> SHA256 每次重新打包都会变化（不是可复现构建）；以 `pack-store.ps1` 本次输出 / `artifacts/store` 里的文件为准。
> 上传前用 `Get-FileHash <file> -Algorithm SHA256` 复核。

两个包都包含 `zh-Hans\EverythingPal.resources.dll`（i18n satellite）。两架构整套打包约 20–30 秒（缓存热时；冷启动首次
publish 会更久，主要是下载 runtime pack + 首次 trim）。trim 期间 `WinRT.Runtime` / `Microsoft.Windows.SDK.NET` 会产生
`IL2104` 警告（第三方程序集 trim 告警，不阻断，项目已设 `ILLinkTreatWarningsAsErrors=false`）。

---

## 2. Manifest 身份字段速查

`src/EverythingPal/Package.appxmanifest` 里几个容易混淆的字段：

| 字段 | 当前值（dev） | 作用 | Store 要求 |
| --- | --- | --- | --- |
| `Identity/@Name` | `EverythingPal` | 包身份（PFN 的一部分） | 必须 **一字不差** 等于 Partner Center 的 `Package/Identity/Name` |
| `Identity/@Publisher` | `CN=czz404` | 发布者身份（签名/PFN），**不会**显示在 UI | 必须等于 Partner Center 的 `Package/Identity/Publisher`（形如 `CN=<GUID>`） |
| `Properties/PublisherDisplayName` | `EverythingPal` | **显示名**：CmdPal 设置 → 扩展 About 卡片显示的就是它 | 应等于 Partner Center 的 `Package/Properties/PublisherDisplayName` |
| `Properties/DisplayName` | `Everything Search` | 包显示名（About 卡片标题、应用列表） | 建议与 Store listing 名称一致 |
| `Identity/@Version` | `0.2.0.0` | 包版本 | `x.y.z.0`，见第 6 节 |

要点：

- 把 `PublisherDisplayName` 从 `czz404` 改成 `EverythingPal` 只影响**显示**，不改包身份，所以可以立刻做（也是这次 About 卡片不再显示 GitHub 用户名的原因）。
- `Identity/@Name`、`Identity/@Publisher` 属于 **Store 身份，当前按用户决定暂缓**：源码保持 dev 值。
  真实值由 `pack-store.ps1` 的 `-IdentityName` / `-Publisher` / `-PublisherDisplayName` 参数**只写进 staged 布局的 `AppxManifest.xml`**，源码 manifest 永远不动。
- `-Publisher` 必须是 Partner Center 的 **CN 字符串**（`CN=...`），不是显示名；注意大小写和 `{}`/连字符的格式。
- 依赖声明只有 `Windows.Desktop`（`MinVersion 10.0.19041.0` / `MaxVersionTested 10.0.26100.0`）。
  不要加回 `Windows.Universal`：那会让 Store 把扩展推给 Xbox/Holo 等根本无法运行 CmdPal 扩展的设备。
- 也没有设置 `AppListEntry="none"`：无参数启动会展示 requirements/diagnostics 对话框（launcher UI 负责）。

> ⚠️ **身份变更的一次性副作用（PFN 变化）**：CmdPal 以 `PackageFamilyName`（PFN）作为扩展设置的 key，
> 持久化内容包括：每个 fallback 行的 `IsEnabled`、`IncludeInGlobalResults`、fallback 顺序、pinned 命令、别名、快捷键。
> 从 dev 身份（`CN=czz404` → PFN `EverythingPal_b32a3jmekh2ja`）切到 Store 身份（`CN=<GUID>` → 新 PFN）后，
> **这些设置会全部重置**：用户在 CmdPal 设置里需要重新打开第三方 fallback 的 "Include in the Global result"
> 并重新配置行数/排序。首版 Store 发布接受该一次性影响；发布后不要再改 `Identity/@Name`/`@Publisher`。

---

## 2.5 本仓库实际注册的 Store 身份（2026-10-20）

| 字段 | 值 |
|---|---|
| 产品名（Store listing） | EverythingPal |
| Package/Identity/Name | EverythingPal.EverythingPal |
| Package/Identity/Publisher | CN=A00B5A3A-58E9-4107-9446-154AD6F2B65F |
| Package/Properties/PublisherDisplayName | EverythingPal |
| Package/Properties/DisplayName（Store 包） | EverythingPal（必须是你预留过的名字之一；用 -DisplayName 注入） |
| Store ID / URL | 9NFD76MCDRJZ / https://apps.microsoft.com/detail/9NFD76MCDRJZ |
| PFN | EverythingPal.EverythingPal_33w78pmn50ga4 |

最终上传包：artifacts/store-identity/EverythingPal_1.0.0.0_x64_arm64.msixbundle
（SHA256 82C236BEF6EADD07FDFBA65ED9A4BA5C7766CF26764B274722157F80B5A80769，未签名）。
生成命令见第 5 节；关键点：Store 严格校验 Identity Name/Publisher/PublisherDisplayName，并拒绝未预留的
Properties/DisplayName，所以必须加 -DisplayName "EverythingPal"。
## 3. Partner Center 一次性流程

1. 注册 Microsoft Partner Center 开发者账号（新 onboarding 流程下个人开发者注册免费；仍需 Microsoft 账户/身份验证）→ **Apps and Games** → **New product** → **MSIX or PWA app**。
2. 预留产品名（Store listing 名称，例如 "Everything Search for Command Palette"）。**listing 名称和 `Identity/@Name` 不必相同**。
3. 创建后进入 **Product management → Product identity**，抄下 4 个值：
   - `Package/Identity/Name`（如 `12345ABCDE6789.5F6E7D8C9B0A1F2E`）
   - `Package/Identity/Publisher`（`CN=<GUID>`）
   - `Package/Properties/PublisherDisplayName`（显示名，可与 "EverythingPal" 不同，按 Partner Center 定的填）
   - **Store ID**（`9N...` 形式，用于商店链接）
4. 用这 4 个值打包（第 0/5 节的命令，`-IdentityName`、`-Publisher`、`-PublisherDisplayName`；Store ID 不进包，只用于 listing/链接）。
5. 上传 `.msixbundle` → 提交认证。

> 没有拿到真实身份前，本地也可以用 dev identity 打"冒烟包"验证整条流水线（就是第 0 节第一条命令），但**不能上传**。

---

## 4. 打包命令与参数

### 4.1 常用命令

```powershell
# 1) 冒烟：dev identity + 源码里的版本（0.2.0.0），两个架构都打
pwsh -File scripts/pack-store.ps1

# 2) 真实 Store 包（版本 + 身份全部覆盖；只影响 staged manifest）
pwsh -File scripts/pack-store.ps1 -Version 1.0.0.0 `
    -IdentityName "<Partner Center Identity Name>" `
    -Publisher "CN=<Partner Center Publisher GUID>" `
    -PublisherDisplayName "<Partner Center PublisherDisplayName>"

# 3) 只重新打包，不重新 publish（改了 manifest/脚本后快速迭代）
pwsh -File scripts/pack-store.ps1 -Version 1.0.1.0 -IdentityName ... -Publisher ... -SkipBuild

# 4) 只打一个架构（本地调试/排查；正式包请用默认的 x64,arm64）
pwsh -File scripts/pack-store.ps1 -Architectures x64

# 5) 关闭 IL trimming（怀疑 trimming 影响 WinRT/COM 激活时）
pwsh -File scripts/pack-store.ps1 -Untrimmed
```

> 若 `dotnet`/`makeappx` 不在 PATH：`$env:PATH = "C:\Program Files\dotnet;" + $env:PATH`（本机 DSH 终端常见），
> 或显式传 `-MakeAppxPath "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"`。

### 4.2 参数表

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Version` | 源码 manifest 的 `Identity/@Version` | 4 段版本；`x.y.z` 会自动补成 `x.y.z.0`。第 4 段必须 0；第 1 段在真实 Store 身份下不能 0 |
| `-IdentityName` | 源码 manifest 的 `Identity/@Name` | 只写 staged manifest |
| `-Publisher` | 源码 manifest 的 `Identity/@Publisher` | 只写 staged manifest；Store 值必须是 `CN=...` |
| `-PublisherDisplayName` | 源码 manifest 的 `Properties/PublisherDisplayName` | 只写 staged manifest |
| `-Architectures` | `x64,arm64` | 支持子集 `x64` / `arm64` |
| `-OutputDir` | `artifacts/store` | 产物目录（相对仓库根或绝对路径） |
| `-EsRoot` | `artifacts/es` | es.exe 缓存根；缺失时自动从 voidtools/ES latest 下载 |
| `-Configuration` | `Release` | 保持 Release |
| `-SkipBuild` | 关 | 复用 `publish-<arch>/`，不跑 `dotnet publish` |
| `-Untrimmed` | 关 | `PublishTrimmed=false`（默认 true） |
| `-MakeAppxPath` | 自动找 Windows SDK | 显式指定 `makeappx.exe` |

---

## 5. `pack-store.ps1` 做了什么

1. **publish（self-contained，每架构）**：

   ```
   dotnet publish src/EverythingPal/EverythingPal.csproj -c Release -r win-<x64|arm64> \
     --self-contained true -p:Platform=<x64|ARM64> -p:WindowsPackageType=None \
     -p:EnableMsixPackaging=false -p:EnableMsixTooling=false -p:PublishProfile= \
     -p:PublishTrimmed=true -o artifacts/store/publish-<arch>
   ```

   `EnableMsixTooling=false` 必须带上，否则 publish 会以 `MSB3030: EverythingPal.pri` 失败（并留下半成品输出）；`PublishProfile=` 用于消掉 NETSDK1198 警告。
2. **自检 publish 输出**：hostfxr/hostpolicy/coreclr/includedFrameworks/PE machine（见第 1 节）。
3. **stage 布局**：复制 publish 输出 → 删 `*.pdb` → 放 `Package.appxmanifest` → 用 XML API 注入
   `Identity/@ProcessorArchitecture`、`Identity/@Version`（以及可选的 Name/Publisher/PublisherDisplayName）→ 重新解析校验。
4. **打包第三方/许可证文件**：`artifacts/es/<arch>/es.exe`（缺失则从 <https://github.com/voidtools/ES> latest 下载，x64/ARM64 zip）
   → `Tools\es.exe`；`THIRD-PARTY-NOTICES.md`、`LICENSE` → 布局根目录。
5. **makeappx pack** 出每架构 `.msix`，然后 `makeappx bundle /d <只含两个 msix 的目录> /p ... /bv <version>`。
   `/bv` 很重要：不带时 makeappx 会用当前日期生成 bundle 版本（如 `2026.1020.733.0`）。
6. **校验**（失败即非 0 退出）：
   - 每个 `.msix` 解包后：`Identity` 的 Name / Publisher / Version / ProcessorArchitecture 与预期一致，
     `PublisherDisplayName` 一致，`EverythingPal.exe` / `runtimeconfig.json` / `hostfxr.dll` / `coreclr.dll` /
     `System.Drawing.Common.dll` / `System.Private.Windows.GdiPlus.dll` / `Tools\es.exe` /
     `THIRD-PARTY-NOTICES.md` / `LICENSE` / `Assets\StoreLogo.png` 存在，包内 PE 架构正确；
   - bundle 解包/读取 bundle manifest：`AppxBundleManifest.xml` 存在，bundle Identity 与版本一致，
     两个架构都在，`FileName` 与包内文件对应；
   - **全链路 unsigned**：`.msix` / `.msixbundle` 里都不能出现 `AppxSignature.p7x`。
7. **输出摘要**：路径、publish/msix/bundle 大小、SHA256、身份/版本横幅、上传提示。

---

## 6. 版本规则（Partner Center）

- 4 段数字：`Major.Minor.Build.Revision`，每段 0–65535。
- **第 4 段（Revision）必须为 0**：Windows 10/11 包该段保留给 Store（Store 可能改它）。
- **第 1 段（Major）不能为 0**：Partner Center 规则（参见 [Package version numbering](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/package-version-numbering?pivots=store-installer-msix)，"except for the first section, which cannot be 0"）。
  - 所以 **`0.2.0.0` 只用于本地/CI/预览**；真实 Store 提交请从 `1.0.0.0` 开始。
- **每次提交必须递增**（同一 app 的 version 不能相同或回退），推荐只 bump 第 3 段：`1.0.0.0` → `1.0.1.0` → …。
- 包内 version 与 bundle version 由脚本保持相同（`/bv`）。
- 脚本在「传了 `-IdentityName`/`-Publisher` 且 Major=0」时直接报错；没传身份时只警告。
- 源码 manifest 当前是 `0.2.0.0`（dev 预览版本，不用于 Store 提交）。

---

## 7. 未签名提交说明

- Partner Center 接受的包类型：`.msix`、`.msixbundle`、`.msixupload`（以及旧 `.appx*`）。本仓库产出 `.msixbundle`。
- **上传未签名包**：Store 会用 Microsoft Store 证书重新签名后分发，不需要你提供证书。
- 你在包里的 `Identity/@Publisher` 只需**等于** Partner Center 的 `Package/Identity/Publisher`；不需要与任何本地证书匹配。
- 本地 `signtool` 只用于 **sideload/GitHub release**（签名证书的 publisher 必须等于包里的 `Identity/@Publisher`）。
- CI/release 在配置了 `SIGNING_PFX_BASE64` / `SIGNING_PFX_PASSWORD` secrets 时会产出**已签名**的 bundle/msix（供 sideload）；
  **Store 上传请用未签名产物**（不配置 secrets 的构建，或本地直接跑脚本）。`pack-store.ps1` 本身永远产出未签名包。

---

## 8. Bundle 内容检查清单

上传前可对照脚本校验项（证据在 `artifacts/store/manifests/`）：

- [ ] bundle 内 `AppxMetadata/AppxBundleManifest.xml` 存在，Identity Name/Publisher/Version 正确；
- [ ] bundle 内含 `EverythingPal_<version>_x64.msix` 与 `_arm64.msix` 两个包，架构分别是 `x64` / `arm64`；
- [ ] 两个包的 manifest 除 `Identity/@ProcessorArchitecture` 外完全一致（同一份源码 + 同一套注入）；
- [ ] 每个包都有：`EverythingPal.exe`、`EverythingPal.dll`、`hostfxr.dll`、`hostpolicy.dll`、`coreclr.dll`、
      `EverythingPal.runtimeconfig.json`（含 `includedFrameworks`）、`System.Drawing.Common.dll`、
      `System.Private.Windows.GdiPlus.dll`、`Tools\es.exe`、`THIRD-PARTY-NOTICES.md`、`LICENSE`、`Assets\*`；
- [ ] 没有 `*.pdb` 残留；
- [ ] 没有任何 `AppxSignature.p7x`（未签名）；
- [ ] `Properties/PublisherDisplayName` = Partner Center 值；`Properties/DisplayName` 与 listing 一致；
- [ ] `Dependencies` 只有 `Windows.Desktop`（`MinVersion 10.0.19041.0` / `MaxVersionTested 10.0.26100.0`）；
- [ ] 版本符合第 6 节；`TargetDeviceFamily` 的 MinVersion（10.0.19041.0）覆盖要支持的 Windows 11 版本；
- [ ] 能力只有 `rescap:runFullTrust`（打包 out-of-process COM server / FullTrust 必需）；**没有**
      `unvirtualizedResources`，也没有 `desktop6:FileSystemWriteVirtualization` / `RegistryWriteVirtualization`
      属性（移除原因见下方说明）。

> **为什么移除 `unvirtualizedResources`**：这是 **restricted capability**，官方定位是"某些桌面游戏 / 外链安装（external
> location）场景"用来关闭 MSIX 的文件/注册表写虚拟化（`desktop6:*WriteVirtualization`）；Store 文档明确表示它不是给普通
> 应用用的，声明后需要在 Submission options 里单独说明，增加认证被问询/延迟的风险。EverythingPal **不需要**它：
> - 设置和日志写在 `%LOCALAPPDATA%\EverythingPal`（每用户可写路径，不受包写虚拟化影响）；
> - `es.exe` 的 `-export-json` 临时文件写在 `%TEMP%`（同样可写）；
> - 包不写 Program Files / HKLM 等需要"unvirtualized"的路径。
>
> 实测（0.2.0.0，manifest 只剩 `runFullTrust`）：CmdPal 页搜索返回真实结果、主页 fallback 行显示真实图标、
> `%LOCALAPPDATA%\EverythingPal\Logs\extension.log` 正常写入、es.exe 临时导出正常，无功能回退。若未来发现某个写入路径
> 确实被虚拟化影响，再考虑加回（会重新触发 restricted capability 审核，需要同步恢复 certification notes 里的说明）。

---

## 9. `.msixupload` 替代方案（可选）

- Partner Center **接受 `.msixbundle` 直接上传**，这是本仓库的主路径，无需 `.msixupload`。
- `.msixupload` 是 Visual Studio 的 "Create App Packages" 产物：本质上是**包含 bundle（以及可选 symbol 文件/资源包）的压缩容器**。
  只有在以下情况才需要它：
  - Partner Center 明确要求 `.msixupload`；
  - 需要随包上传 `.appxsym` 符号或语言资源包；
  - 想用 VS 向导生成并签名。
- 手工把 `.msixbundle` 改成 `.zip` 再改名 `.msixupload` 的做法**未在本仓库验证**，不建议作为首选；如确需，可先用 VS 向导对比结构。
- 本仓库当前不生成 `.msixupload`（TBD，视 Partner Center 页面提示而定）。

---

## 10. CI / release

- `.github/workflows/ci.yml`：每次 push/PR 跑单测；`build-extension` job 直接调用
  `./scripts/pack-store.ps1 -OutputDir artifacts/store`，上传 **unsigned bundle + per-arch msix + manifests** 作为 artifact
  （即 Store 流水线每次都被真实执行一遍）。
- `.github/workflows/release.yml`：`v*` tag 或手动 dispatch 时：
  1. 从 tag/input 解析版本（解析不出则用 manifest 里的版本）；
  2. 调用同一个 `pack-store.ps1`（x64 + ARM64，self-contained，bundle，校验）；
  3. 若配置了签名 secrets：先签 `.msixbundle`（SignTool 会递归签包内 msix），再签单架构 `.msix`，并 `signtool verify`；
  4. 建 draft GitHub release，附 `.msixbundle` + `.msix`。
- **Store 上传用 release 里的 unsigned `.msixbundle`**；如果该 release 的包被签名了，请重新跑一次不配置 secrets 的打包（或用 CI artifact）。

---

## 11. 本地 dev 循环（`deploy.ps1`）与 Store 包的区别

| | `scripts/deploy.ps1`（dev） | `scripts/pack-store.ps1`（Store） |
| --- | --- | --- |
| 构建 | `dotnet publish --self-contained`（同样的 MSIX flags；默认 Debug，`-Configuration Release` 时与 Store 一致） | `dotnet publish --self-contained`（Release + trimming） |
| 身份 | dev identity（`EverythingPal` / `CN=czz404`），直接用源码 manifest | 可用 `-IdentityName` / `-Publisher` / `-PublisherDisplayName` 注入 Partner Center 值（只写 staged manifest） |
| 签名 | 自签 `CN=czz404` dev 证书 | **不签名**（Store re-signs） |
| 安装 | `Add-AppxPackage` 本机安装 | 不安装，只产出包 |
| 用途 | 本机 E2E（验证与 Store 相同的 self-contained payload）、开发热循环 | Partner Center 上传、release 产物 |

`deploy.ps1` 的 `-SkipBuild` 复用 `artifacts/publish/<Platform>`（不重新 publish）；`-Untrimmed` 关闭 trimming 方便排查。
它仍然使用 dev 身份和自签证书：**不要把 dev 产物发给用户或提交 Store**。

与 Store 包做 E2E 对比时建议：

```powershell
pwsh -File scripts/deploy.ps1 -Configuration Release -RestartCmdPal
# 然后 CmdPal -> Reload -> Reload Command Palette extensions，验证 fallback 行/图标/详情面板/错误提示
```

（默认 `-Configuration Debug` 也是 self-contained + trimming，payload 形态相同，只是代码生成/优化不同。）

---

## 12. 常见错误排查

| 现象 / 错误 | 原因与处理 |
| --- | --- |
| `MSB3030: Could not copy ... EverythingPal.pri` | `dotnet publish` 撞上 MSIX tooling。脚本已带 `-p:EnableMsixPackaging=false -p:EnableMsixTooling=false`；自写命令时别漏 |
| `NETSDK1198: A publish profile with the name ... not found` | 加 `-p:PublishProfile=`（脚本已带） |
| 包在干净机器上启动失败 / COM server 拉不起来 | 很可能打成了 framework-dependent。检查 `runtimeconfig.json` 是否有 `includedFrameworks`、包内是否有 `hostfxr.dll`/`coreclr.dll`（脚本第 1 节校验会直接报错） |
| `makeappx.exe not found` | 安装 Windows SDK 10.0.26100，或 `-MakeAppxPath`；也可用 `artifacts/tools/buildtools` 里的 SDK.BuildTools 副本 |
| bundle 版本变成 `2026.1020.733.0` 之类 | 没传 `/bv`。脚本用 `makeappx bundle ... /bv <version>` 固定为包版本 |
| 上传报 publisher / identity 不匹配 | `Identity/@Publisher` 与 Partner Center 的 `CN=...` 必须完全一致（大小写、GUID 格式、无多余空格）；`Identity/@Name` 同理 |
| 上传报版本非法 | 第 4 段非 0、第 1 段为 0、或版本不高于上次提交。见第 6 节 |
| `makeappx pack` 报 `Package creation failed` | 多半是 staged manifest 不合法（XML/属性重复/命名空间）。脚本在注入后会重新解析；先看 `artifacts/store/layout-<arch>/AppxManifest.xml` |
| 下载 es.exe 失败（403 / rate limit） | 设置 `GITHUB_TOKEN` 环境变量重试，或手动把 es.exe 放到 `artifacts/es/<arch>/es.exe`（架构名目录：`x64` / `arm64`） |
| 真实文件图标丢失 / 包内缺 `System.Drawing.Common.dll` 或 `System.Private.Windows.GdiPlus.dll` | trimming 把它们裁掉，或 `System.Drawing.Common` PackageReference 丢了；脚本会直接报错。检查 `src/EverythingPal/EverythingPal.csproj` / `Directory.Packages.props`，必要时加 trimmer root |
| 包内缺 `LICENSE` / `THIRD-PARTY-NOTICES.md` | 布局阶段漏拷贝；脚本会断言，补回仓库根目录的对应文件 |
| 上传报 "device family not supported" | 检查 `Dependencies` 是否被误加回 `Windows.Universal`；桌面扩展只保留 `Windows.Desktop` |
| `Add-AppxPackage` 报 `0x800B0109`（仅 sideload） | 证书未被信任；Store 上传不需要处理。dev 安装用 `deploy.ps1` |
| 安装后 CmdPal 里扩展消失 / `CO_E_WRONG_SERVER_IDENTITY` | 松目录 `-Register` 安装的典型问题；用签名 MSIX 安装（见 README）。与 Store 打包无关 |
| 路径过长 / MAX_PATH 报错 | 把 `-OutputDir` 指到短路径（如 `C:\tmp\store`） |
| 想缩小包体 | 默认已 trimming 且剔除 pdb；`-Untrimmed` 只用于排查 trimming 导致的问题 |

---

## 13. 尚未确认 / 风险（TBD）

- **Store ID / 真实 Identity 值**：需要 Partner Center 账号预留 app 后才能拿到；本文用占位符。
- **Headless 扩展的认证风险**：CmdPal 扩展是 full-trust 后台 COM server，没有主窗口；Store 政策 10.1.2 / 设备兼容性条款可能要求"设备不满足条件时启动给出提示"。缓解方案（另开工单）是在无参数启动时弹出 requirements/About 窗口，并把测试步骤写进 **Notes for certification**（见 [certification-notes.md](certification-notes.md)）。
- **受限能力**：现在只剩 `runFullTrust`（打包 out-of-process COM server 必需）需要在 Submission options 里写理由；
  `unvirtualizedResources` 已移除、`desktop6:*WriteVirtualization` 已删除，不再需要为"关闭写虚拟化"做说明（见第 8 节移除说明）。
- **es.exe 再分发条款**：MIT 允许再分发，但 voidtools Everything 本体许可另算；需确认把 `es.exe` 放进 Store 包的分发许可（研究中，未最终确认）。
- **`.msixupload` 结构**：未在本仓库实测（当前用 `.msixbundle` 即可）。
- **Store 包大小上限**：远超我们预计的 ~50 MB 量级，未专门核对当前上限。
- **认证对 `TargetDeviceFamily` 的要求**：当前只声明 `Windows.Desktop`（桌面 COM server 扩展的诚实声明）；若认证反馈要求调整再单独评审。
- **商店发现性 / Gallery**：CmdPal 0.13+ 设置里有内置 **Gallery/库**（Settings → Extensions → Gallery），内容来自
  `microsoft/CmdPal-Extensions` 社区目录，**链接到扩展的安装来源（WinGet 或 Microsoft Store）**。因此 Store 上架后把本扩展
  提交到 <https://github.com/microsoft/CmdPal-Extensions>（按其 Contributing Guide 提 PR）即可在 Gallery 中被浏览/安装；
  **WinGet 发布可选但推荐**（Gallery 的安装/更新可走 WinGet，且便于包管理器用户）。参考
  [Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery)。
  旧研究文档 D.9 "Store-only 不可发现" 的结论已被 Gallery 功能取代（见 research 文档第 8 节更正）。

---

## 14. 相关文件

- [scripts/pack-store.ps1](../../scripts/pack-store.ps1) — 本文档描述的打包脚本
- [scripts/deploy.ps1](../../scripts/deploy.ps1) — 本地 dev 构建/签名/安装
- [.github/workflows/ci.yml](../../.github/workflows/ci.yml) — 每次 push 验证 Store 打包流水线
- [.github/workflows/release.yml](../../.github/workflows/release.yml) — tag 发布（unsigned Store bundle + 可选签名）
- [src/EverythingPal/Package.appxmanifest](../../src/EverythingPal/Package.appxmanifest) — 包清单（dev identity + Store 注入点说明）
- [listing.md](listing.md) — Store listing 文案素材
- [certification-notes.md](certification-notes.md) — 认证说明 / 审阅者测试步骤
- [docs/research/platform-and-store-research.md](../research/platform-and-store-research.md) — CmdPal/Store 调研（含认证风险与政策条款）

