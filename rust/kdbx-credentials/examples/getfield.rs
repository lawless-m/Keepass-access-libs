//! Print one field of one entry to stdout, so a script can capture it. This is
//! the general form of `getpass`: it selects which of the four built-in fields
//! (username, password, url, notes) to print.
//!
//! ```sh
//! USER=$(kdbx-getfield db.kdbx kdbx-master grp/title username)
//! PW=$(kdbx-getfield db.kdbx kdbx-master grp/title password)
//! ```
//!
//! Like `getpass`, when asked for `password` this DELIBERATELY prints the secret.
//! Use it only where exposing the value to the calling shell (its stdout, the
//! parent process's environment, and any launched program's command line) is
//! acceptable — it moves the secret out of plaintext config, but does not hide
//! it at runtime.

use std::path::Path;
use std::process::ExitCode;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().collect();
    let [_, db_path, secret_store_key, entry_path, field] = args.as_slice() else {
        eprintln!("usage: getfield <db_path> <secret_store_key> <entry_path> <field>");
        eprintln!("       field is one of: username, password, url, notes");
        return ExitCode::from(2);
    };

    let db = match kdbx_credentials::open(Path::new(db_path), secret_store_key) {
        Ok(db) => db,
        Err(e) => {
            eprintln!("open failed: {e}");
            return ExitCode::FAILURE;
        }
    };

    let entry = match kdbx_credentials::lookup(&db, entry_path) {
        Ok(entry) => entry,
        Err(e) => {
            eprintln!("lookup failed: {e}");
            return ExitCode::FAILURE;
        }
    };

    let value = match field.to_lowercase().as_str() {
        "username" => &entry.username,
        "password" => &entry.password,
        "url" => &entry.url,
        "notes" => &entry.notes,
        other => {
            eprintln!("unknown field '{other}': expected username, password, url, or notes");
            return ExitCode::from(2);
        }
    };

    match value {
        // Just the field value, nothing else, so `$(...)`/`for /f` captures it cleanly.
        Some(v) => {
            println!("{v}");
            ExitCode::SUCCESS
        }
        None => {
            eprintln!("entry '{entry_path}' has no {field}");
            ExitCode::FAILURE
        }
    }
}
