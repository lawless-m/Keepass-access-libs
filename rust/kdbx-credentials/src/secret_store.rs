//! Retrieval of the master password from the OS-native secret store.
//!
//! On Windows this is the Credential Manager; on Linux it is the Secret Service
//! API (libsecret / GNOME Keyring), reached over D-Bus. The [`keyring`] crate
//! abstracts the platform difference.
//!
//! The master password is never stored in configuration, environment variables,
//! or source code — it is read from the OS store at runtime and returned wrapped
//! in [`Zeroizing`] so it is wiped from memory when dropped.

use crate::error::KdbxError;
use keyring::{Entry, Error as KeyringError};
use zeroize::Zeroizing;

/// The secret-store account (user) under which the master password is stored.
///
/// The configured `secret_store_key` is used as the *service* name; this
/// constant is the *account*. Keeping it fixed means provisioning is a single,
/// documented `(service = secret_store_key, account = ACCOUNT)` pair that is
/// identical across machines. See the README for the provisioning convention.
const ACCOUNT: &str = "kdbx-credentials";

/// Retrieve the master password for `secret_store_key` from the OS secret store.
///
/// The returned value is wrapped in [`Zeroizing`] so the underlying string is
/// zeroed when it goes out of scope.
pub(crate) fn get_master_password(secret_store_key: &str) -> Result<Zeroizing<String>, KdbxError> {
    let entry =
        Entry::new(secret_store_key, ACCOUNT).map_err(|e| map_error(secret_store_key, e))?;
    let password = entry
        .get_password()
        .map_err(|e| map_error(secret_store_key, e))?;
    Ok(Zeroizing::new(password))
}

/// Map a [`keyring`] error onto the crate's taxonomy.
///
/// Only two secret-store outcomes are modelled by the specification:
/// `SecretNotFound` (the store returned nothing) and `PermissionDenied`
/// (anything else preventing access — no storage access, ambiguous matches,
/// platform failures, or a non-UTF-8 secret). All of the latter require
/// administrator attention to the store and are treated as fatal.
fn map_error(key: &str, err: KeyringError) -> KdbxError {
    match err {
        KeyringError::NoEntry => KdbxError::SecretNotFound(key.to_string()),
        _ => KdbxError::PermissionDenied,
    }
}
