# Everything Search for Command Palette — 支持与常见问题 / Support & FAQ

> 状态：**草稿 v1（2026-10-20）**。此文件同时用作 Store 支持信息页的内容来源与用户 FAQ。
> 支持渠道：**GitHub Issues**（仓库已 public）：<https://github.com/czz404/EverythingPal/issues>
> 支持邮箱：**TBD**（如无邮箱，把 Store 支持联系方式指向 GitHub Issues）。
> 适用版本：v0.2.x（Store 首发；当前开发版）。回复时间：尽力而为，无 SLA（TBD）。

---

## 1. 快速排查（先看这里）/ Quick troubleshooting

| 现象 | 原因 | 解决 |
| --- | --- | --- |
| 命令面板里搜不到 `Everything` 命令 / 安装后没反应 | PowerToys 未安装、Command Palette 未启用，或扩展未加载 | 安装/打开 PowerToys → 设置 → Command Palette 启用；在 CmdPal 设置 → 扩展 里确认 **Everything Search** 已启用；执行 *Reload Command Palette extensions* |
| 主页输入文件名没有内联结果 | Command Palette **默认不把第三方扩展的 fallback 命令加入全局结果** | CmdPal 设置 → 扩展 → Everything Search → **回退命令** → 启用需要的行 + 勾选 **Include in the Global result**；详见 §3 |
| 提示「Everything 没有运行」 | Everything 本体未运行 | 点错误提示里的 **启动 Everything**；或手动启动 Everything 后重试 |
| 提示 es.exe 缺失 / 无法搜索 | 安装包不完整或被杀软隔离（少见） | 从 Store 更新/重装本应用；检查安全软件隔离区 |
| 搜索超时 / 结果迟迟不出现 | Everything 索引尚未就绪（刚启动或正在重建索引） | 等 Everything 索引完成后重试；在 Everything 里确认索引正常 |
| 结果为空但文件确实存在 | Everything 未索引该磁盘/目录，或查询语法与 Everything 不同 | 在 Everything 本体里确认能搜到；检查 Everything 的索引设置（NTFS 卷/文件夹） |
| 同名程序/扩展冲突 | 已安装其他 Everything CmdPal 扩展（如旧插件） | 只保留一个，避免重复结果/快捷键冲突 |
| 界面语言与商店语言不一致 | UI 跟随系统显示语言（中文系统显示中文，其它系统显示 English）；商店列表语言独立 | 在 Windows 设置 → 时间和语言 → 语言 中调整显示语言后重启 CmdPal；商店列表语言与 UI 语言不必一致 |

---

## 2. 常见问题 / FAQ（中文）

