# KdbxCredentials

Read-only retrieval of credentials from a [KeePass](https://keepass.info/)
KDBX 4.x database, with the master password held in the OS-native secret store
(Windows Credential Manager / Linux systemd credentials). The database file
itself is never modified.

This is the C# implementation of a shared specification; a separate Rust crate
(`kdbx-credentials`) implements the same behaviour. Neither depends on the other.
See the `keepass-spec/` directory at the repository root for the canonical spec.

> **Consuming this from another .NET service?** See [`USAGE.md`](USAGE.md) — the `kdbx-services`
> key convention, a read example, and how to provision the secret into a `LocalSystem` service's
> vault (the TinyWeb-CGI case).

## Features

- Opens KDBX 4.0 / 4.1 databases (master password only — no key files).
- Retrieves the master password from the OS secret store at runtime; it never
  lives in source, config, or environment variables.
- Looks up entries by a case-insensitive `group/title` path.
- Returns the `UserName`, `Password`, `URL`, and `Notes` fields.
- Read-only: the library never writes to the `.kdbx` file.

## Target framework

`net9.0`, supported on Windows and Linux. macOS and KDBX 3.1 (or earlier) are
out of scope.

## Usage

```csharp
using KdbxCredentials;

using var db = Database.Open("/srv/secrets/team.kdbx", "kdbx-master");
using Entry entry = db.Lookup("ndb/postgres-prod");

if (entry.Password is not null)
{
    // use the password
}
```

Always wrap `Database` and `Entry` in `using` blocks (or call `Dispose`).

### Paths

Entries are addressed by a `/`-separated path. The implied root group must be
omitted, matching is case-insensitive, and surrounding whitespace on each
segment is ignored. Empty paths, single-segment paths (no group), and paths
beginning with `root/` throw `InvalidPathException`.

## Errors

Every failure is a subtype of `KdbxException`, mapping one-to-one onto the shared
error taxonomy (`keepass-spec/ERRORS.md`): `SecretNotFoundException`,
`DatabaseNotFoundException`, `DatabaseCorruptException`,
`AuthenticationFailedException`, `PermissionDeniedException`,
`InvalidPathException`, `EntryNotFoundException`, `AmbiguousEntryException`.
Catch the specific type to branch on the cause; messages never contain
credential material.

## Provisioning the secret store

The `secretStoreKey` you pass to `Open` names the credential. On Linux it must be
a valid systemd credential ID: a single name with no `/` (e.g. `kdbx-master`).

**Linux** — systemd credentials. There is no lookup-by-key API: systemd decrypts
the credential and hands it to your service as a file under
`$CREDENTIALS_DIRECTORY`. The library reads `$CREDENTIALS_DIRECTORY/<key>`
verbatim, so **store the password with no trailing newline**.

Encrypt the master password to the machine once, as an administrator, then grant
it to the unit that runs your task:

```sh
printf '%s' 'your-master-password' \
    | sudo systemd-creds encrypt --name=kdbx-master - /etc/credstore.encrypted/kdbx-master
```

```ini
[Service]
LoadCredentialEncrypted=kdbx-master
```

**Windows** (built-in Credential Manager). The key is the *target*; the *account*
is the fixed string `kdbx-credentials`:

```powershell
cmdkey /generic:"kdbx-master" /user:"kdbx-credentials" /pass
```

> Provision interactively. Do not bake the master password into an automated
> script. See `keepass-spec/SECURITY.md` for full provisioning and rotation
> guidance.

## Security and a note on zeroing

The master password is held as a `char[]` and zeroed once handed to the KDBX
composite key. However, **`Entry` field values are `string`**, which in .NET are
immutable and may be interned — they cannot be reliably zeroed. `Entry.Dispose`
drops the references so they become collectable, but this is weaker than the
Rust implementation's guarantee. This is a documented limitation; see
`keepass-spec/SECURITY.md`.

## Development

```sh
dotnet test          # unit + integration tests against data/test.kdbx (30 pass)
dotnet build -c Release

# Real end-to-end test of the systemd-credentials path (needs systemd + sudo):
tests/systemd_creds_integration.sh
```

The committed fixture `tests/KdbxCredentials.Tests/data/test.kdbx` is a
throwaway database with master password `test`; it contains no real credentials.

KDBX parsing uses **`pt.KeePassLibStd`**, a .NET Standard 2.0 port of the
official `KeePassLib` that keeps the same API but runs on `net9.0` cross-platform.
(The official `KeePassLib` NuGet package is .NET Framework only and throws
`TypeLoadException` at runtime on modern .NET.)

## License

MIT
