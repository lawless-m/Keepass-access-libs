# KeePass Credential Retrieval — Rust Implementation Notes

Version: 1.0  
Status: Draft

## Crate Name

Suggestion: `kdbx-credentials`  
Check crates.io for availability before settling on a name.

## Recommended Dependencies

| Crate | Purpose |
|-------|---------|
| `keepass-ng` | KDBX4 parsing and entry lookup. Preferred over `keepass` for better KDBX4 support and active maintenance |
| `zeroize` | Verified zeroing of credential memory. Derive `Zeroize` and `ZeroizeOnDrop` on the `Entry` struct |
| `keyring` | Cross-platform OS secret store access (Windows Credential Manager + Linux Secret Service). Abstracts the platform difference cleanly |
| `thiserror` | Ergonomic error type derivation |

## Target Platforms

- `x86_64-pc-windows-msvc` — Windows 11 / Server
- `x86_64-unknown-linux-gnu` — Debian Linux

No other platforms are required. macOS is explicitly out of scope.

## Public API Shape

```rust
pub struct Database { /* opaque */ }

pub struct Entry {
    pub username: Option<String>,
    pub password: Option<String>,
    pub url:      Option<String>,
    pub notes:    Option<String>,
}

// Entry must zero its fields on drop
// Derive: Zeroize, ZeroizeOnDrop

pub fn open(db_path: &Path, secret_store_key: &str) -> Result<Database, KdbxError>;

pub fn lookup(db: &Database, path: &str) -> Result<Entry, KdbxError>;

// Drop on Database must zero master password held in memory
```

## Error Enum

```rust
#[derive(Debug, thiserror::Error)]
pub enum KdbxError {
    #[error("secret not found in OS store for key '{0}'")]
    SecretNotFound(String),

    #[error("database file not found: {0}")]
    DatabaseNotFound(PathBuf),

    #[error("database file is corrupt or unsupported format")]
    DatabaseCorrupt(#[source] Box<dyn std::error::Error + Send + Sync>),

    #[error("authentication failed — master password may be stale")]
    AuthenticationFailed,

    #[error("permission denied accessing database or secret store")]
    PermissionDenied,

    #[error("invalid path '{0}': {1}")]
    InvalidPath(String, &'static str),

    #[error("entry not found at path '{0}'")]
    EntryNotFound(String),

    #[error("ambiguous path '{0}' — multiple entries match")]
    AmbiguousEntry(String),
}
```

## Path Parsing

Split on `/`, trim whitespace from each segment, lowercase all segments for case-insensitive comparison. Validate before lookup:

- Reject empty string
- Reject single segment (no group)
- Reject first segment equal to `"root"` (case-insensitive)

The `keepass-ng` `Group::get()` method takes a `&[&str]` slice of path segments. Pass all segments except the last as the group path, and match the final segment against entry titles within that group (case-insensitively, since `get()` is case-sensitive by default).

## Secret Store

Use the `keyring` crate. On Linux this talks to the Secret Service API over D-Bus. On headless Linux machines without a running Secret Service, consider whether `keyring` with a file-based fallback is acceptable, or document that a Secret Service daemon (e.g. `gnome-keyring-daemon --daemonize`) must be running.

## Publishing Checklist

- [ ] `README.md` with usage example and provisioning instructions
- [ ] `LICENSE` (MIT or Apache-2.0 or both — conventional for Rust crates)
- [ ] All public items documented with `///` doc comments
- [ ] `cargo clippy` clean
- [ ] `cargo test` with an integration test against a test `.kdbx` file (committed to the repo, password `test` or similar — not a real database)
- [ ] `CHANGELOG.md`
- [ ] Check crates.io name availability before first publish

## Notes

- The `keepass-ng` path lookup (`Group::get`) is case-sensitive. Our case-insensitive behaviour must be implemented on top by normalising segments before lookup or by walking the group tree manually
- Do not enable the `save_kdbx4` feature of `keepass-ng` — we have no write requirement and a smaller feature set reduces attack surface
- Consider a `#[cfg(target_os = "linux")]` / `#[cfg(target_os = "windows")]` guard on the secret store module to make platform-specific code explicit
