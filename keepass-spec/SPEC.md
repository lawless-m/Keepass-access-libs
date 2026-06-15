# KeePass Credential Retrieval — Specification

Version: 1.0  
Status: Draft

## Overview

This specification defines the behaviour of a credential retrieval library that:

1. Retrieves a master password from the OS-native secret store
2. Opens a KDBX4 database file using that password
3. Looks up an entry by path
4. Returns the entry's credentials to the caller

Two independent implementations exist — one in Rust (published to crates.io) and one in C# (published to NuGet). Both must conform to this specification. Neither implementation should reference or depend on the other.

---

## Database Format

- Format: KDBX 4.0 or 4.1
- Authentication: master password only (no key file)
- The database is **read-only** — implementations MUST NOT write to or modify the database file
- The database is managed externally by KeePassXC

---

## OS Secret Store

The master password is retrieved from the OS-native secret store at runtime. It is never stored in configuration files, environment variables, or source code.

### Windows

Store: Windows Credential Manager  
Type: Generic credential  
Lookup key: configurable, see Configuration

### Linux

Store: Secret Service API (libsecret / GNOME Keyring)  
Lookup key: configurable, see Configuration

### Provisioning

The master password must be deposited into the OS secret store by an administrator as a one-time setup step on each approved machine. This is outside the scope of the library itself.

---

## Configuration

The library is configured via the following parameters. How these are supplied (function arguments, config file, environment variables) is left to each implementation's README.

| Parameter | Type | Description |
|-----------|------|-------------|
| `db_path` | string | Absolute path to the `.kdbx` file |
| `secret_store_key` | string | The key name used to look up the master password in the OS secret store |

The `secret_store_key` SHOULD follow the convention `organisation/application` (e.g. `acme/keepass`) to avoid collisions with other credentials on the machine. The exact value is agreed upon by the team and must be consistent across all machines.

---

## Entry Model

Each entry returned by the library contains exactly the following fields, all drawn from KeePass built-in fields:

| Field | KeePass field | Type | Nullable |
|-------|--------------|------|----------|
| `username` | `UserName` | string | yes |
| `password` | `Password` | string | yes |
| `url` | `URL` | string | yes |
| `notes` | `Notes` | string | yes |

`title` is used as the lookup key and is NOT returned as part of the entry.

All fields are returned as plain strings. The library decrypts the KeePass protected fields (such as `Password`) before returning them. Callers are responsible for treating the returned struct as sensitive.

---

## Path Lookup

### Format

Entries are identified by a `/`-separated path string:

```
group/title
nested/group/title
```

### Rules

- The root group is always implied and MUST NOT be included in the path
- The separator is `/` (forward slash)
- Matching is **case-insensitive**
- Leading and trailing whitespace on each segment is ignored
- An empty path, or a path with only one segment, is an error (see Errors)
- A path with no matching entry is an error (see Errors)
- A path that matches more than one entry is an error (see Errors) — this should not occur in a well-managed database but must be handled gracefully

### Examples

Given a database with the structure:

```
Root
├── ndb
│   ├── postgres-prod
│   └── mongo-reporting
├── storage
│   └── s3-backups
└── api
    └── github
```

Valid lookups:

```
"ndb/postgres-prod"
"storage/s3-backups"
"api/github"
"NDB/Postgres-Prod"        ← case-insensitive, resolves to ndb/postgres-prod
```

Invalid lookups:

```
"postgres-prod"            ← missing group, only one segment
"root/ndb/postgres-prod"   ← root must not be included
""                         ← empty path
```

---

## API

Both implementations must expose the following logical operations. Method/function naming may follow language conventions (snake_case in Rust, PascalCase in C#).

### `open(db_path, secret_store_key) -> Database`

Opens the database. Retrieves the master password from the OS secret store using `secret_store_key`, then opens the KDBX file at `db_path`.

The returned `Database` handle is used for subsequent lookups. Implementations MAY keep the database open in memory or re-open per lookup — this is an internal detail.

Errors: `SecretNotFound`, `DatabaseNotFound`, `DatabaseCorrupt`, `AuthenticationFailed`, `PermissionDenied`

### `lookup(database, path) -> Entry`

Looks up a single entry by path. Returns the populated `Entry` struct.

Errors: `InvalidPath`, `EntryNotFound`, `AmbiguousEntry`

### `close(database)`

Releases all resources held by the database handle. Implementations MUST zero all in-memory credential material on close. In languages with deterministic destruction (Rust's `Drop`), this MAY be implicit.

---

## Memory Safety

- Passwords and credentials retrieved from the database MUST be zeroed in memory when no longer needed
- The `Entry` struct MUST zero its fields on drop/dispose
- Implementations SHOULD use a verified zeroing mechanism (e.g. the `zeroize` crate in Rust, `SecureString` patterns in C#) rather than relying on the compiler not optimising away the clear

---

## Thread Safety

- `Database` handles are NOT required to be `Send` or thread-safe
- Callers requiring concurrent access must open separate handles
- This may be revisited in a future version

---

## Error Handling

See `ERRORS.md` for the full error taxonomy. Implementations must map all internal errors to the defined error types. Callers must be able to distinguish all error types programmatically — string-only errors are not acceptable.

---

## What This Library Does NOT Do

- Write to or modify the database
- Create or provision OS secret store entries
- Support key file authentication
- Support KDBX 3.1 or earlier (not required for KeePassXC-managed databases)
- Cache credentials between calls
- Manage database locking across processes
- Support in-memory databases

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0 | 2026-06-15 | Initial draft |
