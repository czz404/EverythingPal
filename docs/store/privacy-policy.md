# Everything Search for Command Palette — 隐私政策 / Privacy Policy

> 状态：**草稿 v1（2026-10-20）**。上架前必须托管到公开 URL 并在 Partner Center 填写；托管地址 **TBD**。
> 适用产品：Microsoft Store 应用 `Everything Search for Command Palette`（包名/身份以最终提交为准；下文简称“本应用”）。
> 这是一份**如实反映当前实现**的隐私政策：无网络能力、无遥测、无数据收集。若后续版本新增任何联网/收集行为，必须更新本文并提前告知用户。

---

## 中文版（zh-CN）

### 1. 摘要

本应用是一个 **PowerToys 命令面板（Command Palette）扩展**，用于在本机搜索文件。它**不收集、不传输、不出售**任何个人数据，**不包含遥测、分析、广告或崩溃上报 SDK**，并且**不声明任何网络能力**（包清单中没有 `internetClient` 等网络能力）。所有搜索都在你的设备上完成。

### 2. 本应用处理哪些数据

| 数据 | 处理方式 | 是否离开设备 |
| --- | --- | --- |
| 你输入的搜索关键词 | 作为参数传给包内自带的 `Tools\es.exe`，在内存中处理 | 否 |
| 本机文件/文件夹的名称、完整路径、大小、修改时间、属性 | 由 `es.exe` 从本机 Everything 索引导出为临时 JSON，本应用读取后显示 | 否 |
| 你在设置页填写的选项 | 保存到本机设置文件（见 §4） | 否 |
| 错误诊断信息、时间戳、进程 ID | 写入本机日志文件（见 §5）；错误信息可能包含本机文件路径 | 否 |

**搜索关键词不会写入日志**（日志只记录搜索状态与结果条数）；程序入口会把启动参数写入日志（例如命令行标记，不含搜索内容）。错误日志在打开/定位文件失败时可能记录该本机文件路径，仅用于排障。

### 3. 网络与遥测

- 本应用**自身不发起任何网络请求**，包清单中**没有网络能力声明**（无 `internetClient`、`privateNetworkClientServer` 等）。
- 没有遥测、分析、广告、A/B 测试或第三方崩溃上报 SDK。
- 少数操作会打开系统界面，这些是**你主动触发**的、由 Windows 完成的行为（例如点击「下载 Everything」会调用默认浏览器打开 voidtools 网站；点击「启动 Everything」会启动你已安装的 Everything 程序）。这些操作不向本应用回传任何数据。
- Microsoft Store 本身可能按 Microsoft 的隐私声明收集安装/崩溃等标准诊断数据；这属于 Microsoft Store 的行为，本应用开发者不接收、也无法访问其中包含个人内容的数据。

### 4. 本机保存的设置

- 位置：`%LOCALAPPDATA%\EverythingPal\settings.json`
- 内容（键名）：`everythingPal.EsExePath`（es.exe 路径覆盖）、`everythingPal.MaxResults`（结果条数）、`everythingPal.Sort`（排序）、`everythingPal.FallbackRows`（主页内联行数）。
- 这些数据只保存在你本机，用于记住你的偏好；删除该文件或卸载应用即可清除。
- Command Palette 自身保存的扩展启用/回退命令开关属于 PowerToys（Microsoft）的数据，不在本应用的控制范围内。

### 5. 本机日志

- 位置：`%LOCALAPPDATA%\EverythingPal\Logs\extension.log` 与 `%TEMP%\EverythingPal.log`。
- 内容：时间戳、进程 ID、启动参数、搜索状态/结果数量、错误信息（可能含本机文件路径）。
- 日志**不会上传到任何服务器**，仅用于开发与排障；你可以随时删除，卸载应用不会自动删除日志文件（如需彻底清理请手动删除上述目录）。
- 如果你在反馈问题时提供日志，请注意其中可能包含本机文件路径等个人信息，请自行脱敏。

### 6. 第三方组件

- 本应用在安装包内附带 **`es.exe`**（来自 <https://github.com/voidtools/ES>，MIT 许可，版权归 voidtools）。它在本机运行，通过 Everything 的本地 IPC 查询你正在运行的 Everything 实例；本应用不修改 `es.exe`，其许可见包内 `THIRD-PARTY-NOTICES.md`。
- 本应用**不捆绑** Everything 本体。Everything 由你自行从 voidtools 安装，Everything 自身的数据处理由 voidtools 的隐私政策约束。
- 本应用与 voidtools、Microsoft 均无隶属、赞助或背书关系。

### 7. 你的权利与联系人

- 由于本应用不收集、不保存任何可识别个人身份的数据，不存在需要“导出/删除账户数据”的场景；本机数据可由你直接删除。
- 卸载应用会移除应用包与设置文件；`%LOCALAPPDATA%\EverythingPal` 下的日志如仍存在可手动删除。
- 隐私相关问题：请通过 `https://github.com/czz404/EverythingPal/issues` 联系（邮箱 **TBD**）。

