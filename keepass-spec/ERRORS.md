# KeePass Credential Retrieval — Error Taxonomy

Version: 1.0  
Status: Draft

## Overview

All errors are distinct, programmatically distinguishable types. String messages are optional and for human consumption only. Callers MUST be able to branch on error type without parsing strings.

In Rust this means an `enum` with variants. In C# this means separate exception types or a discriminated union pattern.

---

## Error Types

### `SecretNotFound`

The OS secret store was queried for `secret_store_key` and returned nothing.

**Likely cause:** The machine has not been provisioned. An administrator needs to deposit the master password into the OS credential store.

**Caller action:** Fatal. Cannot proceed without the master password.

---

### `DatabaseNotFound`

The file at `db_path` does not exist or the path is not a file.

**Likely cause:** Misconfiguration, the database has been moved, or the path refers to a network share that is not currently mounted.

**Caller action:** Fatal. Check configuration.

---

### `DatabaseCorrupt`

The file at `db_path` exists and is readable but cannot be parsed as a valid KDBX4 database.

**Likely cause:** File is truncated, corrupted, or is not a KeePass database at all. May also occur if the file is KDBX 3.1 or earlier, which is not supported.

**Caller action:** Fatal. The database needs to be repaired or recreated.

---

### `AuthenticationFailed`

The master password retrieved from the OS secret store was rejected by the KDBX database.

**Likely cause:** The master password in the OS store is stale — the database password was changed in KeePassXC but the OS store was not updated, or the wrong `secret_store_key` was used.

**Caller action:** Fatal. An administrator needs to update the OS secret store entry.

---

### `PermissionDenied`

The process does not have permission to read the database file, or does not have permission to query the OS secret store.

**Likely cause:** File ACL or OS secret store ACL is misconfigured, or the scheduled task is running under the wrong identity.

**Caller action:** Fatal. Check file and secret store permissions.

---

### `InvalidPath`

The path string supplied to `lookup` is malformed.

Specific cases:

| Sub-case | Description |
|----------|-------------|
| Empty | The path is an empty string or whitespace only |
| Single segment | The path contains no `/` separator — only a title with no group |
| Root included | The path begins with `root/` — the root group must not be included |

**Caller action:** Programming error. Fix the path string.

---

### `EntryNotFound`

The path is valid but no entry exists at that location in the database.

**Likely cause:** The entry has been renamed, moved, or deleted in KeePassXC, or the path string is wrong.

**Caller action:** Check the path against the current database contents in KeePassXC.

---

### `AmbiguousEntry`

The path resolves to more than one entry. This should not occur in a well-managed database but must be handled.

**Likely cause:** Duplicate entry titles within the same group in the database.

**Caller action:** Resolve the duplicate in KeePassXC. Entry titles within a group should be unique.

---

## Error Propagation

- Errors from `open` are terminal — if `open` fails, no lookup can proceed
- Errors from `lookup` are per-call — a failed lookup does not invalidate the database handle
- Implementations MUST NOT swallow errors silently or convert them to empty/null returns
- Implementations MUST NOT expose raw internal errors (e.g. underlying crypto errors) directly — these should be wrapped as `DatabaseCorrupt` or `AuthenticationFailed` as appropriate, with the original error available as a cause/inner error for diagnostic purposes

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0 | 2026-06-15 | Initial draft |
