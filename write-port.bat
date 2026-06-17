@echo off
rem Read the Keycloak port from the KeePass DB and write it to C:\port.txt.
rem Requires: the master password provisioned in this account's vault
rem (run provision-credential.bat first) and read access to the DB path.
rem Note: use a UNC path here for a scheduled task - mapped drives like Y:
rem do not exist in a non-interactive service session.
C:\Users\matthew.heath\AppData\Local\Microsoft\WindowsApps\kdbx-getfield.exe "\\rivsts05\Software\KeePass\MasterPasswords.kdbx" kdbx-master ROCS/Keycloak notes > C:\port.txt
