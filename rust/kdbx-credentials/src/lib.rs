//! Read-only retrieval of credentials from a KeePass (KDBX4) database.
//!
//! `kdbx-credentials` opens a KDBX 4.x database using a master password held in
//! the OS-native secret store (Windows Credential Manager / Linux Secret
//! Service) and looks up entries by a `/`-separated `group/title` path. It is
//! one of two implementations of a shared specification; the other is a C#
//! package. Neither depends on the other.
//!
//! # Example
//!
//! ```no_run
//! use std::path::Path;
//!
//! let db = kdbx_credentials::open(Path::new("/srv/secrets/team.kdbx"), "acme/keepass")?;
//! let entry = kdbx_credentials::lookup(&db, "ndb/postgres-prod")?;
//! if let Some(password) = &entry.password {
//!     // use the password — it is zeroed when `entry` is dropped
//! }
//! # Ok::<(), kdbx_credentials::KdbxError>(())
//! ```
//!
//! # Security
//!
//! - Credential fields on [`Entry`] are zeroed on drop via [`zeroize`].
//! - The master password is held only transiently during [`open`] and is zeroed
//!   immediately afterwards; the [`Database`] handle does not retain it.
//! - The database is opened **read-only** — this crate never writes to the file.
//!
//! See `SECURITY.md` for the full security contract and its limitations.

#![forbid(unsafe_code)]
#![warn(missing_docs)]

mod error;
mod path;
mod secret_store;

pub use error::KdbxError;

use std::fs::File;
use std::path::Path;

use keepass_ng::db::{
    Database as KpDatabase, Entry as KpEntry, Node, NodePtr, group_get_children, node_is_entry,
    node_is_group, with_node,
};
use keepass_ng::{DatabaseKey, DatabaseOpenError};
use zeroize::{Zeroize, ZeroizeOnDrop};

/// A single credential entry, with all four built-in fields the specification
/// exposes. Every field is optional, mirroring KeePass.
///
/// The fields are zeroed from memory when the `Entry` is dropped. Callers should
/// treat the struct as sensitive and keep it alive no longer than necessary.
#[derive(Default, Zeroize, ZeroizeOnDrop)]
pub struct Entry {
    /// The `UserName` field.
    pub username: Option<String>,
    /// The `Password` field, decrypted from its KeePass protected form.
    pub password: Option<String>,
    /// The `URL` field.
    pub url: Option<String>,
    /// The `Notes` field.
    pub notes: Option<String>,
}

// A redacting `Debug` impl: it reveals which fields are present but never their
// values, so credentials cannot leak through logging or diagnostics.
impl std::fmt::Debug for Entry {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let redact = |o: &Option<String>| {
            if o.is_some() {
                "Some(<redacted>)"
            } else {
                "None"
            }
        };
        f.debug_struct("Entry")
            .field("username", &format_args!("{}", redact(&self.username)))
            .field("password", &format_args!("{}", redact(&self.password)))
            .field("url", &format_args!("{}", redact(&self.url)))
            .field("notes", &format_args!("{}", redact(&self.notes)))
            .finish()
    }
}

/// An open, decrypted database handle.
///
/// Obtained from [`open`] and passed to [`lookup`]. The handle is **not**
/// thread-safe (`!Send`, `!Sync`); callers needing concurrent access must open
/// separate handles. Resources are released, and any retained credential
/// material is zeroed, when the handle is dropped.
pub struct Database {
    inner: KpDatabase,
}

/// Open the database at `db_path`, authenticating with the master password
/// stored under `secret_store_key` in the OS secret store.
///
/// # Errors
///
/// - [`KdbxError::SecretNotFound`] — the secret store has no entry for the key.
/// - [`KdbxError::DatabaseNotFound`] — the path does not exist or is not a file.
/// - [`KdbxError::DatabaseCorrupt`] — the file is not a valid KDBX4 database.
/// - [`KdbxError::AuthenticationFailed`] — the master password was rejected.
/// - [`KdbxError::PermissionDenied`] — the file or secret store is not readable.
pub fn open(db_path: &Path, secret_store_key: &str) -> Result<Database, KdbxError> {
    // Fetch the master password first: without it nothing can proceed, and we
    // avoid touching the database on a misprovisioned machine. The value is
    // zeroed when it drops at the end of this function.
    let master = secret_store::get_master_password(secret_store_key)?;
    open_with_password(db_path, master.as_str())
}

