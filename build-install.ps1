#!/usr/bin/env pwsh
#
# Build the Rust credential tool in release mode and install it (Windows).
#
# The Windows mirror of build-install.sh: builds the `getfield` example from the
# kdbx-credentials crate and installs it as `kdbx-getfield.exe`. It does NOT
# provision the master password — on Windows that lives in Credential Manager
# (target = the key name, account `kdbx-credentials`); see the README.
#
# The C# `GetField` example is a separate, in-process sample built with
# `dotnet build`; this script installs only the Rust CLI.
#
# Usage:  .\build-install.ps1
# Env:    BINDIR   install location (default $env:LOCALAPPDATA\Microsoft\WindowsApps,
#                  which is on the default user PATH on Windows 10/11)

$ErrorActionPreference = 'Stop'

$RepoDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$CrateDir = Join-Path $RepoDir 'rust\kdbx-credentials'
$BinDir   = if ($env:BINDIR) { $env:BINDIR } else { Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps' }

if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    throw 'cargo not found - install the Rust toolchain (https://rustup.rs)'
}
if (-not (Test-Path (Join-Path $CrateDir 'Cargo.toml'))) {
    throw "crate not found at $CrateDir"
}

Write-Host '==> Building getfield (release)'
cargo build --release --quiet --manifest-path (Join-Path $CrateDir 'Cargo.toml') --example getfield
if ($LASTEXITCODE -ne 0) { throw 'cargo build failed' }

$Bin = Join-Path $CrateDir 'target\release\examples\getfield.exe'
if (-not (Test-Path $Bin)) { throw "build produced no binary: $Bin" }

New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
$Dest = Join-Path $BinDir 'kdbx-getfield.exe'
Write-Host "==> Installing $Dest"
Copy-Item -Force $Bin $Dest

Write-Host "PASS: installed kdbx-getfield.exe to $BinDir"
if (($env:Path -split ';') -notcontains $BinDir) {
    Write-Host "Note: $BinDir is not on your PATH - add it, or set BINDIR to a dir that is."
}
Write-Host 'Next: provision the master password in Credential Manager (see README).'
