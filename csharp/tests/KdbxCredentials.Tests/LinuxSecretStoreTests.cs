using System.Runtime.Versioning;
using KdbxCredentials.SecretStore;

namespace KdbxCredentials.Tests;

/// <summary>
/// Exercises the file-handling logic of the systemd-credentials secret store via
/// the internal <see cref="LinuxSecretStore.ReadFromDir"/>, using a temporary
/// directory so no systemd unit or environment mutation is needed. The real
/// end-to-end flow is covered by <c>tests/systemd_creds_integration.sh</c>.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxSecretStoreTests
{
    [Fact]
    public void ReadsCredentialVerbatim()
    {
        string dir = Directory.CreateTempSubdirectory("kdbx-cred-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "kdbx-master"), "p@ss with spaces");
            char[] secret = LinuxSecretStore.ReadFromDir(dir, "kdbx-master");
            Assert.Equal("p@ss with spaces", new string(secret));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MissingCredentialIsSecretNotFound()
    {
        string dir = Directory.CreateTempSubdirectory("kdbx-cred-test-").FullName;
        try
        {
            Assert.Throws<SecretNotFoundException>(
                () => LinuxSecretStore.ReadFromDir(dir, "definitely-not-present-xyz"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme/keepass")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../etc/passwd")]
    [InlineData("a\\b")]
    public void UnsafeKeysAreRefused(string key)
    {
        // A traversal or multi-component key must never read outside the dir;
        // it is rejected before any file access as SecretNotFound.
        Assert.Throws<SecretNotFoundException>(
            () => LinuxSecretStore.ReadFromDir(Path.GetTempPath(), key));
    }

    [Fact]
    public void NonUtf8CredentialIsPermissionDenied()
    {
        string dir = Directory.CreateTempSubdirectory("kdbx-cred-test-").FullName;
        try
        {
            // 0xFF is not valid UTF-8.
            File.WriteAllBytes(Path.Combine(dir, "kdbx-master"), [0xFF, 0xFE, 0xFD]);
            Assert.Throws<PermissionDeniedException>(
                () => LinuxSecretStore.ReadFromDir(dir, "kdbx-master"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
