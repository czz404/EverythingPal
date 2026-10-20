# Build + package + sign + install the extension (per-user sideload).
#
# The dev loop is:
#   1. dotnet publish: SELF-CONTAINED Release/Debug build, same MSIX flags as scripts/pack-store.ps1
#      (the .NET runtime is inside the package, so this is the exact payload shape the Store build uses)
#   2. assemble the MSIX layout ourselves + inject the concrete architecture (PDBs stripped)
#   3. makeappx pack  (tool from the installed Windows SDK)
#   4. signtool sign  (self-signed dev cert; auto-created and trusted for the current user)
#   5. Add-AppxPackage the signed .msix
#
# Why not Add-AppxPackage -Register with a loose layout? A packaged COM server registered that way
# fails activation with CO_E_WRONG_SERVER_IDENTITY (0x80004015) inside CoResumeClassObjects.
# A properly signed + installed package does not have that problem.
#
# Usage:
#   pwsh -File scripts/deploy.ps1                 # self-contained publish + package + install
#   pwsh -File scripts/deploy.ps1 -RestartCmdPal  # also restart Command Palette
#   pwsh -File scripts/deploy.ps1 -Configuration Release   # exactly the Store-style payload config
#   pwsh -File scripts/deploy.ps1 -SkipBuild      # reinstall the existing self-contained publish output
#   pwsh -File scripts/deploy.ps1 -Untrimmed      # disable IL trimming (debugging trim issues)
#
# Stale state: unless -SkipBuild is passed, artifacts/publish/<Platform>, artifacts/layout and
# artifacts/EverythingPal.msix are deleted and re-created, so the installed package always comes from
# the current source tree + freshly published (self-contained) output.
param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [switch]$SkipBuild,
    [switch]$Untrimmed,
    [switch]$RestartCmdPal
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\EverythingPal\EverythingPal.csproj"
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\$Platform"
$layout = Join-Path $artifacts "layout"
$msix = Join-Path $artifacts "EverythingPal.msix"
$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-$Platform" }
$certSubject = "CN=czz404"

if (-not $SkipBuild) {
    Write-Host "Publishing self-contained ($Configuration/$Platform)..." -ForegroundColor Cyan
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $publish | Out-Null
    # Same flags as scripts/pack-store.ps1: EnableMsixTooling=false avoids MSB3030 (missing .pri),
    # PublishProfile= avoids NETSDK1198, trimming keeps the payload near the Store build size.
    $trimValue = if ($Untrimmed) { "false" } else { "true" }
    dotnet publish $project -c $Configuration -r $rid --self-contained true `
        "-p:Platform=$Platform" "-p:WindowsPackageType=None" `
        "-p:EnableMsixPackaging=false" "-p:EnableMsixTooling=false" `
        "-p:PublishProfile=" "-p:PublishTrimmed=$trimValue" -o $publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path $publish)) { throw "publish output not found: $publish (run without -SkipBuild)" }
if (-not (Test-Path (Join-Path $publish "hostfxr.dll")) -or -not (Test-Path (Join-Path $publish "coreclr.dll"))) {
    throw "publish output is not self-contained (hostfxr.dll/coreclr.dll missing): $publish"
}
Write-Host ("Using publish output: {0} (published {1:HH:mm:ss})" -f $publish, (Get-Item (Join-Path $publish "EverythingPal.exe")).LastWriteTime) -ForegroundColor DarkGray

Write-Host "Assembling layout..." -ForegroundColor Cyan
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout | Out-Null
Copy-Item (Join-Path $publish '*') $layout -Recurse -Force
# Debug symbols are not needed in an installed package and would bloat it.
Get-ChildItem $layout -Recurse -Filter "*.pdb" -File | Remove-Item -Force
$manifest = Join-Path $layout 'AppxManifest.xml'
Copy-Item (Join-Path $root 'src\EverythingPal\Package.appxmanifest') $manifest -Force

# A signed package manifest must carry the concrete architecture.
$manifestText = Get-Content $manifest -Raw
if ($manifestText -notmatch 'ProcessorArchitecture=') {
    $archToken = if ($Platform -eq "ARM64") { "arm64" } else { $Platform }
    $manifestText = $manifestText -replace '(<Identity\s+)', "`$1ProcessorArchitecture=`"$archToken`" "
    Set-Content -Path $manifest -Value $manifestText -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Bundle es.exe so users only need Everything itself (ES is MIT licensed, see THIRD-PARTY-NOTICES.md).
# ---------------------------------------------------------------------------
$esCache = Join-Path $artifacts "es\$Platform"
$esExe = Join-Path $esCache "es.exe"
if (-not (Test-Path $esExe)) {
    Write-Host "Downloading latest ES ($Platform) from github.com/voidtools/ES..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $esCache | Out-Null
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/voidtools/ES/releases/latest" `
        -Headers @{ "User-Agent" = "EverythingPal-build" } -TimeoutSec 120
    $suffix = if ($Platform -eq "ARM64") { "ARM64.zip" } else { "x64.zip" }
    $asset = $release.assets | Where-Object { $_.name -like "*.$suffix" } | Select-Object -First 1
    if (-not $asset) { throw "No ES asset '*.$suffix' in release $($release.tag_name)" }

    $zip = Join-Path $esCache "es.zip"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -TimeoutSec 300
    $extract = Join-Path $esCache "extract"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    $found = Get-ChildItem $extract -Recurse -Filter "es.exe" | Select-Object -First 1
    if (-not $found) { throw "es.exe not found in $($asset.name)" }
    Copy-Item $found.FullName $esExe -Force
    Write-Host "Bundled ES $($release.tag_name)" -ForegroundColor DarkGray
}

$toolsDir = Join-Path $layout "Tools"
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
Copy-Item $esExe (Join-Path $toolsDir "es.exe") -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $layout "THIRD-PARTY-NOTICES.md") -Force
Copy-Item (Join-Path $root "LICENSE") (Join-Path $layout "LICENSE") -Force

