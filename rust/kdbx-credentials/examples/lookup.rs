//! Open a KDBX database using the master password from the OS secret store and
//! print one entry's non-secret fields. Used by the systemd-credentials
//! integration test, but also a minimal usage example.
//!
//! ```text
//! lookup <db_path> <secret_store_key> <entry_path>
//! ```
//!
//! On Linux the master password is read from the systemd credential named
//! `<secret_store_key>` (the file `$CREDENTIALS_DIRECTORY/<secret_store_key>`),
//! so this must be run under a systemd unit that loaded that credential.
//!
//! The looked-up password is deliberately **not** printed; the username and URL
//! are enough to prove the database was decrypted with the provisioned master
//! password.

use std::path::Path;
use std::process::ExitCode;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().collect();
    let [_, db_path, secret_store_key, entry_path] = args.as_slice() else {
        eprintln!("usage: lookup <db_path> <secret_store_key> <entry_path>");
        return ExitCode::from(2);
    };

    let db = match kdbx_credentials::open(Path::new(db_path), secret_store_key) {
        Ok(db) => db,
        Err(e) => {
            eprintln!("open failed: {e}");
            return ExitCode::FAILURE;
        }
    };

    match kdbx_credentials::lookup(&db, entry_path) {
        Ok(entry) => {
            println!("OK: opened database via systemd credential '{secret_store_key}'");
            println!("username: {}", entry.username.as_deref().unwrap_or("<none>"));
            println!("url:      {}", entry.url.as_deref().unwrap_or("<none>"));
            println!("password: <retrieved, not printed>");
            ExitCode::SUCCESS
        }
        Err(e) => {
            eprintln!("lookup failed: {e}");
            ExitCode::FAILURE
        }
    }
}
