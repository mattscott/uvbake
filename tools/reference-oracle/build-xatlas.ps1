#!/usr/bin/env pwsh
# Clones and builds xatlas (MIT) — the primary reference oracle — into the git-ignored _work/ tree.
#
# xatlas is NOT shipped or linked by UVBake; it is built and run locally as an external process to emit
# golden UV layouts + metrics (see run-oracle.ps1). Requires git, cmake, and a C++ toolchain on PATH.
#
# Usage:
#   ./build-xatlas.ps1                 # clone (if needed) + configure + build Release
#   ./build-xatlas.ps1 -Clean          # wipe _work/xatlas first
#
# NOTE: this is the heavy native step and is intentionally NOT run in CI. Confirm your toolchain and the
# xatlas example target name on first use; xatlas builds a small static library plus an example tool.

[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$workDir = Join-Path $PSScriptRoot '_work'
$srcDir = Join-Path $workDir 'xatlas'
$buildDir = Join-Path $srcDir 'build'
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

foreach ($tool in 'git', 'cmake') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is required on PATH to build xatlas."
    }
}

if ($Clean -and (Test-Path $srcDir)) {
    Remove-Item -Recurse -Force $srcDir
}

if (-not (Test-Path $srcDir)) {
    git clone --depth 1 https://github.com/jpcy/xatlas.git $srcDir
}

# xatlas ships a CMakeLists that builds the library and the example/viewer tools.
cmake -S $srcDir -B $buildDir -DCMAKE_BUILD_TYPE=Release
cmake --build $buildDir --config Release

Write-Host "xatlas built under $buildDir"
Write-Host "Set the example tool path for run-oracle.ps1 (e.g. $buildDir/xatlas_example)."