# ---------------------------------------------------------------------------
# Packaging tools (makeappx/signtool). Prefer the installed Windows SDK; the tools shipped in the
# SDK.BuildTools NuGet package can sign PE files but not MSIX (no AppX SIP), so they are a fallback.
# ---------------------------------------------------------------------------
function Get-PackagingTools {
    param([string]$ToolsRoot, [string]$Platform)

    $sdkTool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($sdkTool) { return $sdkTool.Directory }

    $existing = Get-ChildItem $ToolsRoot -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\$Platform\\" } | Select-Object -First 1
    if ($existing) { return $existing.Directory }

    New-Item -ItemType Directory -Force -Path $ToolsRoot | Out-Null
    $nupkg = Join-Path $ToolsRoot "buildtools.nupkg"
    if (-not (Test-Path $nupkg)) {
        Write-Host "Downloading Microsoft.Windows.SDK.BuildTools..." -ForegroundColor DarkGray
        Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/10.0.26100.4188/microsoft.windows.sdk.buildtools.10.0.26100.4188.nupkg" -OutFile $nupkg -TimeoutSec 300
    }
    $extract = Join-Path $ToolsRoot "buildtools"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $extract)

    $exe = Get-ChildItem $extract -Recurse -Filter makeappx.exe | Where-Object { $_.FullName -match "\\$Platform\\" } | Select-Object -First 1
    if (-not $exe) { throw "makeappx.exe not found in package" }
    return $exe.Directory
}

$toolDir = Get-PackagingTools -ToolsRoot (Join-Path $artifacts "tools") -Platform $Platform
$makeappx = Join-Path $toolDir "makeappx.exe"
$signtool = Join-Path $toolDir "signtool.exe"

Write-Host "Packing $msix..." -ForegroundColor Cyan
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msix)) { throw "makeappx pack failed" }

# ---------------------------------------------------------------------------
# Dev signing certificate: self-signed, trusted for the current user only.
# ---------------------------------------------------------------------------
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $certSubject -and $_.HasPrivateKey } | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating self-signed dev certificate ($certSubject)..." -ForegroundColor DarkGray
    $cert = New-SelfSignedCertificate -Type Custom -Subject $certSubject -KeyUsage DigitalSignature `
        -FriendlyName "EverythingPal dev certificate" -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyExportPolicy Exportable -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
}

$cer = Join-Path $artifacts "devcert.cer"
if (-not (Test-Path $cer)) {
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
}

# Per-user trusted store is not enough: sideloading validates the signature against the
# machine's TrustedPeople store, so trust the self-signed root machine-wide (one-time, needs admin).
$machineTrusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $machineTrusted) {
    Write-Host "Trusting dev certificate (LocalMachine\TrustedPeople, one-time, elevated)..." -ForegroundColor DarkGray
    Start-Process -Verb RunAs -FilePath "$env:SystemRoot\System32\certutil.exe" `
        -ArgumentList @("-addstore", "-f", "TrustedPeople", $cer) -Wait -WindowStyle Hidden
    $machineTrusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
    if (-not $machineTrusted) {
        Write-Warning "Could not trust the certificate machine-wide; Add-AppxPackage will likely fail with 0x800B0109."
    }
}

Write-Host "Signing..." -ForegroundColor Cyan
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed (try deleting stale CN=czz404 certs and re-running)" }

$manifestXml = [xml](Get-Content (Join-Path $layout 'AppxManifest.xml') -Raw)
$expectedVersion = [string]$manifestXml.Package.Identity.Version

if ($RestartCmdPal) {
    # Stop the host (and the extension COM server) first: they can hold the old package open and
    # CmdPal would otherwise keep serving the previously loaded extension build.
    Write-Host "Stopping Command Palette..." -ForegroundColor DarkGray
    Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 1000
}
Get-Process EverythingPal -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Installing..." -ForegroundColor Cyan
Get-AppxPackage EverythingPal | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path $msix

$installed = Get-AppxPackage EverythingPal | Select-Object -First 1
if (-not $installed) { throw "Installation did not register an EverythingPal package." }
Write-Host ("Installed: {0} (SignatureKind={1})" -f $installed.PackageFullName, $installed.SignatureKind) -ForegroundColor Green
if ($expectedVersion -and ([version]$installed.Version) -ne ([version]$expectedVersion)) {
    throw "Installed version $($installed.Version) does not match the freshly packed version $expectedVersion - stale package?"
}

if ($RestartCmdPal) {
    Write-Host "Starting Command Palette..." -ForegroundColor Cyan
    Start-Process "shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App"
    $deadline = (Get-Date).AddSeconds(20)
    $cmdPal = $null
    do {
        Start-Sleep -Milliseconds 500
        $cmdPal = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
    } while (-not $cmdPal -and (Get-Date) -lt $deadline)
    if ($cmdPal) {
        Start-Sleep -Seconds 2
        Write-Host ("Command Palette restarted (PID {0})." -f $cmdPal.Id) -ForegroundColor Green
    }
    else {
        Write-Warning "Command Palette process was not detected; start it manually (Win+Alt+Space or the PowerToys tray icon)."
    }
}

Write-Host "Next: in Command Palette run 'Reload' -> 'Reload Command Palette extensions'." -ForegroundColor Yellow

