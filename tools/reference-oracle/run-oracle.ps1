#!/usr/bin/env pwsh
# Runs the xatlas oracle over the corpus and writes golden UV layouts + metrics into tests/golden/.
#
# For each corpus mesh, the built xatlas example unwraps it deterministically (fixed options, pinned
# thread count) and emits an OBJ with UVs plus summary metrics (chart count, total seam length, max/avg
# stretch, packing efficiency, overlap=0). UVBake's own output is later diffed against these goldens
# (hard-gate overlap=0 + coverage; tolerance-gate the metric scorecard) — see
# ../../Agents/Documents/07-quality-artifacts-and-validation.md.
#
# Usage:
#   ./run-oracle.ps1 -XatlasTool <path-to-built-xatlas-example>
#
# NOTE: the exact CLI/stdout format of the xatlas example is confirmed against the built binary; metric
# extraction below is the integration point to flesh out in P4 when the golden gate goes live.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$XatlasTool,
    [int]$Threads = 1
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$corpusDir = Join-Path $repoRoot 'tests/corpus'
$downloadedDir = Join-Path $corpusDir 'downloaded'
$goldenDir = Join-Path $repoRoot 'tests/golden'
New-Item -ItemType Directory -Force -Path $goldenDir | Out-Null

if (-not (Test-Path $XatlasTool)) {
    throw "xatlas tool not found at '$XatlasTool'. Build it with build-xatlas.ps1 first."
}

# Procedural (committed) meshes + any downloaded ones.
$meshes = @()
$meshes += Get-ChildItem -Path $corpusDir -Filter '*.obj' -File
if (Test-Path $downloadedDir) {
    $meshes += Get-ChildItem -Path $downloadedDir -Filter '*.obj' -File
}

# Pin threads so the oracle output is deterministic across runs/machines.
$env:OMP_NUM_THREADS = "$Threads"

foreach ($mesh in $meshes) {
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($mesh.Name)
    $outObj = Join-Path $goldenDir "$stem.uv.obj"
    $metrics = Join-Path $goldenDir "$stem.metrics.txt"

    Write-Host "oracle $($mesh.Name) -> $stem.uv.obj"
    & $XatlasTool $mesh.FullName $outObj | Tee-Object -FilePath $metrics | Out-Null
}

Write-Host "done. goldens in $goldenDir"
