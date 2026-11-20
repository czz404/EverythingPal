# EverythingPal

[English](README.md) · **简体中文**

在 PowerToys 命令面板里用 Everything 搜索本机文件和文件夹。按回车打开选中的结果，右键可以复制路径，或在文件资源管理器中定位文件。

## 快速开始

1. 在 PowerToys 设置中启用「命令面板」，并确认 [Everything](https://www.voidtools.com/zh-cn/downloads/) 1.4 或 1.5 正在运行。
2. [从 Microsoft Store 安装 EverythingPal](https://apps.microsoft.com/detail/9NFD76MCDRJZ)。支持 Windows 10（2004 及以上版本）和 Windows 11，适用于 x64 和 ARM64。
3. 按 **Win + Alt + 空格**，输入并选择「Everything 搜索」，再输入文件名即可看到结果。例如，用 `ext:pdf "Annual Report"` 可以找到文件名中含有「Annual Report」的 PDF。

## 直接在主页搜索

打开「命令面板设置 → 扩展 → Everything 搜索 → 回退命令」，启用结果并勾选「包含在全局结果中」。之后就可以直接在主页输入文件名。

主页默认显示一行。在这一行按回车，只有一个匹配时会打开文件或文件夹，有多个匹配时会打开完整列表。右键「Everything 搜索」并打开设置，可以调整排序、结果上限和主页行数。修改行数后，重新加载扩展即可生效。

## 拼音搜索

在 Everything 1.5 中，用首字母 `pinyin-jian:nhsj` 或全拼 `pinyin-quan:nihaoshijie` 都能找到「你好世界测试.txt」。全局设置和 Everything 1.4 的适用条件见[拼音搜索说明](docs/research/notes/pinyin-search.md)。

## 开发

需要 .NET 10 SDK、Windows SDK 10.0.26100 和开发者模式。

```powershell
dotnet test tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj -c Release -p:Platform=x64
pwsh -File scripts\deploy.ps1 -Configuration Release -RestartCmdPal
```

[反馈问题](https://github.com/czz404/EverythingPal/issues) · [打包指南](docs/store/packaging.md) · [MIT 许可证](LICENSE) · [第三方声明](THIRD-PARTY-NOTICES.md)
