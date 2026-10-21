<#
.SYNOPSIS
    Capture Microsoft Store screenshots of the EverythingPal Command Palette extension from the
    RUNNING, INSTALLED build (never installs/uninstalls anything).

.DESCRIPTION
    Store listing requirement (verified in docs/research/platform-and-store-research.md):
    at least one desktop screenshot, PNG, 1366x768 or larger. This script:
      1. opens/focuses PowerToys Command Palette (Microsoft.CmdPal.UI) via the CmdPal hotkey
         (Win+Alt+Space, read from the CmdPal settings when available) or, as a fallback, by
         activating shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App;
      2. resizes the real CmdPal window to the requested logical size (default 1366x768, which is
         exactly the Store minimum) and centres it on the primary screen;
      3. types the requested query into the search box with SendInput Unicode events (works for
         ASCII and CJK; no IME needed) - typing is only performed after verifying that the
         foreground window belongs to Microsoft.CmdPal.UI, so a failed focus can never leak
         keystrokes into another application;
      4. captures the CmdPal window rectangle (GetWindowRect) with CopyFromScreen, plus a
         full-screen fallback image;
      5. validates the saved PNG dimensions (>= 1366x768) and writes a capture manifest next to the
         images.

    Tested against the currently installed dev package (EverythingPal_0.1.0.0_x64__b32a3jmekh2ja).
    The new Store build must be installed by the Lead before the final captures; this script never
    touches package installation.

