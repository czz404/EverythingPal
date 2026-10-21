# Everything Search for Command Palette — Store 上架材料总览 / Store submission kit

> 状态：**已发布（2026-10-20）** — Microsoft Store: <https://apps.microsoft.com/detail/9NFD76MCDRJZ>（Store ID `9NFD76MCDRJZ`）。材料 v1 由 brand-materials（task-4）产出。
> 图标资源（A）与 Partner Center 材料（B）已完成；隐私/支持 URL 已公开、4 张截图已交付、包能力只剩 `runFullTrust`。**提交前剩余项**：Partner Center 真实身份/名称、voidtools 再分发确认，见 §4 风险与 §3 清单。
> 研究依据：[docs/research/platform-and-store-research.md](../research/platform-and-store-research.md)（CmdPal API / Store 认证研究）。

---

## 0. 交付物地图

| 文件 | 内容 | 状态 |
| --- | --- | --- |
| [listing.md](listing.md) | 应用名候选、中英简短/长描述、功能、分类、搜索词、商标、定价、可发现性（Gallery）、截图清单 | 文案就绪；待最终名称/Publisher 确认 |
| [privacy-policy.md](privacy-policy.md) | 中英双语隐私政策（无网络/无遥测、本地设置与日志、第三方 es.exe） | ✅ 已随 public 仓库发布：https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md |
| [certification-notes.md](certification-notes.md) | 认证备注（可粘贴英文版 + 中文版 + 能力/外部进程/headless 解释 + Q&A） | ✅ 已更新（能力只剩 runFullTrust；UI 已双语）；待复核无参启动文案 |
| [support.md](support.md) | 支持渠道、FAQ（Everything 未运行、fallback 行、Gallery、日志、卸载）、反馈模板 | ✅ 仓库已 public：https://github.com/czz404/EverythingPal/issues |
| [packaging.md](packaging.md) | 打包/Partner Center 流程（store-packaging 负责） | 由 store-packaging 产出，已存在 |
| `assets/store-icon-300.png` | Store 列表 1:1 图标（300×300 PNG） | 已生成 |
| [assets/icon-inventory.md](assets/icon-inventory.md) | 全部图标资源：尺寸、字节、SHA-256 前 12 位（脚本自动生成） | 已生成 |
| [assets/icon-preview.png](assets/icon-preview.png) | 图标预览接触表（浅色/深色背景，供人工检查） | 已生成 |
| [gallery-submission/](gallery-submission/) | CmdPal 社区 Gallery PR 的 staged 文件（extension.json、256 icon、4 张截图、提交步骤） | 待提交 PR |
| screenshots/ | Store 截图（≥1366×768，中文 UI） | ✅ QA 已交付 4 张：`01-search-page`、`02-home-inline-result`（默认 1 行）、`03-context-menu`、`04-zero-result`；可选补设置页/错误态 |
| [../research/platform-and-store-research.md](../research/platform-and-store-research.md) | 平台 API / Store 认证研究 | 已完成（research 任务） |

---

## 1. 图标资源（交付 A）

### 1.1 设计说明（Fluent 风格，无第三方商标）

- **形状**：Fluent 风格圆角方形应用图标（圆角半径 = 边长 22.5%）；图标类资源（StoreLogo / Square44x44 / targetsize-unplated / Store 300）自带圆角轮廓 + 透明角；瓦片背景类资源（Square150 / Wide / Splash）为满幅渐变，避免与 Windows 瓦片形状叠加出现双圆角。
- **渐变**：靛蓝 → 蓝 对角渐变 `#6366F1 → #2563EB`（左上柔光高光 + 右下轻微暗角 + 1px 内描边），符合 Fluent 的“有深度但不花哨”。
- **主图形**：白色放大镜（镜片圆环 + 45° 圆头手柄），粗描边保证 16px 可辨认。
- **Command Palette 语义**：镜片内部三行圆角“结果行”（full 细节）；49–127px 用两行；≤48px 去掉行，避免小尺寸糊成一团。宽版 310×150 瓦片在右侧另画三行结果条，形成“放大镜 + 结果列表”的组合语义。
- **无商标**：图标为脚本参数化原创（放大镜 + 抽象结果行），不含 voidtools Everything 图标、PowerToys/CmdPal 标志；配色也刻意使用项目自有靛蓝而非任何官方品牌色。
- **可复现**：纯 Pillow 参数化绘制，先以 2–8× 超采样绘制再 Lanczos 降采样，无网络、无外部素材、无字体；同一 Pillow 版本下重跑结果逐字节一致（sha256 稳定）。

