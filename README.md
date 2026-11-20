# EverythingPal

**English** · [简体中文](README.zh-CN.md)

Search local files and folders with Everything from PowerToys Command Palette. Press Enter to open a result, or right-click it to copy its path or show it in File Explorer.

## Quick start

1. Enable Command Palette in PowerToys and make sure [Everything](https://www.voidtools.com/downloads/) 1.4 or 1.5 is running.
2. [Install EverythingPal from the Microsoft Store](https://apps.microsoft.com/detail/9NFD76MCDRJZ) on Windows 10 (version 2004 or later) or Windows 11, on x64 or ARM64.
3. Press **Win + Alt + Space**, type **Everything Search**, and select it. Start typing a file name to see matches. Try `ext:pdf "Annual Report"` to find PDFs with that phrase in their names.

## Search from the home screen

In **Command Palette settings → Extensions → Everything Search → Fallback commands**, enable the result and select **Include in global results**. You can then search directly from the home screen.

By default, EverythingPal shows one row. Press Enter on it to open the file or folder if there’s one match, or to see the full list if there are several. To change the sort order, result limit, or number of home rows, right-click **Everything Search** and open its settings. Reload the extensions after changing the row count.

## Pinyin search

With Everything 1.5, `pinyin-jian:nhsj` finds Chinese file names such as `你好世界测试.txt` by their initials. Use `pinyin-quan:nihaoshijie` to search by full pinyin. For the global pinyin setting and the Everything 1.4 + IbEverythingExt setup, see the [pinyin search notes](docs/research/notes/pinyin-search.md).

## Development

Requires the .NET 10 SDK, Windows SDK 10.0.26100, and Developer Mode.

```powershell
dotnet test tests\EverythingPal.Core.Tests\EverythingPal.Core.Tests.csproj -c Release -p:Platform=x64
pwsh -File scripts\deploy.ps1 -Configuration Release -RestartCmdPal
pwsh -File scripts\pack-store.ps1
```

See [docs/store/packaging.md](docs/store/packaging.md) for Store packaging and [HANDOVER.md](HANDOVER.md) for the current release process. The extension is MIT-licensed; third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

[Report an issue](https://github.com/czz404/EverythingPal/issues) · [Packaging guide (Chinese)](docs/store/packaging.md) · [MIT license](LICENSE)