/// Open the database using an already-resolved master password.
///
/// This holds the actual KDBX-opening logic, separate from secret-store
/// retrieval, so it can be exercised in tests without a provisioned OS store.
fn open_with_password(db_path: &Path, password: &str) -> Result<Database, KdbxError> {
    let metadata = std::fs::metadata(db_path).map_err(|e| map_io_error(db_path, e))?;
    if !metadata.is_file() {
        return Err(KdbxError::DatabaseNotFound(db_path.to_path_buf()));
    }

    let mut file = File::open(db_path).map_err(|e| map_io_error(db_path, e))?;

    let key = DatabaseKey::new().with_password(password);
    let inner = KpDatabase::open(&mut file, key).map_err(map_open_error)?;

    Ok(Database { inner })
}

/// Look up a single entry by its `group/title` path.
///
/// Matching is case-insensitive and the implied root group must be omitted.
/// See `SPEC.md` § Path Lookup for the full rules.
///
/// # Errors
///
/// - [`KdbxError::InvalidPath`] — the path is empty, single-segment, or rooted.
/// - [`KdbxError::EntryNotFound`] — the path is valid but matches no entry.
/// - [`KdbxError::AmbiguousEntry`] — the path matches more than one entry.
pub fn lookup(db: &Database, path: &str) -> Result<Entry, KdbxError> {
    let parsed = path::parse(path)?;

    let root = NodePtr::from(&db.inner.root);
    let group = match find_group(&root, &parsed.groups) {
        Some(g) => g,
        None => return Err(KdbxError::EntryNotFound(path.to_string())),
    };

    let mut matches = matching_entries(&group, &parsed.title);
    match matches.len() {
        0 => Err(KdbxError::EntryNotFound(path.to_string())),
        1 => Ok(build_entry(&matches.remove(0))),
        _ => Err(KdbxError::AmbiguousEntry(path.to_string())),
    }
}

/// Walk the group tree from `root`, following each (already lowercased) segment
/// by case-insensitive name match. Returns the final group, or `None` if any
/// segment has no matching child group.
fn find_group(root: &NodePtr, groups: &[String]) -> Option<NodePtr> {
    let mut current = root.clone();
    for segment in groups {
        let next = group_get_children(&current)?
            .into_iter()
            .find(|child| node_is_group(child) && title_matches(child, segment))?;
        current = next;
    }
    Some(current)
}

/// Collect every direct child entry of `group` whose title matches `title`
/// (case-insensitively).
fn matching_entries(group: &NodePtr, title: &str) -> Vec<NodePtr> {
    group_get_children(group)
        .unwrap_or_default()
        .into_iter()
        .filter(|child| node_is_entry(child) && title_matches(child, title))
        .collect()
}

/// Case-insensitive comparison of a node's title against an already-lowercased
/// `target`.
fn title_matches(node: &NodePtr, target: &str) -> bool {
    node.borrow()
        .get_title()
        .map(|t| t.to_lowercase() == target)
        .unwrap_or(false)
}

/// Read the four built-in fields from a KeePass entry node into our [`Entry`].
fn build_entry(node: &NodePtr) -> Entry {
    with_node::<KpEntry, _, _>(node, |e| Entry {
        username: e.get_username().map(str::to_string),
        password: e.get_password().map(str::to_string),
        url: e.get_url().map(str::to_string),
        notes: e.get_notes().map(str::to_string),
    })
    .unwrap_or_default()
}

/// Map a filesystem error encountered while locating/opening the database.
fn map_io_error(db_path: &Path, e: std::io::Error) -> KdbxError {
    match e.kind() {
        std::io::ErrorKind::NotFound => KdbxError::DatabaseNotFound(db_path.to_path_buf()),
        std::io::ErrorKind::PermissionDenied => KdbxError::PermissionDenied,
        _ => KdbxError::DatabaseCorrupt(Box::new(e)),
    }
}

/// Map a `keepass-ng` open error onto the crate's taxonomy, without surfacing
/// raw cryptographic details.
fn map_open_error(e: DatabaseOpenError) -> KdbxError {
    match e {
        // A password-only database rejects a wrong key as a key error.
        DatabaseOpenError::Key(_) => KdbxError::AuthenticationFailed,
        DatabaseOpenError::Io(io) => match io.kind() {
            std::io::ErrorKind::PermissionDenied => KdbxError::PermissionDenied,
            _ => KdbxError::DatabaseCorrupt(Box::new(io)),
        },
        DatabaseOpenError::DatabaseIntegrity(inner) => KdbxError::DatabaseCorrupt(Box::new(inner)),
        DatabaseOpenError::UnsupportedVersion => KdbxError::DatabaseCorrupt(
            "unsupported KDBX version (KDBX 3.1 or earlier is not supported)".into(),
        ),
    }
}