### 1.2 资源清单（37 个 PNG，共约 758 KB）

| 组 | 基准尺寸 | 变体 | 数量 |
| --- | --- | --- | --- |
| `StoreLogo` | 50×50 | 基名 + scale-100/125/150/200/400（63/75/100/200） | 6 |
| `Square44x44Logo` | 44×44 | 基名 + scale-100/125/150/200/400（44/55/66/88/176） | 6 |
| `Square150x150Logo` | 150×150 | 基名 + scale-100/125/150/200/400（150/188/225/300/600） | 6 |
| `Wide310x150Logo` | 310×150 | 基名 + scale-100/125/150/200/400（310×150…1240×600） | 6 |
| `SplashScreen` | 620×300 | 基名 + scale-100/125/150/200/400（620×300…2480×1200） | 6 |
| `Square44x44Logo.targetsize-*_altform-unplated` | 16 / 24 / 32 / 48 / 256 | unplated（任务栏/大图标） | 5 |
| `LockScreenLogo` | 24×24 | 兼容保留（manifest 当前未引用） | 1 |
| `docs/store/assets/store-icon-300.png` | 300×300 | Partner Center 1:1 应用图标 | 1 |

逐文件尺寸/字节/SHA-256：[assets/icon-inventory.md](assets/icon-inventory.md)。程序化校验（全部 PNG、RGBA、尺寸精确匹配）通过：

```text
37 PNG assets, 758,372 bytes total
OK: all assets present with the expected dimensions.
```

### 1.3 重新生成 / 校验

```powershell
# DSH 自带 Python（含 Pillow）；系统 python 不可用时用这个路径
$py = "C:\Users\<you>\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"

& $py scripts/generate-icons.py --preview     # 生成全部图标 + inventory + preview
& $py scripts/generate-icons.py --check       # 只校验：存在性、尺寸、PNG、RGBA（CI 可用）
```

脚本：`scripts/generate-icons.py`（新增，仅依赖 Pillow；numpy 可选，仅用于加速渐变）。

### 1.4 打包注意：`resources.pri` 与 scale 变体

当前 `deploy.ps1` / `pack-store.ps1` 都是**手工布局 + `makeappx pack`**，不会生成 `resources.pri`。这意味着：

- 包内 manifest 引用的基名文件（`Assets\StoreLogo.png` 等）一定可用；
- `.scale-*` / `.targetsize-*` 资源文件会随包发布，但**只有当打包流程生成 resources.pri（MSBuild `GenerateAppxPackageOnBuild` 或 `makepri`）时才会按 DPI/主题被 MRT 选中**；否则 Windows 统一回退到基名图。
- 影响：不会导致安装/认证失败；只是高 DPI 下可能显示基名图。**可选改进**（不属于本任务写权限）：在 packaging 流程里加 `makepri`，或在 CI 用 MSBuild 打包；见 [packaging.md](packaging.md) 的后续优化。

---

## 2. 材料状态（交付 B）

| 材料 | 中文 | English | 状态 / 缺口 |
| --- | --- | --- | --- |
| 应用名候选 | ✅ 5 个候选 + 推荐流程 | ✅ | 待 Partner Center 实际 reserve 结果 |
| 简短描述 | ✅ 124 字符 | ✅ 206 字符 | 符合 ≤270 目标；字段硬上限 TBD |
| 长描述 | ✅（功能/前提/fallback 说明/商标） | ✅ | 可直接粘贴 |
| 功能列表 | ✅ 8 条 | ✅ 8 条 | Product features 上限 TBD |
| 分类 / 搜索词 | ✅ Utilities & tools + 7 搜索词 | ✅ | 子类别/上限 TBD |
| 要求/定价/市场 | ✅ | ✅ | — |
| 商标/版权声明 | ✅ | ✅ | 作者实体名 TBD |
| 隐私政策 | ✅ 完整 | ✅ 完整 | ✅ 已托管（公开仓库文件，URL 可用） |
| 认证备注 | ✅ 中文详解 + Q&A | ✅ 可粘贴短版 | 待复核无参启动最终文案；UI 已双语，能力仅 `runFullTrust` |
| 支持/FAQ | ✅ 14 个 FAQ（新增 Gallery） | ✅ 摘要 | ✅ 仓库已 public |
| 截图清单 | ✅ 4 张已交付 + 3 张可选 shot list | ✅ | ✅ `01`–`04` 已交付（1366×768）；`05`–`07` 可选 |
| 商店可发现性（Gallery） | ✅ 文案/FAQ/清单已加 | ✅ | 发布后提交 `microsoft/CmdPal-Extensions`（见 §3.5、R6） |
| Store 图标 300×300 | ✅ | — | 已生成 |
| 包/身份/打包 | 由 store-packaging 负责 | — | 见 [packaging.md](packaging.md) |

