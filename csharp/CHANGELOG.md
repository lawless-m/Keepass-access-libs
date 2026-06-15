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
  (Secret Service via `secret-tool`) implementations.
- Redacting `Entry.ToString()`; `Entry`/`Database` implement `IDisposable`.
- xUnit unit and integration tests against a committed KDBX4 fixture.
