# EverythingPal — CmdPal Platform API & Microsoft Store Research

**Status:** research only (no production code changed). Author: research subagent. Date: **2026-10-20** (Asia/Shanghai).
**Scope:** CmdPal extension icon API, fallback-command API, extension-settings "About" metadata, Microsoft Store submission requirements for a headless full-trust CmdPal extension, and distribution risks.

---

## 0. Environment facts verified on this machine

| Fact | Verified value | How verified |
|---|---|---|
| Installed CmdPal package | `Microsoft.CommandPalette` **0.13.12862.0** (x64), `Microsoft.CommandPalette_0.13.12862.0_x64__8wekyb3d8bbwe` | `Get-AppxPackage -Name Microsoft.CommandPalette` |
| CmdPal **ABI/SDK** version of that host | **0.13.261013002** (`FileVersion 0.13.2610.13002`) | `(Get-Item "$env:ProgramFiles\WindowsApps\Microsoft.CommandPalette_0.13.12862.0_x64__8wekyb3d8bbwe\Microsoft.CommandPalette.Extensions.dll").VersionInfo` |
| Installed PowerToys | `Microsoft.PowerToys.SparseApp` **0.102.2862.0** | `Get-AppxPackage` |
| Extension SDK referenced by repo | `Microsoft.CommandPalette.Extensions` **0.12.260812002** | `Directory.Packages.props` |
| Newest SDK on NuGet (incl. prerelease) | **0.12.260812002** (no newer version, no newer prerelease) | `https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json` |
| Host winmd vs SDK winmd | Both **14,848 bytes**, same type/member name set, **different SHA-256** (`36CAD325…` host vs `E778A25C…` SDK) | byte compare + ASCII metadata string scan of both `.winmd` files |
| PowerToys `main` used for source quotes | commit **5c9fddcddd9f50401cd5ea6f56b8f421e0badd40** (fetched 2026-10-20) | `GET /repos/microsoft/PowerToys/git/refs/heads/main` |
| Latest PowerToys GitHub release | `v0.101.2362.0` (2026-08-25) — **older** than the installed 0.102 build, i.e. this box runs a newer/insider CmdPal | `GET /repos/microsoft/PowerToys/releases/latest` |

**Important consequence:** the installed host (ABI `0.13.261013002`, dated 2026-10-13) is **newer** than the SDK EverythingPal compiles against (`0.12.260812002`, dated 2026-08-12), and the two `winmd` files have identical type-name sets. So code compiled against 0.12 ABI is ABI-compatible with the installed 0.13 host; the 0.13 host just *also* implements newer host-side icon handling that the 0.12 Toolkit does not expose helpers for (see A.2). This confirms the comment in `Directory.Packages.props` ("host = 0.13.261013002, NuGet max stable = 0.12.x").

> Caveat: `main` is a moving target. All line numbers below are for `main` @ `5c9fddc`; PowerToys source paths are stable but line numbers drift.

---

## A. CmdPal extension icon API

### A.1 What `IconInfo` / `IconData` actually are (SDK 0.12.x and main)

The ABI contract (`Microsoft.CommandPalette.Extensions.idl`, `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl`, L22–31):

```idl
interface IIconData {
    String Icon { get; };
    Windows.Storage.Streams.IRandomAccessStreamReference Data { get; };
};

interface IIconInfo {
    IIconData Light { get; };
    IIconData Dark { get; };
};
```

The Toolkit implementation is a thin, mutable wrapper. `…/Microsoft.CommandPalette.Extensions.Toolkit/IconData.cs` L9–28:

```csharp
public partial class IconData : IIconData
{
    public IRandomAccessStreamReference? Data { get; set; }
    public string? Icon { get; set; } = string.Empty;

    public IconData(string? icon) { Icon = icon; }
    public IconData(IRandomAccessStreamReference data) { Data = data; }
}
```

`…/IconInfo.cs` L9–45:

```csharp
public partial class IconInfo : IIconInfo
{
    public virtual IconData Dark { get; set; } = new();
    public virtual IconData Light { get; set; } = new();

    public IconInfo(string? icon) { Dark = Light = new(icon); }        // glyph, path or protocol string
    public IconInfo(IconData light, IconData dark) { Light = light; Dark = dark; }
    public IconInfo(IconData icon) { Light = icon; Dark = icon; }

    public static IconInfo FromStream(IRandomAccessStream stream)
    {
        var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
        return new IconInfo(data, data);
    }
}
```

**Answer to "what does `IconData.Data` accept?":** an `IRandomAccessStreamReference` (WinRT), i.e. a **binary image stream**, *not* base64 and *not* a path. The host opens it with `OpenReadAsync()` and decodes it as a bitmap — `Microsoft.CmdPal.UI/Helpers/Icons/IconLoaderService.cs` L616–623:

```csharp
if (streamRef != null)
{
    using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);
    return await CreateImageIconSourceAsync(bitmapStream, scaledSize, diagnostics)...
}
```

