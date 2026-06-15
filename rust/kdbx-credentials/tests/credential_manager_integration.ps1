#!/usr/bin/env pwsh
#
# End-to-end test of the Windows Credential Manager secret store (Rust).
#
# The Windows counterpart of systemd_creds_integration.sh. It exercises the
# *real* mechanism the library relies on in production, and covers the
# multiple-databases case: two KDBX files, each with its own master password
# held under its own Credential Manager target (e.g. a personal database and a
# shared IT-department one).
#
#   1. `cmdkey` stores each master password as a generic credential whose
#      target name is the secret_store_key (the documented provisioning step).
#   2. The `lookup` example reads each credential from the Credential Manager,
#      opens the matching database, and looks up an entry.
#   3. A negative check confirms the passwords are genuinely independent: the
#      IT-dept database does NOT open with the personal database's password.
#
# Distinctive target names are used (not the production "kdbx-master") so the
# test cannot clobber a real credential, and the credentials are always deleted
# afterwards.
#
# Usage:  pwsh tests/credential_manager_integration.ps1
# Exit:   0 on success, non-zero on failure.

$ErrorActionPreference = 'Stop'

$CrateDir = Split-Path $PSScriptRoot -Parent
$Account = 'kdbx-credentials'   # the fixed account convention

# Two databases, two distinct master passwords, two distinct store keys.
$Personal = @{
    Key      = 'kdbx-credentials-itest-personal'
    Password = 'test'
    Db        = Join-Path $CrateDir 'tests/data/test.kdbx'
    Entry     = 'ndb/postgres-prod'
    Username  = 'pgadmin'
}
$ItDept = @{
    Key      = 'kdbx-credentials-itest-itdept'
    Password = 'it-dept-secret'
    Db        = Join-Path $CrateDir 'tests/data/it-dept.kdbx'
    Entry     = 'it/domain-admin'
    Username  = 'svc-domain-admin'
}

function Fail($msg) { Write-Error "FAIL: $msg"; exit 1 }

function Invoke-Lookup($bin, $db, $key, $entry) {
    $out = (& $bin $db $key $entry 2>&1 | Out-String)
    return @{ Output = $out; Code = $LASTEXITCODE }
}

if (-not (Get-Command cmdkey -ErrorAction SilentlyContinue)) { Fail 'cmdkey not found' }
if (-not (Get-Command cargo  -ErrorAction SilentlyContinue)) { Fail 'cargo not found' }
foreach ($d in @($Personal.Db, $ItDept.Db)) {
    if (-not (Test-Path $d)) { Fail "test database missing: $d" }
}

# Refuse to run if either target already exists, so we never overwrite (and then
# delete) something a human put there.
foreach ($k in @($Personal.Key, $ItDept.Key)) {
    if ((cmdkey /list:$k 2>&1 | Out-String) -notmatch 'NONE') {
        Fail "credential '$k' already exists; aborting so it is not clobbered"
    }
}

Write-Host '==> Building lookup example'
cargo build --quiet --manifest-path (Join-Path $CrateDir 'Cargo.toml') --example lookup
if ($LASTEXITCODE -ne 0) { Fail 'cargo build failed' }
$Bin = Join-Path $CrateDir 'target/debug/examples/lookup.exe'
if (-not (Test-Path $Bin)) { Fail "example binary not built: $Bin" }

try {
    Write-Host '==> Provisioning both master passwords via cmdkey'
    cmdkey /generic:$($Personal.Key) /user:$Account /pass:$($Personal.Password) | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'cmdkey provisioning failed (personal)' }
    cmdkey /generic:$($ItDept.Key) /user:$Account /pass:$($ItDept.Password) | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'cmdkey provisioning failed (it-dept)' }

    foreach ($db in @($Personal, $ItDept)) {
        Write-Host "==> Opening $($db.Db) with key '$($db.Key)'"
        $r = Invoke-Lookup $Bin $db.Db $db.Key $db.Entry
        Write-Host $r.Output
        if ($r.Code -ne 0) { Fail "example exited with code $($r.Code) for $($db.Db)" }
        if ($r.Output -notmatch 'OK: opened database') { Fail "success marker not found for $($db.Db)" }
        if ($r.Output -notmatch "username: $($db.Username)") { Fail "expected username '$($db.Username)' not found for $($db.Db)" }
    }

    Write-Host '==> Negative check: IT-dept database must NOT open with the personal password'
    $bad = Invoke-Lookup $Bin $ItDept.Db $Personal.Key $ItDept.Entry
    Write-Host $bad.Output
    if ($bad.Code -eq 0) { Fail 'IT-dept database opened with the wrong password; passwords are not independent' }

    Write-Host 'PASS: each database opened with its own master password; wrong password rejected'
}
finally {
    cmdkey /delete:$($Personal.Key) | Out-Null
    cmdkey /delete:$($ItDept.Key) | Out-Null
}
