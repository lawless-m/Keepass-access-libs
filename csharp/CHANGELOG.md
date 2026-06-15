# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial implementation of the KeePass credential retrieval specification v1.0.
- `Database.Open(dbPath, secretStoreKey)` — retrieves the master password from
  the OS secret store and opens a KDBX 4.x database read-only.
- `Database.Lookup(path)` — case-insensitive `group/title` entry lookup
  returning `Username`, `Password`, `Url`, and `Notes`.
- `KdbxException` hierarchy with eight distinct exception types.
- `ISecretStore` with Windows (Credential Manager via `CredRead`) and Linux
  (systemd credentials) implementations.
- Redacting `Entry.ToString()`; `Entry`/`Database` implement `IDisposable`.
- xUnit unit and integration tests against a committed KDBX4 fixture.

### Changed

- The Linux secret store now reads the master password from systemd credentials
  (`$CREDENTIALS_DIRECTORY/<secretStoreKey>`, verbatim) instead of the Secret
  Service `secret-tool`. The key is validated as a single safe path component,
  and `secretStoreKey` must be a valid systemd credential ID (no `/`).
- Replaced the KDBX dependency with `pt.KeePassLibStd` (a .NET Standard 2.0 port
  of `KeePassLib` that runs on `net9.0` cross-platform). The original
  `KeePassLib 2.55.0` did not exist on NuGet, and `2.30.0` targets .NET Framework
  and throws `TypeLoadException` (`System.Security.Cryptography.ProtectedMemory`)
  at runtime on `net9.0`. All 30 tests now pass on Linux.
- Added the `kdbx-lookup` example and `tests/systemd_creds_integration.sh`,
  verifying the real `systemd-creds` + `systemd-run` credential flow end-to-end.
