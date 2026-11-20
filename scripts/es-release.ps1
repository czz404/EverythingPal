# EverythingPal - pinned ES (es.exe) release used by scripts/pack-store.ps1 and scripts/deploy.ps1.
#
# Why pinned: downloading the "latest" release made the same source commit bundle different es.exe
# builds on a developer machine and on a clean CI runner. The version, asset name and SHA-256 below
# are the values that were validated with this extension (ES 1.1.1.39 / Everything 1.5.0.1423b).
#
# The hash is for the es.exe extracted from the official zip (not the zip itself).
#
# Usage:
#   . "$PSScriptRoot\es-release.ps1"
#   $esExe = Resolve-PinnedEsExe -EsRoot (Join-Path $root 'artifacts\es') -Arch 'x64'
#
# Never change the version/hash without re-running the ES integration check in
# docs/qa/verification.md (fuzzy term1, 1.4 retry, quoted phrases, sort order).

$script:EsRelease = @{
    Version      = '1.1.1.39'
    Tag          = '1.1.1.39'
    DownloadBase = 'https://github.com/voidtools/ES/releases/download/1.1.1.39'
    Architectures = @{
        x64   = @{
            Asset  = 'ES-1.1.1.39.x64.zip'
            Sha256 = '23DCD7158947B660DBABF52DC983D42840CF34CFF96859F9608BF8A7A3BA8ADE'
        }
        arm64 = @{
            Asset  = 'ES-1.1.1.39.ARM64.zip'
            Sha256 = '1F4F79B6B1723096A0437CD02DBEEF53E0BCC14B3B2C300A5E1AF9BE93BA3CF6'
        }
    }
}

function Get-EsReleaseVersion {
    return $script:EsRelease.Version
}

function Get-EsArchitecture {
    param([Parameter(Mandatory)][string]$Arch)

    $key = $Arch.ToLowerInvariant()
    if (-not $script:EsRelease.Architectures.ContainsKey($key)) {
        throw "Unsupported es.exe architecture '$Arch'. Supported: x64, arm64."
    }

    return $script:EsRelease.Architectures[$key]
}

function Get-EsExpectedHash {
    param([Parameter(Mandatory)][string]$Arch)
    return (Get-EsArchitecture -Arch $Arch).Sha256
}

function Get-EsAssetName {
    param([Parameter(Mandatory)][string]$Arch)
    return (Get-EsArchitecture -Arch $Arch).Asset
}

function Get-EsDownloadUrl {
    param([Parameter(Mandatory)][string]$Arch)
    return ("{0}/{1}" -f $script:EsRelease.DownloadBase, (Get-EsAssetName -Arch $Arch))
}

function Test-EsFileHash {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Arch
    )

    $expected = Get-EsExpectedHash -Arch $Arch
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    return $actual -eq $expected
}

<#
.SYNOPSIS
    Returns a verified es.exe for the given architecture, downloading the pinned release when needed.

.DESCRIPTION
    The cache is <EsRoot>/<arch>/es.exe. A cache hit is only accepted when its SHA-256 matches the
    pinned value; a wrong file (including a cache accidentally filled with the other architecture)
    is discarded and replaced from the pinned release. Any download/hash failure reports the ES
    version and architecture and throws, so packaging stops instead of shipping an unknown binary.
#>
function Resolve-PinnedEsExe {
    param(
        [Parameter(Mandatory)][string]$EsRoot,
        [Parameter(Mandatory)][string]$Arch
    )

    $key = $Arch.ToLowerInvariant()
    $definition = Get-EsArchitecture -Arch $key
    $version = Get-EsReleaseVersion
    $expectedHash = $definition.Sha256
    $cacheDir = Join-Path $EsRoot $key
    $cached = Join-Path $cacheDir 'es.exe'

    if (Test-Path -LiteralPath $cached) {
        $actualHash = (Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -eq $expectedHash) {
            Write-Host ("  es.exe {0} {1}: cache hit, SHA-256 OK" -f $version, $key) -ForegroundColor DarkGray
            return $cached
        }

        Write-Warning ("Cached es.exe for {0} does not match pinned ES {1}: expected {2}, found {3}. Re-fetching." -f $key, $version, $expectedHash, $actualHash)
        Remove-Item -LiteralPath $cached -Force
    }

    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

    $url = Get-EsDownloadUrl -Arch $key
    $assetName = $definition.Asset
    $zip = Join-Path $cacheDir $assetName
    $extract = Join-Path $cacheDir ("extract-" + [guid]::NewGuid().ToString("N"))

    Write-Host ("  Downloading pinned es.exe {0} ({1}) from {2} ..." -f $version, $key, $url) -ForegroundColor DarkGray
    try {
        try {
            Invoke-WebRequest -Uri $url -OutFile $zip -TimeoutSec 300
        }
        catch {
            throw ("Could not download pinned ES {0} ({1}) from {2}: {3}" -f $version, $key, $url, $_.Exception.Message)
        }

        Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
        $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter 'es.exe' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $found) {
            throw ("Pinned ES {0} ({1}) archive '{2}' does not contain es.exe." -f $version, $key, $assetName)
        }

        $downloadedHash = (Get-FileHash -LiteralPath $found.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($downloadedHash -ne $expectedHash) {
            throw ("Pinned ES {0} ({1}) SHA-256 mismatch: expected {2}, found {3}. Refusing to package an unverified es.exe." -f $version, $key, $expectedHash, $downloadedHash)
        }

        Copy-Item -LiteralPath $found.FullName -Destination $cached -Force
        Write-Host ("  Bundled pinned ES {0} ({1}), SHA-256 verified" -f $version, $key) -ForegroundColor DarkGray
        return $cached
    }
    finally {
        if (Test-Path -LiteralPath $extract) {
            Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $zip) {
            Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
        }
    }
}
