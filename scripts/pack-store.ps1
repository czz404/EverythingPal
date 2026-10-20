<#
.SYNOPSIS
    Build an UNSIGNED x64 + ARM64 .msixbundle of the Everything Search Command Palette extension,
    ready to upload to Microsoft Partner Center, and validate the staged package identity/payload.

.DESCRIPTION
    Microsoft Store submission does NOT use the local dev identity (Identity/@Publisher = CN=czz404)
    and does NOT use a locally signed package: Partner Center reserves an identity of its own and the
    Store re-signs the uploaded packages. This script therefore:

      1. PUBLISHES every requested architecture in Release as SELF-CONTAINED, so the .NET runtime is
         bundled into the package and a clean Store/reviewer machine (or any user without the .NET 10
         runtime installed) can launch the extension. MSIX MSBuild packaging targets are disabled
         (-p:WindowsPackageType=None, same approach as scripts/deploy.ps1);
      2. stages a layout per architecture: publish output (PDB-stripped) + Package.appxmanifest +
         bundled MIT es.exe + THIRD-PARTY-NOTICES.md + LICENSE;
      3. injects ProcessorArchitecture and the package Version into the *staged* AppxManifest.xml,
         and (optionally) the Partner Center Identity/@Name, Identity/@Publisher and
         Properties/PublisherDisplayName. The source Package.appxmanifest is never modified here;
      4. packs one .msix per architecture with makeappx, then bundles them into
         EverythingPal_<version>_<archs>.msixbundle with `makeappx bundle`;
      5. validates the result: unpacks every .msix and the bundle, asserts the identity/version/
         architecture of each staged manifest, the self-contained runtime, the GDI+ icon assemblies
         (System.Drawing.Common / System.Private.Windows.GdiPlus), es.exe + THIRD-PARTY-NOTICES.md +
         LICENSE presence in both, both architectures present in the bundle, and that nothing is signed.

    The output of this script is the file to upload on Partner Center -> Packages. It is expected to
    be unsigned (Store re-signs). Use scripts/deploy.ps1 for the local signed sideload loop instead
    (it now publishes self-contained too, so it exercises the same payload shape).

    NOTE: a self-contained .NET publish is much bigger than a framework-dependent one, but IL
    trimming (on by default, -p:PublishTrimmed=true) keeps it around ~24 MB per architecture. Pass
    -Untrimmed if trimming breaks WinRT/COM activation. Debug symbols (*.pdb) are stripped before
    packing the MSIX.

.PARAMETER Version
    Package version, 4 sections ("x.y.z.0"). Defaults to Identity/@Version of
    src/EverythingPal/Package.appxmanifest. A 1-3 section value is normalized to 4 sections by
    appending ".0". The 4th section must stay 0 for Store builds (the Store reserves it) and every
    section is limited to 65535. Partner Center also requires a non-zero major section: a 0.x.y.0
    version is accepted here only for local/CI smoke packages (i.e. without -IdentityName/-Publisher);
    pass e.g. -Version 1.0.0.0 for a real Store submission.

.PARAMETER IdentityName
    Partner Center "Package/Identity/Name". Applied ONLY to the staged layout manifests. When omitted
    the source manifest value is used (fine for local/CI smoke tests, invalid for a Store submission).

.PARAMETER Publisher
    Partner Center "Package/Identity/Publisher", i.e. a "CN=<GUID>" string. Applied ONLY to the
    staged layout manifests. Must match Partner Center exactly or the upload is rejected.

.PARAMETER PublisherDisplayName
    Display publisher name shown in the Store and in CmdPal's extension "About" card. Applied ONLY to
    the staged layout manifests. Must match Partner Center "Properties/PublisherDisplayName".

.PARAMETER DisplayName
    Store product display name (Properties/DisplayName and the app VisualElements DisplayName).
    The Store rejects a package whose Properties/DisplayName is not one of your reserved app names,
    so pass the reserved Partner Center product name here (e.g. "EverythingPal"). Applied ONLY to the
    staged layout manifests; the source manifest keeps its friendlier dev name.

.PARAMETER Architectures
    Architectures to build and bundle. Default: x64,arm64. Accepts a subset (x64 / arm64).

.PARAMETER OutputDir
    Output directory (repo-relative or absolute). Default: artifacts/store. Contains the .msix per
    architecture, the .msixbundle, the staged layouts and validation evidence.

.PARAMETER EsRoot
    Cache root for es.exe. Default: artifacts/es. The script looks for <EsRoot>/<arch>/es.exe and
    otherwise downloads the latest official release from https://github.com/voidtools/ES.

.PARAMETER Configuration
    Build configuration. Default: Release (keep Release for Store packages).

.PARAMETER SkipBuild
    Reuse the existing publish output (publish-<arch> under -OutputDir) instead of running
    dotnet publish.

.PARAMETER Untrimmed
    Publish with -p:PublishTrimmed=false instead of the default -p:PublishTrimmed=true. Trimming
    gives a much smaller bundle but can break WinRT/COM activation (reflection); use this switch only
    if the trimmed build misbehaves, and validate whichever mode you ship.