---

## 3. 提交前清单（Checklist）

### 3.1 决策（Lead / 用户）

- [ ] 确定 Store 产品名（建议 `Everything Search for Command Palette`；备选见 [listing.md](listing.md) §1）
- [ ] 确定作者/发布者显示名（与包内 `Properties/PublisherDisplayName` 一致）
- [ ] 决定列表语言（zh-CN + en-US）与 UI 语言是否一致
- [ ] 确认 voidtools 对 `es.exe` 再分发的许可
- [ ] 发布后提交 CmdPal 社区 Gallery（`microsoft/CmdPal-Extensions`），并决定是否同时发 WinGet
- [ ] 个人开发者注册（新 onboarding 流程免费；需 Microsoft 账户 + 身份验证）

### 3.2 包与身份（store-packaging）

- [ ] Partner Center 建产品、reserve 名称，复制 `Package/Identity/Name`、`Package/Identity/Publisher`（`CN=<GUID>`）、`Properties/PublisherDisplayName`、Store ID
- [ ] 用真实身份重打包：`scripts/pack-store.ps1 -Version x.y.z.0 -IdentityName ... -Publisher ... -PublisherDisplayName ...`
- [ ] `Identity/@Version` 四段、第四段 0（Learn 要求；Store 可改第四段）
- [ ] 产物为 **unsigned x64+ARM64 `.msixbundle`**（Store 会重新签名；[Learn: App package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)）
- [ ] bundle 内两包除 `ProcessorArchitecture` 外 manifest 一致
- [ ] 包内 `Tools\es.exe`、`THIRD-PARTY-NOTICES.md`、全部 `Assets\*.png` 存在
- [ ] 干净机器实测安装 + CmdPal `Reload` 后 `Loaded 2 command(s)`（1 命令页 + 1 默认 fallback 行；行数改为 N 后应为 N+1）、搜索/详情/错误提示正常

### 3.3 列表素材（4S/Lead）

- [ ] 上传 `assets/store-icon-300.png`（300×300，1:1）
- [ ] 上传 QA 已交付的 4 张（`01-search-page` / `02-home-inline-result`（默认 1 行）/ `03-context-menu` / `04-zero-result`，均 1366×768），按 [listing.md](listing.md) §10 配中英说明（≤200 字符/张）；可选补设置页/错误态
- [ ] 隐私政策 URL（https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md）与支持 URL（https://github.com/czz404/EverythingPal/issues）确认可访问（✅ 仓库已 public）
- [ ] 长/短描述、功能列表、搜索词、分类、价格/市场、年龄分级（IARC）

### 3.4 认证与合规

- [ ] `runFullTrust` justification（打包 COM 服务器；非提权）——当前唯一受限能力，无需写虚拟化说明
- [ ] Notes for certification 粘贴 [certification-notes.md](certification-notes.md) §1 英文短版
- [ ] 确认无参启动诊断对话框已实现并实测（launcher-ui）
- [ ] 确认应用无网络能力、无遥测（manifest + 代码核对）
- [ ] 确认 `THIRD-PARTY-NOTICES.md` 在包根、es.exe MIT 文本完整

### 3.5 提交后

- [ ] 认证结果跟进（通常 1–3 个工作日）
- [ ] 发布后验证：Store 安装 → CmdPal 可见 → 中文/英文列表正确
- [ ] 记录 identity 切换导致 CmdPal 设置重置的问题（见 R5），在发布说明/支持页提示
- [ ] **提交 CmdPal 社区 Gallery**：向 https://github.com/microsoft/CmdPal-Extensions 提 PR（按其 Contributing Guide）；上架后用户可在 CmdPal Settings → Extensions → Gallery 搜索/一键安装（Gallery 链接 WinGet 或 Microsoft Store 安装源）
- [ ] 可选：发布 WinGet（Gallery 安装/更新可走 WinGet，推荐）；提供 Store 关联标签（见 R6）

---

## 4. 高风险（按严重度排序，**提交前必须逐条处理**）

> 研究文档对每条的详细依据见 [platform-and-store-research.md](../research/platform-and-store-research.md) §D/§E/§6。

