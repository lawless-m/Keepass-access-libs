# KeePass Credential Retrieval — Contents

## Start Here

Read `SPEC.md` first. It is the canonical definition of behaviour that both implementations must follow.

## Files

| File | Description |
|------|-------------|
| `SPEC.md` | **Start here.** The full behavioural specification — database format, OS secret store, entry model, path lookup rules, and API contract |
| `ERRORS.md` | Complete error taxonomy — all error types, their causes, and expected caller behaviour |
| `SECURITY.md` | Security contract — what the library guarantees, what it does not, threat model, and rotation procedure |
| `RUST-NOTES.md` | Rust-specific implementation guidance — recommended crates, API shape, error enum, publishing checklist |
| `CSHARP-NOTES.md` | C#-specific implementation guidance — recommended packages, API shape, exception types, platform notes, publishing checklist |
| `CONTENTS.md` | This file |

## Implementation Summary

Two independent libraries implementing the same specification:

- **Rust crate** — publish to crates.io. See `RUST-NOTES.md`
- **C# package** — publish to NuGet. See `CSHARP-NOTES.md`

Neither implementation references or depends on the other. The specification is the source of truth.

## Scope

Both libraries:
- Retrieve a master password from the OS secret store (Windows Credential Manager / Linux systemd credentials)
- Open a KDBX4 database file
- Look up entries by group path and title
- Return Username, Password, URL, Notes
- Zero credential material on release
- Are read-only — they never modify the database

## Out of Scope

- Writing to the database
- Key file authentication
- KDBX 3.1 or earlier
- macOS
- Provisioning the OS secret store (documented but not automated)