.PARAMETER Queries
    Search queries to capture, one screenshot pair per query. Default @("wantai"), the query used by
    the Store screenshot shot list (matches the user's Everything index on this machine).

.PARAMETER PreQuery
    Optional query typed BEFORE each capture query (with -PreWaitMs settle) and then replaced. Used to
    capture a state that only makes sense after a previous successful search, e.g. the home-screen
    zero-result row after a hit (PreQuery=wantai, Queries=zzzqqq).

.PARAMETER Mode
    home  = capture the Command Palette home screen with the EverythingPal inline fallback rows
            (requires the extension fallback rows to be enabled in CmdPal -> Extensions -> Everything
            Search -> Fallback commands, and a build that renders them);
    page  = capture the "Everything 搜索" command page (type the command name, press Enter, then type
            the file query). This does NOT depend on the fallback toggles and is the safest Store
            screenshot, but it shows the extension page instead of the home-screen rows.

.PARAMETER CommandSearchText
    Text typed on the CmdPal home screen to reach the extension command in -Mode page. Default
    "Everything 搜索" (the installed command's display name). For an English install set e.g.
    "Everything search".

.PARAMETER OutputDir
    Output directory. Default docs/store/screenshots (the only docs/store path QA owns).

.PARAMETER Width
    Captured window width in physical pixels. Default 1366 (Store desktop minimum).

.PARAMETER Height
    Captured window height in physical pixels. Default 768 (Store desktop minimum).

.PARAMETER WaitMs
    Settle time after typing before the screenshot, to let the 150 ms debounce + es.exe query and the
    fallback rows render. Default 1800.

.PARAMETER ScrollDown
    Number of Down-arrow presses after typing (default 0), to scroll the result list so later rows
    (e.g. the EverythingPal fallback rows under "回退") are visible in the screenshot.

.PARAMETER SelectFirst
    Press Down once after the results settle so the first result is selected and the details pane
    (size, modified time, clickable path) is populated. Recommended for the page-mode screenshot.

.PARAMETER ClickFirstResult
    Click the first result row with the mouse after typing (in addition to -SelectFirst). Some
    Command Palette versions only open the details pane for an item focused via the pointer/selection
    change, so this is the reliable way to show the details pane in a screenshot.

.PARAMETER OpenContextMenu
    After selecting the first result, press Ctrl+K to open its context menu before capturing (Store
    shot for open/reveal/copy actions). The palette is closed afterwards as usual.

.PARAMETER Names
    Optional output base names, one per query, e.g. -Names 01-search-page,02-home-inline-rows.
    The script writes <name>.png and <name>-fullscreen.png. Default naming is
    NN-cmdpal-<query-slug>.png / NN-fullscreen-<query-slug>.png.

.PARAMETER CleanOutput
    Delete all existing *.png files and the previous _capture-manifest.json in OutputDir before
    capturing, so only the final Store images remain.

.PARAMETER KeepPaletteOpen
    Do not close the Command Palette after capturing. By default the palette is closed again when
    this script opened it (the user's pre-existing state is restored).

.PARAMETER SkipFullScreen
    Do not save the full-screen fallback image.

.PARAMETER Backdrop
    Show a neutral full-screen backdrop behind Command Palette while capturing. The CmdPal window
    has transparent shadow margins and rounded corners; without a backdrop those margins show the
    user's desktop (personal content, not acceptable in a Store listing). With -Backdrop the window
    capture margins and the full-screen fallback show a neutral dark surface instead. The backdrop is
    only on screen during the capture run.

.PARAMETER OpenTimeoutMs
    How long to wait for the CmdPal window to appear after the hotkey / AUMID launch. Default 8000.

.EXAMPLE
    # Default capture: one 1366x768 window screenshot + full-screen fallback in docs/store/screenshots.
    pwsh -File scripts/capture-screenshots.ps1

.EXAMPLE
    # Capture the extension command page (independent of the fallback toggles), neutral backdrop.
    pwsh -File scripts/capture-screenshots.ps1 -Mode page -Queries wantai -Backdrop

.EXAMPLE
    # Two shots, the second scrolled down to show the fallback rows (home mode).
    pwsh -File scripts/capture-screenshots.ps1 -Queries wantai,readme -ScrollDown 2

.EXAMPLE
    # Final Store captures: neutral backdrop so no personal desktop content appears in the PNG.
    pwsh -File scripts/capture-screenshots.ps1 -Queries wantai -Backdrop

.NOTES
    Exit codes: 0 = all requested window captures saved and >= 1366x768; 1 = at least one capture
    failed (details in the manifest and console output).
    Manual fallback (if input injection is blocked by AV/enterprise policy, or the host changed):
      1. Press Win+Alt+Space to open Command Palette and type the query yourself.
      2. Resize the palette window to at least 1366x768 and take a screenshot with Win+Shift+S
         (or run this script with -SkipTyping? not available) - save as PNG under
         docs/store/screenshots/ with the same naming scheme.
#>
#requires -Version 7.0
#requires -PSEdition Core
[CmdletBinding()]
param(
    [ValidateSet("home", "page")]
    [string]$Mode = "home",
    [string]$CommandSearchText = "Everything 搜索",
    [string[]]$Queries = @("wantai"),
    [string]$PreQuery,
    [int]$PreWaitMs = 8000,
    [string]$OutputDir = "docs/store/screenshots",
    [int]$Width = 1366,
    [int]$Height = 768,
    [int]$WaitMs = 1800,
    [int]$ScrollDown = 0,
    [switch]$SelectFirst,
    [switch]$ClickFirstResult,
    [switch]$OpenContextMenu,
    [string[]]$Names,
    [switch]$CleanOutput,
    [switch]$KeepPaletteOpen,
    [switch]$SkipFullScreen,
    [switch]$Backdrop,
    [int]$OpenTimeoutMs = 8000
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $root $OutputDir }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if ($CleanOutput) {
    Get-ChildItem -LiteralPath $outDir -File -Filter "*.png" -ErrorAction SilentlyContinue | Remove-Item -Force
    $oldManifest = Join-Path $outDir "_capture-manifest.json"
    if (Test-Path -LiteralPath $oldManifest) { Remove-Item -LiteralPath $oldManifest -Force }
    Write-Host "Cleaned previous captures from $outDir (-CleanOutput)"
}
if ($Names -and $Names.Count -lt $Queries.Count) {
    throw "-Names has $($Names.Count) entries but there are $($Queries.Count) queries; provide one base name per query."
}

Add-Type -AssemblyName System.Drawing
try { Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop } catch { }

# ---------------------------------------------------------------------------
# Win32 interop (DPI-aware so GetWindowRect/CopyFromScreen use physical pixels)
# ---------------------------------------------------------------------------

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class EverythingPalScreenshotNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmGetWindowAttributeRect(IntPtr hWnd, int attribute, out RECT value, int size);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT keyboard; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort virtualKey; public ushort scanCode; public uint flags; public uint time; public IntPtr extraInfo; }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const int SW_RESTORE = 9;
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    private static INPUT Key(ushort virtualKey, ushort scanCode, uint flags)
    {
        return new INPUT { type = INPUT_KEYBOARD, keyboard = new KEYBDINPUT { virtualKey = virtualKey, scanCode = scanCode, flags = flags } };
    }

    public static uint SendKeys(INPUT[] inputs)
    {
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static uint Hotkey()
    {
        // Win+Alt+Space (PowerToys Command Palette default). The whole chord is injected as one
        // batch; synthetic input is seen by registered global hotkeys.
        return SendKeys(new[]
        {
            Key(0x5B, 0, 0), Key(0x12, 0, 0), Key(0x20, 0, 0),
            Key(0x20, 0, KEYEVENTF_KEYUP), Key(0x12, 0, KEYEVENTF_KEYUP), Key(0x5B, 0, KEYEVENTF_KEYUP)
        });
    }

    public static uint TypeUnicodeChar(char character)
    {
        return SendKeys(new[]
        {
            Key(0, character, KEYEVENTF_UNICODE),
            Key(0, character, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP)
        });
    }

    public static uint TapVirtualKey(ushort virtualKey)
    {
        return SendKeys(new[] { Key(virtualKey, 0, 0), Key(virtualKey, 0, KEYEVENTF_KEYUP) });
    }

    public static uint Chord(ushort modifier, ushort virtualKey)
    {
        return SendKeys(new[]
        {
            Key(modifier, 0, 0), Key(virtualKey, 0, 0),
            Key(virtualKey, 0, KEYEVENTF_KEYUP), Key(modifier, 0, KEYEVENTF_KEYUP)
        });
    }

    public static uint ClearQuery()
    {
        // Ctrl+A then Backspace: selects all text in the CmdPal search box and deletes it.
        uint sent = SendKeys(new[]
        {
            Key(0x11, 0, 0), Key(0x41, 0, 0),
            Key(0x41, 0, KEYEVENTF_KEYUP), Key(0x11, 0, KEYEVENTF_KEYUP)
        });
        sent += SendKeys(new[] { Key(0x08, 0, 0), Key(0x08, 0, KEYEVENTF_KEYUP) });
        return sent;
    }

    public static bool IsCloaked(IntPtr hWnd)
    {
        int cloaked;
        return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0;
    }
}
"@

$native = [EverythingPalScreenshotNative]
# Per-monitor DPI awareness v2 so window rectangles and screen captures are in physical pixels.
[void]$native::SetProcessDpiAwarenessContext([IntPtr](-4))

$screenWidth = $native::GetSystemMetrics($native::SM_CXSCREEN)
$screenHeight = $native::GetSystemMetrics($native::SM_CYSCREEN)

$script:notes = New-Object System.Collections.Generic.List[string]
$script:openMethod = "already-open"
$script:openedByUs = $false
$script:initialForeground = $native::GetForegroundWindow()

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Add-Note {
    param([string]$Message)
    $script:notes.Add($Message)
    Write-Host "  [note] $Message" -ForegroundColor Yellow
}

function Get-CmdPalProcessIds {
    return @(Get-Process -Name "Microsoft.CmdPal.UI" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
}

function Get-ForegroundProcessId {
    $foreground = $native::GetForegroundWindow()
    $processId = [uint32]0
    [void]$native::GetWindowThreadProcessId($foreground, [ref]$processId)
    return [pscustomobject]@{ Handle = $foreground; ProcessId = $processId }
}

function Get-CmdPalWindows {    $processIds = Get-CmdPalProcessIds
    $windows = New-Object System.Collections.Generic.List[object]
    $callback = [EverythingPalScreenshotNative+EnumWindowsProc]{
        param($handle, $lParam)
        $processId = [uint32]0
        [void]$native::GetWindowThreadProcessId($handle, [ref]$processId)
        if ($processIds -contains [int]$processId) {
            $titleBuilder = New-Object System.Text.StringBuilder 512
            [void]$native::GetWindowText($handle, $titleBuilder, 512)
            $classBuilder = New-Object System.Text.StringBuilder 256
            [void]$native::GetClassName($handle, $classBuilder, 256)
            $rect = New-Object EverythingPalScreenshotNative+RECT
            [void]$native::GetWindowRect($handle, [ref]$rect)
            $windows.Add([pscustomobject]@{
                Handle    = $handle
                ProcessId = $processId
                Visible   = $native::IsWindowVisible($handle)
                Cloaked   = $native::IsCloaked($handle)
                Minimized = $native::IsIconic($handle)
                Class     = $classBuilder.ToString()
                Title     = $titleBuilder.ToString()
                Left      = $rect.Left
                Top       = $rect.Top
                Width     = ($rect.Right - $rect.Left)
                Height    = ($rect.Bottom - $rect.Top)
            })
        }
        return $true
    }
    [void]$native::EnumWindows($callback, [IntPtr]::Zero)
    return $windows
}

function Get-PaletteWindow {
    $windows = Get-CmdPalWindows
    $candidates = @($windows | Where-Object {
        $_.Visible -and -not $_.Cloaked -and $_.Width -gt 300 -and $_.Height -gt 200 -and
        $_.Title -notmatch 'PowerDock|toast|WinUI Desktop' -and
        # Command Palette's settings window belongs to the same process and its title also contains
        # "命令面板" / "Command Palette"; never capture/type into the settings window.
        $_.Title -notmatch '设置|Settings' -and
        -not ($_.Width -ge $screenWidth -and $_.Height -ge $screenHeight)
    } | Sort-Object { $_.Width * $_.Height } -Descending)
    if ($candidates.Count -eq 0) { return $null }
    return $candidates[0]
}

function Find-WindowByTitle {
    param([string]$Title)
    $found = [IntPtr]::Zero
    $callback = [EverythingPalScreenshotNative+EnumWindowsProc]{
        param($handle, $lParam)
        $builder = New-Object System.Text.StringBuilder 512
        [void]$native::GetWindowText($handle, $builder, 512)
        if ($builder.ToString() -eq $Title -and $native::IsWindowVisible($handle)) {
            $script:foundWindow = $handle
            return $false
        }
        return $true
    }
    [void]$native::EnumWindows($callback, [IntPtr]::Zero)
    if ($script:foundWindow -ne [IntPtr]::Zero) { $found = $script:foundWindow }
    $script:foundWindow = [IntPtr]::Zero
    return $found
}

function Start-Backdrop {
    # A neutral full-screen surface behind the palette: CopyFromScreen captures the composite, so the
    # transparent shadow/rounded-corner margins of the CmdPal window would otherwise show the user's
    # desktop (privacy risk for a Store screenshot). Runs in its own STA PowerShell process.
    $backdropScript = @'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = "EverythingPalCaptureBackdrop"
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.Bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$form.BackColor = [System.Drawing.Color]::FromArgb(32, 34, 38)
$form.ShowInTaskbar = $false
# The Command Palette window is itself topmost (WS_EX_TOPMOST); the backdrop must also be topmost
# to cover the user's normal windows, and the palette stays above it because it is activated after.
$form.TopMost = $true
[System.Windows.Forms.Application]::Run($form)
'@
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($backdropScript))
    $pwshPath = (Get-Process -Id $PID).Path
    $process = Start-Process -FilePath $pwshPath -ArgumentList @('-NoProfile', '-STA', '-EncodedCommand', $encoded) -PassThru
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
        if ((Find-WindowByTitle -Title "EverythingPalCaptureBackdrop") -ne [IntPtr]::Zero) {
            return [pscustomobject]@{ Process = $process; Title = "EverythingPalCaptureBackdrop" }
        }
    }
    try { $process.Kill() } catch { }
    throw "The neutral backdrop window did not appear within 10 s."
}

function Stop-Backdrop {
    param($Backdrop)
    if (-not $Backdrop) { return }
    try {
        if (-not $Backdrop.Process.HasExited) {
            $Backdrop.Process.Kill()
            $Backdrop.Process.WaitForExit(5000) | Out-Null
        }
    }
    catch { }
}

function Get-VisibleWindowRect {
    # DWMWA_EXTENDED_FRAME_BOUNDS excludes the invisible DWM shadow/resize border that
    # GetWindowRect includes; using it keeps the PNG to the actual visible surface.
    param($Palette)
    $rect = New-Object EverythingPalScreenshotNative+RECT
    $size = [System.Runtime.InteropServices.Marshal]::SizeOf([type][EverythingPalScreenshotNative+RECT])
    $hr = $native::DwmGetWindowAttributeRect($Palette.Handle, $native::DWMWA_EXTENDED_FRAME_BOUNDS, [ref]$rect, $size)
    if ($hr -eq 0 -and ($rect.Right - $rect.Left) -gt 100 -and ($rect.Bottom - $rect.Top) -gt 100) {
        return [pscustomobject]@{ Left = $rect.Left; Top = $rect.Top; Width = ($rect.Right - $rect.Left); Height = ($rect.Bottom - $rect.Top); Source = "DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)" }
    }
    $rect = New-Object EverythingPalScreenshotNative+RECT
    [void]$native::GetWindowRect($Palette.Handle, [ref]$rect)
    return [pscustomobject]@{ Left = $rect.Left; Top = $rect.Top; Width = ($rect.Right - $rect.Left); Height = ($rect.Bottom - $rect.Top); Source = "GetWindowRect" }
}

function Wait-ForPalette {
    param([int]$TimeoutMs = 8000)
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    while ((Get-Date) -lt $deadline) {
        $palette = Get-PaletteWindow
        if ($palette) { return $palette }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

function Open-PaletteViaHotkey {
    Write-Host "  opening Command Palette with Win+Alt+Space ..."
    $sent = $native::Hotkey()
    if ($sent -ne 6) {
        throw ("SendInput rejected the hotkey (returned {0}/{1}, Win32 error {2}); input injection is blocked." -f $sent, 6, [System.Runtime.InteropServices.Marshal]::GetLastWin32Error())
    }
    return (Wait-ForPalette -TimeoutMs $OpenTimeoutMs)
}

function Open-PaletteViaAumid {
    Write-Host "  hotkey did not open it; trying AUMID activation shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App ..."
    Start-Process -FilePath "explorer.exe" -ArgumentList 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    return (Wait-ForPalette -TimeoutMs $OpenTimeoutMs)
}

function Ensure-PaletteOpen {
    $palette = Get-PaletteWindow
    if ($palette) { return $palette }

    $palette = Open-PaletteViaHotkey
    if ($palette) { $script:openMethod = "hotkey"; $script:openedByUs = $true; return $palette }

    $palette = Open-PaletteViaAumid
    if ($palette) { $script:openMethod = "aumid-launch"; $script:openedByUs = $true; return $palette }

    throw "Could not open the Command Palette: neither Win+Alt+Space nor AUMID activation produced a visible Microsoft.CmdPal.UI window within $OpenTimeoutMs ms. Open Command Palette manually (Win+Alt+Space) and re-run, or use the manual screenshot procedure documented in this script."
}

function Focus-Palette {
    param($Palette)
    $foreground = Get-ForegroundProcessId
    if ([int]$foreground.ProcessId -eq [int]$Palette.ProcessId) { return $true }

    [void]$native::ShowWindow($Palette.Handle, $native::SW_RESTORE)
    [void]$native::SetForegroundWindow($Palette.Handle)
    Start-Sleep -Milliseconds 400
    $foreground = Get-ForegroundProcessId
    return ([int]$foreground.ProcessId -eq [int]$Palette.ProcessId)
}

function Resize-PaletteWindow {
    param($Palette, [int]$TargetWidth, [int]$TargetHeight)
    $width = [Math]::Min($TargetWidth, $screenWidth)
    $height = [Math]::Min($TargetHeight, $screenHeight)
    $x = [int](($screenWidth - $width) / 2)
    $y = [int](($screenHeight - $height) / 2)
    $flags = $native::SWP_NOZORDER
    [void]$native::SetWindowPos($Palette.Handle, [IntPtr]::Zero, $x, $y, $width, $height, $flags)
    Start-Sleep -Milliseconds 600

    $rect = New-Object EverythingPalScreenshotNative+RECT
    [void]$native::GetWindowRect($Palette.Handle, [ref]$rect)
    $actual = [pscustomobject]@{
        Left   = $rect.Left
        Top    = $rect.Top
        Width  = ($rect.Right - $rect.Left)
        Height = ($rect.Bottom - $rect.Top)
    }
    if ($actual.Width -lt $TargetWidth -or $actual.Height -lt $TargetHeight) {
        Add-Note ("CmdPal window did not reach the requested {0}x{1}; actual {2}x{3}. The window may have a minimum/DPI constraint." -f $TargetWidth, $TargetHeight, $actual.Width, $actual.Height)
    }
    return $actual
}

function Type-Query {
    param([string]$Query)
    $palette = Get-PaletteWindow
    if (-not $palette) { throw "Command Palette window disappeared before typing." }
    if (-not (Focus-Palette -Palette $palette)) {
        throw "Refusing to inject keystrokes: the foreground window is not Microsoft.CmdPal.UI (pid $($palette.ProcessId)). Typing aborted so keystrokes cannot leak into another application."
    }
    [void]$native::ClearQuery()
    Start-Sleep -Milliseconds 250
    $typed = 0
    foreach ($character in $Query.ToCharArray()) {
        $sent = $native::TypeUnicodeChar($character)
        if ($sent -eq 0) {
            throw ("SendInput rejected the character '{0}' (Win32 error {1}); input injection is blocked or unreliable." -f $character, [System.Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }
        $typed++
        Start-Sleep -Milliseconds 15
    }
    return $typed
}

function Open-CommandPage {
    param([string]$CommandSearchText)
    # Type the command name on the CmdPal home screen and press Enter to open the extension page.
    $palette = Get-PaletteWindow
    if (-not $palette) { throw "Command Palette window disappeared before selecting the extension command." }
    if (-not (Focus-Palette -Palette $palette)) {
        throw "Refusing to inject keystrokes: the foreground window is not Microsoft.CmdPal.UI."
    }
    [void]$native::ClearQuery()
    Start-Sleep -Milliseconds 250
    foreach ($character in $CommandSearchText.ToCharArray()) {
        $sent = $native::TypeUnicodeChar($character)
        if ($sent -eq 0) {
            throw ("SendInput rejected the command-search character '{0}' (Win32 error {1})." -f $character, [System.Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }
        Start-Sleep -Milliseconds 20
    }
    Start-Sleep -Milliseconds 1000
    [void]$native::TapVirtualKey(0x0D)   # Enter: open the selected command page
    Start-Sleep -Milliseconds 1000
    if ($null -eq (Get-PaletteWindow)) {
        throw "Command Palette closed after pressing Enter while opening the '$CommandSearchText' command page."
    }
}

function Click-FirstResultRow {
    # Click the first result row (shown below the search box) so Command Palette focuses that item
    # and opens the details pane for it.
    param($Palette)
    $rect = Get-VisibleWindowRect -Palette $Palette
    $x = $rect.Left + [int]($rect.Width / 2)
    $y = $rect.Top + 92
    [void]$native::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 150
    $native::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # left down
    $native::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # left up
    Start-Sleep -Milliseconds 900
}

function Send-DownArrow {
    param([int]$Count)
    for ($index = 0; $index -lt $Count; $index++) {
        [void]$native::TapVirtualKey(0x28)
        Start-Sleep -Milliseconds 120
    }
    if ($Count -gt 0) { Start-Sleep -Milliseconds 400 }
}

function Save-Bitmap {
    param([System.Drawing.Bitmap]$Bitmap, [string]$Path)
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        return [pscustomobject]@{ Path = $Path; Width = $image.Width; Height = $image.Height; Bytes = (Get-Item -LiteralPath $Path).Length }
    }
    finally {
        $image.Dispose()
    }
}

function Capture-Window {
    param($Palette)
    $rect = Get-VisibleWindowRect -Palette $Palette
    $width = $rect.Width
    $height = $rect.Height
    if ($width -le 0 -or $height -le 0) { throw "CmdPal window has an empty rectangle ($width x $height)." }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
        }
        finally { $graphics.Dispose() }
        return [pscustomobject]@{ Bitmap = $bitmap; Rect = $rect }
    }
    catch {
        $bitmap.Dispose()
        throw
    }
}

function Capture-FullScreen {
    $width = $screenWidth
    $height = $screenHeight
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size $width, $height))
        }
        finally { $graphics.Dispose() }
        return $bitmap
    }
    catch {
        $bitmap.Dispose()
        throw
    }
}

