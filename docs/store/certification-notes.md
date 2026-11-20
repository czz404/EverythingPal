# Everything Search for Command Palette — 认证备注 / Notes for Certification

> 状态：**v2（2026-11-20，1.0.3.0 更新）**。Partner Center 提交流程：*Submission options → Notes for certification*（即 **Additional testing information**）。
> 英文短版可直接粘贴；中文版供内部审核/必要时补充。1.0.3.0 重点：多匹配回车、引号短语、设置恢复、显式排序、详情面板。UI 已支持中文/English 双语（跟随系统显示语言）；包能力只有 `runFullTrust`（`unvirtualizedResources` 与 desktop6 写虚拟化已移除并实测通过）。

---

## 1. 可直接粘贴的英文短版（Notes for certification，1.0.3.0）

```text
EverythingPal is a file search extension for Microsoft PowerToys Command Palette. Launching it from the Start menu opens a diagnostics dialog. The search interface is inside Command Palette.

Prerequisites
Install Microsoft PowerToys and enable Command Palette. Install voidtools Everything 1.4 or 1.5 and keep it running with a local folder indexed. EverythingPal includes the es.exe command-line client. No account or sign-in is required.

How to verify this update
1. Open Command Palette (default shortcut Win+Alt+Space), type Everything Search, and open the command. On a Simplified Chinese system, the command is named “Everything 搜索”.
2. In an indexed folder, create two empty files named Annual Report.pdf and Annual revised Report.pdf. Once both appear in Everything, enter ext:pdf "Annual Report" in the extension. Of those two files, only Annual Report.pdf should appear. Select it to see its full path and size.
3. To test home search, open the extension’s fallback commands in Command Palette settings. Enable the first row and “Include in the global result”. With the default one-row setting, search for Annual from the Command Palette home screen. Press Enter on the EverythingPal result to open the full list.
4. To test settings persistence, change the result limit from 50 to 17, save, restart PowerToys, and reopen the extension settings. The saved value should still be 17.

The extension queries the local Everything index through the bundled es.exe client. Search terms and results stay on the device.
```

---

## 2. 中文版（内部审核 / 需要时粘贴）

```text
应用类型：PowerToys 命令面板（Command Palette）扩展，MSIX 全信任包，无主窗口、无独立界面（设计如此）。
安装后出现在 Command Palette 设置 → 扩展 中，并提供「Everything 搜索」命令。

前提条件
- Windows 11，或 Windows 10 版本 2004（build 19041）及以上（x64 / ARM64）。
- 已安装 Microsoft PowerToys 并启用 Command Palette（默认快捷键 Win+Alt+Space）。
- 已安装并正在运行 voidtools Everything 1.4 或 1.5a（本扩展查询其本地索引，不替代 Everything）。
  Everything 未运行时，扩展会显示可操作提示（启动 Everything / 下载 Everything）。

测试步骤
1. 从 Store 安装 PowerToys，在设置中启用 Command Palette。
2. 从 Store 安装本应用（无需其他配置，所需 es.exe 已内置）。
3. 按 Win+Alt+Space，输入 “Everything”，选择「Everything 搜索」命令，再输入设备上存在的文件名
   （如 “winver”）。结果来自本地 Everything 索引；回车打开，右键菜单可资源管理器定位、复制路径、
   在 Everything 中打开。
4. （可选）主页内联结果：Command Palette 设置 → 扩展 → Everything Search → 回退命令。
   第三方扩展的回退命令默认不进入全局结果（这是 CmdPal 平台行为）；启用对应行并勾选
   “Include in the Global result” 后，在命令面板主页输入文件名即可看到内联结果
   （默认 1 行；扩展设置里可调 0–5 行）。
5. 若未安装 Everything，第 3 步会显示可操作错误提示，属预期行为；安装并运行 Everything 后即可搜索。

能力说明
- 包内**只声明了一个受限能力 `runFullTrust`**：注册并运行托管 Command Palette 扩展的打包 COM 服务器
  （com:ExeServer / com:Class）所必需；应用不提权，以普通用户完整性运行。
- 没有其他受限能力，也没有 `unvirtualizedResources` / `desktop6:*WriteVirtualization` 声明。设置/日志
  写入正常的用户路径 `%LOCALAPPDATA%\EverythingPal` 与 `%TEMP%`，临时 JSON 导出路径传给内置 es.exe；
  移除写虚拟化关闭后已实测这些路径工作正常。
- 未声明任何网络能力；应用不发起网络请求。

外部进程 / 数据
- 内置 Tools\es.exe（MIT，来自 github.com/voidtools/ES，许可见 THIRD-PARTY-NOTICES.md），每次搜索
  时启动，通过 Everything 的本地 IPC 查询并导出 JSON 到临时文件；不在运行时下载。
- 不捆绑 Everything 本体；仅在用户点击“启动 Everything”时启动用户已安装的 Everything.exe。
- 无遥测、不收集数据、无网络；设置与日志只保存在本机。

图标为本项目脚本生成的原创作品，不含第三方商标/标志。
支持：https://github.com/czz404/EverythingPal/issues
```

