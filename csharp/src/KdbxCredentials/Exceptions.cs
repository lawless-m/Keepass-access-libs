namespace KdbxCredentials;

/// <summary>
/// Base type for all errors raised by this library. Callers can catch this to
/// handle any failure, or catch a specific subtype to branch on the cause.
/// </summary>
/// <remarks>
/// Messages are intended for operators and never contain credential material.
/// Where an underlying cause exists it is preserved as <see cref="Exception.InnerException"/>,
/// already wrapped so that no secret values leak.
/// </remarks>
public abstract class KdbxException : Exception
{
    private protected KdbxException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// The OS secret store was queried for the configured key and returned nothing.
/// The machine has most likely not been provisioned.
/// </summary>
public sealed class SecretNotFoundException : KdbxException
{
    /// <summary>The secret-store key that was looked up.</summary>
    public string Key { get; }

    /// <summary>Creates a new <see cref="SecretNotFoundException"/>.</summary>
    public SecretNotFoundException(string key)
        : base($"secret not found in OS store for key '{key}'")
        => Key = key;
}

/// <summary>The file at the configured path does not exist or is not a file.</summary>
public sealed class DatabaseNotFoundException : KdbxException
{
    /// <summary>The database path that could not be found.</summary>
    public string Path { get; }

    /// <summary>Creates a new <see cref="DatabaseNotFoundException"/>.</summary>
    public DatabaseNotFoundException(string path)
        : base($"database file not found: {path}")
        => Path = path;
}

/// <summary>
/// The file exists and is readable but cannot be parsed as a valid KDBX4
/// database (truncated, corrupt, or an unsupported format such as KDBX 3.1).
/// </summary>
public sealed class DatabaseCorruptException : KdbxException
{
    /// <summary>Creates a new <see cref="DatabaseCorruptException"/>.</summary>
    public DatabaseCorruptException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// The master password retrieved from the OS secret store was rejected by the
/// database. The stored password is most likely stale.
/// </summary>
public sealed class AuthenticationFailedException : KdbxException
{
    /// <summary>Creates a new <see cref="AuthenticationFailedException"/>.</summary>
    public AuthenticationFailedException(Exception? inner = null)
        : base("authentication failed — master password may be stale", inner)
    {
    }
}

/// <summary>
/// The process lacks permission to read the database file or to query the OS
/// secret store.
/// </summary>
public sealed class PermissionDeniedException : KdbxException
{
    /// <summary>Creates a new <see cref="PermissionDeniedException"/>.</summary>
    public PermissionDeniedException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>The path string supplied to <see cref="Database.Lookup"/> is malformed.</summary>
public sealed class InvalidPathException : KdbxException
{
    /// <summary>The offending path, as supplied by the caller.</summary>
    public string Path { get; }

    /// <summary>Creates a new <see cref="InvalidPathException"/>.</summary>
    public InvalidPathException(string path, string reason)
        : base($"invalid path '{path}': {reason}")
        => Path = path;
}

/// <summary>The path is well-formed but no entry exists at that location.</summary>
public sealed class EntryNotFoundException : KdbxException
{
    /// <summary>The path that matched no entry.</summary>
    public string Path { get; }

    /// <summary>Creates a new <see cref="EntryNotFoundException"/>.</summary>
    public EntryNotFoundException(string path)
        : base($"entry not found at path '{path}'")
        => Path = path;
}

/// <summary>
/// The path resolves to more than one entry. Indicates duplicate titles within
/// a group in the database.
/// </summary>
public sealed class AmbiguousEntryException : KdbxException
{
    /// <summary>The path that matched more than one entry.</summary>
    public string Path { get; }

    /// <summary>Creates a new <see cref="AmbiguousEntryException"/>.</summary>
    public AmbiguousEntryException(string path)
        : base($"ambiguous path '{path}' — multiple entries match")
        => Path = path;
}
