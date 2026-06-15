# kdbx-credentials

Read-only retrieval of credentials from a [KeePass](https://keepass.info/)
KDBX 4.x database, with the master password held in the OS-native secret store
(Windows Credential Manager / Linux Secret Service). The database file itself is
never modified.

This is the Rust implementation of a shared specification; a separate C# package
implements the same behaviour. Neither depends on the other. See the
`keepass-spec/` directory at the repository root for the canonical spec.

## Features

- Opens KDBX 4.0 / 4.1 databases (master password only — no key files).
- Retrieves the master password from the OS secret store at runtime; it never
  lives in source, config, or environment variables.
- Looks up entries by a case-insensitive `group/title` path.
- Returns the `UserName`, `Password`, `URL`, and `Notes` fields.
- Zeroes credential material from memory on drop (via [`zeroize`]).
- Read-only: the library never writes to the `.kdbx` file.

## Supported platforms

- `x86_64-pc-windows-msvc` — Windows Credential Manager
- `x86_64-unknown-linux-gnu` — Secret Service API (libsecret / GNOME Keyring)

macOS and KDBX 3.1 (or earlier) are out of scope.

## Usage

```rust
use std::path::Path;

let db = kdbx_credentials::open(Path::new("/srv/secrets/team.kdbx"), "acme/keepass")?;
let entry = kdbx_credentials::lookup(&db, "ndb/postgres-prod")?;

if let Some(password) = &entry.password {
    // use the password; `entry` zeroes its fields when dropped
}
# Ok::<(), kdbx_credentials::KdbxError>(())
```

### Paths

Entries are addressed by a `/`-separated path. The implied root group must be
omitted, matching is case-insensitive, and surrounding whitespace on each
segment is ignored:

| Path | Resolves to |
|------|-------------|
| `ndb/postgres-prod` | entry `postgres-prod` in group `ndb` |
| `NDB/Postgres-Prod` | same (case-insensitive) |
| `a/b/c/title` | entry `title` in nested group `a/b/c` |

Empty paths, single-segment paths (no group), and paths beginning with `root/`
are rejected with [`KdbxError::InvalidPath`].

## Errors

Every operation returns [`KdbxError`], whose variants map one-to-one onto the
shared error taxonomy (`keepass-spec/ERRORS.md`): `SecretNotFound`,
`DatabaseNotFound`, `DatabaseCorrupt`, `AuthenticationFailed`,
`PermissionDenied`, `InvalidPath`, `EntryNotFound`, `AmbiguousEntry`. Branch on
the variant — the `Display` string is for humans only and never contains
credential material.

## Provisioning the secret store

The master password must be deposited into the OS secret store once per machine,
by an administrator. The library reads it using:

- **service** = the `secret_store_key` you pass to `open` (e.g. `acme/keepass`)
- **account** = the fixed string `kdbx-credentials`

Pick a `secret_store_key` of the form `organisation/application` and keep it
consistent across machines.

**Linux** (with `secret-tool` from libsecret):

```sh
secret-tool store --label='acme/keepass' service 'acme/keepass' account 'kdbx-credentials'
# then type the master password when prompted
```

**Windows** (PowerShell, using the built-in Credential Manager):

```powershell
cmdkey /generic:"acme/keepass" /user:"kdbx-credentials" /pass
```

> Provision interactively. Do not bake the master password into an automated
> script. See `keepass-spec/SECURITY.md` for the full provisioning and rotation
> guidance.

On headless Linux machines a Secret Service daemon must be running (for example
`gnome-keyring-daemon --daemonize`).

## Security

Credential fields are zeroed on drop, the master password is held only
transiently during `open`, and errors never leak secret values. This does not
protect against a compromised process, swap/core-dump exposure, or an untrusted
`db_path`. See `keepass-spec/SECURITY.md` for the complete contract and its
limitations.

## Development

```sh
cargo test     # unit + integration tests against tests/data/test.kdbx
cargo clippy --all-targets
```

The committed fixture `tests/data/test.kdbx` is a throwaway database with master
password `test`. It contains no real credentials.

## License

MIT
