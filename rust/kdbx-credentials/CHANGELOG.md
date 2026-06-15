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
