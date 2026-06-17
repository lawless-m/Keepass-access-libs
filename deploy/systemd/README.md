# systemd deployment example

A worked example of running a KeePass-backed task on Linux: a script that fetches
a database password from a `.kdbx` and connects to PostgreSQL. The master
password (which unlocks the `.kdbx`) comes from the OS secret store, never from
the script.

| File | Purpose |
|------|---------|
| `kdbx-db-task.sh` | the task: fetch the entry password with `kdbx-getpass`, connect |
| `kdbx-db-task.service` | runs the task as a `oneshot` service, with the master password as a systemd credential |
| `kdbx-db-task.timer` | runs the service on a schedule (the cron replacement) |

## The two ways the master password is resolved

`kdbx-getpass` resolves the master password exactly as the library does, so the
**same task script works in both contexts**:

- **As the service/timer** — systemd decrypts the credential into
  `$CREDENTIALS_DIRECTORY` and getpass reads it there. Encrypted at rest.
- **Run by hand on a headless box** — getpass reads it from the file store
  `/etc/kdbx/<key>`. Plaintext at rest, protected by file permissions.

Provision whichever you need (or both, with the same value). See the crate README
(*Provisioning the secret store*) for the exact commands.

## Build and install

```sh
# Build the getpass binary from the crate.
cargo build --release --example getpass
sudo install -m 0755 target/release/examples/getpass /usr/local/bin/kdbx-getpass
sudo install -m 0755 deploy/systemd/kdbx-db-task.sh   /usr/local/bin/kdbx-db-task.sh

# Provision the master password as a systemd credential (for the service):
printf '%s' 'master-password' \
  | sudo systemd-creds encrypt --name=kdbx-master - /etc/credstore.encrypted/kdbx-master

# Install and enable the timer:
sudo install -m 0644 deploy/systemd/kdbx-db-task.service /etc/systemd/system/
sudo install -m 0644 deploy/systemd/kdbx-db-task.timer   /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now kdbx-db-task.timer

# Run once now to check it:
sudo systemctl start kdbx-db-task.service
journalctl -u kdbx-db-task.service -n 20
```

Adjust the paths, entry, connection details, and `User=` to your environment.
The `.kdbx` on the network share requires `RequiresMountsFor=` (already in the
unit) and that `User=` can read the file.
