#!/usr/bin/env bash
#
# End-to-end test of the Linux systemd-credentials secret store (C#).
#
# Mirrors the Rust crate's test: it exercises the real mechanism the library
# relies on in production.
#
#   1. `systemd-creds encrypt` encrypts the master password to the host key.
#   2. `systemd-run` launches the `kdbx-lookup` example as a transient *system*
#      service with `LoadCredentialEncrypted=`, so PID 1 decrypts the credential
#      and exposes it under $CREDENTIALS_DIRECTORY.
#   3. The example (running as the invoking user) reads that credential, opens
#      the committed test.kdbx, and looks up an entry.
#
# Encrypting/decrypting with the host key requires root, so the encrypt step and
# the system-manager `systemd-run` are invoked via sudo. The service itself runs
# as the invoking user (User=), with DOTNET_ROOT set so the framework-dependent
# app can find the .NET runtime.
#
# Usage:  tests/systemd_creds_integration.sh
# Exit:   0 on success, non-zero on failure.

set -euo pipefail

CSHARP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB_PATH="$CSHARP_DIR/tests/KdbxCredentials.Tests/data/test.kdbx"
CRED_ID="kdbx-master"
ENTRY_PATH="ndb/postgres-prod"
MASTER_PASSWORD="test"        # the fixture's master password
EXPECTED_USERNAME="pgadmin"   # username stored at $ENTRY_PATH

fail() { echo "FAIL: $*" >&2; exit 1; }

command -v systemd-creds >/dev/null || fail "systemd-creds not found"
command -v systemd-run   >/dev/null || fail "systemd-run not found"
command -v dotnet        >/dev/null || fail "dotnet not found"
[[ -f "$DB_PATH" ]]              || fail "test database missing: $DB_PATH"

DOTNET_BIN="$(command -v dotnet)"
DOTNET_ROOT_DIR="$(dirname "$(readlink -f "$DOTNET_BIN")")"

echo "==> Building kdbx-lookup example"
dotnet build --nologo -v quiet "$CSHARP_DIR/examples/Lookup/Lookup.csproj"
DLL="$CSHARP_DIR/examples/Lookup/bin/Debug/net9.0/kdbx-lookup.dll"
[[ -f "$DLL" ]] || fail "example not built: $DLL"

WORK="$(mktemp -d)"
CRED_FILE="$WORK/$CRED_ID.cred"
trap 'sudo rm -rf "$WORK"' EXIT

echo "==> Encrypting master password to host key (sudo systemd-creds encrypt)"
printf '%s' "$MASTER_PASSWORD" \
    | sudo systemd-creds encrypt --name="$CRED_ID" - "$CRED_FILE"
sudo test -s "$CRED_FILE" || fail "encrypted credential not produced"

echo "==> Launching example under systemd-run with LoadCredentialEncrypted"
OUTPUT="$(sudo systemd-run --pipe --wait --collect \
    -p "User=$(id -un)" \
    -p "Environment=DOTNET_ROOT=$DOTNET_ROOT_DIR" \
    -p "Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1" \
    -p "LoadCredentialEncrypted=$CRED_ID:$CRED_FILE" \
    "$DOTNET_BIN" "$DLL" "$DB_PATH" "$CRED_ID" "$ENTRY_PATH" 2>&1)" \
    || { echo "$OUTPUT"; fail "systemd-run unit exited non-zero"; }

echo "---- service output ----"
echo "$OUTPUT"
echo "------------------------"

grep -q "OK: opened database via systemd credential '$CRED_ID'" <<<"$OUTPUT" \
    || fail "success marker not found in output"
grep -q "username: $EXPECTED_USERNAME" <<<"$OUTPUT" \
    || fail "expected username '$EXPECTED_USERNAME' not found in output"

echo "PASS: master password retrieved from systemd credential and database opened"
