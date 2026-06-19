# Using KdbxCredentials from another .NET service

A consumer's quickstart for reading a credential out of the shared KeePass store at runtime.

> Two things that bite first-time integrators:
> 1. **The secret-store key for RI services is `kdbx-services`** — that is the default `$Key` the
>    deployed `provision-credential.ps1` writes. (The repo's test fixtures use `kdbx-master`; don't
>    copy that into a service config.)
> 2. **The credential is per-user (DPAPI).** It must live in the vault of the account your process
>    *actually runs as*. For a CGI under TinyWeb that means **`LocalSystem`/`NT AUTHORITY\SYSTEM`** —
>    see provisioning below. Provisioning it as your own login does nothing for the service.

## 1. Reference it

```xml
<ProjectReference Include="..\..\..\Keepass-access-libs\csharp\src\KdbxCredentials\KdbxCredentials.csproj" />
```

(or the `KdbxCredentials` NuGet package). Targets `net9.0`, Windows + Linux.

## 2. Read a credential

```csharp
using KdbxCredentials;

// secretStoreKey names the OS-secret-store entry holding the kdbx MASTER password.
using var db = Database.Open(
    @"\\RIVSPROD02\RI Services\Credentials\ServicePasswords.kdbx",  // the shared services db
    "kdbx-services");                                              // OS secret-store key

using Entry entry = db.Lookup("Exportmaster/RIVSEM01");            // case-insensitive group/title
string user = entry.Username ?? "";
string pass = entry.Password ?? "";
// also: entry.Url, entry.Notes
```

Always `using` both `Database` and `Entry`. Paths are `group/title`, case-insensitive; omit the
implied root group. Every failure is a `KdbxException` subtype (`SecretNotFoundException`,
`EntryNotFoundException`, `AuthenticationFailedException`, …) — catch the specific type to branch.

## 3. Provisioning the master password into the right vault

The master password lives in the OS secret store, never in config. Which vault depends on the
account your service runs as. Put the password in `C:\kdbx.txt` first (no trailing newline); the
provisioning script reads and shreds it.

**Interactive account** (e.g. your admin login) — just run `cmdkey` as yourself, or
`provision-credential.bat`.

**Named, runas-able service account** (e.g. `NISAINT\scheduler.service.ac`):

```cmd
provision-runas.bat NISAINT\scheduler.service.ac
```

**A `LocalSystem` service (TinyWeb CGIs, etc.).** `runas` *cannot* assume `SYSTEM` (it has no
password), so `provision-runas.bat` is no use here. Write into SYSTEM's vault via a one-shot
scheduled task that runs as SYSTEM:

```cmd
schtasks /create /tn KdbxProvisionSystem /sc once /st 00:00 /ru SYSTEM /rl HIGHEST /f ^
    /tr "\"C:\path\to\provision-credential.bat\""
schtasks /run /tn KdbxProvisionSystem
:: check Last Result = 0, then:
schtasks /delete /tn KdbxProvisionSystem /f
```

(The deploy share has this wrapped as `R:\kdbx\provision-system.bat`.) Because the secret is in
SYSTEM's vault, `cmdkey /list` as an admin will *not* show it — that's expected. Confirm by having
the service itself read it.

## 4. How to tell which account a service runs as

For an NSSM-managed service: `sc qc <ServiceName>` and read `SERVICE_START_NAME` (or the
`nssm … set <svc> ObjectName …` line in its install script). TinyWeb on RIVSPROD02 is
`ObjectName LocalSystem`, so its CGIs read from SYSTEM's vault.