| # | 风险 | 严重度 | 说明 | 缓解 / 负责人 |
| --- | --- | --- | --- | --- |
| R1 | **headless CmdPal 扩展能否通过认证未知** | 🔴 高 | Microsoft 官方文档明确“Command Palette 扩展可以发布到 Store”，并给出流程/备注要求；但**没有可验证的公开 headless 扩展成功样本**（研究文档标记未验证）。Store 政策 10.1.2“完全可用”+“设备不兼容时启动须提示”可能被审核套用。 | ① 无参启动诊断对话框已实现（`Program.cs` 47–51 + `Ui/LaunchDialog.cs`）并需实测；② 认证备注写清扩展性质 + 测试步骤；③ 保留开始菜单入口可点击；④ 如仍被拒，按反馈补 UI/说明。**Lead 决策** |
| R2 | ~~`unvirtualizedResources` 认证风险~~（已解决） | ✅ 已解决 | 该能力与两条 `desktop6:*WriteVirtualization` 声明已从 manifest 移除，并实测通过（设置/日志/es.exe 临时导出正常）；现在唯一受限能力是 `runFullTrust`，认证只需解释它。 | store-packaging/Lead；已由 manifest + 实机验证 |
| R3 | ~~支持 URL / 隐私 URL 不可访问~~（已解决） | ✅ 已解决 | 仓库已 public：隐私政策 URL = https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md，支持 URL = https://github.com/czz404/EverythingPal/issues。 | Lead/用户已完成；提交时直接填入 Partner Center |
| R4 | voidtools 对 `es.exe` 再分发许可未确认 | 🟠 中 | MIT 许可允许再分发，但 Everything 本体条款不同；Store 政策 10.1.6/10.2 把第三方内容合规责任放在开发者。 | 用户/Lead 向 voidtools 确认或保留书面记录；认证备注主动说明 es.exe 为 MIT 第三方组件 |
| R5 | **身份切换（Store PFN）会清空 CmdPal 设置** | 🟠 中 | CmdPal 以 PFN+AUMID 为 key 保存 provider 设置（fallback 开关、pin、别名等）；从 dev 身份换到 Store 身份后这些设置会“消失”，用户会以为扩展坏了。 | 发布说明 + 支持页提示：装 Store 版后重新启用回退命令；或在首启诊断对话框提示（launcher-ui 已在做） |
| R6 | Store 上架后的可发现性（Gallery 提交） | 🟠 中 | CmdPal 0.13+ 有内置 **Gallery/库**（Settings → Extensions → Gallery），由 `microsoft/CmdPal-Extensions` 社区目录驱动，安装来源是 **WinGet 或 Microsoft Store**；**不提交社区目录就不会出现在 Gallery**（用户只能靠 Store 搜索/商店链接找到）。旧结论“Store-only 扩展在 CmdPal 内不可发现/必须 WinGet”已过时，已修正。 | Store 上架后立即向 https://github.com/microsoft/CmdPal-Extensions 提 PR（按 Contributing Guide）→ Gallery 可搜到；WinGet 发布可选/推荐；可用 `x-cmdpal://extensions/gallery` 链接辅助。 |
| R7 | 第三方 fallback 默认不进全局结果 | 🟠 中 | 安装后主页默认 **1 行**内联结果需要手动启用该回退命令；用户以为“不工作”；审核也可能误判功能缺失。 | 长描述 + 支持页 + 认证备注 + 首启诊断对话框均说明开启方法（默认 1 行，0–5 可调） |
| R8 | 多 scale 资源需要 `resources.pri` 才生效 | 🟡 低 | 当前 makeappx 打包不生成 PRI，高 DPI 回退到基名图（功能/认证不受影响） | 可选：packaging 流程加 makepri/MSBuild 打包（见 §1.4）；不影响本次提交 |
| R9 | 名称含 “Everything” 的商标风险 | 🟡 低 | 描述性使用通常可接受，但第三方商标出现在产品名有被下架/异议风险 | 首选 `Everything Search for Command Palette`；描述与支持页加非官方声明；准备备选名 |
| R10 | 截图需真实展示扩展结果（已基本解决，剩隐私细节） | 🟡 低 | QA 已用 0.2.0.0 交付 4 张 1366×768：`01-search-page`、`02-home-inline-result`（默认 1 行）、`03-context-menu`、`04-zero-result`。`01`/`03` 画面含本机路径 `C:\Users\Lenovo\...`。 | 提交前按 [listing.md](listing.md) §10 配说明文字；可选补设置页/错误态；若介意用户名，重拍时用测试账户或查询不含 `C:\Users` 的结果 |
| R11 | Partner Center 字段上限未核实 | 🟡 低 | Short description / Product features / 搜索词 / 认证备注长度上限未经 Learn 证实 | 提交时按字段提示删减；文档内所有文案均短于常见上限 |

