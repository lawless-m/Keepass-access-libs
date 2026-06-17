#!/usr/bin/env pwsh
#
# Provision the KeePass master password into the CURRENT account's Windows
# Credential Manager vault, so kdbx-getfield (and the C# library) can read it.
#
# Why this exists: Credential Manager generic credentials are per-user and
# DPAPI-protected. A secret you store as your interactive login is invisible to a
# service account such as scheduler.service.ac. This script is meant to run ONCE
# *as the account that will run the scheduled task/service* - Task Scheduler runs
# it in that account's logon session, so the credential lands in the correct
# vault. For an account you can log into interactively (e.g. mh.admin) you can
# just run plain `cmdkey` instead; this script is for the headless service case.
#
# It writes the exact shape the reader looks up
# (rust/kdbx-credentials/src/secret_store.rs):
#     target = <Key>    user = kdbx-credentials    type = generic
#
# The password is NEVER hard-coded. It is read from -PasswordFile (default
# C:\kdbx.txt - a temporary file you create just before running) and that file is
# shredded afterwards, so the plaintext exists only transiently and never lands
# in the task definition.
#
# Usage (the action of a one-shot scheduled task, run as the service account).
# With the defaults you can drop -PasswordFile/-Key entirely:
#     pwsh -NoProfile -ExecutionPolicy Bypass -File provision-credential.ps1
# Put the master password in C:\kdbx.txt first; the script shreds it on success.
#
# Exit code 0 = credential written; non-zero = failed (check -LogFile, since a
# headless task has no console).

[CmdletBinding()]
param(
    [string] $PasswordFile = 'C:\kdbx.txt',
    [string] $Key     = 'kdbx-master',
    [string] $Account = 'kdbx-credentials',
    # The OS account whose vault is being provisioned, used in the log output.
    # Defaults to the account actually running the script. Note: this is reported
    # only; the credential always lands in the vault of whoever runs the process.
    [string] $User    = "$env:USERDOMAIN\$env:USERNAME",
    [string] $LogFile
)

$ErrorActionPreference = 'Stop'

function Log($msg) {
    $line = '{0}  {1}' -f (Get-Date -Format 's'), $msg
    Write-Host $line
    if ($LogFile) { Add-Content -LiteralPath $LogFile -Value $line }
}

try {
    Log "Provisioning '$Key' (account '$Account') as $User"

    if (-not (Test-Path -LiteralPath $PasswordFile)) {
        throw "password file not found: $PasswordFile"
    }

    # Read the password and strip a single trailing newline a text editor may add.
    # Everything else is taken verbatim - the reader does not trim, so the stored
    # secret must match the database password exactly.
    $pw = (Get-Content -LiteralPath $PasswordFile -Raw) -replace '(\r?\n)\z', ''
    if ([string]::IsNullOrEmpty($pw)) { throw "password file is empty: $PasswordFile" }

    # Write the generic credential into THIS account's vault. cmdkey's generic
    # credentials are the same shape kdbx-getfield reads (verified against the
    # keyring reader). The password is briefly visible on cmdkey's command line -
    # acceptable for a one-shot provisioning step run as the target account.
    & cmdkey /generic:$Key /user:$Account /pass:$pw | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "cmdkey failed with exit code $LASTEXITCODE" }

    # Confirm it is present (metadata only; cmdkey never prints the secret).
    if (-not ((& cmdkey /list:$Key) -match [regex]::Escape($Key))) {
        throw "credential '$Key' not found after write"
    }
    Log "PASS: credential '$Key' written to the vault of $User"
    $exit = 0
}
catch {
    Log "FAIL: $_"
    $exit = 1
}
finally {
    # Shred the plaintext password file: overwrite with zeros, then delete.
    if (Test-Path -LiteralPath $PasswordFile) {
        try {
            $len = (Get-Item -LiteralPath $PasswordFile).Length
            if ($len -gt 0) {
                [System.IO.File]::WriteAllBytes($PasswordFile, (New-Object byte[] $len))
            }
            Remove-Item -LiteralPath $PasswordFile -Force
            Log "Shredded password file: $PasswordFile"
        }
        catch { Log "WARN: could not shred $PasswordFile - $_" }
    }
}

exit $exit
