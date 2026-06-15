# KdbxCredentials

Read-only retrieval of credentials from a [KeePass](https://keepass.info/)
KDBX 4.x database, with the master password held in the OS-native secret store
(Windows Credential Manager / Linux Secret Service). The database file itself is
never modified.

This is the C# implementation of a shared specification; a separate Rust crate
(`kdbx-credentials`) implements the same behaviour. Neither depends on the other.
See the `keepass-spec/` directory at the repository root for the canonical spec.

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

using var db = Database.Open("/srv/secrets/team.kdbx", "acme/keepass");
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

The master password must be deposited into the OS secret store once per machine,
by an administrator. The library reads it using:

- **service / target** = the `secretStoreKey` you pass to `Open` (e.g. `acme/keepass`)
- **account** = the fixed string `kdbx-credentials`

**Linux** (libsecret's `secret-tool`, which must be installed and on `PATH`):

```sh
secret-tool store --label='acme/keepass' service 'acme/keepass' account 'kdbx-credentials'
# then type the master password when prompted
```

A Secret Service daemon must be running (for example
`gnome-keyring-daemon --daemonize` on headless machines).

**Windows** (built-in Credential Manager):

```powershell
cmdkey /generic:"acme/keepass" /user:"kdbx-credentials" /pass
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
dotnet test          # unit + integration tests against data/test.kdbx
dotnet build -c Release
```

The committed fixture `tests/KdbxCredentials.Tests/data/test.kdbx` is a
throwaway database with master password `test`; it contains no real credentials.

> Note: the `KeePassLib` NuGet package version in the `.csproj` should be
> verified against what is published, and the secret-store implementations
> (Win32 `CredRead` / `secret-tool`) should be exercised on each platform before
> the first release.

## License

MIT
