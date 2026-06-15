# KeePass Access Libraries

Two independent libraries that retrieve credentials from a read-only KeePass
(KDBX4) database, using a master password held in the OS-native secret store.
Both implement the same behavioural specification; neither depends on the other.

| Implementation | Location | Publishes to | Status |
|----------------|----------|--------------|--------|
| Rust crate `kdbx-credentials` | [`rust/kdbx-credentials/`](rust/kdbx-credentials/) | crates.io | Implemented and tested |
| C# package `KdbxCredentials`  | [`csharp/`](csharp/) | NuGet | Implemented; build/test pending a .NET SDK environment |

## The specification

The canonical spec lives in [`keepass-spec/`](keepass-spec/). **Start with
[`keepass-spec/SPEC.md`](keepass-spec/SPEC.md).**

- [`SPEC.md`](keepass-spec/SPEC.md) — database format, secret store, entry model, path lookup, API contract
- [`ERRORS.md`](keepass-spec/ERRORS.md) — the eight-variant error taxonomy
- [`SECURITY.md`](keepass-spec/SECURITY.md) — security contract, threat model, rotation
- [`RUST-NOTES.md`](keepass-spec/RUST-NOTES.md) / [`CSHARP-NOTES.md`](keepass-spec/CSHARP-NOTES.md) — per-language guidance

## What both libraries do

- Retrieve the master password from Windows Credential Manager / Linux Secret Service
- Open a KDBX 4.x database (master password only — no key files)
- Look up entries by case-insensitive `group/title` path
- Return `Username`, `Password`, `URL`, `Notes`
- Zero credential material on release (with a documented limitation in C#)
- Are strictly read-only

Out of scope: writing to the database, key-file auth, KDBX 3.1 or earlier, macOS,
and provisioning the OS secret store (documented, not automated).

## Provisioning convention

Both implementations look the secret up under **service = `secret_store_key`**
(e.g. `acme/keepass`) and **account = `kdbx-credentials`**. See each
implementation's README for the platform-specific provisioning commands.

## Building

```sh
# Rust
cd rust/kdbx-credentials && cargo test && cargo clippy --all-targets -- -D warnings

# C#  (requires the .NET 9 SDK)
cd csharp && dotnet test
```

Both projects share a throwaway KDBX4 test fixture (master password `test`) with
no real credentials.

## License

MIT — see [LICENSE](LICENSE).