#[cfg(test)]
mod tests {
    //! Integration-style tests that open a real KDBX4 fixture
    //! (`tests/data/test.kdbx`, master password `test`) and exercise lookup,
    //! error mapping, and field decryption. The secret-store path is bypassed
    //! via [`open_with_password`] so the tests need no provisioned OS store.

    use super::*;
    use std::path::PathBuf;

    const PASSWORD: &str = "test";

    fn fixture() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("tests/data/test.kdbx")
    }

    fn open_fixture() -> Database {
        open_with_password(&fixture(), PASSWORD).expect("fixture should open")
    }

    #[test]
    fn opens_and_reads_all_fields() {
        let db = open_fixture();
        let entry = lookup(&db, "ndb/postgres-prod").unwrap();
        assert_eq!(entry.username.as_deref(), Some("pgadmin"));
        assert_eq!(entry.password.as_deref(), Some("s3cr3t-pg"));
        assert_eq!(
            entry.url.as_deref(),
            Some("postgres://db.internal:5432/prod")
        );
        assert_eq!(entry.notes.as_deref(), Some("Production Postgres"));
    }

    #[test]
    fn lookup_is_case_insensitive() {
        let db = open_fixture();
        let entry = lookup(&db, "NDB/Postgres-Prod").unwrap();
        assert_eq!(entry.username.as_deref(), Some("pgadmin"));
    }

    #[test]
    fn nested_and_other_groups_resolve() {
        let db = open_fixture();
        assert_eq!(
            lookup(&db, "storage/s3-backups")
                .unwrap()
                .username
                .as_deref(),
            Some("AKIAEXAMPLE")
        );
        assert_eq!(
            lookup(&db, "api/github").unwrap().username.as_deref(),
            Some("ci-bot")
        );
    }

    #[test]
    fn missing_entry_and_group() {
        let db = open_fixture();
        assert!(matches!(
            lookup(&db, "ndb/does-not-exist"),
            Err(KdbxError::EntryNotFound(_))
        ));
        assert!(matches!(
            lookup(&db, "nosuchgroup/whatever"),
            Err(KdbxError::EntryNotFound(_))
        ));
    }

    #[test]
    fn duplicate_titles_are_ambiguous() {
        let db = open_fixture();
        assert!(matches!(
            lookup(&db, "dup/duplicate"),
            Err(KdbxError::AmbiguousEntry(_))
        ));
    }

    #[test]
    fn invalid_paths_are_rejected_before_db_access() {
        let db = open_fixture();
        for bad in ["", "   ", "single", "root/ndb/postgres-prod"] {
            assert!(matches!(
                lookup(&db, bad),
                Err(KdbxError::InvalidPath { .. })
            ));
        }
    }

    #[test]
    fn wrong_password_is_authentication_failed() {
        assert!(matches!(
            open_with_password(&fixture(), "wrong-password"),
            Err(KdbxError::AuthenticationFailed)
        ));
    }

    #[test]
    fn missing_file_is_database_not_found() {
        assert!(matches!(
            open_with_password(&PathBuf::from("/no/such/file.kdbx"), PASSWORD),
            Err(KdbxError::DatabaseNotFound(_))
        ));
    }

    #[test]
    fn non_kdbx_file_is_corrupt() {
        let mut tmp = std::env::temp_dir();
        tmp.push(format!(
            "kdbx-credentials-not-a-db-{}.bin",
            std::process::id()
        ));
        std::fs::write(&tmp, b"this is definitely not a kdbx database").unwrap();
        let result = open_with_password(&tmp, PASSWORD);
        let _ = std::fs::remove_file(&tmp);
        assert!(matches!(result, Err(KdbxError::DatabaseCorrupt(_))));
    }

    #[test]
    fn debug_does_not_leak_secrets() {
        let db = open_fixture();
        let entry = lookup(&db, "ndb/postgres-prod").unwrap();
        let rendered = format!("{entry:?}");
        assert!(!rendered.contains("s3cr3t-pg"));
        assert!(rendered.contains("<redacted>"));
    }
}
