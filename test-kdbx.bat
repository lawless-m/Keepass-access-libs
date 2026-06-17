@echo off
rem Read the Keycloak port from the KeePass DB and write it to C:\test-kdbx.txt.
rem Requires: the master password provisioned in this account's vault
rem (run provision-credential.bat first) and read access to the DB path.
rem Note: use a UNC path here for a scheduled task - mapped drives like Y:
rem do not exist in a non-interactive service session.
"C:\Program Files\kdbx\kdbx-getfield.exe" "\\rivsprod02\RI Services\Credentials\ServicePasswords.kdbx" kdbx-services ROCS/Keycloak username
