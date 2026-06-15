namespace KdbxCredentials;

/// <summary>
/// A single credential entry exposing the four built-in fields defined by the
/// specification. Every field is optional, mirroring KeePass.
/// </summary>
/// <remarks>
/// <para>
/// .NET <see cref="string"/> instances are immutable and may be interned, so the
/// values exposed here cannot be reliably zeroed from memory — this is a
/// documented limitation (see <c>SECURITY.md</c>). <see cref="Dispose"/> drops
/// the references so they become eligible for garbage collection promptly.
/// </para>
/// <para>Treat instances as sensitive and dispose them as soon as possible.</para>
/// </remarks>
public sealed class Entry : IDisposable
{
    /// <summary>The <c>UserName</c> field, or <see langword="null"/> if absent.</summary>
    public string? Username { get; private set; }

    /// <summary>The <c>Password</c> field, decrypted from its protected form.</summary>
    public string? Password { get; private set; }

    /// <summary>The <c>URL</c> field, or <see langword="null"/> if absent.</summary>
    public string? Url { get; private set; }

    /// <summary>The <c>Notes</c> field, or <see langword="null"/> if absent.</summary>
    public string? Notes { get; private set; }

    internal Entry(string? username, string? password, string? url, string? notes)
    {
        Username = username;
        Password = password;
        Url = url;
        Notes = notes;
    }

    /// <summary>
    /// Drops references to the credential fields. See the type remarks for the
    /// limitations of zeroing <see cref="string"/> values in .NET.
    /// </summary>
    public void Dispose()
    {
        Username = null;
        Password = null;
        Url = null;
        Notes = null;
    }

    /// <summary>
    /// Returns a redacted representation that reveals which fields are present
    /// but never their values, so credentials cannot leak through logging.
    /// </summary>
    public override string ToString()
    {
        static string R(string? v) => v is null ? "null" : "<redacted>";
        return $"Entry {{ Username = {R(Username)}, Password = {R(Password)}, " +
               $"Url = {R(Url)}, Notes = {R(Notes)} }}";
    }
}
