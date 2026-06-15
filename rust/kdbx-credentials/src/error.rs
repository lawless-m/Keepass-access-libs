//! Error taxonomy for the `kdbx-credentials` crate.
//!
//! Every fallible operation returns [`KdbxError`]. The variants map one-to-one
//! onto the error taxonomy defined in the shared specification (`ERRORS.md`), so
//! callers can branch on the variant without parsing strings.

use std::path::PathBuf;

/// The single error type returned by every operation in this crate.
///
/// Variants are programmatically distinguishable — callers MUST branch on the
/// variant rather than inspecting the `Display` string, which is intended for
/// human consumption only and never contains credential material.
#[derive(Debug, thiserror::Error)]
#[non_exhaustive]
pub enum KdbxError {
    /// The OS secret store was queried for the configured key and returned
    /// nothing. The machine has most likely not been provisioned.
    #[error("secret not found in OS store for key '{0}'")]
    SecretNotFound(String),

    /// The file at `db_path` does not exist or is not a regular file.
    #[error("database file not found: {0}")]
    DatabaseNotFound(PathBuf),

    /// The file exists and is readable but cannot be parsed as a valid KDBX4
    /// database. The underlying cause is preserved as the error source for
    /// diagnostics, but never carries credential material.
    #[error("database file is corrupt or in an unsupported format")]
    DatabaseCorrupt(#[source] Box<dyn std::error::Error + Send + Sync>),

    /// The master password retrieved from the OS secret store was rejected by
    /// the database. The stored password is most likely stale.
    #[error("authentication failed — master password may be stale")]
    AuthenticationFailed,

    /// The process lacks permission to read the database file or to query the
    /// OS secret store.
    #[error("permission denied accessing database or secret store")]
    PermissionDenied,

    /// The path string supplied to [`lookup`](crate::lookup) is malformed.
    /// `reason` is a fixed, credential-free description of the sub-case.
    #[error("invalid path '{path}': {reason}")]
    InvalidPath {
        /// The offending path, as supplied by the caller.
        path: String,
        /// A fixed description of why the path was rejected.
        reason: &'static str,
    },

    /// The path is well-formed but no entry exists at that location.
    #[error("entry not found at path '{0}'")]
    EntryNotFound(String),

    /// The path resolves to more than one entry. Indicates duplicate titles
    /// within a group in the database.
    #[error("ambiguous path '{0}' — multiple entries match")]
    AmbiguousEntry(String),
}
