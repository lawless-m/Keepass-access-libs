# Changelog

All notable changes to this crate are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial implementation of the KeePass credential retrieval specification v1.0.
- `open(db_path, secret_store_key)` — retrieves the master password from the OS
  secret store and opens a KDBX 4.x database read-only.
- `lookup(db, path)` — case-insensitive `group/title` entry lookup returning
  `username`, `password`, `url`, and `notes`.
- `KdbxError` error taxonomy with eight programmatically distinguishable
  variants.
- Credential zeroing on drop for `Entry` via `zeroize`; redacting `Debug` impl.
- Integration tests against a committed KDBX4 fixture.
- Linux secret store backed by systemd credentials: the master password is read
  verbatim from `$CREDENTIALS_DIRECTORY/<secret_store_key>`, with the key
  validated as a single safe path component. `keyring` is now a Windows-only
  dependency, so the Linux build no longer pulls in the Secret Service/D-Bus
  stack.
- `examples/lookup.rs` and `tests/systemd_creds_integration.sh` exercising the
  real `systemd-creds` + `systemd-run` credential flow end-to-end.

### Changed

- On Linux the master password now comes from systemd credentials instead of the
  Secret Service API. `secret_store_key` must be a valid systemd credential ID
  (a single name, no `/`).
