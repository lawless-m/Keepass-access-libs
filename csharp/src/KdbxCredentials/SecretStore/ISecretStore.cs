namespace KdbxCredentials.SecretStore;

/// <summary>
/// Abstraction over the OS-native secret store. One implementation exists per
/// supported platform, isolating platform-specific code and keeping it testable.
/// </summary>
internal interface ISecretStore
{
    /// <summary>
    /// Retrieve the master password stored under <paramref name="secretStoreKey"/>.
    /// </summary>
    /// <returns>The master password as a <see cref="char"/> array. The caller
    /// owns the array and is responsible for zeroing it after use.</returns>
    /// <exception cref="SecretNotFoundException">No entry exists for the key.</exception>
    /// <exception cref="PermissionDeniedException">The store could not be queried.</exception>
    char[] GetMasterPassword(string secretStoreKey);
}
