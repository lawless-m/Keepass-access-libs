#!/usr/bin/env pwsh
#
# Build the Rust credential tool in release mode and install it, together with
# the provisioning scripts, into one machine-wide directory (Windows).
#
# The Windows mirror of build-install.sh: builds the `getfield` example from the
# kdbx-credentials crate and installs it as `kdbx-getfield.exe`, alongside
# provision-credential.ps1/.bat. The binary is built with a statically linked
# CRT, so it has no VC++ redistributable dependency and can be copied to another
# machine as-is.
#
# The default install directory is C:\Program Files\Kdbx, which is readable by
# all accounts (including a service account that runs the provisioning as a
# scheduled task) but requires administrator to write to - run this elevated.
# It does NOT provision the master password; see the README / provision-credential.ps1.
#
# Usage:  .\build-install.ps1            (from an elevated shell)
# Env:    BINDIR   install location (default C:\Program Files\Kdbx)

$ErrorActionPreference = 'Stop'

$RepoDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$CrateDir = Join-Path $RepoDir 'rust\kdbx-credentials'
$BinDir   = if ($env:BINDIR) { $env:BINDIR } else { 'C:\Program Files\Kdbx' }

if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    throw 'cargo not found - install the Rust toolchain (https://rustup.rs)'
}
if (-not (Test-Path (Join-Path $CrateDir 'Cargo.toml'))) {
    throw "crate not found at $CrateDir"
}

Write-Host '==> Building getfield (release, static CRT)'
# Static CRT so the binary has no external runtime dependency and is portable.
$env:RUSTFLAGS = '-C target-feature=+crt-static'
cargo build --release --quiet --manifest-path (Join-Path $CrateDir 'Cargo.toml') --example getfield
if ($LASTEXITCODE -ne 0) { throw 'cargo build failed' }

$Bin = Join-Path $CrateDir 'target\release\examples\getfield.exe'
if (-not (Test-Path $Bin)) { throw "build produced no binary: $Bin" }

# Files to install into $BinDir: the tool, plus the provisioning scripts (which
# reference each other via %~dp0, so they work from this directory).
$installs = @(
    @{ Src = $Bin;                                          Dest = 'kdbx-getfield.exe' }
    @{ Src = Join-Path $RepoDir 'provision-credential.ps1'; Dest = 'provision-credential.ps1' }
    @{ Src = Join-Path $RepoDir 'provision-credential.bat'; Dest = 'provision-credential.bat' }
    @{ Src = Join-Path $RepoDir 'provision-runas.bat';      Dest = 'provision-runas.bat' }
)

try {
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
    foreach ($f in $installs) {
        $dest = Join-Path $BinDir $f.Dest
        Write-Host "==> Installing $dest"
        Copy-Item -Force -LiteralPath $f.Src -Destination $dest
    }
}
catch [System.UnauthorizedAccessException] {
    throw "access denied writing to $BinDir - run this from an elevated (Administrator) shell, or set BINDIR to a writable directory."
}

Write-Host "PASS: installed kdbx-getfield.exe and provisioning scripts to $BinDir"
Write-Host 'Next:'
Write-Host "  - Provision the master password into the service account's vault by"
Write-Host "    registering $BinDir\provision-credential.bat as a one-shot task that"
Write-Host '    runs as that account (put the master password in C:\kdbx.txt first).'
if (($env:Path -split ';') -notcontains $BinDir) {
    Write-Host "  - $BinDir is not on PATH; call kdbx-getfield by full path, or add it to PATH."
}
