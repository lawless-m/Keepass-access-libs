#!/usr/bin/env bash
#
# Build the Rust credential tool in release mode and install it.
#
# Builds the `getfield` example from the kdbx-credentials crate and installs it
# as `kdbx-getfield`. It does NOT provision the master password — see the README
# for the `/etc/kdbx/<key>` step.
#
# Usage:  ./build-install.sh
# Env:    BINDIR   install location (default /usr/local/bin)
# Exit:   0 on success, non-zero on failure.

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CRATE_DIR="$REPO_DIR/rust/kdbx-credentials"
BINDIR="${BINDIR:-/usr/local/bin}"

fail() { echo "FAIL: $*" >&2; exit 1; }

command -v cargo >/dev/null || fail "cargo not found — install the Rust toolchain (https://rustup.rs)"
[[ -f "$CRATE_DIR/Cargo.toml" ]] || fail "crate not found at $CRATE_DIR"

# install(1) needs root only when BINDIR is not writable by the current user.
if [[ -w "$BINDIR" ]]; then SUDO=""; else SUDO="sudo"; fi

echo "==> Building getfield (release)"
cargo build --release --quiet --manifest-path "$CRATE_DIR/Cargo.toml" --example getfield
bin="$CRATE_DIR/target/release/examples/getfield"
[[ -x "$bin" ]] || fail "build produced no binary: $bin"

dest="$BINDIR/kdbx-getfield"
echo "==> Installing $dest"
$SUDO install -m 0755 "$bin" "$dest"

echo "PASS: installed kdbx-getfield to $BINDIR"
echo "Next: provision the master password at /etc/kdbx/<key> (see README)."
