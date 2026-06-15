using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace KdbxCredentials.SecretStore;

/// <summary>
/// Reads the master password from the Windows Credential Manager as a generic
/// credential, via the Win32 <c>CredRead</c> API. This works in any standard
/// .NET process (console app or Windows Service), unlike the WinRT
/// <c>PasswordVault</c> which requires an app-container/package identity.
/// </summary>
/// <remarks>
/// The credential is looked up by <c>TargetName == secretStoreKey</c>. Provision
/// it with, for example: <c>cmdkey /generic:"acme/keepass" /user:"kdbx-credentials" /pass</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSecretStore : ISecretStore
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int ERROR_NOT_FOUND = 1168;
    private const int ERROR_ACCESS_DENIED = 5;

    public char[] GetMasterPassword(string secretStoreKey)
    {
        if (!CredRead(secretStoreKey, CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
        {
            int error = Marshal.GetLastWin32Error();
            throw error switch
            {
                ERROR_NOT_FOUND => new SecretNotFoundException(secretStoreKey),
                ERROR_ACCESS_DENIED => new PermissionDeniedException(
                    "permission denied querying Windows Credential Manager"),
                _ => new PermissionDeniedException(
                    $"failed to read credential (Win32 error {error})"),
            };
        }

        try
        {
            CREDENTIAL cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            int byteCount = (int)cred.CredentialBlobSize;
            if (byteCount == 0 || cred.CredentialBlob == IntPtr.Zero)
            {
                return [];
            }

            // The blob is stored as UTF-16 (Unicode) bytes by convention.
            byte[] blob = new byte[byteCount];
            Marshal.Copy(cred.CredentialBlob, blob, 0, byteCount);
            try
            {
                char[] chars = Encoding.Unicode.GetChars(blob);
                return chars;
            }
            finally
            {
                Array.Clear(blob);
            }
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredReadW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr cred);

    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
