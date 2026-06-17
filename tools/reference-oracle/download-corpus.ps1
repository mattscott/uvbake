#!/usr/bin/env pwsh
# Downloads the tier-B (real-mesh) corpus into tests/corpus/downloaded/.
#
# The tier-A procedural corpus (cube, sphere, cylinder, beveled-box, torus) is generated in-code and
# committed under tests/corpus/ — this script only fetches the large, externally-hosted meshes used for
# realism/stress. Downloaded raws are git-ignored; only the small committed goldens (tests/golden/) and
# the procedural meshes live in the repo.
#
# Usage:
#   ./download-corpus.ps1            # download anything missing
#   ./download-corpus.ps1 -Force     # re-download everything
#
# NOTE: verify each URL is still live before relying on it in CI. Sizes are large; this is opt-in.

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$targetDir = Join-Path $repoRoot 'tests/corpus/downloaded'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

# name = output filename; url = source; sha256 = optional integrity check ('' to skip).
$sources = @(
    @{ name = 'spot.obj';           url = 'https://github.com/alecjacobson/common-3d-test-models/raw/master/data/spot.obj';           sha256 = '' }
    @{ name = 'armadillo.obj';      url = 'https://github.com/alecjacobson/common-3d-test-models/raw/master/data/armadillo.obj';      sha256 = '' }
    @{ name = 'stanford-bunny.obj'; url = 'https://github.com/alecjacobson/common-3d-test-models/raw/master/data/stanford-bunny.obj'; sha256 = '' }
    # xatlas ships its own example model (gazebo) — compare UVBake's atlas directly against the oracle on it.
    @{ name = 'gazebo.obj';         url = 'https://github.com/jpcy/xatlas/raw/master/extra/models/gazebo.obj';                        sha256 = '' }
)

foreach ($source in $sources) {
    $outPath = Join-Path $targetDir $source.name
    if ((Test-Path $outPath) -and -not $Force) {
        Write-Host "skip   $($source.name) (already present)"
        continue
    }

    Write-Host "fetch  $($source.name) <- $($source.url)"
    Invoke-WebRequest -Uri $source.url -OutFile $outPath

    if ($source.sha256) {
        $actual = (Get-FileHash -Algorithm SHA256 -Path $outPath).Hash.ToLowerInvariant()
        if ($actual -ne $source.sha256.ToLowerInvariant()) {
            Remove-Item $outPath
            throw "checksum mismatch for $($source.name): expected $($source.sha256), got $actual"
        }
    }
}

Write-Host "done. downloaded corpus in $targetDir"