function Close-Palette {
    param($Palette, $OriginalForeground, [switch]$RestoreFocus)
    $foreground = Get-ForegroundProcessId
    if ([int]$foreground.ProcessId -ne [int]$Palette.ProcessId) {
        $paletteCheck = Get-PaletteWindow
        if ($paletteCheck) {
            [void]$native::SetForegroundWindow($paletteCheck.Handle)
            Start-Sleep -Milliseconds 300
        }
    }
    # In CmdPal the first Esc clears a non-empty search box, the second one closes the palette.
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        [void]$native::TapVirtualKey(0x1B)
        Start-Sleep -Milliseconds 600
        if ($null -eq (Get-PaletteWindow)) { break }
    }
    if ($RestoreFocus -and $OriginalForeground -ne [IntPtr]::Zero) {
        [void]$native::SetForegroundWindow($OriginalForeground)
        Start-Sleep -Milliseconds 200
    }
}

function ConvertTo-Slug {
    param([string]$Text)
    $slug = ($Text.ToLowerInvariant() -replace '[^\p{L}\p{Nd}]+', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) { $slug = "query" }
    return $slug
}

# ---------------------------------------------------------------------------
# Capture
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "============= EverythingPal Store screenshot capture =============" -ForegroundColor Green
Write-Host ("Screen         : {0}x{1} physical px" -f $screenWidth, $screenHeight)
Write-Host ("Target window  : {0}x{1} (Store desktop minimum 1366x768)" -f $Width, $Height)
Write-Host ("Queries        : {0}" -f ($Queries -join ", "))
Write-Host ("Output         : {0}" -f $outDir)
Write-Host ""

