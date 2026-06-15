#!/usr/bin/env pwsh
#
# End-to-end test of the Windows Credential Manager secret store (C#).
#
# Mirrors the Rust crate's credential_manager_integration.ps1. It exercises the
# *real* mechanism the library relies on in production:
#
#   1. `cmdkey` stores the master password as a generic credential whose
#      target name is the secret_store_key (the documented provisioning step).
#   2. The `kdbx-lookup` example reads that credential from the Credential
#      Manager, opens the committed test.kdbx, and looks up an entry.
#
# A distinctive target name is used (not the production "kdbx-master") so the
# test cannot clobber a real credential, and the credential is always deleted
# afterwards.
#
# Usage:  pwsh tests/credential_manager_integration.ps1
# Exit:   0 on success, non-zero on failure.

$ErrorActionPreference = 'Stop'

$CsharpDir = Split-Path $PSScriptRoot -Parent
$DbPath = Join-Path $CsharpDir 'tests/KdbxCredentials.Tests/data/test.kdbx'
$CredId = 'kdbx-credentials-itest'   # distinctive; not the production key
$Account = 'kdbx-credentials'        # the fixed account convention
$EntryPath = 'ndb/postgres-prod'
$MasterPassword = 'test'             # the fixture's master password
$ExpectedUsername = 'pgadmin'        # username stored at $EntryPath

function Fail($msg) { Write-Error "FAIL: $msg"; exit 1 }

if (-not (Get-Command cmdkey -ErrorAction SilentlyContinue)) { Fail 'cmdkey not found' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet not found' }
if (-not (Test-Path $DbPath)) { Fail "test database missing: $DbPath" }

# Refuse to run if a credential with this target already exists, so we never
# overwrite (and then delete) something a human put there.
$existing = (cmdkey /list:$CredId 2>&1 | Out-String)
if ($existing -notmatch 'NONE') { Fail "credential '$CredId' already exists; aborting so it is not clobbered" }

Write-Host '==> Building kdbx-lookup example'
dotnet build --nologo -v quiet (Join-Path $CsharpDir 'examples/Lookup/Lookup.csproj')
if ($LASTEXITCODE -ne 0) { Fail 'dotnet build failed' }
$Dll = Join-Path $CsharpDir 'examples/Lookup/bin/Debug/net9.0/kdbx-lookup.dll'
if (-not (Test-Path $Dll)) { Fail "example not built: $Dll" }

try {
    Write-Host '==> Provisioning master password via cmdkey'
    cmdkey /generic:$CredId /user:$Account /pass:$MasterPassword | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'cmdkey provisioning failed' }

    Write-Host '==> Running kdbx-lookup example against the Credential Manager'
    $output = (dotnet $Dll $DbPath $CredId $EntryPath 2>&1 | Out-String)
    $code = $LASTEXITCODE

    Write-Host '---- example output ----'
    Write-Host $output
    Write-Host '------------------------'

    if ($code -ne 0) { Fail "example exited with code $code" }
    if ($output -notmatch 'OK: opened database') { Fail 'success marker not found in output' }
    if ($output -notmatch "username: $ExpectedUsername") { Fail "expected username '$ExpectedUsername' not found in output" }

    Write-Host 'PASS: master password retrieved from Credential Manager and database opened'
}
finally {
    cmdkey /delete:$CredId | Out-Null
}
