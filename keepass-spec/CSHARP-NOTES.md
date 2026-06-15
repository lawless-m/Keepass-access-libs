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
| `KeePassLib` | Official KeePass library extracted from the KeePass source. Handles KDBX3 and KDBX4 parsing. Available on NuGet |
| `Microsoft.Windows.SDK.Contracts` (Windows only) | Access to Windows Credential Manager via `Windows.Security.Credentials.PasswordVault` |
| `SecretService` or direct D-Bus | Linux Secret Service access. Options: `SecretService` NuGet package, or direct D-Bus via `Tmds.DBus` |

## Platform Handling

Use `RuntimeInformation.IsOSPlatform()` to branch at runtime:

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    // use PasswordVault
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    // use Secret Service
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

```csharp
var vault = new Windows.Security.Credentials.PasswordVault();
var credential = vault.Retrieve(secretStoreKey, Environment.MachineName);
credential.RetrievePassword();
return credential.Password;
```

The `Resource` field maps to `secretStoreKey`. Use `Environment.MachineName` as the `UserName` field, or a fixed string — document the convention so the provisioning script matches.

## Linux — Secret Service

The Secret Service API is accessed over D-Bus. Use the `SecretService` NuGet package or equivalent. Look up the secret by the `label` or attribute matching `secretStoreKey`.

Note: on headless Linux machines a Secret Service daemon must be running (e.g. `gnome-keyring-daemon`). Document this as a prerequisite.

## Publishing Checklist

- [ ] `README.md` with usage example and provisioning instructions
- [ ] NuGet metadata in `.csproj` (authors, description, license, repository URL)
- [ ] XML doc comments on all public members
- [ ] Unit tests with a test `.kdbx` file (not a real database)
- [ ] Integration tests for Windows and Linux secret store (may need to be skipped in CI if no secret store is available)
- [ ] `CHANGELOG.md`
- [ ] Strong name signing (optional but conventional for NuGet packages)
- [ ] Check NuGet name availability before first publish

## Notes

- `KeePassLib` from NuGet may lag behind the official KeePass source. Verify it supports KDBX4 before committing to it. An alternative is `KeePass2.x` or a direct port of the relevant parsing code
- The `PasswordVault` API on Windows requires the calling process to be a UWP app or to use the `Windows.Security.Credentials` WinRT API via interop — verify this works in a standard .NET 8 console application / Windows Service context before committing to it. The older `System.Security.Credentials` or direct DPAPI via `System.Security.Cryptography.ProtectedData` may be needed as a fallback
- Consider providing a `KdbxCredentialsBuilder` or options object for `Open()` if configuration needs grow in future