---

## 3. 审核要点逐条解释（供内部答辩）

### 3.1 为什么看起来“没有界面 / 像 headless”？

这是 **Command Palette 扩展**，不是普通窗口应用：它的 UI 全部由 Command Palette 宿主呈现（命令页、列表、详情面板、设置页），扩展进程本身是后台 COM 服务器。安装后：

- Command Palette 设置 → **扩展** 中可见 **Everything Search**；
- 命令面板里输入 `Everything` 能找到「Everything 搜索」命令页；
- 无参启动（开始菜单点击 / `--about` / `--diagnostics`）会显示诊断对话框，说明这是 CmdPal 扩展、检查 PowerToys/Everything/es.exe 是否就绪，并提供「打开命令面板」等动作（**已实现**：`src/EverythingPal/Program.cs` 第 47–51 行 + `src/EverythingPal/Ui/LaunchDialog.cs`；提交前需按最终文案实测）。

> Microsoft 官方文档已明确支持把 Command Palette 扩展发布到 Store（见 [Publish to Microsoft Store](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-store)），并在其中给出“描述要写 integrates with the Windows Command Palette…”与“在认证备注写 PowerToys 前提 + 步骤”的要求；本文即按此准备。**但“headless 扩展能否稳定通过认证”本身没有可验证的公开样本（研究文档 §6 #16 标记为未验证）**，见 `README.md` 风险。

### 3.2 为什么需要 `runFullTrust`？

> 后台若询问 runFullTrust 用途，可直接粘贴：
>
> ```text
> EverythingPal uses runFullTrust to host the packaged out-of-process COM server through which PowerToys Command Palette loads the extension. The extension runs under the signed-in user’s permissions and launches the bundled es.exe client to query the local Everything index. Administrator privileges are not required.
> ```

- 包清单通过 `com:ExeServer` + `com:Class` 注册**打包的 out-of-process COM server**，CmdPal 用 `-RegisterProcessAsComServer` 启动它并创建扩展实例；这需要 mediumIL 全信任。
- 全信任不等于提权：进程以当前用户身份运行，不请求管理员权限，不修改系统设置。
- 引用：[App capability declarations — Full trust permission level / Restricted capabilities](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)。

### 3.3 能力声明：只有 `runFullTrust`（无其他受限能力）

- 当前 manifest **只声明 `runFullTrust`**：注册并运行打包 out-of-process COM server
  （`com:ExeServer` + `com:Class`）所必需；非提权（mediumIL，以当前用户身份运行）。
- **不再声明** `unvirtualizedResources`，也**不再设置** `desktop6:FileSystemWriteVirtualization=disabled`
  / `RegistryWriteVirtualization=disabled`。这两项已从 manifest 移除，并已实测通过：删包重装后，
  命令页搜索（`es.exe` 的 `%TEMP%` JSON 导出）、主页 fallback、图标、日志写入
  `%LOCALAPPDATA%\EverythingPal\Logs` 全部正常。
- 因此 Submission options 只需解释 `runFullTrust`，不需要为写虚拟化/其他受限能力补充理由。
- 引用：[App capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)。

### 3.4 外部进程与网络

