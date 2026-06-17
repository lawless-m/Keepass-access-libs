@echo off
rem Provision the KeePass master password into the service account's Credential
rem Manager vault by running provision-credential.bat AS that account via runas.
rem
rem You are prompted for the SERVICE ACCOUNT password - runas cannot take it
rem non-interactively (there is no /pass), so this is the interactive one-shot
rem alternative to a scheduled task. Put the master password in C:\kdbx.txt first;
rem the inner script reads and shreds it.
rem
rem Override the account as the first argument; defaults to the standard one.
rem
rem Notes:
rem   - runas cannot launch a .bat directly (it needs an executable), so the
rem     command is routed through `cmd`.
rem   - %~dps0 is this folder's 8.3 short path (no spaces), which sidesteps the
rem     quoting problems a "C:\Program Files\..." path would cause through runas.
rem   - cmd /k keeps the window open so you can read PASS/FAIL; close it after.
rem   - If the service account is denied interactive logon by policy, runas fails
rem     with access denied; use the scheduled-task method instead.

setlocal
set "SVC=%~1"
if "%SVC%"=="" set "SVC=NISAINT\scheduler.service.ac"

runas /user:%SVC% "cmd /k %~dps0provision-credential.bat"
