# 拼音搜索（Pinyin search）— 研究与决策

> 2026-11-02。问题来源：用户看到 [IbEverythingExt（EvEx）](https://github.com/Chaoses-Ib/IbEverythingExt)
> 能让 Everything 支持拼音搜索，问 EverythingPal 能否集成；并认为 Everything 1.4/1.5 官方都没做拼音。
> 结论：**不集成、不改代码**；拼音能力天然来自 Everything，文档里说明即可（本笔记 + README + support FAQ）。
> 关联文件：`README.md`（*Pinyin search* 段）、`docs/store/support.md`（Q16）。

## 1. 结论（TL;DR）

1. EverythingPal 不含拼音引擎：它只把查询交给 `es.exe` → Everything 求值。**拼音匹配发生在 Everything
   内部**，所以 Everything 有什么匹配能力，扩展就有什么，无需在扩展里做任何事。
2. **Everything 1.5（beta）其实内置了拼音搜索**（用户原先以为没有）：默认关闭，需在
   *工具 → 选项 → 高级* 里设置 `pinyin` / `pinyin_type`；查询级修饰符 `pinyin:`、`pinyin-jian:`、
   `pinyin-quan:` 也可直接使用，扩展会原样透传。
3. **Everything 1.4 没有内置拼音**（这部分用户说得对）。要拼音需要 Everything 侧插件
   [IbEverythingExt（EvEx）](https://github.com/Chaoses-Ib/IbEverythingExt)。它通过打补丁替换
   Everything 的匹配引擎实现拼音，因此所有让 Everything 求值的客户端（包括本扩展的 `es.exe` 查询）
   都会生效，**无需集成**。
4. 因此不打包、不 P/Invoke、不改代码；仅补文档说明“拼音搜索取决于 Everything 本体”。不改版本号、
   不重新提交 Store（Store 列表文案未改；如以后要宣传该能力，需要随下一次包更新一并提交）。
5. 可选（未做，低优先级）：查询为空时自动用 `pinyin:` 再试一次（仅 1.5）；或探测 Everything 的
   `pinyin` 设置。风险/收益低，先不做。

## 2. Everything 1.5 内置拼音（官方文档 + 本机实测）

### 文档

- [Search Modifiers](https://www.voidtools.com/support/everything/search_modifiers/)（Requires Everything 1.5）：
  - `pinyin:` / `no-pinyin:`（别名 `py:`）— Match Chinese characters with QuanPin Pinyin，
    `pinyin:nihao` matches `你好`；默认值取 *Tools → Options → Advanced → `pinyin` 设置*（默认 disabled）。
  - `pinyin-jian:`（别名 `pyj:`）— Single Character Pinyin，`pinyin-jian:nh` matches `你好`。
  - `pinyin-quan:`（别名 `pyq:`）— QuanPin Pinyin，`pinyin-quan:nihao` matches `你好`。
- 版本历史（[voidtools forum t=12073](https://www.voidtools.com/forum/viewtopic.php?t=12073)）：
  1.5.0.1320a 加入实验性 `pinyin:`；1.5.0.1384a 加入 QuanPin（全拼）。
- 开启方式（第三方教程一致）：Everything → 工具 → 选项 → Advanced → `pinyin` 设为 true，
  `pinyin_type` 选 `QuanPin`（全拼）或 `Single Character`（首字母）。

### 本机实测（2026-11-02）

环境：Windows 11 26H1 build 28000.3231 x64；Everything **1.5.0.1423b**（x64，`D:\software\Everything`）；
内置命令行客户端 **ES 1.1.1.39**（`artifacts/es/x64/es.exe`）；测试文件
`E:\workspace\deepseek\EverythingPal\artifacts\qa\pinyin-probe\你好世界测试.txt`（已被 Everything 索引）。

`es.exe -n 10 -export-json <out> -search "<query>"`（`-search` 值即扩展发送的查询；JSON 为 UTF-8）：

| 查询 | `pinyin=0`（默认） | `pinyin=1`, `pinyin_type=1` | 说明 |
| --- | --- | --- | --- |
| `pinyin-quan:nihaoshijie` | 1 hit（你好世界测试.txt） | 1 hit | 修饰符始终可用 |
| `pinyin-jian:nhsj` | 1 hit | 1 hit | 首字母可用 |
| `pinyin:nihaoshijie` | 1 hit | 1 hit | 裸 `pinyin:` 也可用 |
| `nihaoshijie`（普通查询） | **0 hit** | **1 hit** | 全局开启后才自动匹配 |
| `nihao`（普通查询） | — | 1 hit | |
| `nhsj` | — | 0 hit | `pinyin_type=1` 是全拼；首字母需 `pinyin-jian:` 或改 `pinyin_type` |
| `nocase:ignorews:nihaoshijie`（扩展的智能匹配前缀） | — | **0 hit** | 该前缀会抑制拼音匹配 |

结论：扩展对普通单词查询会先发 `nocase:ignorews:<q>`，该形式不触发拼音；`SearchRetryPolicy`
在首次为空时会用**原始查询**重试一次（已有逻辑），所以开启全局拼音后普通拼音查询仍能出结果，
代价是多一次 `es.exe` 调用。显式 `pinyin:`/`pinyin-quan:`/`pinyin-jian:` 查询含 `:`，不会被
智能匹配改写，直接生效。

注：实测时 `pinyin` 开关需改 `%APPDATA%\Everything\Everything.ini` 并重启 Everything；测试后已恢复
原值（`pinyin=0`，`pinyin_type=1`），未改动用户 Everything 配置。

## 3. IbEverythingExt（EvEx）— 机制与限制

来源：仓库 <https://github.com/Chaoses-Ib/IbEverythingExt>（README / README.zh-Hans / docs/third_party /
src）。**它是 Everything 侧插件，不进入本扩展的进程。**

- **机制**：v1.5 是 Everything 插件，启动时按特征码扫描 `Everything64.exe`，接管其内部 PCRE
  编译/执行（`plugin/src/search/mod.rs` 导出 `search_compile`/`search_exec`；`ib_matcher` 的
  `Pattern::parse_ev` 实现拼音）；v1.4 用 DLL 劫持 + Detours，同样替换匹配引擎。因此 **Everything 收到
  的查询都能享受拼音匹配**，与查询来自 UI、SDK 还是 IPC 无关。
- **对本扩展是否生效**：是。`es.exe` 把查询经 IPC 交给 Everything 求值，属于“Everything 求值”的客户端。
  官方 docs/third_party 列表以 GUI 程序为例（含 PowerToys CmdPal 的 Everything 插件），未点名 `es.exe`，
  这是文档层面的推断；反例 Total Commander 是“先取全部文件、自己匹配”，所以 EvEx 对它无效——正好说明
  判定标准是“谁匹配”。
- **限制**（来自 README / 发布说明）：
  - 仅 64 位 Everything（x86/ARM64、Lite 版不支持），v1.4 最高 **1.4.1.1026**（1027+ 阻止 DLL 劫持）。
  - v1.5：需在 *Options → Plug-ins* 勾选 Enable plug-in；需关闭命名实例（否则第三方程序调不到，
    issue #5）；便携整合包已带 `No_Alpha_Instance`。
  - 兼容性按 Everything 版本做特征码匹配，**每个 Everything 新版都可能失效**：v0.9.3 修 1.5.0.1403
    LTCG；v0.11.0 修 1.5.0.1409+ 配置错乱；最新 release 测试到 Everything **1.5.0.1416 Beta**
    （本机 1.5.0.1423b 比它新，未验证 EvEx 是否仍能加载，故未在本机安装测试）。
- **能力**：简拼/全拼/带声调全拼/双拼、多音字、Unicode 辅助平面、日语罗马字、通配符增强、快速选择、
  Shell 扩展——比 Everything 1.5 内置拼音（仅全拼/首字母）更丰富。
- **集成可行性结论**：不需要也不应该“集成”。它是 Everything 插件（安装/加载由 Everything 负责），
  本扩展只要文档建议用户自行搭配即可；不打包也避免许可证/架构（ARM64）/版本兼容问题。

## 4. 复现命令（本机）

```powershell
# 1) 测试文件（Everything 已索引 E:）
$dir='E:\workspace\deepseek\EverythingPal\artifacts\qa\pinyin-probe'
New-Item -ItemType Directory -Force $dir | Out-Null
Set-Content "$dir\你好世界测试.txt" 'pinyin probe' -Encoding utf8
Start-Sleep 3

# 2) 直接用内置 es.exe 查询（本 session 的实际做法见下方“环境注意”）
$es="$env:TEMP\esprobe\es.exe"   # 把 artifacts/es/x64/es.exe 复制到 %TEMP%
& $es -n 10 -export-json "$dir\out.json" -search 'pinyin-quan:nihaoshijie'
Get-Content "$dir\out.json" -Encoding utf8 | ConvertFrom-Json

# 3) 开启全局拼音（先退出 Everything）：%APPDATA%\Everything\Everything.ini 里 pinyin=0 -> pinyin=1，
#    重启 Everything，再试普通查询 nihaoshijie（应命中），最后恢复 pinyin=0。
```

环境注意（本 session 观察）：在 workspace 路径里直接运行 `artifacts\es\...\es.exe` 会出现
`Error 7: Unable to send IPC message or bad IPC reply`（调试输出 `CreateFile failed 5`），把同一
`es.exe` 复制到 `%TEMP%` 再运行即可正常查询；JSON 导出到 workspace 路径正常。疑似 DSH 沙箱/安全软件
对 workspace 路径进程的 IPC 限制，与产品无关（Store 包内 `es.exe` 在 CmdPal 进程中运行）。

## 5. 参考链接

- Everything Search Modifiers（`pinyin:`/`pinyin-jian:`/`pinyin-quan:`）：
  <https://www.voidtools.com/support/everything/search_modifiers/>
- Everything INI 文档（设置/`about:config`、修改前需退出 Everything）：
  <https://www.voidtools.com/support/everything/ini/>
- Everything 拼音修饰符论坛帖（1.5.0.1320a / 1.5.0.1384a）：<https://www.voidtools.com/forum/viewtopic.php?t=12073>
- IbEverythingExt：<https://github.com/Chaoses-Ib/IbEverythingExt>（README、`docs/third_party/README.md`、
  `plugin/src/search/mod.rs`、`sig/src/lib.rs`）
- ES（`es.exe`）文档与错误码：<https://github.com/voidtools/ES>