| 行为 | 说明 | 网络 |
| --- | --- | --- |
| 每次搜索启动 `Tools\es.exe` | 包内自带（MIT）；`Process.Start`，参数含 `-export-json <临时文件>`；读取 JSON 后删除临时文件；超时 1500 ms | 无（与本地 Everything IPC） |
| 点击「启动 Everything」 | 仅当用户点击时，启动用户已安装的 Everything.exe（先定位安装路径） | 无 |
| 点击「下载 Everything」 | 调用 `shell` 打开 voidtools 下载页（浏览器/商店网络行为） | 用户主动、由系统浏览器发起 |
| 打开 CmdPal 设置/命令面板 | 用户点击「打开命令面板」时通过 `shell:AppsFolder` 启动 | 无 |

- 包清单**没有** `internetClient` / `privateNetworkClientServer` 能力；应用自身无 HTTP 客户端、无遥测。
- 内置 `es.exe` 通过 Everything 的**本地 IPC** 通信（非网络）；Everything 本体由用户安装。
- 引用：[Everything CLI / ES 项目](https://github.com/voidtools/ES)、[Everything 官网](https://www.voidtools.com/)。

### 3.5 第三方组件与许可

- `es.exe`：来自 voidtools/ES，**MIT**；包内 `THIRD-PARTY-NOTICES.md` 保留版权与许可全文。
- Everything 本体：**不随包分发**，由用户自行安装；本应用仅查询其索引。
- 应用图标：原创（`scripts/generate-icons.py`），无 voidtools/Microsoft 商标。
- 待确认风险：voidtools 是否允许把 `es.exe` **再分发**进商业 Store 包（MIT 文本允许，但官方 FAQ/条款需确认；研究文档标记未验证）。

### 3.6 数据与隐私

- 不收集、不传输任何个人数据；无遥测/广告/崩溃 SDK；查询不写日志（只记录状态与条数）。
- 本地保存：`%LOCALAPPDATA%\EverythingPal\settings.json`、`%LOCALAPPDATA%\EverythingPal\Logs\extension.log`、`%TEMP%\EverythingPal.log`；错误日志可能含本机文件路径，仅本地排障用。
- 详见 [privacy-policy.md](privacy-policy.md)（公开 URL：<https://github.com/czz404/EverythingPal/blob/main/docs/store/privacy-policy.md>）。

### 3.7 可能被问到的问题（Q&A）

| 审核问题 | 回答 |
| --- | --- |
| 为什么安装后没有可执行窗口？ | 这是 Command Palette 扩展，UI 由 CmdPal 宿主呈现；扩展进程是后台 COM 服务器。见 §3.1 与测试步骤。 |
| 为什么只申请 full trust？ | 打包 out-of-process COM server 托管 CmdPal 扩展所必需，非提权。包内没有其他受限能力、没有网络能力（见 §3.3）。 |
| 为什么没有其他受限能力？ | 包只申请了打包 COM server 必需的 `runFullTrust`；没有网络/写虚拟化等受限能力（见 §3.3）。 |
| 应用会读取我的文件内容吗？ | 不会读取文件内容；只显示 Everything 索引返回的文件名/路径/大小/时间等元数据。 |
| 会联网吗？ | 不会；包未声明网络能力，无网络调用。用户主动点击下载链接时由系统浏览器打开网站。 |
| 为什么搜索结果为空？ | 需要 Everything 正在运行；若未安装/未运行，界面会给出启动/下载提示。 |
| 为什么主页没有内联结果？ | CmdPal 默认不把第三方 fallback 加入全局结果；需在 CmdPal 设置中启用（见测试步骤 4）。 |

---

## 4. 提交前必须复核的 TBD

| # | 项 | 为什么 | 负责人 |
| --- | --- | --- | --- |
| 1 | 无参启动诊断对话框的最终文案/按钮/退出行为 | 认证政策 10.1.2 / 兼容性条款；launcher-ui 已实现，需实测后再提交 | launcher-ui / Lead |
| 2 | ~~UI 语言~~（已解决：已提供 zh-Hans + en-US 资源，跟随系统显示语言） | 提交前确认截图/列表语言配置 | — |
| 3 | ~~支持 URL / 隐私 URL~~（已解决：仓库已 public） | — | — |
| 4 | `THIRD-PARTY-NOTICES.md` 是否在包根目录 | 许可合规；packaging 验证 | store-packaging |
| 5 | 认证备注字段长度上限 | 短版约 2,400 字符，若超限需删减 | 提交时确认 |
| 6 | 测试机是否预装 Everything | 若审核机没有 Everything，结果页停在“未运行”属预期；备注已说明 | Lead |

