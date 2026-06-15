//! Retrieval of the master password from the OS-native secret store.
//!
//! The mechanism is platform-specific:
//!
//! - **Linux**: systemd credentials. The service is granted the master password
//!   by systemd (via `LoadCredentialEncrypted=` / `LoadCredential=` /
//!   `SetCredential=` on the unit), which decrypts it and exposes it as a file
//!   under the directory named by `$CREDENTIALS_DIRECTORY`. The file name is the
//!   credential ID, which is the configured `secret_store_key`. See the README
//!   for the provisioning convention.
//! - **Windows**: the Credential Manager, reached through the [`keyring`] crate.
//!
//! The master password is never stored in configuration, environment variables,
//! or source code — it is read from the OS store at runtime and returned wrapped
//! in [`Zeroizing`] so it is wiped from memory when dropped.

use crate::error::KdbxError;
use zeroize::Zeroizing;

/// Retrieve the master password for `secret_store_key` from the OS secret store.
///
/// The returned value is wrapped in [`Zeroizing`] so the underlying string is
/// zeroed when it goes out of scope.
#[cfg(target_os = "linux")]
pub(crate) fn get_master_password(secret_store_key: &str) -> Result<Zeroizing<String>, KdbxError> {
    linux::get_master_password(secret_store_key)
}

/// Retrieve the master password for `secret_store_key` from the OS secret store.
///
/// The returned value is wrapped in [`Zeroizing`] so the underlying string is
/// zeroed when it goes out of scope.
#[cfg(target_os = "windows")]
pub(crate) fn get_master_password(secret_store_key: &str) -> Result<Zeroizing<String>, KdbxError> {
    windows::get_master_password(secret_store_key)
}

#[cfg(target_os = "linux")]
mod linux {
    use super::*;
    use std::path::{Component, Path};

    /// The environment variable systemd sets for a service that has been granted
    /// one or more credentials. It points at a directory (on a tmpfs that is not
    /// swapped) holding one file per credential, each named by its credential ID
    /// and readable only by the service user.
    const CREDENTIALS_DIR_ENV: &str = "CREDENTIALS_DIRECTORY";

    pub(crate) fn get_master_password(
        secret_store_key: &str,
    ) -> Result<Zeroizing<String>, KdbxError> {
        // Without $CREDENTIALS_DIRECTORY the process is not running under a
        // systemd unit that loaded any credentials. The secret store mechanism
        // itself is unavailable, which we model as a permission/access failure
        // rather than "this particular secret is missing".
        let dir = std::env::var_os(CREDENTIALS_DIR_ENV).ok_or(KdbxError::PermissionDenied)?;
        read_from_dir(Path::new(&dir), secret_store_key)
    }

    /// Read the credential `key` from an already-resolved credentials directory.
    ///
    /// Split out from [`get_master_password`] so the file-handling logic can be
    /// tested with a temporary directory, without depending on systemd or
    /// mutating the process environment.
    fn read_from_dir(dir: &Path, key: &str) -> Result<Zeroizing<String>, KdbxError> {
        // The key is used directly as the credential file name. Reject anything
        // that is not a single, plain path component so a malicious or
        // misconfigured key cannot escape the credentials directory.
        if !is_safe_credential_id(key) {
            return Err(KdbxError::SecretNotFound(key.to_string()));
        }

        match std::fs::read_to_string(dir.join(key)) {
            // The credential is stored and returned verbatim: secret material is
            // never trimmed or otherwise altered. Provisioning must therefore
            // store the password with no trailing newline (see the README).
            Ok(secret) => Ok(Zeroizing::new(secret)),
            Err(e) => Err(match e.kind() {
                std::io::ErrorKind::NotFound => KdbxError::SecretNotFound(key.to_string()),
                // Anything else — a permission error, an I/O failure, or a
                // non-UTF-8 secret (InvalidData) — needs administrator attention
                // to the store and is treated as fatal.
                _ => KdbxError::PermissionDenied,
            }),
        }
    }

    /// A usable systemd credential ID is a single path component: no separators,
    /// no NUL byte, and neither `.` nor `..`. This guarantees `dir.join(key)`
    /// stays inside the credentials directory.
    fn is_safe_credential_id(key: &str) -> bool {
        let mut components = Path::new(key).components();
        matches!(
            (components.next(), components.next()),
            (Some(Component::Normal(_)), None)
        ) && !key.contains('\0')
    }

    #[cfg(test)]
    mod tests {
        use super::*;

        #[test]
        fn accepts_plain_ids_and_rejects_unsafe_ones() {
            assert!(is_safe_credential_id("kdbx-master"));
            assert!(is_safe_credential_id("acme-keepass"));

            for bad in ["", "acme/keepass", "..", ".", "/abs", "a/b", "with\0nul"] {
                assert!(!is_safe_credential_id(bad), "should reject {bad:?}");
            }
        }

        #[test]
        fn reads_credential_verbatim() {
            let dir = std::env::temp_dir().join(format!("kdbx-cred-test-{}", std::process::id()));
            std::fs::create_dir_all(&dir).unwrap();
            std::fs::write(dir.join("kdbx-master"), "p@ss with spaces").unwrap();

            let secret = read_from_dir(&dir, "kdbx-master").unwrap();
            assert_eq!(secret.as_str(), "p@ss with spaces");

            let _ = std::fs::remove_dir_all(&dir);
        }

        #[test]
        fn missing_credential_is_secret_not_found() {
            let dir = std::env::temp_dir();
            assert!(matches!(
                read_from_dir(&dir, "definitely-not-present-xyz"),
                Err(KdbxError::SecretNotFound(_))
            ));
        }

        #[test]
        fn traversal_key_is_refused() {
            let dir = std::env::temp_dir();
            assert!(matches!(
                read_from_dir(&dir, "../etc/passwd"),
                Err(KdbxError::SecretNotFound(_))
            ));
        }
    }
}

#[cfg(target_os = "windows")]
mod windows {
    use super::*;
    use keyring::{Entry, Error as KeyringError};

    /// The secret-store account (user) under which the master password is stored.
    /// The configured `secret_store_key` is used as the *service* name; this
    /// constant is the *account*.
    const ACCOUNT: &str = "kdbx-credentials";

    pub(crate) fn get_master_password(
        secret_store_key: &str,
    ) -> Result<Zeroizing<String>, KdbxError> {
        // On Windows the *target name* is the sole lookup key, so address the
        // credential by `secret_store_key` directly (matching the documented
        // `cmdkey /generic:"<key>"` provisioning and the C# library). Plain
        // `Entry::new` would instead derive the target name as
        // `"<account>.<key>"`, which no provisioning step ever creates.
        let entry = Entry::new_with_target(secret_store_key, secret_store_key, ACCOUNT)
            .map_err(|e| map_error(secret_store_key, e))?;
        let password = entry
            .get_password()
            .map_err(|e| map_error(secret_store_key, e))?;
        Ok(Zeroizing::new(password))
    }

    /// Map a [`keyring`] error onto the crate's taxonomy. Only `SecretNotFound`
    /// (the store returned nothing) and `PermissionDenied` (anything else) are
    /// modelled by the specification.
    fn map_error(key: &str, err: KeyringError) -> KdbxError {
        match err {
            KeyringError::NoEntry => KdbxError::SecretNotFound(key.to_string()),
            _ => KdbxError::PermissionDenied,
        }
    }
}