### 8. 政策更新

本政策随应用版本更新。若处理方式发生实质变化（例如未来加入联网功能），我们会在更新说明中提示，并在本页更新“生效日期”。当前版本：v1.0，生效日期 2026-10-20。

---

## English version (en-US)

### 1. Summary

This app is a **PowerToys Command Palette extension** for searching files on your PC. It does **not collect, transmit, or sell** any personal data, contains **no telemetry, analytics, advertising, or crash-reporting SDK**, and **declares no network capability** (the package manifest contains no `internetClient` or similar capability). All searching happens on your device.

### 2. Data the app processes

| Data | How it is processed | Leaves the device? |
| --- | --- | --- |
| Search terms you type | Passed as arguments to the bundled `Tools\es.exe`, processed in memory | No |
| Names, full paths, size, modified time, attributes of local files/folders | Exported by `es.exe` from your local Everything index to a temporary JSON file and displayed by the app | No |
| Options you choose on the settings page | Stored in a local settings file (see §4) | No |
| Error diagnostics, timestamps, process ID | Written to local log files (see §5); error messages may include local file paths | No |

**Search terms are not written to the log** (the log records only search status and result counts); the entry point logs its startup arguments (command-line switches, not search text). Error logs may include the local path of a file that failed to open or reveal, for troubleshooting only.

### 3. Network and telemetry

- The app makes **no network requests** and the package declares **no network capabilities** (no `internetClient`, `privateNetworkClientServer`, etc.).
- No telemetry, analytics, advertising, A/B testing, or third-party crash-reporting SDK.
- A few actions open system UI, always **initiated by you** and performed by Windows (e.g. "Download Everything" opens voidtools.com in your default browser; "Start Everything" launches the Everything program you installed). These actions send nothing back to the app.
- The Microsoft Store may collect standard install/crash diagnostics under Microsoft's privacy statement; that is Microsoft Store behavior, and the developer of this app does not receive or have access to any personal content in it.

### 4. Settings stored on your device

- Location: `%LOCALAPPDATA%\EverythingPal\settings.json`
- Keys: `everythingPal.EsExePath` (es.exe override), `everythingPal.MaxResults` (result count), `everythingPal.Sort` (sort order), `everythingPal.FallbackRows` (inline home-screen rows).
- These values stay on your device and only remember your preferences; delete the file or uninstall the app to remove them.
- Extension/fallback toggles stored by Command Palette itself belong to PowerToys (Microsoft) and are outside this app's control.

### 5. Local logs

- Location: `%LOCALAPPDATA%\EverythingPal\Logs\extension.log` and `%TEMP%\EverythingPal.log`.
- Contents: timestamps, process ID, startup arguments, search status/result counts, error messages (which may include local file paths).
- Logs are **never uploaded to any server**; they exist for development and troubleshooting only. You may delete them at any time. Uninstalling the app does not automatically remove log files — delete the folders above if you want a full cleanup.
- If you share logs when reporting an issue, note that they may contain local file paths; redact anything sensitive.

### 6. Third-party components

- The package bundles **`es.exe`** from <https://github.com/voidtools/ES> (MIT license, copyright voidtools). It runs locally and queries your running Everything instance over Everything's local IPC. The app does not modify `es.exe`; its license is included in `THIRD-PARTY-NOTICES.md`.
- The app does **not** bundle Everything itself. Everything is installed separately by you from voidtools, and Everything's own data handling is governed by voidtools' privacy terms.
- The app is not affiliated with, sponsored by, or endorsed by voidtools or Microsoft.

### 7. Your rights and contact

- Because the app collects and stores no personally identifiable data, there is no account data to export or delete; local data can be removed by you directly.
- Uninstalling removes the app package and settings; log files under `%LOCALAPPDATA%\EverythingPal` may remain and can be deleted manually.
- Privacy questions: open an issue at `https://github.com/czz404/EverythingPal/issues` (email **TBD**).

### 8. Changes to this policy

This policy is updated with the app. If data handling changes materially (e.g. a future version adds networking), we will note it in the release notes and update the effective date on this page. Current version: v1.0, effective 2026-10-20.

---

## 托管说明（内部，发布前删除）

- 建议托管方式：GitHub Pages（仓库 public 后 `https://czz404.github.io/EverythingPal/privacy-policy.html`）或公开 Gist/独立静态页；**必须是可直接访问的公开 HTTPS 页面**，不能是登录后可见的私有仓库文件。
- 隐私政策 URL 与支持 URL 在 Partner Center 的 *Privacy policy* / *Support info* 中填写；未托管 = 阻塞提交。
- 若作者实体名/邮箱在提交前确定，替换文中的 **TBD** 并同步 `listing.md` §7。