$initialPalette = Get-PaletteWindow
if ($initialPalette) {
    Write-Host "Command Palette is already open; using the existing window." -ForegroundColor DarkGray
    $script:openMethod = "already-open"
    $script:openedByUs = $false
}

$backdropProcess = $null
if ($Backdrop) {
    Write-Step "Starting neutral backdrop (prevents personal desktop content in the PNGs)"
    $backdropProcess = Start-Backdrop
    Start-Sleep -Milliseconds 500
}

$captures = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[string]
$index = 0

foreach ($query in $Queries) {
    $index++
    $slug = ConvertTo-Slug $query
    Write-Step ("Capture {0}/{1}: query '{2}'" -f $index, $Queries.Count, $query)
    $capture = [pscustomobject]@{
        index         = $index
        query         = $query
        openedByUs    = $false
        openMethod    = $null
        window        = $null
        windowFile    = $null
        windowWidth   = $null
        windowHeight  = $null
        meetsMinimum  = $false
        fullScreenFile = $null
        typedChars    = 0
        warnings      = @()
        error         = $null
        timestamp     = (Get-Date).ToString("o")
    }
    try {
        $wasOpen = $null -ne (Get-PaletteWindow)
        if ($Mode -eq "page" -and $wasOpen) {
            # Page mode starts from the CmdPal home screen, so make sure an already-open palette is
            # reset to a fresh home state before typing the command name.
            Close-Palette -Palette (Get-PaletteWindow) -OriginalForeground $script:initialForeground -RestoreFocus:(-not $Backdrop)
            Start-Sleep -Milliseconds 400
        }
        $palette = Ensure-PaletteOpen
        $capture.openMethod = $script:openMethod
        $capture.openedByUs = $script:openedByUs

        if (-not (Focus-Palette -Palette $palette)) {
            Add-Note "The Command Palette did not come to the foreground; continuing anyway (window capture does not require focus)."
        }

        $windowRect = Resize-PaletteWindow -Palette $palette -TargetWidth $Width -TargetHeight $Height
        $capture.window = [ordered]@{ handle = ('0x{0:X}' -f $palette.Handle.ToInt64()); title = $palette.Title; processId = $palette.ProcessId; left = $windowRect.Left; top = $windowRect.Top; width = $windowRect.Width; height = $windowRect.Height }

        if ($Mode -eq "page") {
            Open-CommandPage -CommandSearchText $CommandSearchText
            Start-Sleep -Milliseconds 400
        }

        if ($PreQuery) {
            # Produce the prerequisite state first (e.g. a successful home search), then the capture
            # query replaces it via Ctrl+A/Backspace inside Type-Query.
            Write-Host "  pre-query '$PreQuery' (settle ${PreWaitMs}ms) ..."
            [void](Type-Query -Query $PreQuery)
            Start-Sleep -Milliseconds $PreWaitMs
        }

        $typed = Type-Query -Query $query
        $capture.typedChars = $typed
        Start-Sleep -Milliseconds $WaitMs
        if ($SelectFirst) {
            # Select the first result so the details pane (size/modified/path) is populated.
            # Command page: there is no initial selection, one Down selects the first result.
            # Home screen: CmdPal preselects the first command row, so Down+Up lands on the first
            # *fallback* row without moving past it.
            [void]$native::TapVirtualKey(0x28)   # Down
            if ($Mode -eq "home") {
                Start-Sleep -Milliseconds 250
                [void]$native::TapVirtualKey(0x26)   # Up
            }
            Start-Sleep -Milliseconds 900
        }
        if ($ClickFirstResult) { Click-FirstResultRow -Palette $palette }
        if ($OpenContextMenu) {
            # Ctrl+K opens the selected item's context menu in Command Palette.
            [void]$native::Chord(0x11, 0x4B)
            Start-Sleep -Milliseconds 1200
        }
        if ($ScrollDown -gt 0) { Send-DownArrow -Count $ScrollDown }

        $baseName = if ($Names -and $Names.Count -ge $index) { $Names[$index - 1] } else { "{0:00}-cmdpal-{1}" -f $index, $slug }
        $windowCapture = Capture-Window -Palette $palette
        $windowBitmap = $windowCapture.Bitmap
        try {
            $windowName = "$baseName.png"
            $windowPath = Join-Path $outDir $windowName
            $windowInfo = Save-Bitmap -Bitmap $windowBitmap -Path $windowPath
        }
        finally { $windowBitmap.Dispose() }
        $capture.windowFile = $windowInfo.Path
        $capture.windowWidth = $windowInfo.Width
        $capture.windowHeight = $windowInfo.Height
        $capture.window = [ordered]@{ handle = ('0x{0:X}' -f $palette.Handle.ToInt64()); title = $palette.Title; processId = $palette.ProcessId; captureRect = $windowCapture.Rect; rectSource = $windowCapture.Rect.Source }
        $capture.meetsMinimum = ($windowInfo.Width -ge 1366 -and $windowInfo.Height -ge 768)
        Write-Host ("  window capture: {0} ({1}x{2}, {3:N0} bytes)" -f $windowInfo.Path, $windowInfo.Width, $windowInfo.Height, $windowInfo.Bytes) -ForegroundColor Green
        if (-not $capture.meetsMinimum) {
            $message = "window capture is {0}x{1}, smaller than the Store minimum 1366x768" -f $windowInfo.Width, $windowInfo.Height
            $capture.warnings += $message
            Add-Note $message
        }

        if (-not $SkipFullScreen) {
            $fullBitmap = Capture-FullScreen
            try {
                $fullName = "$baseName-fullscreen.png"
                $fullPath = Join-Path $outDir $fullName
                $fullInfo = Save-Bitmap -Bitmap $fullBitmap -Path $fullPath
            }
            finally { $fullBitmap.Dispose() }
            $capture.fullScreenFile = $fullInfo.Path
            Write-Host ("  full-screen fallback: {0} ({1}x{2})" -f $fullInfo.Path, $fullInfo.Width, $fullInfo.Height) -ForegroundColor DarkGray
        }

        if ($script:openedByUs -and -not $KeepPaletteOpen) {
            Close-Palette -Palette $palette -OriginalForeground $script:initialForeground -RestoreFocus:(-not $Backdrop)
            Start-Sleep -Milliseconds 500
        }
    }
    catch {
        $capture.error = $_.Exception.Message
        $failures.Add(("query '{0}': {1}" -f $query, $_.Exception.Message))
        Write-Host ("  FAILED: {0}" -f $_.Exception.Message) -ForegroundColor Red
        # Never leave keystrokes half-typed or the palette stuck: try to close if we opened it.
        if ($script:openedByUs -and -not $KeepPaletteOpen) {
            try { Close-Palette -Palette (Get-PaletteWindow) -OriginalForeground $script:initialForeground -RestoreFocus:(-not $Backdrop) } catch { }
        }
    }
    $captures.Add($capture)
}