.PARAMETER MakeAppxPath
    Explicit path to makeappx.exe. Default: newest x64 Windows SDK makeappx under
    "C:\Program Files (x86)\Windows Kits\10\bin", with a NuGet BuildTools fallback.

.EXAMPLE
    # Local smoke test with the source (dev) identity - NOT a valid Store package.
    pwsh -File scripts/pack-store.ps1

.EXAMPLE
    # Real Store submission package (values copied from Partner Center -> Product identity).
    # Partner Center requires a non-zero major version and a 0 fourth section.
    pwsh -File scripts/pack-store.ps1 `
        -Version 1.0.0.0 `
        -IdentityName "12345ABCDE6789.5F6E7D8C9B0A1F2E" `
        -Publisher "CN=01234567-89AB-CDEF-0123-456789ABCDEF" `
        -PublisherDisplayName "EverythingPal" `
        -DisplayName "EverythingPal"

.EXAMPLE
    # Re-pack the current Release build output without rebuilding (faster loop while tweaking manifests).
    pwsh -File scripts/pack-store.ps1 -SkipBuild

.NOTES
    Never committed: artifacts/ is gitignored. Do not sign the Store bundle - Partner Center re-signs.
    See docs/store/packaging.md for the full Partner Center workflow.
#>
#requires -Version 7.0
#requires -PSEdition Core
[CmdletBinding()]
param(
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$DisplayName,
    [string[]]$Architectures = @("x64", "arm64"),
    [string]$OutputDir = "artifacts/store",
    [string]$EsRoot = "artifacts/es",
    [string]$Configuration = "Release",
    [switch]$SkipBuild,
    [switch]$Untrimmed,
    [string]$MakeAppxPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\EverythingPal\EverythingPal.csproj"
$sourceManifestPath = Join-Path $root "src\EverythingPal\Package.appxmanifest"
$esReleaseApi = "https://api.github.com/repos/voidtools/ES/releases/latest"

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

function Resolve-RepoPath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $root $Path))
}

function Get-DotnetPath {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $fallback = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $fallback) { return $fallback }
    throw "dotnet not found. Install the .NET 10 SDK or add it to PATH (C:\Program Files\dotnet)."
}

function Get-ZipEntryNames {
    param([string]$Path)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try { return @($zip.Entries | ForEach-Object { $_.FullName }) }
    finally { $zip.Dispose() }
}

function Read-ZipTextEntry {
    param([string]$Path, [string]$EntryName)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.GetEntry($EntryName)
        if (-not $entry) { throw "archive entry not found: $EntryName in $Path" }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

function Assert-Equal {
    param([string]$Expected, [string]$Actual, [string]$What)
    if ($Expected -ne $Actual) {
        throw "$What mismatch: expected '$Expected', found '$Actual'."
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# Resolve / validate inputs
# ---------------------------------------------------------------------------

if (-not (Test-Path -LiteralPath $project)) { throw "project not found: $project" }
if (-not (Test-Path -LiteralPath $sourceManifestPath)) { throw "manifest not found: $sourceManifestPath" }

$outDir = Resolve-RepoPath $OutputDir
$esDir = Resolve-RepoPath $EsRoot

$sourceManifest = New-Object System.Xml.XmlDocument
$sourceManifest.PreserveWhitespace = $true
$sourceManifest.Load($sourceManifestPath)
$sourceIdentity = $sourceManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
if (-not $sourceIdentity) { throw "Identity element not found in $sourceManifestPath" }

$sourceVersion = $sourceIdentity.GetAttribute("Version")
if (-not $Version) { $Version = $sourceVersion }
if ($Version -notmatch '^\d+(\.\d+){0,3}$') {
    throw "Invalid -Version '$Version'. Use x.y.z.0 (four numeric sections, e.g. 0.2.0.0)."
}
$versionParts = @($Version.Split('.') | ForEach-Object { [int]$_ })
while ($versionParts.Count -lt 4) { $versionParts += 0 }
$Version = $versionParts -join '.'

# Partner Center: "The other sections must be set to an integer between 0 and 65535 (except for the
# first section, which cannot be 0)". The dev/CI package may still use 0.x (sideload does not care),
# but a real Store submission must start at 1.x.
$storeIdentityRequested = [bool]($IdentityName -or $Publisher)
if ($versionParts[0] -eq 0 -and $storeIdentityRequested) {
    throw "Invalid -Version '$Version' for a Store package: the major section cannot be 0 (Partner Center rule). Use e.g. 1.0.0.0 for the first Store submission and bump the 3rd section afterwards."
}
if ($versionParts[3] -ne 0) {
    throw "Invalid -Version '$Version': the 4th section must be 0 for Store packages (the Store reserves it). Use e.g. 0.2.0.0 and bump the 3rd section per submission."
}
foreach ($part in $versionParts) {
    if ($part -lt 0 -or $part -gt 65535) { throw "Invalid -Version '$Version': each section must be 0-65535." }
}

# What the staged manifests must end up with. Defaults come from the source manifest, so a local
# smoke run still works; only a Partner Center-reserved identity is accepted by the Store.
$expectedIdentityName = if ($IdentityName) { $IdentityName } else { $sourceIdentity.GetAttribute("Name") }
$expectedPublisher = if ($Publisher) { $Publisher } else { $sourceIdentity.GetAttribute("Publisher") }
$sourcePublisherDisplayNameNode = $sourceManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']/*[local-name()='PublisherDisplayName']")
if (-not $sourcePublisherDisplayNameNode) { throw "Properties/PublisherDisplayName not found in $sourceManifestPath" }
$expectedPublisherDisplayName = if ($PublisherDisplayName) { $PublisherDisplayName } else { $sourcePublisherDisplayNameNode.InnerText.Trim() }
$sourceDisplayNameNode = $sourceManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']/*[local-name()='DisplayName']")
if (-not $sourceDisplayNameNode) { throw "Properties/DisplayName not found in $sourceManifestPath" }
$expectedDisplayName = if ($DisplayName) { $DisplayName } else { $sourceDisplayNameNode.InnerText.Trim() }

# Architecture tokens: MSBuild platform names differ from the MSIX ProcessorArchitecture tokens.
$archDefinitions = @{
    "x64"   = @{ Token = "x64"; Platform = "x64"; Rid = "win-x64" }
    "arm64" = @{ Token = "arm64"; Platform = "ARM64"; Rid = "win-arm64" }
}
$selectedArchs = New-Object System.Collections.Generic.List[object]
foreach ($requested in $Architectures) {
    if ([string]::IsNullOrWhiteSpace($requested)) { continue }
    $key = $requested.Trim().ToLowerInvariant()
    if (-not $archDefinitions.ContainsKey($key)) {
        throw "Unsupported architecture '$requested'. Supported: x64, arm64."
    }
    $definition = $archDefinitions[$key]
    $alreadySelected = $false
    foreach ($existing in $selectedArchs) { if ($existing.Token -eq $definition.Token) { $alreadySelected = $true } }
    if (-not $alreadySelected) { $selectedArchs.Add($definition) }
}
if ($selectedArchs.Count -eq 0) { throw "No architectures selected (-Architectures x64,arm64)." }

$dotnet = Get-DotnetPath

Write-Host "Everything Pal - Store bundle packaging" -ForegroundColor Green
Write-Host ("  Repo            : {0}" -f $root)
Write-Host ("  Version         : {0}" -f $Version)
Write-Host ("  Architectures   : {0}" -f (($selectedArchs | ForEach-Object { $_.Token }) -join ', '))
Write-Host ("  Identity/Name   : {0}" -f $expectedIdentityName)
Write-Host ("  Identity/Publ.  : {0}" -f $expectedPublisher)
Write-Host ("  PublisherDisplay: {0}" -f $expectedPublisherDisplayName)
Write-Host ("  Configuration   : {0}" -f $Configuration)
Write-Host ("  Self-contained  : YES (.NET runtime bundled into the package)")
Write-Host ("  Trimming        : {0}" -f $(if ($Untrimmed) { "disabled (-Untrimmed)" } else { "enabled (PublishTrimmed=true)" }))
Write-Host ("  Output          : {0}" -f $outDir)
Write-Host ("  es.exe cache    : {0}" -f $esDir)

if ($versionParts[0] -eq 0) {
    Write-Host ""
    Write-Host "WARNING: a 0.x.y.0 version is fine for a local/CI smoke package, but Partner Center requires a" -ForegroundColor Yellow
    Write-Host "         NON-ZERO first version section. For the real Store build use e.g. -Version 1.0.0.0 and" -ForegroundColor Yellow
    Write-Host "         bump the 3rd section on every submission." -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Fresh output for this run (only our own artifacts are deleted).
Get-ChildItem -LiteralPath $outDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "EverythingPal_*.msix" -or $_.Name -like "EverythingPal_*.msixbundle" } |
    Remove-Item -Force
$bundleInputDir = Join-Path $outDir "_bundle-input"
if (Test-Path -LiteralPath $bundleInputDir) { Remove-Item -LiteralPath $bundleInputDir -Recurse -Force }
$manifestsDir = Join-Path $outDir "manifests"
if (Test-Path -LiteralPath $manifestsDir) { Remove-Item -LiteralPath $manifestsDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $manifestsDir | Out-Null

# ---------------------------------------------------------------------------
# makeappx
# ---------------------------------------------------------------------------

function Resolve-MakeAppx {
    if ($MakeAppxPath) {
        $explicit = Resolve-RepoPath $MakeAppxPath
        if (-not (Test-Path -LiteralPath $explicit)) { throw "makeappx.exe not found at -MakeAppxPath '$explicit'" }
        return $explicit
    }
    $candidates = New-Object System.Collections.Generic.List[object]
    $kitsRoots = @(
        (Join-Path $env:ProgramFiles "Windows Kits\10\bin"),
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin")
    )
    foreach ($kitsRoot in $kitsRoots) {
        if ($kitsRoot -and (Test-Path -LiteralPath $kitsRoot)) {
            foreach ($found in Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter "makeappx.exe" -File -ErrorAction SilentlyContinue) {
                if ($found.FullName -match '\\x64\\') { $candidates.Add($found) }
            }
        }
    }
    if ($candidates.Count -gt 0) {
        return ($candidates | Sort-Object FullName -Descending | Select-Object -First 1).FullName
    }
    # Fallback: Microsoft.Windows.SDK.BuildTools extracted by scripts/deploy.ps1 (artifacts/tools).
    $buildTools = Join-Path $root "artifacts\tools\buildtools"
    if (Test-Path -LiteralPath $buildTools) {
        $found = Get-ChildItem -LiteralPath $buildTools -Recurse -Filter "makeappx.exe" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    throw "makeappx.exe not found. Install the Windows SDK 10.0.26100 (Windows Kits) or pass -MakeAppxPath."
}

$makeappx = Resolve-MakeAppx
Write-Host ("  makeappx        : {0}" -f $makeappx)

# ---------------------------------------------------------------------------
# es.exe (MIT, from voidtools/ES) - bundled so users only need Everything itself
# ---------------------------------------------------------------------------

function Resolve-EsExe {
    param([hashtable]$Arch)

    $cacheDir = Join-Path $esDir $Arch.Token
    $cached = Join-Path $cacheDir "es.exe"
    if (Test-Path -LiteralPath $cached) { return $cached }

    if (Test-Path -LiteralPath $esDir) {
        $existing = Get-ChildItem -LiteralPath $esDir -Recurse -Filter "es.exe" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -match [regex]::Escape($Arch.Token) } |
            Select-Object -First 1
        if ($existing) {
            New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
            Copy-Item -LiteralPath $existing.FullName -Destination $cached -Force
            Write-Host ("  Using cached es.exe ({0}): {1}" -f $Arch.Token, $existing.FullName) -ForegroundColor DarkGray
            return $cached
        }
    }

    Write-Host ("  Downloading latest es.exe ({0}) from github.com/voidtools/ES ..." -f $Arch.Token) -ForegroundColor DarkGray
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    # CI passes GITHUB_TOKEN to avoid the anonymous GitHub API rate limit.
    $headers = @{ "User-Agent" = "EverythingPal-pack-store" }
    if ($env:GITHUB_TOKEN) { $headers["Authorization"] = "Bearer $env:GITHUB_TOKEN" }
    try {
        $release = Invoke-RestMethod -Uri $esReleaseApi -Headers $headers -TimeoutSec 120
    }
    catch {
        throw "Could not query the latest voidtools/ES release: $($_.Exception.Message). Place es.exe manually at $cached and re-run."
    }
    $suffix = if ($Arch.Token -eq "arm64") { "ARM64.zip" } else { "x64.zip" }
    $asset = $release.assets | Where-Object { $_.name -like "*.$suffix" } | Select-Object -First 1
    if (-not $asset) { throw "No ES asset '*.$suffix' in the latest voidtools/ES release ($($release.tag_name))." }

    $zip = Join-Path $cacheDir "es.zip"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -TimeoutSec 300
    $extract = Join-Path $cacheDir "extract"
    if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter "es.exe" -File | Select-Object -First 1
    if (-not $found) { throw "es.exe not found inside $($asset.name)." }
    Copy-Item -LiteralPath $found.FullName -Destination $cached -Force
    Write-Host ("  Bundled ES {0} ({1})" -f $release.tag_name, $asset.name) -ForegroundColor DarkGray
    return $cached
}

# ---------------------------------------------------------------------------
# Publish (self-contained) + stage + pack per architecture
# ---------------------------------------------------------------------------

function Get-PublishDirectory {
    param([hashtable]$Arch)
    return (Join-Path $outDir "publish-$($Arch.Token)")
}

function Invoke-ArchPublish {
    param([hashtable]$Arch)

    $publishDir = Get-PublishDirectory -Arch $Arch
    if ($SkipBuild) {
        if (-not (Test-Path -LiteralPath $publishDir)) {
            throw "-SkipBuild was passed but the publish output is missing: $publishDir. Run without -SkipBuild first."
        }
        Write-Host ("  [skip] publish {0} (reusing {1})" -f $Arch.Token, $publishDir) -ForegroundColor DarkGray
        return $publishDir
    }

    if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

    # Self-contained: the .NET runtime is bundled, so a clean Store/certification machine without
    # the .NET 10 runtime can launch the packaged COM server.
    # EnableMsixTooling/EnableMsixPackaging MUST be false for a plain publish: otherwise the publish
    # target fails with MSB3030 ("EverythingPal.pri" missing) after producing partial output.
    # PublishProfile= avoids the NETSDK1198 "publish profile not found" warning.
    $trimValue = if ($Untrimmed) { "false" } else { "true" }
    $publishArguments = @(
        "publish", $project,
        "-c", $Configuration,
        "-r", $Arch.Rid,
        "--self-contained", "true",
        "-p:Platform=$($Arch.Platform)",
        "-p:WindowsPackageType=None",
        "-p:EnableMsixPackaging=false",
        "-p:EnableMsixTooling=false",
        "-p:PublishProfile=",
        "-p:PublishTrimmed=$trimValue",
        "-o", $publishDir
    )

    Write-Host ("  dotnet {0}" -f ($publishArguments -join ' ')) -ForegroundColor DarkGray
    # Out-Host keeps native output visible without polluting this function's return value.
    & $dotnet @publishArguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($Arch.Token) (exit code $LASTEXITCODE)." }

    $exe = Join-Path $publishDir "EverythingPal.exe"
    if (-not (Test-Path -LiteralPath $exe)) { throw "dotnet publish produced no EverythingPal.exe in $publishDir." }
    return $publishDir
}

function Get-PeMachine {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        throw "Not a PE file: $Path"
    }
    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0 -or ($peOffset + 6) -ge $bytes.Length) { throw "Corrupt PE header: $Path" }
    if ($bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45) { throw "Corrupt PE signature: $Path" }
    return [System.BitConverter]::ToUInt16($bytes, $peOffset + 4)
}

function Test-PublishLayout {
    param([hashtable]$Arch, [string]$PublishDir)

    # A framework-dependent publish only has EverythingPal.exe + managed DLLs and would fail to start
    # on a machine without the .NET 10 runtime. Self-contained means the host + runtime are inside.
    foreach ($name in @("hostfxr.dll", "hostpolicy.dll", "coreclr.dll", "EverythingPal.runtimeconfig.json")) {
        $path = Join-Path $PublishDir $name
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Publish output for $($Arch.Token) is NOT self-contained: '$name' is missing in $PublishDir."
        }
    }
    # Real file-type icons use System.Drawing.Common (GDI+) via the CmdPal toolkit's ThumbnailHelper.
    # Trimming can drop them; fail loudly instead of shipping generic glyphs only.
    foreach ($name in @("System.Drawing.Common.dll", "System.Private.Windows.GdiPlus.dll")) {
        $path = Join-Path $PublishDir $name
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Publish output for $($Arch.Token) is missing '$name' (required for real shell/file icons; check the System.Drawing.Common PackageReference and trimming roots)."
        }
    }
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $PublishDir "EverythingPal.runtimeconfig.json") -Raw
    if ($runtimeConfig -notmatch '"includedFrameworks"') {
        throw "Publish output for $($Arch.Token) is framework-dependent (runtimeconfig.json has no 'includedFrameworks')."
    }

    $expectedMachine = if ($Arch.Token -eq "x64") { 0x8664 } else { 0xAA64 }
    foreach ($peName in @("EverythingPal.exe", "coreclr.dll")) {
        $machine = Get-PeMachine -Path (Join-Path $PublishDir $peName)
        if ($machine -ne $expectedMachine) {
            throw ("{0} publish output has the wrong PE architecture: {1} (machine 0x{2:X4}, expected 0x{3:X4})." -f $Arch.Token, $peName, $machine, $expectedMachine)
        }
    }
    Write-Host ("  self-contained {0} publish OK (hostfxr/coreclr present, PE machine 0x{1:X4})" -f $Arch.Token, $expectedMachine) -ForegroundColor DarkGray
}

function New-Layout {
    param([hashtable]$Arch, [string]$PublishDir)

    $layout = Join-Path $outDir "layout-$($Arch.Token)"
    if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $layout | Out-Null
    Copy-Item -Path (Join-Path $PublishDir "*") -Destination $layout -Recurse -Force

    # Debug symbols are useless in a Store package; dropping them keeps the bundle smaller.
    Get-ChildItem -LiteralPath $layout -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $manifest = Join-Path $layout "AppxManifest.xml"
    Copy-Item -LiteralPath $sourceManifestPath -Destination $manifest -Force

    $doc = New-Object System.Xml.XmlDocument
    $doc.PreserveWhitespace = $true
    $doc.Load($manifest)
    $identity = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    if (-not $identity) { throw "Identity element not found in the staged manifest." }
    $identity.SetAttribute("ProcessorArchitecture", $Arch.Token)
    $identity.SetAttribute("Version", $Version)
    if ($IdentityName) { $identity.SetAttribute("Name", $IdentityName) }
    if ($Publisher) { $identity.SetAttribute("Publisher", $Publisher) }
    if ($PublisherDisplayName) {
        $properties = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']")
        if (-not $properties) { throw "Properties element not found in the staged manifest." }
        # NOTE: do not reuse a $publisherDisplayName variable here - PowerShell variable names are
        # case-insensitive and would clobber the $PublisherDisplayName parameter (string) with the
        # XML node, writing "System.Xml.XmlElement" into the manifest.
        $pdnNode = $properties.SelectSingleNode("*[local-name()='PublisherDisplayName']")
        if (-not $pdnNode) { throw "Properties/PublisherDisplayName missing in the staged manifest." }
        $pdnNode.InnerText = $PublisherDisplayName
    }
    if ($DisplayName) {
        # The Store rejects a package whose Properties/DisplayName is not one of the reserved app
        # names, so the Store build uses the reserved product name even though the dev manifest keeps
        # the friendlier "Everything Search" name.
        $properties = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']")
        if (-not $properties) { throw "Properties element not found in the staged manifest." }
        $dnNode = $properties.SelectSingleNode("*[local-name()='DisplayName']")
        if (-not $dnNode) { throw "Properties/DisplayName missing in the staged manifest." }
        $dnNode.InnerText = $DisplayName

        $visual = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Applications']/*[local-name()='Application']/*[local-name()='VisualElements']")
        if ($visual) { $visual.SetAttribute("DisplayName", $DisplayName) }
    }
    $doc.Save($manifest)

    # Sanity-check that the staged manifest still parses and carries what we expect.
    $check = New-Object System.Xml.XmlDocument
    $check.Load($manifest)
    $checkIdentity = $check.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    Assert-Equal $Version ($checkIdentity.GetAttribute("Version")) "staged Identity/@Version"
    Assert-Equal $Arch.Token ($checkIdentity.GetAttribute("ProcessorArchitecture")) "staged Identity/@ProcessorArchitecture"
    Assert-Equal $expectedIdentityName ($checkIdentity.GetAttribute("Name")) "staged Identity/@Name"
    Assert-Equal $expectedPublisher ($checkIdentity.GetAttribute("Publisher")) "staged Identity/@Publisher"

    # Bundle the MIT-licensed ES client + its notice.
    $toolsDir = Join-Path $layout "Tools"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $esExe = Resolve-EsExe -Arch $Arch
    Copy-Item -LiteralPath $esExe -Destination (Join-Path $toolsDir "es.exe") -Force
    Copy-Item -LiteralPath (Join-Path $root "THIRD-PARTY-NOTICES.md") -Destination (Join-Path $layout "THIRD-PARTY-NOTICES.md") -Force
    Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination (Join-Path $layout "LICENSE") -Force

    return $layout
}

function Invoke-PackMsix {
    param([hashtable]$Arch, [string]$Layout)
    $msix = Join-Path $outDir "EverythingPal_${Version}_$($Arch.Token).msix"
    if (Test-Path -LiteralPath $msix) { Remove-Item -LiteralPath $msix -Force }
    Write-Host ("  makeappx pack {0} ..." -f (Split-Path -Leaf $msix)) -ForegroundColor DarkGray
    & $makeappx pack /d $Layout /p $msix /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $($Arch.Token) (exit code $LASTEXITCODE)." }
    if (-not (Test-Path -LiteralPath $msix)) { throw "makeappx reported success but $msix does not exist." }
    return $msix
}

$msixByArch = @{}
$publishSizes = @{}
foreach ($arch in $selectedArchs) {
    Write-Step ("Publish + stage + pack {0}" -f $arch.Token)
    $publishDir = Invoke-ArchPublish -Arch $arch
    Test-PublishLayout -Arch $arch -PublishDir $publishDir
    $publishSizes[$arch.Token] = (Get-ChildItem -LiteralPath $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host ("  publish size {0:N1} MB (before stripping PDBs)" -f ($publishSizes[$arch.Token] / 1MB)) -ForegroundColor DarkGray
    $layout = New-Layout -Arch $arch -PublishDir $publishDir
    $msixByArch[$arch.Token] = Invoke-PackMsix -Arch $arch -Layout $layout
}

# ---------------------------------------------------------------------------
# Bundle
# ---------------------------------------------------------------------------

$bundleArchSuffix = ($selectedArchs | ForEach-Object { $_.Token }) -join '_'
$bundlePath = Join-Path $outDir "EverythingPal_${Version}_${bundleArchSuffix}.msixbundle"
New-Item -ItemType Directory -Force -Path $bundleInputDir | Out-Null
foreach ($msix in $msixByArch.Values) { Copy-Item -LiteralPath $msix -Destination $bundleInputDir -Force }

Write-Step "Bundle"
Write-Host ("  makeappx bundle {0} ..." -f (Split-Path -Leaf $bundlePath)) -ForegroundColor DarkGray
if (Test-Path -LiteralPath $bundlePath) { Remove-Item -LiteralPath $bundlePath -Force }
# /bv pins the bundle identity version to the package version; without it makeappx derives a
# date-based version (e.g. 2026.1020.733.0), which is not what we want for a submission.
& $makeappx bundle /d $bundleInputDir /p $bundlePath /bv $Version /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed (exit code $LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath $bundlePath)) { throw "makeappx bundle reported success but $bundlePath does not exist." }
Remove-Item -LiteralPath $bundleInputDir -Recurse -Force

# ---------------------------------------------------------------------------
# Validation - unpack what we produced and assert the Store-relevant facts
# ---------------------------------------------------------------------------

function Assert-Unsigned {
    param([string]$PackagePath)
    $entries = Get-ZipEntryNames -Path $PackagePath
    if ($entries -contains "AppxSignature.p7x") {
        throw "$(Split-Path -Leaf $PackagePath) is signed. The Store submission package must be UNSIGNED (the Store re-signs it)."
    }
}

function Test-StagedPackage {
    param([string]$Arch, [string]$MsixPath)

    Assert-Unsigned -PackagePath $MsixPath

    $unpackDir = Join-Path ([System.IO.Path]::GetTempPath()) ("everythingpal-validate-" + [guid]::NewGuid().ToString("N"))
    try {
        New-Item -ItemType Directory -Force -Path $unpackDir | Out-Null
        $unpackOutput = & $makeappx unpack /p $MsixPath /d $unpackDir /o 2>&1
        if ($LASTEXITCODE -ne 0) {
            $unpackOutput | Write-Host
            throw "makeappx unpack failed for $(Split-Path -Leaf $MsixPath)."
        }

        $manifestPath = Join-Path $unpackDir "AppxManifest.xml"
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw "AppxManifest.xml missing after unpacking $(Split-Path -Leaf $MsixPath)." }
        Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $manifestsDir "AppxManifest-$Arch.xml") -Force

        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($manifestPath)
        $identity = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
        Assert-Equal $expectedIdentityName ($identity.GetAttribute("Name")) "$Arch unpacked Identity/@Name"
        Assert-Equal $expectedPublisher ($identity.GetAttribute("Publisher")) "$Arch unpacked Identity/@Publisher"
        Assert-Equal $Version ($identity.GetAttribute("Version")) "$Arch unpacked Identity/@Version"
        Assert-Equal $Arch ($identity.GetAttribute("ProcessorArchitecture")) "$Arch unpacked Identity/@ProcessorArchitecture"
        $pdnNode = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']/*[local-name()='PublisherDisplayName']")
        if (-not $pdnNode) { throw "Properties/PublisherDisplayName missing in the $Arch package." }
        Assert-Equal $expectedPublisherDisplayName ($pdnNode.InnerText.Trim()) "$Arch unpacked PublisherDisplayName"
        $dnNode = $doc.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']/*[local-name()='DisplayName']")
        if (-not $dnNode) { throw "Properties/DisplayName missing in the $Arch package." }
        Assert-Equal $expectedDisplayName ($dnNode.InnerText.Trim()) "$Arch unpacked DisplayName"

        $required = @(
            "EverythingPal.exe",
            "EverythingPal.runtimeconfig.json",
            "hostfxr.dll",
            "hostpolicy.dll",
            "coreclr.dll",
            "System.Drawing.Common.dll",
            "System.Private.Windows.GdiPlus.dll",
            "Tools\es.exe",
            "THIRD-PARTY-NOTICES.md",
            "LICENSE",
            "Assets\StoreLogo.png"
        )
        foreach ($relative in $required) {
            $path = Join-Path $unpackDir $relative
            if (-not (Test-Path -LiteralPath $path)) { throw "Required payload item missing from $Arch package: $relative" }
        }
        # The packaged app must be self-contained (runs on machines without the .NET 10 runtime) and
        # its native host/runtime must match the package architecture we just stamped.
        $runtimeConfig = Get-Content -LiteralPath (Join-Path $unpackDir "EverythingPal.runtimeconfig.json") -Raw
        if ($runtimeConfig -notmatch '"includedFrameworks"') {
            throw "$Arch package is framework-dependent (runtimeconfig.json has no 'includedFrameworks'); Store users without .NET 10 would fail to launch it."
        }
        $expectedMachine = if ($Arch -eq "x64") { 0x8664 } else { 0xAA64 }
        foreach ($peName in @("EverythingPal.exe", "coreclr.dll")) {
            $machine = Get-PeMachine -Path (Join-Path $unpackDir $peName)
            if ($machine -ne $expectedMachine) {
                throw ("{0} package contains the wrong PE architecture for {1} (machine 0x{2:X4}, expected 0x{3:X4})." -f $Arch, $peName, $machine, $expectedMachine)
            }
        }
        $esLength = (Get-Item -LiteralPath (Join-Path $unpackDir "Tools\es.exe")).Length
        if ($esLength -le 0) { throw "$Arch package Tools\es.exe is empty." }
        return $esLength
    }
    finally {
        if (Test-Path -LiteralPath $unpackDir) { Remove-Item -LiteralPath $unpackDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Step "Validation"
$esSizes = @{}
foreach ($arch in $selectedArchs) {
    $esSizes[$arch.Token] = Test-StagedPackage -Arch $arch.Token -MsixPath $msixByArch[$arch.Token]
    Write-Host ("  {0}: identity/version/architecture OK, es.exe {1:N0} bytes" -f $arch.Token, $esSizes[$arch.Token]) -ForegroundColor DarkGray
}

Assert-Unsigned -PackagePath $bundlePath
$bundleEntries = Get-ZipEntryNames -Path $bundlePath
if (-not ($bundleEntries -contains "AppxMetadata/AppxBundleManifest.xml")) {
    throw "Bundle manifest (AppxMetadata/AppxBundleManifest.xml) missing from $bundlePath."
}
$bundleManifestText = Read-ZipTextEntry -Path $bundlePath -EntryName "AppxMetadata/AppxBundleManifest.xml"
Set-Content -LiteralPath (Join-Path $manifestsDir "AppxBundleManifest.xml") -Value $bundleManifestText -Encoding UTF8
$bundleManifest = New-Object System.Xml.XmlDocument
$bundleManifest.LoadXml($bundleManifestText)

$bundleIdentity = $bundleManifest.SelectSingleNode("/*[local-name()='Bundle']/*[local-name()='Identity']")
if (-not $bundleIdentity) { throw "Identity element missing from the bundle manifest." }
Assert-Equal $expectedIdentityName ($bundleIdentity.GetAttribute("Name")) "bundle manifest Identity/@Name"
Assert-Equal $expectedPublisher ($bundleIdentity.GetAttribute("Publisher")) "bundle manifest Identity/@Publisher"
Assert-Equal $Version ($bundleIdentity.GetAttribute("Version")) "bundle manifest Identity/@Version"
$bundlePublisherDisplayName = $bundleManifest.SelectSingleNode("/*[local-name()='Bundle']/*[local-name()='Properties']/*[local-name()='PublisherDisplayName']")
if ($bundlePublisherDisplayName) {
    Assert-Equal $expectedPublisherDisplayName ($bundlePublisherDisplayName.InnerText.Trim()) "bundle manifest PublisherDisplayName"
}

$bundledPackages = @($bundleManifest.SelectNodes("/*[local-name()='Bundle']/*[local-name()='Packages']/*[local-name()='Package']"))
$bundledArchitectures = @($bundledPackages | ForEach-Object { $_.GetAttribute("Architecture") })
foreach ($arch in $selectedArchs) {
    if ($bundledArchitectures -notcontains $arch.Token) {
        throw "Bundle manifest does not list the $($arch.Token) package (found: $($bundledArchitectures -join ', '))."
    }
    $innerName = "EverythingPal_${Version}_$($arch.Token).msix"
    if ($bundleEntries -notcontains $innerName) {
        throw "Bundle is missing the inner package $innerName."
    }
}
foreach ($bundledPackage in $bundledPackages) {
    $innerName = $bundledPackage.GetAttribute("FileName")
    if ($bundleEntries -notcontains $innerName) {
        throw "Bundle manifest references '$innerName' but the bundle does not contain it."
    }
}
Write-Host ("  bundle: {0} architectures {1}, unsigned OK" -f $bundledPackages.Count, ($bundledArchitectures -join '+')) -ForegroundColor DarkGray

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

function Format-Size {
    param([long]$Bytes)
    return ("{0:N2} MB" -f ($Bytes / 1MB))
}

$bundleItem = Get-Item -LiteralPath $bundlePath
$bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "===================== STORE PACKAGE READY =====================" -ForegroundColor Green
Write-Host ("Bundle        : {0}" -f $bundleItem.FullName)
Write-Host ("  Size        : {0}" -f (Format-Size $bundleItem.Length))
Write-Host ("  SHA256      : {0}" -f $bundleHash)
Write-Host ("  Signed      : NO (expected - the Store re-signs the upload)")
Write-Host ("  Self-contained: YES (bundled .NET runtime; no .NET install needed on user machines)")
Write-Host ("Identity Name : {0}" -f $expectedIdentityName)
Write-Host ("Publisher     : {0}" -f $expectedPublisher)
Write-Host ("PublishDisp.  : {0}" -f $expectedPublisherDisplayName)
Write-Host ("Version       : {0}" -f $Version)
Write-Host ("Architectures : {0}" -f ((($selectedArchs | ForEach-Object { $_.Token }) -join ' + ')))
Write-Host ""
Write-Host "Per-architecture packages:" -ForegroundColor Cyan
foreach ($arch in $selectedArchs) {
    $msix = $msixByArch[$arch.Token]
    $msixItem = Get-Item -LiteralPath $msix
    $msixHash = (Get-FileHash -LiteralPath $msix -Algorithm SHA256).Hash
    Write-Host ("  {0,-6} publish {1,8:N1} MB -> msix {2,8:N1} MB" -f $arch.Token, ($publishSizes[$arch.Token] / 1MB), ($msixItem.Length / 1MB))
    Write-Host ("         {0}" -f $msixItem.FullName)
    Write-Host ("         sha256 {0}" -f $msixHash)
}
Write-Host ""
Write-Host "Validation evidence (keep with the submission):" -ForegroundColor Cyan
Write-Host ("  {0}" -f $manifestsDir)
Write-Host ("  {0}" -f $outDir)
Write-Host ""
Write-Host "Next steps (see docs/store/packaging.md):" -ForegroundColor Yellow
Write-Host "  1. Partner Center -> your app -> Packages -> upload the .msixbundle above (it must stay unsigned)."
Write-Host "  2. The uploaded Identity/@Name and Identity/@Publisher must equal Partner Center's Product identity exactly."
Write-Host "  3. Versioning: x.y.z.0 (4th section stays 0); Partner Center also requires a non-zero major"
Write-Host "     section, so use e.g. 1.0.0.0 for the Store build and bump z on every submission."
Write-Host "  4. Store certification re-signs the package; do not signtool it here."
Write-Host ""