---

## 5. 研究结论核对（reconciled with research doc）

| 事实 | 结论 | 来源 |
| --- | --- | --- |
| 截图：≥1 张必需、桌面 ≤10 张、PNG ≤50 MB、桌面 ≥1366×768、说明 ≤200 字符 | **已核实**（与 research 一致） | [Learn: screenshots-and-images](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images) |
| Store 列表 1:1 图标 300×300 推荐，且 Store 优先于包内图标 | **已核实** | 同上 |
| Command Palette 扩展可以发布到 Microsoft Store（官方流程、描述/备注要求、上传 `.msixbundle`） | **已核实** | [Learn: Publish to Microsoft Store (CmdPal)](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-store) |
| 未签名 `.msixbundle` 提交后由 Store 重新签名 | **已核实** | [Learn: App package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements) |
| 版本四段、第 4 段提交时置 0；bundle 内各包 manifest 除架构外一致 | **已核实** | 同上 |
| `runFullTrust` 为受限能力、需 justification；包内现已无 `unvirtualizedResources`/desktop6 写虚拟化 | **已核实**（manifest + 实机复测） | [Learn: App capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations) |
| CmdPal 0.13+ 内置 Gallery（Settings → Extensions → Gallery），由 `microsoft/CmdPal-Extensions` 社区目录驱动，安装来源为 **WinGet 或 Microsoft Store**；提交 PR 即可上架 Gallery | **已核实**（修正旧结论“Store-only 扩展在 CmdPal 内不可发现、必须 WinGet”） | [Learn: Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery)、[Finding and installing Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/finding-and-installing-extensions) |
| 个人开发者注册费已取消（免费；公司账号 $99 一次性；需身份证件 + 自拍验证，入口 storedeveloper.microsoft.com） | **已核实** | [Learn: Free developer registration for individual developers](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer) |
| **headless CmdPal 扩展能否通过认证** | **未验证**（官方文档支持该形态，但无公开成功案例） | research §6 #16；R1 |
| 任何具体 CmdPal 扩展当前在 Store 上架 | **未验证**（Store 目录不可枚举） | research §6 #17 |
| Short description / features / 搜索词 / 认证备注的硬上限 | **TBD**（提交时看 Partner Center 提示） | R11 |
| voidtools 允许在 Store 包内再分发 `es.exe` | **未验证** | R4 |

---

## 6. 边界与后续（本任务已做/未做）

**已做（brand-materials / task-4）**
- `scripts/generate-icons.py`（新）+ 37 个图标资产中的 36 个包内 PNG（另 1 个为 Store 300 图标）+ inventory + preview；程序化尺寸校验。
- `docs/store/listing.md`、`privacy-policy.md`、`certification-notes.md`、`support.md`、本 README。

**未做（不属于本任务写权限，或由其他任务并行负责）**
- `src/EverythingPal/Package.appxmanifest` 的身份/版本调整 → store-packaging（task-3）；能力已缩到 `runFullTrust` 并实测通过（`unvirtualizedResources`/desktop6 写虚拟化已移除）
- 无参启动诊断对话框 → launcher-ui（task-5）已实现（`src/EverythingPal/Ui/LaunchDialog.cs` + `Program.cs` 入口），认证备注 §3.1 已按其实现更新；提交前需实测最终文案
- 截图采集 → qa-verify（task-6）：已交付 4 张（`01`–`04`，1366×768），见 `docs/store/screenshots/`
- UI 中英文 i18n → core-i18n（task-2 Phase 2）**已实现**：`Resources/Strings.resx`（en）+ `Strings.zh-Hans.resx`，跟随系统显示语言；清单含 en-US + zh-Hans
- Store 包构建/上传 → store-packaging + Lead
- Store 上架后提交 CmdPal 社区 Gallery（`microsoft/CmdPal-Extensions`）→ Lead/用户（发布后）

**复核记录**：图标生成后已检查 16/24/32/48/44/50/150/300/310×150 及 1240×600 预览；文字/商标/隐私表述按当前源码（AppLog、Program、settings key、manifest capabilities = runFullTrust only）核对 2026-10-20。2026-10-20 二次修正：Gallery 可发现性、个人开发者免费注册、主页 fallback 默认 1 行、`unvirtualizedResources` 已移除、公开 URL/截图状态。



