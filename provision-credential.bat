@echo off
rem Run the credential provisioning script. Put the master password in C:\kdbx.txt
rem first; the script writes it to this account's vault and shreds the file.
pwsh -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Matthew.Heath\Git\Keepass-access-libs\provision-credential.ps1" %*
