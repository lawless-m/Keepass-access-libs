//! Print one entry's password to stdout, so a script can capture it and pass it
//! to another program. This is the companion to `lookup` for the batch-file use
//! case described in the README:
//!
//! ```bat
//! @echo off
//! for /f "usebackq delims=" %%p in (`kdbx-getpass db.kdbx kdbx-master grp/title`) do set "PW=%%p"
//! program --password %PW% --log c:\tmp
//! set "PW="
//! ```
//!
//! Unlike `lookup`, this DELIBERATELY prints the password. Use it only where
//! exposing the secret to the calling shell (its stdout, the parent process's
//! environment, and the launched program's command line) is acceptable — it
//! moves the secret out of plaintext config, but does not hide it at runtime.

use std::path::Path;
use std::process::ExitCode;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().collect();
    let [_, db_path, secret_store_key, entry_path] = args.as_slice() else {
        eprintln!("usage: getpass <db_path> <secret_store_key> <entry_path>");
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
        Ok(entry) => match &entry.password {
            // Just the password, nothing else, so `for /f` captures it cleanly.
            Some(password) => {
                println!("{password}");
                ExitCode::SUCCESS
            }
            None => {
                eprintln!("entry '{entry_path}' has no password");
                ExitCode::FAILURE
            }
        },
        Err(e) => {
            eprintln!("lookup failed: {e}");
            ExitCode::FAILURE
        }
    }
}
