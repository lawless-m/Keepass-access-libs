using System.Runtime.InteropServices;

namespace KdbxCredentials.SecretStore;

/// <summary>
/// Selects the appropriate <see cref="ISecretStore"/> for the current OS at
/// runtime. Only Windows and Linux are supported.
/// </summary>
internal static class SecretStoreFactory
{
    /// <summary>Create the secret store for the current platform.</summary>
    /// <exception cref="PlatformNotSupportedException">
    /// The current OS is neither Windows nor Linux.
    /// </exception>
    internal static ISecretStore Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSecretStore();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxSecretStore();
        }

        throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
    }
}
