# KeePass Credential Retrieval — C# Implementation Notes

Version: 1.0  
Status: Draft

## Package Name

Suggestion: `KdbxCredentials`  
Check NuGet for availability before settling on a name.

## Target Framework

`net9.0` — supported on both Windows and Linux.

## Recommended Dependencies

| Package | Purpose |
|---------|---------|
| `pt.KeePassLibStd` | .NET Standard 2.0 port of the official `KeePassLib`, keeping the same API (`PwDatabase` / `CompositeKey` / `KcpPassword`) but running cross-platform on `net9.0`. The official `KeePassLib` NuGet package is .NET Framework only and throws `TypeLoadException` at runtime on modern .NET |
| Win32 `CredRead` (Windows only) | Access to Windows Credential Manager as a generic credential via `advapi32.dll` P/Invoke. Works in any standard .NET process, unlike the WinRT `PasswordVault` |
| (none) | Linux uses systemd credentials — the master password is read from the `$CREDENTIALS_DIRECTORY/<secretStoreKey>` file, so no library is required |

## Platform Handling

Use `RuntimeInformation.IsOSPlatform()` to branch at runtime:

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    // read the Windows Credential Manager (CredRead)
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    // read $CREDENTIALS_DIRECTORY/<secretStoreKey> (systemd credentials)
else
    throw new PlatformNotSupportedException("Only Windows and Linux are supported");
```

Consider using a `ISecretStore` interface with `WindowsSecretStore` and `LinuxSecretStore` implementations to keep platform code isolated and testable.

## Public API Shape

```csharp
namespace KdbxCredentials;

public sealed class Entry : IDisposable
{
    public string? Username { get; }
    public string? Password { get; }
    public string? Url      { get; }
    public string? Notes    { get; }

    public void Dispose(); // zeros sensitive fields
}

public sealed class Database : IDisposable
{
    public static Database Open(string dbPath, string secretStoreKey);
    public Entry Lookup(string path);
    public void Dispose(); // zeros master password, releases resources
}
```

Prefer `IDisposable` over finalizers. Document that callers MUST use `using` blocks or explicitly call `Dispose()`.

## Exception Types

```csharp
public class KdbxException : Exception { }

public class SecretNotFoundException     : KdbxException { }
public class DatabaseNotFoundException   : KdbxException { }
public class DatabaseCorruptException    : KdbxException { }
public class AuthenticationFailedException : KdbxException { }
public class PermissionDeniedException   : KdbxException { }
public class InvalidPathException        : KdbxException { }
public class EntryNotFoundException      : KdbxException { }
public class AmbiguousEntryException     : KdbxException { }
```

All exceptions should carry a `Message` useful to an operator and, where applicable, an `InnerException` with the original cause (without leaking credential material).

## Credential Zeroing

C# strings are immutable and interned — they cannot be reliably zeroed. Options:

- Use `SecureString` for the master password (deprecated in .NET but still available and functional)
- Use `char[]` or `byte[]` for sensitive values internally and zero the array on dispose
- Accept that `string` fields in `Entry` cannot be perfectly zeroed and document this limitation clearly in `SECURITY.md`

The recommended approach is to hold the master password as a `char[]` internally, zero it on dispose, and accept `string` as the return type for `Entry` fields with the limitation documented.

## Path Parsing

Split on `/`, trim whitespace from each segment, compare case-insensitively using `StringComparison.OrdinalIgnoreCase`. Validate before lookup:

- Reject empty string
- Reject single segment (no group)
- Reject first segment equal to `"root"` (case-insensitive)

Walk the `KeePassLib` group tree using the path segments, then find the entry by title within the final group.

## Windows — Credential Manager

Read a generic credential with the Win32 `CredRead` API (`advapi32.dll`), looking it up by `TargetName == secretStoreKey`. This works in any standard .NET process (console app or Windows Service), unlike the WinRT `PasswordVault`, which requires an app-container/package identity. The credential blob is UTF-16; decode it and clear the intermediate byte buffer.

Provision with, for example: `cmdkey /generic:"kdbx-master" /user:"kdbx-credentials" /pass`.

## Linux — systemd credentials

There is no lookup-by-key API. systemd decrypts the credential and exposes it to the service as a file under `$CREDENTIALS_DIRECTORY`, named by the credential ID. Read `$CREDENTIALS_DIRECTORY/<secretStoreKey>` and return it verbatim (do not trim — provision with no trailing newline). Validate `secretStoreKey` as a single safe path component (no `/`, `\`, NUL; not `.`/`..`) so it cannot escape the directory.

- No `$CREDENTIALS_DIRECTORY` → `PermissionDeniedException` (the process is not running under a systemd unit that loaded credentials).
- Credential file missing → `SecretNotFoundException`.
- The credential is granted to the unit via `LoadCredentialEncrypted=` (or `LoadCredential=` / `SetCredential=`), so the process must run as a systemd service. Encrypt the password once with `systemd-creds encrypt`.

## Publishing Checklist

- [ ] `README.md` with usage example and provisioning instructions
- [ ] NuGet metadata in `.csproj` (authors, description, license, repository URL)
- [ ] XML doc comments on all public members
- [ ] Unit tests with a test `.kdbx` file (not a real database)
- [ ] Integration tests for Windows and Linux secret store (the Linux systemd flow can be tested with `systemd-creds` + `systemd-run`; may need to be skipped in CI without systemd)
- [ ] `CHANGELOG.md`
- [ ] Strong name signing (optional but conventional for NuGet packages)
- [ ] Check NuGet name availability before first publish

## Notes

- The official `KeePassLib` NuGet package (2.30.0) targets .NET Framework and throws `TypeLoadException` (removed `System.Security.Cryptography.ProtectedMemory`) at runtime on modern .NET. Use `pt.KeePassLibStd`, a .NET Standard 2.0 port with the same API, instead
- The `PasswordVault` WinRT API requires an app-container/package identity and is unreliable in a plain console app or Windows Service. Use the Win32 `CredRead` P/Invoke instead, as described above
- Consider providing a `KdbxCredentialsBuilder` or options object for `Open()` if configuration needs grow in future
