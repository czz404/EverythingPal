# Build + package + sign + install the extension (per-user sideload).
#
# The dev loop is:
#   1. dotnet build (packaging targets disabled, no Windows SDK needed)
#   2. assemble the MSIX layout ourselves + inject the concrete architecture
#   3. makeappx pack  (tool downloaded from Microsoft.Windows.SDK.BuildTools NuGet)
#   4. signtool sign  (self-signed dev cert; auto-created and trusted for the current user)
#   5. Add-AppxPackage the signed .msix
#
# Why not Add-AppxPackage -Register with a loose layout? A packaged COM server registered that way
# fails activation with CO_E_WRONG_SERVER_IDENTITY (0x80004015) inside CoResumeClassObjects.
# A properly signed + installed package does not have that problem.
#
# Usage:
#   pwsh -File scripts/deploy.ps1                 # build Debug + package + install
#   pwsh -File scripts/deploy.ps1 -RestartCmdPal  # also restart Command Palette
param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [switch]$RestartCmdPal
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\EverythingPal\EverythingPal.csproj"
$artifacts = Join-Path $root "artifacts"
$layout = Join-Path $artifacts "layout"
$msix = Join-Path $artifacts "EverythingPal.msix"
$bin = Join-Path $root "src\EverythingPal\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\win-$Platform"
$certSubject = "CN=czz404"

Write-Host "Building ($Configuration/$Platform)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -p:Platform=$Platform -p:RuntimeIdentifier=win-$Platform -p:WindowsPackageType=None
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

if (-not (Test-Path $bin)) { throw "build output not found: $bin" }

Write-Host "Assembling layout..." -ForegroundColor Cyan
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout | Out-Null
Copy-Item (Join-Path $bin '*') $layout -Recurse -Force
$manifest = Join-Path $layout 'AppxManifest.xml'
Copy-Item (Join-Path $root 'src\EverythingPal\Package.appxmanifest') $manifest -Force

# A signed package manifest must carry the concrete architecture.
$manifestText = Get-Content $manifest -Raw
if ($manifestText -notmatch 'ProcessorArchitecture=') {
    $manifestText = $manifestText -replace '(<Identity\s+)', "`$1ProcessorArchitecture=`"$Platform`" "
    Set-Content -Path $manifest -Value $manifestText -Encoding UTF8
}

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

Write-Host "Installing..." -ForegroundColor Cyan
Get-AppxPackage EverythingPal | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path $msix

$installed = Get-AppxPackage EverythingPal
Write-Host ("Installed: {0} (SignatureKind={1})" -f $installed.PackageFullName, $installed.SignatureKind) -ForegroundColor Green

if ($RestartCmdPal) {
    Write-Host "Restarting Command Palette..." -ForegroundColor Cyan
    Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    Start-Process "shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App"
    Start-Sleep -Seconds 4
}

Write-Host "Next: in Command Palette run 'Reload' -> 'Reload Command Palette extensions'." -ForegroundColor Yellow
