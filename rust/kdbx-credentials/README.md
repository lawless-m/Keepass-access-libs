# kdbx-credentials

Read-only retrieval of credentials from a [KeePass](https://keepass.info/)
KDBX 4.x database, with the master password held in the OS-native secret store
(Windows Credential Manager / Linux systemd credentials). The database file
itself is never modified.

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
- `x86_64-unknown-linux-gnu` — systemd credentials (`$CREDENTIALS_DIRECTORY`)

macOS and KDBX 3.1 (or earlier) are out of scope.

## Usage

```rust
use std::path::Path;

let db = kdbx_credentials::open(Path::new("/srv/secrets/team.kdbx"), "kdbx-master")?;
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

The `secret_store_key` you pass to `open` names the credential. It must be a
valid systemd credential ID on Linux: a single name with no `/`, made of
alphanumerics plus `_`, `.`, `-` (e.g. `kdbx-master`). Keep it consistent across
machines.

**Linux** — systemd credentials. There is no lookup-by-key API: systemd decrypts
the credential and hands it to your service as a file under
`$CREDENTIALS_DIRECTORY`. The library reads `$CREDENTIALS_DIRECTORY/<key>`
verbatim, so **store the password with no trailing newline**.

Encrypt the master password to the machine (host key, or TPM with
`--with-key=tpm2`) once per machine, as an administrator:

```sh
printf '%s' 'your-master-password' \
    | sudo systemd-creds encrypt --name=kdbx-master - /etc/credstore.encrypted/kdbx-master
```

Then grant it to the unit that runs your task:

```ini
[Service]
LoadCredentialEncrypted=kdbx-master
# (systemd searches /etc/credstore.encrypted for a file named after the credential)
```

For an ad-hoc run, `systemd-run` works the same way:

```sh
sudo systemd-run --pipe --wait \
    -p User=svc-account \
    -p LoadCredentialEncrypted=kdbx-master:/etc/credstore.encrypted/kdbx-master \
    your-binary /srv/secrets/team.kdbx kdbx-master ndb/postgres-prod
```

**Windows** (PowerShell, using the built-in Credential Manager). The key is the
*target*; the *account* is the fixed string `kdbx-credentials`:

```powershell
cmdkey /generic:"kdbx-master" /user:"kdbx-credentials" /pass
```

> Provision interactively. Do not bake the master password into an automated
> script. See `keepass-spec/SECURITY.md` for the full provisioning and rotation
> guidance.

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

# Real end-to-end test of the systemd-credentials path (needs systemd + sudo):
tests/systemd_creds_integration.sh
```

The committed fixture `tests/data/test.kdbx` is a throwaway database with master
password `test`. It contains no real credentials.

## License

MIT
