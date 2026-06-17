#!/usr/bin/env bash
#
# Example Linux task: fetch a database password from KeePass and use it to
# connect to PostgreSQL from a script. The Linux counterpart of the Windows
# batch-file `--password %PW%` pattern.
#
# The same script works in both contexts, because `kdbx-getpass` resolves the
# master password the same way the library does:
#   - Run as the systemd service below, systemd has decrypted the master
#     password into $CREDENTIALS_DIRECTORY and getpass reads it there.
#   - Run by hand on a headless box, getpass reads it from the file store
#     (/etc/kdbx/<key>).
#
# Only the password is secret and comes from KeePass; the .kdbx path and the
# host/user/database are ordinary, non-secret config and live here.

set -euo pipefail

DB_FILE='/mnt/RIVSTS05_SOFTWARE/KeePass/MasterPasswords.kdbx'  # the .kdbx holding the entry
SECRET_KEY='kdbx-master'           # credential ID: systemd cred name AND /etc/kdbx/<key>
ENTRY='ndb/postgres-prod'          # group/title path of the entry
GETPASS='/usr/local/bin/kdbx-getpass'

# Non-secret connection details.
export PGHOST='db.internal'
export PGPORT='5432'
export PGUSER='pgadmin'
export PGDATABASE='prod'

# Fetch the password. Handed to psql via PGPASSWORD (the standard env channel for
# non-interactive psql), so it is not placed on a command line.
PGPASSWORD="$("$GETPASS" "$DB_FILE" "$SECRET_KEY" "$ENTRY")"
export PGPASSWORD

# Do the actual work.
psql --no-psqlrc -c 'SELECT now();'