### Q1. 这个应用和 voidtools 的 Everything 是什么关系？
本应用是**第三方** PowerToys Command Palette 扩展，只通过 Everything 的命令行客户端 `es.exe` 查询你本机正在运行的 Everything 索引。它**不包含** Everything 本体，也与 voidtools 无隶属或背书关系。要使用完整搜索功能，请先安装并运行 [Everything](https://www.voidtools.com/)（1.4 或 1.5a）。

### Q2. 需要单独下载 es.exe 或配置 PATH 吗？
不需要。安装包内已附带 MIT 许可的 `es.exe`（来自 <https://github.com/voidtools/ES>），自动优先使用包内版本。只有当你想覆盖路径时，才需要在设置页填写 `es.exe` 路径。

### Q3. 为什么主页输入文件名看不到结果，但「Everything 搜索」命令页可以？
这是 Command Palette 的平台行为：第三方扩展的 fallback（回退）命令默认**不进入全局结果**。开启方式：
1. 打开 Command Palette 设置（齿轮）；
2. **扩展 → Everything Search → 回退命令**（Fallback commands）；
3. 打开需要的行（默认 1 行；可在扩展设置页把主页行数调为 0–5），并勾选 **Include in the Global result**（“在全局结果中显示 / Show results on queries without direct activation command”）；
4. 返回命令面板主页，输入文件名即可看到内联结果（行数可在扩展设置页调整为 0–5，默认 1）。

### Q4. 为什么搜索结果和 Everything 本体不完全一样？
扩展把查询交给 `es.exe`，并在此基础上做轻量排序（精确名 > 前缀 > 包含 > 文件夹/更短名称），结果条数、排序方式、超时都在设置里可调。Everything 本体的全部高级语法/筛选器不保证等价；若结果与预期不符，请先在 Everything 本体验证同一查询。

### Q5. 能搜索网络位置 / 映射驱动器 / 非 NTFS 卷吗？
取决于 Everything 本体的索引配置。Everything 只索引它配置的 NTFS 卷与文件夹索引；扩展本身不额外扫描磁盘。

### Q6. 会收集我的搜索词或文件信息吗？会联网吗？
不会。应用**没有网络能力、无遥测、不收集数据**；搜索词只在内存中传给本地 `es.exe`，不写日志。详见隐私政策（`privacy-policy.md`，Store 列表也会提供公开链接）。

### Q7. 设置保存在哪里？如何恢复默认？
`%LOCALAPPDATA%\EverythingPal\settings.json`。删除该文件（先退出命令面板/结束扩展进程）或卸载重装即可恢复默认。设置页也可直接改回默认值。

### Q8. 日志在哪里？反馈问题时需要提供什么？
- `%LOCALAPPDATA%\EverythingPal\Logs\extension.log`
- `%TEMP%\EverythingPal.log`

反馈时请提供：Windows 版本、PowerToys/Command Palette 版本、Everything 版本、本应用版本（CmdPal 设置 → 扩展 → About 卡片）、复现步骤、错误提示截图、相关日志片段（**请先脱敏本机路径/用户名**）。

### Q9. 如何卸载？卸载后会残留什么？
Windows 设置 → 应用 → 已安装的应用 → **Everything Search for Command Palette** → 卸载（或在 Store 库中卸载）。卸载会移除应用包与设置；`%LOCALAPPDATA%\EverythingPal`（日志）可能残留，可手动删除。你的 Everything 本体与本应用无关，不受影响。

### Q10. 支持哪些系统？需要什么版本的 PowerToys？
Windows 11，或 Windows 10 2004（build 19041）及以上，x64 / ARM64；PowerToys 需包含 Command Palette（当前开发环境 0.13.12862，旧版本若不含 CmdPal 则无法使用）。Everything 支持 1.4 与 1.5a。

### Q11. 为什么安装后开始菜单里点它只弹一个说明对话框？
本应用是**命令面板扩展**，没有独立主界面。开始菜单入口用于展示诊断信息（是否检测到 PowerToys/Everything/es.exe、如何打开命令面板），这是预期行为，不是启动失败。（该诊断对话框已实现，见 `src/EverythingPal/Ui/LaunchDialog.cs`；若与你的版本不一致，请更新到最新版。）

### Q12. 会与其它 Everything CmdPal 扩展冲突吗？
会。若同时安装多个查询 Everything 的 CmdPal 扩展，可能出现重复结果或快捷键/设置混淆。建议只保留一个。

### Q13. 为什么 CmdPal 设置里的回退命令显示「Everything 搜索 · 主页结果」（或 `1/3`、`2/3`…）？
不是多个不同的功能，而是**主页内联结果每一行各占一个「回退命令」**。CmdPal 的扩展接口规定“一个回退命令只能显示一行”，所以主页要显示 N 行，就必须注册 N 个回退命令，设置页也就列出 N 个开关。**默认只注册 1 行**（与旧版 Everything 插件一致），单行时标题显示为「Everything 搜索 · 主页结果」；把「主页搜索内联结果行数」改成 2–5 才会出现多个开关（标题带 `1/3`、`2/3`…），改成 0 则完全关闭。**修改行数后需要重新加载 Command Palette 扩展**才会生效。行的显示顺序与是否进入全局结果由 CmdPal 管理（可在“管理回退顺序”里拖动排序）。

### Q14. 怎么找到/安装这个扩展？CmdPal 里有 Gallery/库吗？
- **Store 安装**：在 Microsoft Store 搜索应用名（上架后），或使用商店链接安装。
- **命令面板内 Gallery**：CmdPal 0.13+ 的设置里有 **Settings → Extensions → Gallery（扩展 → 库）**，由 `microsoft/CmdPal-Extensions` 社区目录驱动，安装/更新会走 WinGet 或 Microsoft Store。**本扩展在 Store 上架后会提交到该社区目录**，之后即可在 Gallery 中浏览、一键安装（提交完成前 Gallery 可能搜不到，属正常）。
- **WinGet**：可选（推荐）。若发布 WinGet 包，可用 `winget install` 安装；Gallery 的安装/更新同样会走 WinGet。
- 如果 Gallery 里看不到：先更新 PowerToys/CmdPal 到较新版本，并在 Gallery 刷新；或直接用 Store 安装。

---

## 3. 如何启用主页内联结果（图文步骤，供截图/说明复用）

1. 按 `Win + Alt + Space` 打开 Command Palette。
2. 打开 **设置**（左下角齿轮）→ **扩展** → 找到 **Everything Search**。
3. 展开 **回退命令 / Fallback commands**。
4. 打开需要的行（默认 1 行；可在扩展设置页把主页行数调为 0–5），并勾选 **Include in the Global result**。
5. 返回主页，输入任意本机文件名（例如 `report` 或 `winver`）。
6. 若仍不显示：确认 Everything 正在运行，并在 CmdPal 中执行 *Reload Command Palette extensions*。

> 说明：这是 Command Palette 对第三方扩展的默认安全策略；不是本应用的故障。

---

## 4. 反馈模板（GitHub Issue）

```text
### 环境
- Windows: (winver)
- PowerToys / Command Palette: (版本)
- Everything: (1.4 / 1.5a，是否正在运行)
- EverythingPal: (版本，CmdPal 设置 → 扩展 → About)
- 体系结构: x64 / ARM64

### 问题
（现象 + 期望行为）

### 复现步骤
1.
2.
3.

### 错误提示 / 截图
（粘贴错误提示；注意脱敏路径/用户名）

### 日志片段
（%LOCALAPPDATA%\EverythingPal\Logs\extension.log 最近几十行，脱敏后粘贴）
```

---

## 5. 语言说明 / Language

- UI 已提供 **中文（zh-Hans）与 English** 两套文案，跟随 Windows 显示语言（`CultureInfo.CurrentUICulture`）：中文系统显示中文，其它系统显示英文；包内清单包含 `en-US` + `zh-Hans` 资源语言。
- Store 列表同时提供中文与英文文案（见 `listing.md`）；如语言显示异常，请提供系统显示语言与区域设置反馈。
- 错误提示中的技术细节（Everything/es.exe 状态）保持英文可读。

---

## 6. English summary (short)

- **What it is:** a third-party PowerToys Command Palette extension that searches your local files through the bundled MIT `es.exe` and your running Everything index. It is not affiliated with voidtools or Microsoft.
- **Requirements:** Windows 11, or Windows 10 version 2004 (build 19041)+, x64/ARM64; PowerToys with Command Palette enabled; Everything 1.4/1.5 running.
- **Home-screen inline rows:** Command Palette does not include third-party fallback rows in global results by default. Enable them in Command Palette settings → Extensions → Everything Search → Fallback commands (check "Include in the Global result"). Default is **one row**; 0–5 configurable in the extension settings.
- **Finding the extension:** install from the Microsoft Store; after the Store release it is also submitted to the `microsoft/CmdPal-Extensions` community Gallery (Command Palette Settings → Extensions → Gallery), which installs via WinGet or the Microsoft Store. WinGet publishing is optional/recommended.
- **Privacy:** no telemetry, no network capability, no data collection; settings and logs stay on your device (`%LOCALAPPDATA%\EverythingPal`).
- **Support / logs:** open an issue at <https://github.com/czz404/EverythingPal/issues> and include your Windows/PowerToys/Everything versions, reproduction steps and a redacted `extension.log`.
- **Uninstall:** Windows Settings → Apps → Everything Search for Command Palette → Uninstall; delete `%LOCALAPPDATA%\EverythingPal` to remove leftover logs.

