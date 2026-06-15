# KeePass Credential Retrieval — Security Contract

Version: 1.0  
Status: Draft

## What This Library Guarantees

### Credentials are zeroed on release
All credential material — master password, entry passwords, usernames, and any other sensitive string — is actively zeroed from memory when the owning struct is dropped or disposed. Implementations use a verified zeroing mechanism that the compiler cannot optimise away.

### The master password is never in source code or config files
The master password is retrieved at runtime from the OS-native secret store only. It is never written to disk, logged, or passed through environment variables.

### The database is opened read-only
The library never writes to or modifies the `.kdbx` file. Concurrent read access from multiple processes is therefore safe from the library's perspective (KDBX itself does not provide inter-process locking).

### Errors do not leak credential material
Error messages and diagnostics do not include passwords, decrypted field values, or the master password. Error causes from underlying libraries are wrapped before being surfaced to callers.

---

## What This Library Does NOT Guarantee

### Protection against a compromised process
If the calling process is compromised, an attacker with access to the process memory can read decrypted credentials before they are zeroed. This is an inherent limitation of in-process credential handling and cannot be solved at the library level.

### Protection against swap / page file exposure
Zeroing memory does not prevent credentials from having been written to swap or the page file before zeroing occurs. On machines handling sensitive credentials, encrypted swap should be configured at the OS level.

### Protection against core dumps
A core dump taken while credentials are in memory will contain them. Scheduled tasks on sensitive machines should disable core dumps at the OS level.

### Protection against a malicious `db_path`
The library opens the file at the path it is given. It does not validate that the path is on an expected volume or share. Callers are responsible for ensuring `db_path` comes from a trusted configuration source.

### Audit trail of credential access
The library does not log which entries were accessed or when. If an audit trail is required, the caller is responsible for implementing it.

### Resistance to timing attacks
No attempt is made to provide constant-time comparison for path lookups or credential comparisons. This is not a relevant threat model for scheduled task credential retrieval.

---

## Threat Model

This library is designed to address the following threat:

**Credentials stored in plaintext JSON files on a shared network location.**

The library improves on this by:

- Encrypting credentials at rest (KDBX4 with AES-256 or ChaCha20)
- Ensuring the decryption key (master password) is never on the shared location
- Tying the decryption key to specific approved machines via OS secret stores
- Reducing the attack surface: compromise of the `.kdbx` file alone is not sufficient

The library is **not** designed to address:

- Insider threats with administrative access to approved machines
- Attackers with physical access to approved machines
- Compromise of the KeePassXC desktop application or the human managing the database
- Network interception (credentials are never transmitted by this library)

---

## Provisioning Security Notes

The one-time setup step that deposits the master password into the OS secret store is outside the scope of this library but is a critical security operation. Recommendations:

- The provisioning step should be performed by an administrator interactively on each machine, not via an automated script that itself contains the password in plaintext
- The provisioning script (without embedded secrets) should be version controlled
- When the master password is rotated, all machines must be updated before the new database is deployed
- Access to the OS secret store entry should be scoped to the service account running the scheduled tasks, not the entire machine

---

## Rotation Procedure

When the KeePass master password is changed:

1. Change the password in KeePassXC and save the database
2. Update the OS secret store entry on each approved machine
3. Verify each machine can open the database before deploying the updated `.kdbx`

Failure to update all machines before deploying the new database will result in `AuthenticationFailed` errors on unupdated machines.

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0 | 2026-06-15 | Initial draft |
