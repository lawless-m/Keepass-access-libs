using KdbxCredentials.SecretStore;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace KdbxCredentials;

/// <summary>
/// An open, decrypted KeePass (KDBX4) database, opened read-only.
/// </summary>
/// <remarks>
/// <para>
/// Obtain an instance via <see cref="Open"/> and release it with
/// <see cref="Dispose"/> (use a <c>using</c> block). Instances are not
/// thread-safe; callers needing concurrent access must open separate handles.
/// </para>
/// <para>The database file is never modified by this library.</para>
/// </remarks>
public sealed class Database : IDisposable
{
    private readonly PwDatabase _db;
    private bool _disposed;

    private Database(PwDatabase db) => _db = db;

    /// <summary>
    /// Open the database at <paramref name="dbPath"/>, authenticating with the
    /// master password stored under <paramref name="secretStoreKey"/> in the OS
    /// secret store.
    /// </summary>
    /// <exception cref="SecretNotFoundException">The secret store has no entry for the key.</exception>
    /// <exception cref="DatabaseNotFoundException">The path does not exist or is not a file.</exception>
    /// <exception cref="DatabaseCorruptException">The file is not a valid KDBX4 database.</exception>
    /// <exception cref="AuthenticationFailedException">The master password was rejected.</exception>
    /// <exception cref="PermissionDeniedException">The file or secret store is not readable.</exception>
    public static Database Open(string dbPath, string secretStoreKey)
    {
        ArgumentNullException.ThrowIfNull(dbPath);
        ArgumentNullException.ThrowIfNull(secretStoreKey);

        // Resolve the master password first; without it nothing can proceed.
        // The char[] is zeroed once it has been handed to the composite key.
        char[] master = SecretStoreFactory.Create().GetMasterPassword(secretStoreKey);
        try
        {
            return OpenWithPassword(dbPath, new string(master));
        }
        finally
        {
            Array.Clear(master);
        }
    }

    /// <summary>
    /// Open the database using an already-resolved master password. Holds the
    /// KDBX-opening logic separate from secret-store retrieval so it can be
    /// exercised in tests without a provisioned OS store.
    /// </summary>
    internal static Database OpenWithPassword(string dbPath, string password)
    {
        if (!File.Exists(dbPath))
        {
            throw new DatabaseNotFoundException(dbPath);
        }

        var key = new CompositeKey();
        key.AddUserKey(new KcpPassword(password));

        var db = new PwDatabase();
        try
        {
            db.Open(IOConnectionInfo.FromPath(dbPath), key, null);
        }
        catch (InvalidCompositeKeyException ex)
        {
            throw new AuthenticationFailedException(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PermissionDeniedException(
                "permission denied reading the database file", ex);
        }
        catch (FileNotFoundException)
        {
            throw new DatabaseNotFoundException(dbPath);
        }
        catch (KdbxException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Wrap any other parse/crypto failure without leaking detail.
            throw new DatabaseCorruptException(
                "database file is corrupt or in an unsupported format", ex);
        }

        return new Database(db);
    }

    /// <summary>
    /// Look up a single entry by its case-insensitive <c>group/title</c> path.
    /// The implied root group must be omitted. See <c>SPEC.md</c> § Path Lookup.
    /// </summary>
    /// <exception cref="InvalidPathException">The path is empty, single-segment, or rooted.</exception>
    /// <exception cref="EntryNotFoundException">The path is valid but matches no entry.</exception>
    /// <exception cref="AmbiguousEntryException">The path matches more than one entry.</exception>
    public Entry Lookup(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        PathParser.ParsedPath parsed = PathParser.Parse(path);

        PwGroup group = _db.RootGroup;
        foreach (string segment in parsed.Groups)
        {
            PwGroup? next = group.Groups.FirstOrDefault(
                g => string.Equals(g.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                throw new EntryNotFoundException(path);
            }

            group = next;
        }

        List<PwEntry> matches = group.Entries.Where(e => string.Equals(
            e.Strings.ReadSafe(PwDefs.TitleField),
            parsed.Title,
            StringComparison.OrdinalIgnoreCase)).ToList();

        return matches.Count switch
        {
            0 => throw new EntryNotFoundException(path),
            1 => BuildEntry(matches[0]),
            _ => throw new AmbiguousEntryException(path),
        };
    }

    private static Entry BuildEntry(PwEntry e) => new(
        username: Field(e, PwDefs.UserNameField),
        password: Field(e, PwDefs.PasswordField),
        url: Field(e, PwDefs.UrlField),
        notes: Field(e, PwDefs.NotesField));

    /// <summary>Read a field's decrypted value, or <see langword="null"/> if absent.</summary>
    private static string? Field(PwEntry e, string key)
        => e.Strings.Exists(key) ? e.Strings.ReadSafe(key) : null;

    /// <summary>Releases the database and clears its in-memory contents.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // PwDatabase.Close() releases the decrypted data held in memory. The
        // master password was already zeroed in Open and is not retained here.
        _db.Close();
        _disposed = true;
    }
}