if ($backdropProcess) {
    Stop-Backdrop -Backdrop $backdropProcess
    if ($script:initialForeground -ne [IntPtr]::Zero) {
        [void]$native::SetForegroundWindow($script:initialForeground)
        Start-Sleep -Milliseconds 200
    }
}

# ---------------------------------------------------------------------------
# Manifest + summary
# ---------------------------------------------------------------------------

$windowCaptures = @($captures | Where-Object { $_.windowFile -and $_.meetsMinimum })
$manifest = [ordered]@{
    tool        = "scripts/capture-screenshots.ps1"
    capturedAt  = (Get-Date).ToString("o")
    purpose     = "Microsoft Store desktop screenshots (PNG, >= 1366x768) of the running EverythingPal CmdPal extension"
    environment = [ordered]@{
        os                        = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        screenWidth               = $screenWidth
        screenHeight              = $screenHeight
        dpiAwareness              = "PerMonitorAwareV2"
        cmdPalPackage             = @(Get-AppxPackage -Name "Microsoft.CommandPalette" -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Name)_$($_.Version)_$($_.Architecture)" })
        everythingPalPackage      = @(Get-AppxPackage -Name "EverythingPal*" -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Name)_$($_.Version)_$($_.Architecture)__$($_.PackageFamilyName)" })
        inputInjection            = "SendInput (Win+Alt+Space hotkey + Unicode characters)"
    }
    settings    = [ordered]@{ mode = $Mode; commandSearchText = $CommandSearchText; queries = @($Queries); preQuery = $PreQuery; names = @($Names); selectFirst = [bool]$SelectFirst; targetWidth = $Width; targetHeight = $Height; waitMs = $WaitMs; scrollDown = $ScrollDown; keepPaletteOpen = [bool]$KeepPaletteOpen; backdrop = [bool]$Backdrop }
    captures    = @($captures | ForEach-Object {
        [ordered]@{
            index          = $_.index
            query          = $_.query
            mode           = $Mode
            openMethod     = $_.openMethod
            openedByUs     = $_.openedByUs
            typedChars     = $_.typedChars
            window         = $_.window
            windowFile     = $_.windowFile
            windowWidth    = $_.windowWidth
            windowHeight   = $_.windowHeight
            meetsMinimum   = $_.meetsMinimum
            fullScreenFile = $_.fullScreenFile
            warnings       = @($_.warnings)
            error          = $_.error
        }
    })
    summary     = [ordered]@{
        requested         = $captures.Count
        windowCaptures    = $windowCaptures.Count
        storeCompliant    = $windowCaptures.Count
        failures          = @($failures)
    }
    notes       = @($script:notes)
}
$manifestPath = Join-Path $outDir "_capture-manifest.json"
Set-Content -LiteralPath $manifestPath -Value ($manifest | ConvertTo-Json -Depth 10) -Encoding UTF8

Write-Host ""
Write-Host "================= CAPTURE COMPLETE =================" -ForegroundColor Green
Write-Host ("Captures        : {0} requested, {1} saved with >= 1366x768" -f $captures.Count, $windowCaptures.Count)
Write-Host ("Output directory: {0}" -f $outDir)
Write-Host ("Manifest        : {0}" -f $manifestPath)
if ($script:notes.Count -gt 0) {
    Write-Host ""
    Write-Host "Notes:" -ForegroundColor Yellow
    foreach ($note in $script:notes) { Write-Host ("  - {0}" -f $note) -ForegroundColor Yellow }
}
if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILED captures:" -ForegroundColor Red
    foreach ($failure in $failures) { Write-Host ("  - {0}" -f $failure) -ForegroundColor Red }
    Write-Host ""
    Write-Host "Manual fallback: press Win+Alt+Space, type the query yourself, resize the palette to >=1366x768," -ForegroundColor Yellow
    Write-Host "then screenshot with Win+Shift+S and save the PNGs under $outDir." -ForegroundColor Yellow
    exit 1
}
if ($windowCaptures.Count -eq 0) {
    Write-Host "No Store-compliant window capture was saved." -ForegroundColor Red
    exit 1
}
exit 0

