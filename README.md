# EverythingPal

Everything 搜索扩展 for [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (CmdPal).

An Everything search extension for Command Palette, built as a **managed-only** CmdPal extension:
it never ships or P/Invokes `Everything64.dll`; it queries Everything through the official
[`es.exe`](https://www.voidtools.com/support/everything/command_line_interface/) CLI and parses its
JSON export. This keeps the extension free of native binaries and portable across Everything 1.4 / 1.5
installs.

> Status: **M1 (scaffold)** — the extension registers and loads in Command Palette; search wiring is next.

## Requirements

- Windows 11 (x64), PowerToys with Command Palette (`Microsoft.CommandPalette` 0.13+)
- Everything 1.5a (or 1.4) installed and running
- `es.exe` (ships with Everything; also available via `winget install voidtools.Everything.Cli`)
- For development: .NET 10 SDK, Windows Developer Mode (loose MSIX registration)

## Build & deploy (development loop)

```powershell
# build only
dotnet build src/EverythingPal/EverythingPal.csproj -c Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64

# build + produce an MSIX layout + register it for the current user (Developer Mode required)
pwsh -File scripts/deploy.ps1 -RestartCmdPal
```

After deploying, run `Reload` → **Reload Command Palette extensions** inside Command Palette
(or use the `-RestartCmdPal` switch above).

Release MSIX (unsigned unless a certificate is configured):

```powershell
dotnet build src/EverythingPal/EverythingPal.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="AppPackages\"
```

## Project layout

```
src/EverythingPal/
  Program.cs                    COM/WinRT server host (-RegisterProcessAsComServer)
  EverythingPalExtension.cs     IExtension implementation (GUID must match the manifest CLSID)
  EverythingPalCommandsProvider.cs
  Pages/EverythingPalPage.cs    search page (DynamicListPage)
  Services/                     es.exe client, result model, ranking
  Package.appxmanifest          comServer + windows.appExtension registration
scripts/deploy.ps1              build + loose registration helper
```

## Design notes

- **Search backend**: `es.exe -export-json <temp file> ... -search "<query>"`, read back as UTF-8.
  (`es.exe` writes the console code page to stdout, but `-export-json` writes UTF-8 — important for
  CJK file names.)
- **No native dependencies**: no voidtools SDK DLLs are redistributed.
- **Fallback UX**: a `FallbackCommandItem` surfaces Everything results from the Command Palette home
  search (single-row model of CmdPal; full list opens the page).
- **Path handling**: always build links with `new Uri(Path.GetFullPath(path))`; never concatenate
  `file:///` with a path (drive-root paths such as `D:` throw `UriFormatException`).

## Roadmap

- [x] M1: MSIX + COM registration, extension loads in CmdPal
- [ ] M2: `es.exe` live search, open/reveal/copy actions
- [ ] M3: home-search fallback, settings page (es.exe path, result count, sort, folders)
- [ ] M4: polish, tests, release packaging

## License

MIT — see [LICENSE](LICENSE).
