using System.Diagnostics;
using System.Runtime.Versioning;

namespace KdbxCredentials.SecretStore;

/// <summary>
/// Reads the master password from the Linux Secret Service (libsecret / GNOME
/// Keyring) via the <c>secret-tool</c> CLI shipped with libsecret.
/// </summary>
/// <remarks>
/// <para>
/// The secret is looked up by the attributes
/// <c>service=&lt;secretStoreKey&gt;</c> and <c>account=kdbx-credentials</c>,
/// matching the provisioning command:
/// </para>
/// <code>secret-tool store --label='acme/keepass' service 'acme/keepass' account 'kdbx-credentials'</code>
/// <para>
/// A Secret Service daemon must be running (for example
/// <c>gnome-keyring-daemon --daemonize</c> on headless machines), and
/// <c>secret-tool</c> must be on <c>PATH</c>.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class LinuxSecretStore : ISecretStore
{
    public char[] GetMasterPassword(string secretStoreKey)
    {
        var psi = new ProcessStartInfo("secret-tool")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("lookup");
        psi.ArgumentList.Add("service");
        psi.ArgumentList.Add(secretStoreKey);
        psi.ArgumentList.Add("account");
        psi.ArgumentList.Add(SecretStoreFactory.Account);

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new PermissionDeniedException("failed to start 'secret-tool'");
        }
        catch (Exception ex) when (ex is not KdbxException)
        {
            throw new PermissionDeniedException(
                "could not invoke 'secret-tool' — is libsecret installed and on PATH?", ex);
        }

        // secret-tool writes the secret to stdout with a trailing newline, and
        // exits non-zero with empty output when no matching secret exists.
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || output.Length == 0)
        {
            throw new SecretNotFoundException(secretStoreKey);
        }

        return output.TrimEnd('\n', '\r').ToCharArray();
    }
}