…and `CreateImageIconSourceAsync` (L743–772) does `new BitmapImage(); ApplyDecodeSize(...); bitmap.SetSourceAsync(bitmapStream)`. **PNG is the safe format** (that is what the SDK's own `ThumbnailHelper` emits, see A.2); SVG via stream is *not* a stream path (SVG is handled by URI or the `|Svg|` protocol).

`IconData.Icon` is the *string* channel and accepts four different things depending on how the host parses it — see A.3.

The host caches both channels separately and treats "has icon" as "`Icon` non-empty **or** `Data` non-null" — `Microsoft.CmdPal.UI.ViewModels/IconDataViewModel.cs` L20–29:

```csharp
public bool HasIcon => !string.IsNullOrEmpty(Icon) || Data.Unsafe is not null;
public string Icon { get; private set; } = string.Empty;
public ExtensionObject<IRandomAccessStreamReference> Data { get; private set; } = new(null);
```

**Can an extension show a dynamic per-item image icon?** **Yes, two supported ways**, both available in the *already-referenced* 0.12.260812002 Toolkit (verified by reflection over `artifacts/layout/Microsoft.CommandPalette.Extensions.Toolkit.dll`):

* `Microsoft.CommandPalette.Extensions.Toolkit.IconInfo` ctors: `IconInfo(string)`, `IconInfo(IconData)`, `IconInfo(IconData light, IconData dark)`; static `IconInfo.FromStream(IRandomAccessStream)`.
* `Microsoft.CommandPalette.Extensions.Toolkit.IconData` ctors: `IconData(string)`, `IconData(IRandomAccessStreamReference)`; props `Icon`, `Data`.
* `IconHelpers`: `FromRelativePath(string)`, `FromRelativePaths(string light, string dark)`.
* `ThumbnailHelper`: `GetThumbnail(string path, bool jumbo = false)`, `IsImagePath(string?)`, `GetProtocolIconStream(string protocol, bool jumbo)`.

Precedence in the host (`IconLoaderService.LoadIconCoreAsync`, L458–637): **`Icon` string is tried first**; the `Data` stream is only used when `Icon` is null/empty (L471 `if (!string.IsNullOrEmpty(iconString))`, L616 `if (streamRef != null)`). Consequence: **set exactly one of them**; `IconInfo.FromStream(...)` is the right helper (it leaves `Icon` empty).

### A.2 Recommended way to show *real file-type icons* for search results

**The first-party precedent is the built-in Indexer (file search) extension**, `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Indexer/Data/IndexerListItem.cs` L37–44:

```csharp
FilePath = indexerItem.FullPath;
Title = indexerItem.FileName;
Subtitle = indexerItem.FullPath;
if (!string.IsNullOrEmpty(FilePath))
{
    Icon = new IconInfo(ShellItemIconProtocol.Create(FilePath));
}
```

So on current hosts the *sanctioned* approach is the **`|ShellItemIcon|` icon protocol** (new in the 0.13 host; helper class lives in `main` but **not** in the 0.12 NuGet package). Wire format, `…/Toolkit/ShellItemIconProtocol.cs` L20–43 + L99–106:

```csharp
private const string ShellItemIconPrefix = "|ShellItemIcon|";         // standard
private const string JumboShellItemIconPrefix = "|JumboShellItemIcon|"; // 256px
private const string WireVersion = "v1;";
public static string Create(string itemPath) => CreateCore(ShellItemIconPrefix, itemPath);
public static string CreateJumbo(string itemPath) => CreateCore(JumboShellItemIconPrefix, itemPath);
// payload = prefix + "v1;" + itemPath.Length + ":" + itemPath   (length in UTF-16 code units)
```

Sibling protocols: `|AppIcon|` / `|JumboAppIcon|` (`AppIconProtocol.cs` L21–22 — app icons with fallback candidates) and `|Svg|` / `|ThemedSvg|` (`Microsoft.CmdPal.UI/Helpers/Icons/SvgIconProtocol.cs` L26–27 — inline SVG or SVG file path, theme-aware color substitution).

**Verified on this machine:** the installed host binary `Microsoft.CmdPal.UI.exe` (0.13.12862) contains the literals `|ShellItemIcon|`, `|JumboShellItemIcon|`, `|AppIcon|`, `|JumboAppIcon|`, `|Svg|`, `|ThemedSvg|` (UTF-16 scan) and the symbol names `ShellItemIconProtocol`, `AppIconProtocol`, `IconProtocolRegistry` (11 hits). So the installed host *can* resolve them. (Presence of the string is strong evidence but not a runtime test — marked as such in §6.)

Because the 0.12 Toolkit does not ship `ShellItemIconProtocol`, an extension on 0.12 must either (a) format the wire string by hand (it is a documented 3-token format), or (b) stay on the classic route:

```csharp
// classic route, works on any host with IIconData.Data (0.12+)
var stream = await ThumbnailHelper.GetThumbnail(fullPath, jumbo: false); // image thumb → else shell file icon
if (stream is not null) item.Icon = IconInfo.FromStream(stream);
```

`ThumbnailHelper.GetThumbnail` (`…/Toolkit/ThumbnailHelper.cs` L65–93) tries an image thumbnail first for known image extensions (`IsImagePath`, L46–63, list at L21–39: png/jpg/gif/bmp/svg/tif/ico/webp/heic/avif…), then falls back to a shell file icon; internally it uses `SHParseDisplayName` + `SHGetFileInfo` + `IImageList(SHIL_JUMBO)` + `SHDefExtractIconW`, converts HICON → **PNG** → `InMemoryRandomAccessStream` (`GetMemoryStreamFromIcon` L110–136, `FromHIconToStream` L398–411). `GetProtocolIconStream(protocol, jumbo)` (L317) resolves `mailto:`/`steam:`-style protocol icons.

**Community/other samples:** every built-in extension uses `new IconInfo("\uE7xx")` glyphs for its *rows* (e.g. `Microsoft.CmdPal.Ext.Indexer/FallbackOpenFileItem.cs` L43 `Icon = Icons.FileExplorerIcon;`, L199 `Icon = iconInfo ?? Icons.FileExplorerIcon;`) and only per-file icons on the *page items* (`IndexerListItem.cs` L43). A Store-published third-party extension sample I could not verify (see §6).

**Performance caveats (documented in host source):**

* Each inline fallback row gets a **200 ms** budget before the host detaches it and continues: `Microsoft.CmdPal.UI.ViewModels/FallbackUpdateManager.cs` L23–25 (`FallbackItemSlowTimeout = 200ms`), with sibling-worker escalation at L77–120. Shell icon extraction per row can easily exceed this.
* "Global" fallbacks are **re-scored on every render**, precisely because their titles resolve asynchronously — `Commands/MainListPage.cs` L78–81 and L852–855. Doing per-row icon work on every keystroke is a real risk.
* The host has a layered icon cache/queue (`CachedIconSourceProvider.cs`, `IconLoadQueue.cs`, `ShellIconLocationCache.cs`, `IconCachePartition.cs`, plus PRs "[CmdPal: Icons (8/n) … defer app icon extraction](https://github.com/microsoft/PowerToys/pull/50188)" and "[CmdPal: Icons (11/n) … SVG icon protocols](https://github.com/microsoft/PowerToys/pull/50191)") — i.e. Microsoft explicitly treats icon extraction as a perf-sensitive subsystem. Cache icons per path/extension yourself and never block the fallback `UpdateQuery` path with shell calls.

### A.3 What exactly does `IconInfo(string)` mean? (glyph vs path vs protocol)

The string is interpreted **by the host**, in this order — `Microsoft.CmdPal.UI/Helpers/Icons/IconPathConverter.cs` L32–64:

```csharp
if (IconProtocolRegistry.Find(iconPath) is { } protocolProcessor) { ... }      // 1. |Svg|, |AppIcon|, |ShellItemIcon| ...
if (IconPathParser.TryParseBinaryIconReference(iconPath, out var binaryIcon))  // 2. "C:\x\app.exe,-12" / "shell32.dll,21"
{ ... SHDefExtractIcon ... }
if (iconPath[0] < 128 && Uri.TryCreate(iconPath, UriKind.Absolute, out var uri)) // 3. absolute URI (file:, ms-appx:, http:)
{ return PreparedIcon.FromUri(uri, isSvg: uri.AbsolutePath.EndsWith(".svg"), targetSize); }
var glyphKind = FontIconGlyphClassifier.Classify(iconPath);                     // 4. otherwise: Segoe Fluent glyph
var glyph = glyphKind == FontIconGlyphKind.Invalid ? "\u25CC" : iconPath;
```

Details that matter:

* **Order is significant.** A path ending in `.exe`/`.dll`/`.lnk` (or `.ico,<index>`) is treated as a *binary icon reference* with an optional index parsed after the last comma (`IconPathParser.TryParseBinaryIconReference`, L12–30, incl. native `til::to_int` quirks L32–107). Everything else that parses as an absolute URI becomes an `ImageIconSource` (bitmap) or `SvgImageSource`.
* **Glyph is the last resort**, not the primary meaning — and glyph detection is ASCII-based (`FontIconGlyphClassifier`): a single char ≥ U+0080 is a glyph; the empty string renders nothing (`PreparedIcon.Empty`).
* **A Windows absolute path therefore works as an image path**, because `C:\dir\foo.png` parses as an absolute `file:` URI. There is **no** "package-relative image path" constructor: a *relative* string like `Assets\foo.png` would fail `Uri.TryCreate(..., Absolute)` and be classified as a glyph (garbage). That is exactly why the Toolkit ships `IconHelpers` (`…/IconHelpers.cs` L9–14):

```csharp
public static IconInfo FromRelativePath(string path) =>
    new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), path));

public static IconInfo FromRelativePaths(string lightIcon, string darkIcon) =>
    new(new(... lightIcon), new(... darkIcon));   // light first, dark second
```

Note the parameter order: `IconInfo(IconData light, IconData dark)` — `IconHelpers.FromRelativePaths(light, dark)` respects that; `new IconInfo("a","b")` does **not** exist, but `new IconInfo(light, dark)` does.

* **`ms-appx:///` is a trap for extensions.** `ms-appx` resolves against the *host* package (CmdPal), not the extension package (they are different packages). Use `IconHelpers.FromRelativePath("Assets\\foo.png")` (absolute file path under `AppDomain.CurrentDomain.BaseDirectory`) or a `Data` stream. *(Inference from the resolver code + MSIX identity model; not explicitly documented — see §6.)*

---

## B. Fallback command API

### B.4 The fallback model: one `IFallbackCommandItem` per row (confirmed; no newer multi-result API)

ABI (`…Extensions.idl` L378–404):

```idl
interface IFallbackCommandItem requires ICommandItem {
    IFallbackHandler FallbackHandler{ get; };
    String DisplayTitle{ get; };
};
interface IFallbackCommandItem2 requires IFallbackCommandItem {
    String Id { get; };          // ← the only addition; used as the settings key
};
interface ICommandProvider ... {
    ICommandItem[] TopLevelCommands();
    IFallbackCommandItem[] FallbackCommands();   // ← array: N items = N rows
    ...
};
```

Toolkit (`…/Toolkit/FallbackCommandItem.cs` L7–43):

```csharp
public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem, IFallbackHandler, IFallbackCommandItem2, IExtendedAttributesProvider
{
    public FallbackCommandItem(string displayTitle, string id) { DisplayTitle = displayTitle; Id = id; }
    public FallbackCommandItem(ICommand command, string displayTitle, string id) : base(command) { ... }
    public IFallbackHandler? FallbackHandler { get => _fallbackHandler ?? this; init => _fallbackHandler = value; }
    public virtual string Id { get; }
    public virtual string DisplayTitle { get; }
    public virtual void UpdateQuery(string query) => _fallbackHandler?.UpdateQuery(query);
}
```

**Findings:**

* Registering **N `FallbackCommandItem`s to get N inline rows is the legitimate, documented model.** Each item is a *single* `ICommandItem`; the extension's own `DynamicListPage`/`ListItem`s are what produce multiple *page* results after the row is activated. The PowerToys template skill states this explicitly (`.github/skills/add-fallback-commands/SKILL.md` L25–27, L154–155): "CmdPal asks extensions for fallback results → your extension's `FallbackCommands()` provides items that respond to the query"; "`FallbackCommands()` returns `IFallbackCommandItem[]`".
* **There is no API — in 0.12, in the installed 0.13 host, or in `main` — that lets *one* fallback item return multiple inline rows.** Evidence: (i) the full IDL above; (ii) `IFallbackCommandItem2` only adds `Id`; (iii) both `winmd` files (0.12 SDK and 0.13.12862 host) contain exactly the same fallback-related names (`IFallbackCommandItem`, `IFallbackCommandItem2`, `IFallbackHandler`, `FallbackHandler`, `FallbackCommands`) — an ASCII metadata-dump comparison showed identical sets, no `IFallbackCommandItem3`/`FallbackCommands2`; (iv) NuGet has nothing newer than `0.12.260812002`, including prereleases.
* Newer provider surfaces exist (`ICommandProvider2.GetApiExtensionStubs`, `ICommandProvider3.GetDockBands`, `ICommandProvider4.GetCommandItem` — IDL L412–471) but none changes fallback cardinality.

**Practical implication for EverythingPal:** one `FallbackCommandItem` per home-search row is the only way to get N rows; ~N rows is also a UX limit, because the host merges fallbacks into the same scored list (`MainListPageResultFactory.Create`), and each row consumes 200 ms of the fallback update budget.

### B.5 What drives the "fallback commands" settings list (names, toggles) — and hiding/grouping

The UI is generated from the live fallback items. `Microsoft.CmdPal.UI/Settings/ExtensionPage.xaml` L191–236:

```xml
<ItemsRepeater ItemsSource="{x:Bind ViewModel.FallbackCommands, Mode=OneWay}" ...>
  <DataTemplate x:DataType="viewModels:FallbackSettingsViewModel">
    <controls:SettingsExpander
        AutomationProperties.AutomationId="{x:Bind Id, Mode=OneWay}"
        Header="{x:Bind DisplayName, Mode=OneWay}" IsExpanded="False">
      ... IconBox SourceKey="{x:Bind Icon, Mode=OneWay}" ...
      <ToggleSwitch IsOn="{x:Bind IsEnabled, Mode=TwoWay}" />
      <controls:SettingsExpander.Items>
        <controls:SettingsCard ContentAlignment="Left">
          <ptcontrols:CheckBoxWithDescriptionControl
              x:Uid="Settings_FallbacksPage_GlobalResults_SettingsCard"
              IsChecked="{x:Bind IncludeInGlobalResults, Mode=TwoWay}"
              IsEnabled="{x:Bind IsEnabled, Mode=OneWay}" />
        </controls:SettingsCard>
      </controls:SettingsExpander.Items>
    </controls:SettingsExpander>
```

Name resolution — `Microsoft.CmdPal.UI.ViewModels/FallbackSettingsViewModel.cs` L81–87:

```csharp
Id = fallback.Id;
DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
    ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
    : fallback.DisplayTitle;
Icon = new(fallback.InitialIcon); Icon.InitializeProperties();
```

Build/enumeration — `ProviderSettingsViewModel.cs` L264–285: `HasFallbackCommands => _provider.FallbackItems?.Length > 0`; `BuildFallbackViewModels()` creates **one view-model per `IFallbackCommandItem`, keyed by its `Id`** from `_providerSettings.FallbackCommands` (falling back to `new FallbackSettings()`).

**Answers:**

* Display name in the toggle list = **`IFallbackCommandItem.DisplayTitle`** (fall back to `Title`, then provider `DisplayName`). It is *localizable strings owned by the extension* — so you control the wording of every row, and should make each `DisplayTitle` self-describing (e.g. "Everything: open first result", "Everything: search in Everything").
* Toggle identity = **`IFallbackCommandItem2.Id`** — a stable, unique, non-empty string. `FallbackCommandItem` throws if `id` is null/whitespace (L20–23) in the `(ICommand, string, string)` ctor. This `Id` is also the persisted settings key (`FallbackSettings` per provider, `ProviderSettings.FallbackCommands[id]`), and the UI-test `AutomationId`. **Changing an `Id` silently resets that row's user toggle.**
* **You cannot hide or group rows in that list.** It is a flat `ItemsRepeater` over all fallback items; hidden ≠ possible unless you return fewer items from `FallbackCommands()`. The only levers are: how many items you register, their `DisplayTitle`, their `Icon`, and their order.
* Row order in settings = order returned by `FallbackCommands()` (the repeater binds the list as built, L271–284). User-configurable *execution* order is separate: a "Manage fallback order" dialog with drag & drop (`ExtensionPage.xaml` L165–189 `RankButton_Click`; strings `ManageFallbackOrderDialogDescription.Text` = "Drag items to set which fallback commands run first…"), persisted as `SettingsModel.FallbackRanks` (`SettingsModel.cs` L141–147) and used by the `FallbackRanker` control.

### B.6 Exact semantics: per-fallback `IsEnabled` vs `IncludeInGlobalResults`

Data model — `Microsoft.CmdPal.UI.ViewModels/FallbackSettings.cs` L9–29:

```csharp
public record FallbackSettings
{
    public bool IsEnabled { get; init; } = true;
    public bool IncludeInGlobalResults { get; init; }
    public FallbackSettings(bool isBuiltIn) { IncludeInGlobalResults = isBuiltIn; }  // extensions default FALSE
    ...
}
```

Coupling rules — `FallbackSettingsViewModel.cs` L25–69:

* turning `IsEnabled` **off** forces `IncludeInGlobalResults = false`;
* turning `IncludeInGlobalResults` **on** forces `IsEnabled = true`;
* the "Include in the Global result" checkbox is disabled in the UI while `IsEnabled == false` (`ExtensionPage.xaml` L230);
* every change persists and sends `ReloadCommandsMessage` (i.e. commands are re-read, this is a hot action, so keep `FallbackCommands()` cheap).

Global set — `SettingsModel.cs` L513–529:

```csharp
public string[] GetGlobalFallbacks()
{
    foreach (var provider in ProviderSettings.Values)
      foreach (var fallback in provider.FallbackCommands)
        if (fallback.Value.IsEnabled && fallback.Value.IncludeInGlobalResults)
            globalFallbacks.Add(fallback.Key);
    return globalFallbacks.ToArray();
}
```

Consumption — `Commands/MainListPage.cs` L650–672:

```csharp
var configuredGlobalFallbackIds = _settingsService.Settings.GetGlobalFallbacks();
foreach (var s in commands) {
    if (!s.IsFallback) continue;
    if (configuredGlobalFallbackIds.Contains(s.Id)) specialFallbacks.Add(s);   // "global" set
    else if (s.IsEnabled)                          commonFallbacks.Add(s);     // "inline" set
}
_fallbackUpdateManager.BeginUpdate(SearchText, [.. specialFallbacks, .. commonFallbacks], token);
```

Runtime per-row enablement — `TopLevelViewModel.cs` L160–178: `IsFallback` → `_providerSettings.FallbackCommands[_fallbackId].IsEnabled` (default `true` if absent); `SafeUpdateFallbackTextSynchronous` returns early when `!IsEnabled` (L417–427), so a disabled fallback's `UpdateQuery` is never called.

User-facing wording — `Microsoft.CmdPal.UI/Strings/en-us/Resources.resw` L1197–1202:

* "Include in the Global result" — "**Show results on queries without direct activation command**"
* `ExtensionFallbackCommandsHeader.Text` = "Fallback commands" — "…the commands presented to the user when the search text doesn't exactly match the name of a command."

**Net semantics:**

| Toggle | Effect |
|---|---|
| Per-fallback `IsEnabled = false` | The fallback never runs: it is excluded from both sets, `UpdateQuery` is skipped. Hidden from results; not "just a toggle in settings". |
| `IsEnabled = true`, `IncludeInGlobalResults = false` (extension default) | Fallback runs and is fuzzy-scored against the query at keystroke time (score frozen); shows rows only when its `Title` matches. Cheapest mode. |
| `IsEnabled = true`, `IncludeInGlobalResults = true` | Treated as a "special/global" fallback: always updated, and **re-scored on every render against its live title** (`MainListPage.cs` L78–81, L852–855 `_globalFallbackSources`, `ScoreDeferredFallbacks` at L333–337) so asynchronously-resolved results still surface — i.e. it can show results for queries with no matching command. More expensive; also what built-ins get by default (`FallbackSettings(bool isBuiltIn)`, L19–22). |

---

## C. Extension settings page "About" section

### C.7 Where the About fields come from — `Package.DisplayName`, `Package.PublisherDisplayName`, `Package.Id.Version`

`Microsoft.CmdPal.UI/Settings/ExtensionPage.xaml` L269–282:

```xml
<TextBlock x:Uid="ExtensionAboutHeader" ... Visibility="{x:Bind ViewModel.IsFromExtension}" />
<controls:SettingsCard
    Description="{x:Bind ViewModel.Extension.Publisher, Mode=OneWay}"
    Header="{x:Bind ViewModel.Extension.PackageDisplayName, Mode=OneWay}">
  <TextBlock Foreground="{ThemeResource TextFillColorSecondaryBrush}"
             IsTextSelectionEnabled="True" Text="{x:Bind ViewModel.ExtensionVersion}" />
</controls:SettingsCard>
```

`Microsoft.CmdPal.UI.ViewModels/Models/ExtensionWrapper.cs` L34–43:

```csharp
public ExtensionWrapper(AppExtension appExtension, string classId)
{
    PackageDisplayName = appExtension.Package.DisplayName;
    ExtensionDisplayName = appExtension.DisplayName;
    PackageFullName = appExtension.Package.Id.FullName;
    PackageFamilyName = appExtension.Package.Id.FamilyName;
    ...
    Publisher = appExtension.Package.PublisherDisplayName;
    Version = appExtension.Package.Id.Version;
}
```

`ProviderSettingsViewModel.cs` L104–108 formats the version as `Major.Minor.Build.Revision`; `IsFromExtension` = "has a package" (built-ins show a different card, `ExtensionPage.xaml` L294–298).

**Confirmed mapping (WinRT `Windows.ApplicationModel.Package` → MSIX manifest):**

| About field | Source | Manifest element in `src/EverythingPal/Package.appxmanifest` |
|---|---|---|
| Header (app name) | `Package.DisplayName` | `<Properties><DisplayName>Everything Search</DisplayName>` (L18) — **not** `<uap:VisualElements DisplayName>` and **not** the AppExtension `DisplayName` |
| Description (publisher) | `Package.PublisherDisplayName` | `<Properties><PublisherDisplayName>` (L19, currently `czz404`) |
| Version text | `Package.Id.Version` | `<Identity Version="0.1.0.0">` (L15) |

So **yes — the publisher string shown in CmdPal is `Properties/PublisherDisplayName`, and no, it is not `Identity/@Publisher`** (the `CN=…` value, which CmdPal never displays). Changing `PublisherDisplayName` to a real publisher/brand name is sufficient to stop showing `czz404` in the About card; `Identity/@Publisher` only matters for package identity/signing and **must equal the Partner Center `Package/Identity/Publisher` (a `CN=<GUID>` value) for a Store submission** (see D.12).

Note that the *AppExtension*'s own `DisplayName`/`Description` (manifest L61–62) feed `ExtensionDisplayName` (used in the extensions list and in the header text `ExtensionEnable` + `ViewModel.DisplayName`), so it should agree with the package display name.

### C.8 Is there extension-provided metadata feeding About?

**No.** There is no assembly-info, attribute, or `CommandProvider` property in the About path: the card binds only to `IExtensionWrapper` values read from the package via `AppExtension.Package` (above). `CommandProvider`'s `DisplayName`/`Icon`/`Id` (Toolkit `CommandProvider.cs`; `ICommandProvider` IDL L390–404) drive the *extension list / commands*, not About. `IExtendedAttributesProvider` is used for icon font-family attributes only (`IconDataViewModel.cs` L50–65, `WellKnownExtensionAttributes.FontFamily`).

Practical consequences:
* To change the About "publisher", edit `Properties/PublisherDisplayName` (and for Store, this is overwritten/validated against Partner Center's `PublisherDisplayName`).
* Localized display names would need `ms-resource:` indirection in `Properties/DisplayName`/`PublisherDisplayName` and a `.resw` — not implemented in this repo.

---

## D. Microsoft Store submission for a headless CmdPal extension

### D.9 Is a headless full-trust MSIX (COM server + appExtension only) submittable?

**Official position: Microsoft documents this path.** PowerToys ships a first-party publishing guide for CmdPal extensions, `.github/skills/publish-extension/references/store-publishing.md` (main), which describes exactly this package shape (no app UI at all) and even the expected Store listing and certification notes:

> L113–127: "In **Packages**, upload your `.msixbundle` file … In **Store Listings** → **Description**, include a note like: *`YourExtension` integrates with the Windows Command Palette to provide […]. Requires PowerToys with Command Palette enabled.* … In **Notes for certification**, add testing instructions: *This extension requires Microsoft PowerToys … with the Command Palette feature enabled. To test: 1. Install PowerToys and enable Command Palette 2. Install this extension 3. Open Command Palette (Win+Alt+Space by default) 4. Search for […]'* … Certification typically takes 1–3 business days."

It also documents the discovery limitation (L145–157): CmdPal cannot browse Store-only extensions; users need the store tag `ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette`, and WinGet publishing is recommended for in-palette discovery.

**But the general Store policies bite a truly non-launchable app.** From [Microsoft Store Policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies):

* **10.1.2** — "Your product must be **fully functional** and must provide appropriate functionality for targeted systems and devices."
* **10.1.1** — metadata "should accurately describe the functions, features, user experience and **any important limitations** of your product, including required or supported input devices."
* Product-compatibility clause — "Products **must support the devices and platforms** on which they are downloaded … If a product is downloaded on a device with which it is **not compatible, it must detect that at launch and display a message** to the customer detailing the requirements."
* **10.2** — "You are solely responsible for all product safety testing, certificate acquisition (**unless provided by Microsoft Store signing**), and the implementation of any appropriate feature safeguards."

That last product-compatibility clause is the real hazard: a CmdPal extension installed on a machine **without PowerToys/CmdPal** must, per policy, *detect incompatibility at launch and tell the user*. Today `src/EverythingPal/Program.cs` does nothing at all when started with no arguments (it only handles `--search-test` and `-RegisterProcessAsComServer`; the outer `try` simply falls through and the process exits), while the manifest does **not** set `AppListEntry="none"` — so Windows shows an "Everything Search" Start-menu tile that, when clicked, does nothing. That is precisely the combination a tester can flag under 10.1.2 / the compatibility clause.

**Evidence about live CmdPal extensions in the Store:** I could **not** verify a list of currently published CmdPal extensions (the Store catalog is not enumerable without the Store client; `apps.microsoft.com` search HTML is JS-rendered). What *is* verifiable: (a) Microsoft documents Store publishing for these packages (above), (b) the store association tag exists, (c) Microsoft's own [CmdPalGitHubExtension](https://github.com/microsoft/CmdPalGitHubExtension/) says as of 2026-09 "The Command Palette GitHub Extension is **coming** to the Microsoft Store" — i.e. even Microsoft had not yet shipped a CmdPal extension to the Store at that date, and (d) a community directory exists at [microsoft.github.io/CmdPal-Extensions](https://microsoft.github.io/CmdPal-Extensions/) (categories include "Utilities & tools"). Treat "CmdPal extensions are routinely accepted by Store certification" as **unproven** (§6).

**Recommended de-risking (standard workarounds):**

1. **Make the package launchable and self-explanatory** — keep the Start-menu entry and, when `EverythingPal.exe` starts with **no arguments**, show a small WinUI/Win32 "Everything Search for Command Palette" window that states: what it is, that it requires PowerToys Command Palette, whether PowerToys/Everything is detected, the version, links (support/privacy), and an "Open Command Palette" button. This satisfies 10.1.2 + the "message detailing the requirements" clause and gives reviewers something to see. (This is the single highest-value change before submission.)
2. If you prefer a fully silent package, set `uap:VisualElements … AppListEntry="none"` — but then *nothing* is launchable, which is the riskier certification posture. Microsoft's own guide assumes the extension case is acceptable, so either choice is defensible; the launchable-about-window is safer.
3. **Notes for certification** must explain the PowerToys prerequisite and the exact 4-step test script (quote from the PowerToys guide, above).
4. List it as a **utility/extension for PowerToys**, and say so in the first line of the description.

### D.10 Exact packaging requirements

| Requirement | Verified answer | Source |
|---|---|---|
| Accepted upload types | `.msix`, `.msixupload`, `.msixbundle`, `.appx`, `.appxupload`, `.appxbundle` are all accepted on the Packages page; **`.msixupload`/`.appxupload` recommended** for Win10+ | [Upload MSIX app packages](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages) |
| `.msixbundle` for Store? | Yes — accepted, and the PowerToys CmdPal guide uploads a `.msixbundle` | Learn (above) + `store-publishing.md` L113 |
| Bundle content rules | A bundle may contain per-architecture packages; "**the manifest of each included package must use the same elements and attributes, except for the `ProcessorArchitecture` attribute of the `Identity` element**" | [App package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements) |
| Unsigned submission / Store re-signs | **In practice yes**: Partner Center validates that `Identity/@Publisher` equals the reserved `Package/Identity/Publisher` (`CN=<GUID>`); the Store signs the delivered package with the Microsoft Store certificate. Corroborated by third-party evidence ([GeoLibre PR #1044](https://github.com/opengeos/GeoLibre/pull/1044), [hermes-agent commit](https://github.com/NousResearch/hermes-agent/commit/830bfa275bf67837bbed473f06dcde7888fee0a4), [Q&A on CN mismatch](https://learn.microsoft.com/en-us/answers/questions/3953814/matching-microsoft-package-identity-publisher-cn-w)). The Store policy text only says cert acquisition is your job "unless provided by Microsoft Store signing". **Marked: strongly supported, not a first-party sentence that says "unsigned bundles are accepted".** | Policy 10.2 + above links |
| Identity values must match Partner Center | `Identity Name` = `Package/Identity/Name`, `Identity Publisher` = `Package/Identity/Publisher`, `Properties/PublisherDisplayName` = `Package/Properties/PublisherDisplayName` (all three, exactly) | `store-publishing.md` L10–17; guide checklist L135 |
| Version format | 4 parts; sections 2–4 integer 0–65535, first section cannot be 0; **for Windows 10/11 packages the 4th section is reserved for the Store and must be built as 0** (Store may change it) | App package requirements → "Package version numbering" |
| `TargetDeviceFamily` | Required; `MinVersion`/`MaxVersionTested` must be present. Repo currently declares `Windows.Universal` **and** `Windows.Desktop`, both `MinVersion 10.0.19041.0` — redundant but legal; `Windows.Desktop` alone is the honest declaration for a desktop-only COM server, and `Windows.Universal` is what makes the Store offer it to Xbox/Holo (which you do not want) | App package requirements → "Supported versions"; manifest L25–28 |
| Architecture | Build x64 **and** ARM64 and bundle them (Microsoft's own CmdPal guide builds both). For equal versions the Store ranks x64 > x86 > Arm > neutral | `store-publishing.md` L66–104; App package requirements → version/ranking |
| `runFullTrust` allowed? | **Allowed but it is a restricted capability**: "you must provide info during the app submission process … on the Submission options page" and it is reviewed. It is *required* for full-trust/mediumIL apps and specifically "to be able to register **out-of-process COM servers** … a packaged app needs `runFullTrust`". This is a well-trodden scenario (all MSIX-packaged COM servers, incl. many Store apps, do it) | [App capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations) → "Full Trust Permission Level" / "Restricted capabilities" |
| `unvirtualizedResources` allowed? | **Restricted, and the docs are discouraging**: it exists to disable `RegistryWriteVirtualization`/`FileSystemWriteVirtualization`; "This capability is designed for certain types of desktop PC **games** that are published by Microsoft and our partners. It's also needed for apps packaged with external location … **It is not intended to be used for other scenarios**, because it could compromise the system's ability to uninstall cleanly." Expect pushback; have a justification ready or drop it | same Learn page → "Unvirtualized Resources" |
| Certification notes expected | PowerToys prerequisite + numbered test steps + how to obtain Everything (`es.exe`) if it isn't bundled | `store-publishing.md` L118–125 |

**Restricted-capability note:** both `runFullTrust` (needed for packaged COM) and `unvirtualizedResources` (needed because the manifest sets `desktop6:FileSystemWriteVirtualization=disabled` / `RegistryWriteVirtualization=disabled`) trigger the Partner Center "Submission options" justification flow ([Learn](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations) L151–176), which "may add some additional time" and can **fail certification** if not approved. A pragmatic option is to remove the two `desktop6:*Virtualization` properties (and `unvirtualizedResources`) if the app does not truly need unvirtualized writes — the extension itself only reads the filesystem via `es.exe` and stores settings in its own package/local data. *(Whether removal is functionally safe for EverythingPal is a product decision, not verified here.)*

### D.11 Store listing assets, category, privacy, age rating, pricing

| Item | Requirement |
|---|---|
| Screenshots | **At least one required**; up to **10** desktop screenshots (up to 8 for other device families); `.png`, landscape or portrait, ≤50 MB; **desktop: 1366×768 or larger** (4K supported). Captions optional (≤200 chars). Suggested: ≥4 per device family. |
| Store logo / app tile icon | **1:1 App tile icon, 300×300 px, recommended** — "the Store will prioritize this image over the one included in your app package". Other art (2:3 poster, 1:1 box art, 16:9 super hero) is game-oriented/optional for apps. |
| Package logos still needed | Yes — `Assets\StoreLogo.png`, `Square150x150Logo`, `Square44x44Logo`, `Wide310x150Logo`, `SplashScreen` are referenced by the manifest and must exist and be valid sizes, even if the Store overrides the tile art. |
| Category | Policy 10.1.1: "should be listed in the most appropriate category and genre based on the features and functionality it offers." For this product **Utilities & tools** is the natural fit (the community CmdPal directory also groups extensions under "Utilities & tools"). Category is chosen in the submission's Properties page. *(Category *ids* are Partner Center data, not verifiable offline.)* |
| Privacy policy URL | Required **if** the app "accesses, collects or transmits Personal Information", and Partner Center auto-flags apps whose declared capabilities could allow PI access. Entering the URL is part of submission; if you answer "Yes" and provide none, "your submission may fail certification". EverythingPal searches local file names locally and sends nothing — answering "No" is defensible; still recommended to host a short policy page. |
| Support info | Support-info section: website URL field "**optional, but recommended**"; it must point at your own site, not the Store listing. Provide at least a support/contact URL and (recommended) a GitHub issues link. |
| Age rating | IARC questionnaire in Partner Center ("Age ratings" step); no content → typically 3+/E. Required to publish. |
| Pricing / markets | Free, all markets — set in the Pricing & availability step (defaults are free/all markets; per-market pricing is out of scope). |
| Notes for certification | See D.10. |

Sources: [App screenshots, images, and trailers for MSIX apps](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images); [Privacy policy and support info](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info); [Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies); [Age ratings](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/age-ratings).

### D.12 Partner Center steps and identity values

1. **Partner Center** → *Apps and Games* → **New product** → **MSIX or PWA app** → **reserve the app name** (this name is the Store listing name; it does not have to equal `Identity Name`).
2. After creation: **Product Management → Product Identity**. Copy four values: **Package/Identity/Name**, **Package/Identity/Publisher** (this is `CN=<GUID>`, *not* a display name), **Package/Properties/PublisherDisplayName**, **Store ID**.
3. Put them in the package (and keep CI in sync):
   * `Package.appxmanifest`: `<Identity Name="…" Publisher="CN=<GUID>" Version="x.y.z.0" />`, `<Properties><DisplayName>…</DisplayName><PublisherDisplayName>…</PublisherDisplayName>`, and bump `Version` on every submission.
   * `.csproj`: `<AppxPackageIdentityName>`, `<AppxPackagePublisher>`, `<AppxPackageVersion>` (`store-publishing.md` L46–51).
4. Build x64 **and** ARM64, then `makeappx bundle /f bundle_mapping.txt /p <Name>_<ver>_Bundle.msixbundle` (guide L86–108).
5. **Start a new submission → Packages**: upload the `.msixbundle` (accepted; `.msixupload` also accepted and recommended in general Learn guidance).
6. **Submission options**: justify `runFullTrust` (packaged out-of-proc COM server hosting the CmdPal extension) and `unvirtualizedResources` (if kept) — this is where a rejection is most likely to start.
7. **Store listing**: description (state the PowerToys Command Palette requirement in the first lines), 1–10 desktop screenshots, 300×300 tile icon, category *Utilities & tools*, support info, privacy policy (if applicable), search terms.
8. **Age ratings** (IARC questionnaire), **Pricing and availability** (free, markets).
9. **Notes for certification**: the 4-step PowerToys test script + how to get Everything (`es.exe`).
10. **Submit for certification** → typically 1–3 business days; then Store auto-updates users (`store-publishing.md` L129, L169).

Repo-specific gaps to fix before step 3: `Identity Name="EverythingPal"` and `Publisher="CN=czz404"` are placeholder identity values and **will not** match a reserved product; `Properties/DisplayName` is "Everything Search" while the visible extension name is the same — fine, but `PublisherDisplayName` = `czz404` must change (it is exactly what the CmdPal About card shows, §C.7).

---

## E. Risks and unknowns

### E.13 Known gotchas

1. **Package family name is part of CmdPal's settings key → changing identity orphans user settings.** `Commands/CommandProviderWrapper.cs` L51: `ProviderId => Extension?.ExtensionUniqueId ?? Id`; `Models/ExtensionWrapper.cs` L64–73: `ExtensionUniqueId => _appUserModelId + "!" + _extensionId`, where AUMID is "the concatenation of the package family name and the application id". All persisted state is keyed by `ProviderId`: provider enable, **per-fallback toggles** (`ProviderSettings.FallbackCommands`), pinned commands, alias, dock bands, fallback ranks (`SettingsModel.cs`, `ProviderSettingsViewModel.UpdateFallbackSettings` L287–299). Consequence: moving from a test-signed `CN=czz404` identity to the Store identity (`CN=<GUID>` → new PFN) **loses**: enabled/disabled state, per-row fallback toggles, `IncludeInGlobalResults`, pins, aliases, hotkeys. Plan a one-time settings migration note, or accept it for the first Store release.
2. **`Id` churn resets toggles** even within the same identity (same mechanism) — treat fallback `Id`s as a public contract after release.
3. **COM activation on Store installs.** The package registers a packaged out-of-proc COM server via the manifest (`com:ExeServer` + `com:Class Id`), which is the supported path; `runFullTrust` is required for that ([Learn](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)). Nothing about Store distribution changes COM registration, but the **exe must exist at the declared path in every architecture package** and `-RegisterProcessAsComServer` must be the exact argument consumed in `Program.cs`. If activation fails on a user machine, CmdPal logs "Failed to find {ExtensionDisplayName}" (`ExtensionWrapper.cs` L121–131) and the extension silently disappears.
4. **`unvirtualizedResources` + `desktop6:*WriteVirtualization=disabled`** is the most likely certification question. See D.10.
5. **Headless + Start-menu entry that does nothing** is the second most likely certification problem (D.9). Fix by handling the no-argument launch.
6. **Store-only extensions are not discoverable inside CmdPal** (first-party statement, `store-publishing.md` L145–157). Store-only distribution will get you very few installs; publish to **WinGet** as well (`references/winget-publishing.md` in the same folder; Microsoft's own CmdPal extensions ship via `winget install …`).
7. **Store auto-update silently changes the host-facing behavior.** The host is a separate package (`Microsoft.CommandPalette`) that updates independently — an extension built against 0.12 must keep working on newer hosts. This repo is currently *ahead*: host 0.13.261013002 vs SDK 0.12.260812002. Relying on 0.13-only features (icon protocols) means older hosts get an invalid glyph; gate them behind a try/fallback (e.g. check host version, or default to `ThumbnailHelper` + stream).
8. **Bundling `es.exe` (MIT).** Store policy does not forbid bundling third-party executables; policy 10.1.6 requires that if you use third-party content you are "specifically permitted to do so under the service's terms", and 10.2 puts legal compliance on you. MIT requires including the copyright + permission notice — the repo already has `THIRD-PARTY-NOTICES.md`, so ship it inside the package (e.g. `Assets\THIRD-PARTY-NOTICES.txt`) and mention it in the listing. Two practical caveats: (a) **Voidtools Everything's own licence is not MIT** — only `es.exe`'s redistribution terms matter, so verify Voidtools' redistribution permission for the CLI in a commercial Store package (their site currently permits free use; *not verified for redistribution inside a Store app*); (b) SmartScreen/certification does not scan bundled exes, but bundling an unsigned third-party exe that opens a TCP/IPC channel (`es.exe` talks to the Everything service) is exactly the kind of behavior reviewers ask about — put it in the certification notes.
9. **Unsigned → Store-signed switch can break local test loops**: once Store-signed, sideloading the same version over the Store copy fails (different publisher); keep a distinct test identity or uninstall first.

---

## 6. Facts vs assumptions

| # | Claim | Status | Basis |
|---|---|---|---|
| 1 | `IconData.Data` is an `IRandomAccessStreamReference`; `Icon` is a string | **Fact** | winmd + Toolkit source + host source |
| 2 | Host prefers `Icon` string; falls back to `Data` stream | **Fact** | `IconLoaderService.LoadIconCoreAsync` L471/L616 |
| 3 | `IconInfo(string)` is parsed as: protocol → `path,index` binary ref → absolute URI → glyph | **Fact** | `IconPathConverter.Prepare` L32–64 |
| 4 | `IconHelpers.FromRelativePath` turns a relative path into an absolute file path (relative paths alone are *not* supported) | **Fact** | `IconHelpers.cs` L9–14 + parser order |
| 5 | Dynamic per-item PNG icons are possible in the 0.12 SDK (stream) | **Fact** | 0.12 Toolkit reflection (`IconInfo.FromStream`, `IconData(IRandomAccessStreamReference)`) |
| 6 | Installed host 0.13.12862 resolves `\|ShellItemIcon\|`, `\|AppIcon\|`, `\|Svg\|` protocols | **Strong evidence, not runtime-tested** | UTF-16 string scan of `Microsoft.CmdPal.UI.exe`; symbol names present |
| 7 | `ms-appx:///` inside an icon string resolves to the host package, not the extension | **Assumption (high confidence)** | MSIX identity model; not stated in CmdPal docs |
| 8 | One `IFallbackCommandItem` = one row; no multi-result fallback API exists in 0.12/0.13/main | **Fact** | IDL, both winmds, NuGet index |
| 9 | Fallback settings are keyed by `IFallbackCommandItem2.Id`; `DisplayTitle` is the row label | **Fact** | `FallbackSettingsViewModel`, `ProviderSettingsViewModel`, `ExtensionPage.xaml` |
| 10 | `IsEnabled` = master switch; `IncludeInGlobalResults` = "always-run, render-time rescored" fallback | **Fact (from code)**, wording is mine | `FallbackSettings`, `SettingsModel.GetGlobalFallbacks`, `MainListPage` L650–672, resw strings |
| 11 | About publisher = `Properties/PublisherDisplayName`; name = `Properties/DisplayName`; version = `Identity/@Version` | **Fact** | `ExtensionWrapper.cs` L36–43 + WinRT `Package` mapping |
| 12 | Store accepts `.msixbundle` / `.msixupload`; recommends `.msixupload` | **Fact** | Learn: Upload MSIX app packages |
| 13 | Store re-signs unsigned submissions; only `Identity/@Publisher` CN must match Partner Center | **Strong evidence, no single first-party sentence** | Policy 10.2 "unless provided by Microsoft Store signing" + multiple third-party reports + PowerToys guide never signs |
| 14 | `runFullTrust` and `unvirtualizedResources` are restricted capabilities requiring Submission-options justification | **Fact** | Learn: App capability declarations |
| 15 | Screenshots ≥1, ≤10 desktop, ≥1366×768 PNG; 300×300 tile icon recommended | **Fact** | Learn: screenshots/images doc |
| 16 | A CmdPal extension with no launchable UI passes certification | **Unverified** — Microsoft documents the submission flow, but no verified live Store example of a *headless* CmdPal extension; policy 10.1.2 + compatibility clause are the risks | `store-publishing.md`; policies; GitHub extension "coming to Store" |
| 17 | Any specific CmdPal extension is currently live in the Store | **Not verified** (Store catalog not enumerable here) | — |
| 18 | EverythingPal can safely drop `unvirtualizedResources` | **Unknown** — product decision | — |
| 19 | Voidtools permits redistributing `es.exe` inside a Store package | **Not verified** | — |

**Verification commands used (reproducible):**

```powershell
Get-AppxPackage -Name Microsoft.CommandPalette | Select Name,Version,InstallLocation
(Get-Item "$env:ProgramFiles\WindowsApps\Microsoft.CommandPalette_0.13.12862.0_x64__8wekyb3d8bbwe\Microsoft.CommandPalette.Extensions.dll").VersionInfo
Get-FileHash "<host>\Microsoft.CommandPalette.Extensions.winmd","$env:USERPROFILE\.nuget\packages\microsoft.commandpalette.extensions\0.12.260812002\winmd\Microsoft.CommandPalette.Extensions.winmd"
Invoke-WebRequest https://api.nuget.org/v3-flatcontainer/microsoft.commandpalette.extensions/index.json
# protocol-literal scan of the installed host:
$t=[Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes("<host>\Microsoft.CmdPal.UI.exe")); '|ShellItemIcon|','|Svg|' | % { "$_ => " + ([regex]::Matches($t,[regex]::Escape($_))).Count }
```

---

## 7. Top recommendations for EverythingPal

1. **Icons — do not fight the API, use the shell.** For each result row/page item, set exactly one icon channel:
   * preferred (host ≥ 0.13): `new IconInfo("|ShellItemIcon|v1;" + path.Length + ":" + path)` (or `|JumboShellItemIcon|` for big rows) — this is what Microsoft's own Indexer extension does, and the installed host supports it;
   * fallback (any 0.12+ host): `ThumbnailHelper.GetThumbnail(path)` → `IconInfo.FromStream(...)`, with your own `path → icon` LRU cache and a cheap extension-based glyph as the *last* resort.
   Never call shell extraction synchronously from `UpdateQuery`; the host gives each inline fallback **200 ms** before detaching it.
2. **Own the `Icon` string precedence.** Since the host tries `Icon` before `Data`, keep `Icon` **empty** when you pass a stream (`IconInfo.FromStream` does this). Use `IconHelpers.FromRelativePath("Assets\\x.png")` for packaged images — a bare relative path becomes a garbage glyph, and `ms-appx:///` points at CmdPal's package, not yours.
3. **Fallbacks: keep N rows, but make N small and self-describing.** N `FallbackCommandItem`s is the only supported model (no multi-row API in 0.12/0.13/main). Give each a stable, meaningful `Id` (it is the persisted settings key **and** the test AutomationId) and a `DisplayTitle` that reads well in Settings → Extensions → *Fallback commands* (that string is the only label users see; rows cannot be hidden or grouped). Prefer `IncludeInGlobalResults = false` (i.e. don't ask users to enable it) unless a row must answer arbitrary queries — global rows are re-scored on every render.
4. **Fix the About card by fixing the package, not the code.** The About publisher is `Properties/PublisherDisplayName` only. Replace `czz404` there; about-name comes from `Properties/DisplayName` and the version from `Identity/@Version`. No assembly attribute or provider property can override it.
5. **Before submission, make the package launchable and honest.** Handle the no-argument launch with a small "About / requirements" window (name, version, PowerToys + Everything detection, privacy/support links). This simultaneously satisfies Store policy 10.1.2 ("fully functional") and the "detect incompatibility at launch and display a message" clause, and it gives reviewers something to click.
6. **Plan the identity switch as a breaking change.** Moving to the reserved Store identity (`CN=<GUID>`, new PFN) changes `ProviderId` and therefore wipes per-fallback toggles, pins, aliases, dock bands, fallback ranks. Either migrate settings or document it; publish the release notes accordingly.
7. **Keep `runFullTrust` (it is required for the packaged COM server) and try to drop `unvirtualizedResources`.** That capability is documented as intended for games / external-location packaging, and expect a justification request (or rejection) at Submission options. If it must stay, pre-write the justification: full-trust COM server hosting the CmdPal extension + `es.exe` search over user-selected paths.
8. **Ship a real bundle: x64 + ARM64, `.msixbundle`, version `x.y.z.0`.** Identity values copied verbatim from Partner Center → Product identity; listing = *Utilities & tools*, free, all markets, 4+ desktop screenshots at ≥1366×768, 300×300 tile icon, support URL, privacy policy page (even if "no personal data" is the content), plus the PowerToys test script in **Notes for certification**.
9. **Don't rely on the Store alone for distribution.** CmdPal cannot browse Store-only extensions (first-party statement); also publish to WinGet and link the Store tag `ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette`.

---

## Appendix — primary sources

**PowerToys source (`main` @ `5c9fddc`, `github.com/microsoft/PowerToys`)**
* `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl` (L22–31, L378–404)
* `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/IconInfo.cs`, `IconData.cs`, `IconHelpers.cs`, `ShellItemIconProtocol.cs`, `AppIconProtocol.cs`, `ThumbnailHelper.cs`, `FallbackCommandItem.cs`
* `src/modules/cmdpal/Microsoft.CmdPal.UI/Helpers/Icons/IconPathConverter.cs`, `IconLoaderService.cs`, `SvgIconProtocol.cs`
* `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/IconDataViewModel.cs`, `IconInfoViewModel.cs`, `FallbackSettings.cs`, `FallbackSettingsViewModel.cs`, `ProviderSettingsViewModel.cs`, `SettingsModel.cs`, `FallbackUpdateManager.cs`, `Commands/MainListPage.cs`, `Commands/MainListPageResultFactory.cs`, `Models/ExtensionWrapper.cs`
* `src/modules/cmdpal/Microsoft.CmdPal.UI/Settings/ExtensionPage.xaml`(+`.cs`), `Strings/en-us/Resources.resw`
* `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Indexer/Data/IndexerListItem.cs`, `FallbackOpenFileItem.cs`
* `src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/.github/skills/add-fallback-commands/SKILL.md`
* `src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension/.github/skills/publish-extension/references/store-publishing.md`, `winget-publishing.md`

**Microsoft Learn**
* [Upload MSIX app packages](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages)
* [App package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
* [App capability declarations (restricted capabilities, runFullTrust, unvirtualizedResources)](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)
* [Microsoft Store Policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
* [App screenshots, images, and trailers](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
* [Privacy policy and support info](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
* [Age ratings](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/age-ratings)
* [IconInfo class (toolkit docs)](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/microsoft-commandpalette-extensions-toolkit/iconinfo)
* [How Command Palette extensions work](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
* [Finding and installing Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/finding-and-installing-extensions)

**Other**
* [CmdPal GitHub Extension — "coming to the Microsoft Store"](https://github.com/microsoft/CmdPalGitHubExtension/)
* [Community CmdPal extension directory](https://microsoft.github.io/CmdPal-Extensions/)
* [PowerToys PR #50188 — "CmdPal: Icons (8/n) — Add icon protocols and defer app icon extraction"](https://github.com/microsoft/PowerToys/pull/50188)
* [PowerToys PR #50191 — "CmdPal: Icons (11/n) — plain and theme-aware SVG icon protocols"](https://github.com/microsoft/PowerToys/pull/50191)
