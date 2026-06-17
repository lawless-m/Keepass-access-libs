#!/usr/bin/env bash
#
# End-to-end test of the Linux file-backed secret store.
#
# This covers the interactive / manually-run case on a headless box, where the
# process is NOT launched by systemd, so the master password is read from the
# permission-protected file store instead of $CREDENTIALS_DIRECTORY. It is the
# counterpart of systemd_creds_integration.sh (which covers the systemd case).
#
#   1. The master password is written to a file named by the credential ID, with
#      no trailing newline and restrictive permissions (here 0400 in a temp dir;
#      in production /etc/kdbx/<key>, root-owned, group-readable).
#   2. The `getpass` example reads it from there (KDBX_CREDENTIALS_DIR points at
#      the directory), opens the committed test.kdbx, and prints the looked-up
#      entry's password.
#
# Usage:  tests/file_store_integration.sh
# Exit:   0 on success, non-zero on failure.

set -euo pipefail

CRATE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB_PATH="$CRATE_DIR/tests/data/test.kdbx"
CRED_ID="kdbx-master"
ENTRY_PATH="ndb/postgres-prod"
MASTER_PASSWORD="test"              # the fixture's master password
EXPECTED_ENTRY_PASSWORD="s3cr3t-pg" # the password stored at $ENTRY_PATH

fail() { echo "FAIL: $*" >&2; exit 1; }

command -v cargo >/dev/null || fail "cargo not found"
[[ -f "$DB_PATH" ]]       || fail "test database missing: $DB_PATH"

echo "==> Building getpass example"
cargo build --quiet --manifest-path "$CRATE_DIR/Cargo.toml" --example getpass
BIN="$CRATE_DIR/target/debug/examples/getpass"
[[ -x "$BIN" ]] || fail "example binary not built: $BIN"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Store with no trailing newline (printf '%s'): the library reads verbatim.
printf '%s' "$MASTER_PASSWORD" > "$WORK/$CRED_ID"
chmod 0400 "$WORK/$CRED_ID"

# Force the file store: point KDBX_CREDENTIALS_DIR at our dir and make sure no
# systemd credentials directory is set so the library does not take that branch.
echo "==> Running getpass against the file store"
OUTPUT="$(env -u CREDENTIALS_DIRECTORY KDBX_CREDENTIALS_DIR="$WORK" \
    "$BIN" "$DB_PATH" "$CRED_ID" "$ENTRY_PATH" 2>&1)" \
    || { echo "$OUTPUT"; fail "getpass exited non-zero"; }

echo "---- getpass output ----"
echo "$OUTPUT"
echo "------------------------"

[[ "$OUTPUT" == "$EXPECTED_ENTRY_PASSWORD" ]] \
    || fail "expected entry password '$EXPECTED_ENTRY_PASSWORD', got '$OUTPUT'"

echo "PASS: master password retrieved from the file store and database opened"
