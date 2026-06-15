using System.Runtime.Versioning;
using System.Text;

namespace KdbxCredentials.SecretStore;

/// <summary>
/// Reads the master password from systemd credentials on Linux.
/// </summary>
/// <remarks>
/// <para>
/// There is no lookup-by-key API: systemd decrypts the credential and exposes it
/// to the service as a file under the directory named by the
/// <c>CREDENTIALS_DIRECTORY</c> environment variable, on a tmpfs that is not
/// swapped and readable only by the service user. This store reads
/// <c>$CREDENTIALS_DIRECTORY/&lt;secretStoreKey&gt;</c> verbatim, so the password
/// must be provisioned with no trailing newline.
/// </para>
/// <para>
/// The credential is granted to the unit via <c>LoadCredentialEncrypted=</c>
/// (or <c>LoadCredential=</c> / <c>SetCredential=</c>), for example after
/// encrypting it with:
/// </para>
/// <code>printf '%s' 'master-password' | sudo systemd-creds encrypt --name=kdbx-master - /etc/credstore.encrypted/kdbx-master</code>
/// <para>
/// The process must therefore run under a systemd unit that loaded the
/// credential; <c>secretStoreKey</c> is the credential ID and must be a single,
/// valid credential name (no path separators).
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class LinuxSecretStore : ISecretStore
{
    private const string CredentialsDirEnv = "CREDENTIALS_DIRECTORY";

    public char[] GetMasterPassword(string secretStoreKey)
    {
        // Without $CREDENTIALS_DIRECTORY the process is not running under a
        // systemd unit that loaded any credentials — the store is unavailable.
        string? dir = Environment.GetEnvironmentVariable(CredentialsDirEnv);
        if (string.IsNullOrEmpty(dir))
        {
            throw new PermissionDeniedException(
                $"{CredentialsDirEnv} is not set — not running under a systemd unit with credentials");
        }

        return ReadFromDir(dir, secretStoreKey);
    }

    /// <summary>
    /// Read the credential <paramref name="key"/> from an already-resolved
    /// credentials directory. Split out from <see cref="GetMasterPassword"/> so
    /// the file-handling logic can be tested with a temporary directory, without
    /// depending on systemd or mutating the process environment.
    /// </summary>
    internal static char[] ReadFromDir(string dir, string key)
    {
        // The key is used directly as the credential file name. Reject anything
        // that is not a single, plain path component so a malicious or
        // misconfigured key cannot escape the credentials directory.
        if (!IsSafeCredentialId(key))
        {
            throw new SecretNotFoundException(key);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(Path.Combine(dir, key));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new SecretNotFoundException(key);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PermissionDeniedException(
                "permission denied reading the systemd credential", ex);
        }
        catch (IOException ex)
        {
            throw new PermissionDeniedException(
                "could not read the systemd credential", ex);
        }

        // Decode strictly: a non-UTF-8 secret is a provisioning error, not a
        // password to be silently mangled with replacement characters.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetChars(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new PermissionDeniedException(
                "the stored credential is not valid UTF-8", ex);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    /// <summary>
    /// A usable systemd credential ID is a single path component: no directory
    /// separators or NUL byte, and neither <c>.</c> nor <c>..</c>. This
    /// guarantees the joined path stays inside the credentials directory.
    /// </summary>
    private static bool IsSafeCredentialId(string key) =>
        key.Length > 0
        && key != "."
        && key != ".."
        && key.IndexOf('/') < 0
        && key.IndexOf('\\') < 0
        && key.IndexOf('\0') < 0;
}
