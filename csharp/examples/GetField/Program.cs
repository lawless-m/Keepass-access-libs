// Print one field of one entry to stdout, so a script can capture it. The
// general form of GetPass: it selects which of the four built-in fields
// (username, password, url, notes) to print.
//
//   for /f "usebackq delims=" %%u in (`kdbx-getfield db.kdbx kdbx-master grp\title username`) do set "USER=%%u"
//   program --user %USER%
//
// Like GetPass, when asked for `password` this DELIBERATELY prints the secret.
// Use it only where exposing the value to the calling shell (its stdout, the
// parent process's environment, and any launched program's command line) is
// acceptable — it moves the secret out of plaintext config, but does not hide
// it at runtime.

using KdbxCredentials;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: kdbx-getfield <db_path> <secret_store_key> <entry_path> <field>");
    Console.Error.WriteLine("       field is one of: username, password, url, notes");
    return 2;
}

string dbPath = args[0];
string secretStoreKey = args[1];
string entryPath = args[2];
string field = args[3].ToLowerInvariant();

if (field is not ("username" or "password" or "url" or "notes"))
{
    Console.Error.WriteLine($"unknown field '{args[3]}': expected username, password, url, or notes");
    return 2;
}

try
{
    using Database db = Database.Open(dbPath, secretStoreKey);
    using Entry entry = db.Lookup(entryPath);

    // `field` is validated above, so the final arm is one of the known fields.
    string? value = field switch
    {
        "username" => entry.Username,
        "password" => entry.Password,
        "url" => entry.Url,
        _ => entry.Notes,
    };

    if (value is null)
    {
        Console.Error.WriteLine($"entry '{entryPath}' has no {field}");
        return 1;
    }

    // Just the field value, nothing else, so the caller captures it cleanly.
    Console.WriteLine(value);
    return 0;
}
catch (KdbxException ex)
{
    Console.Error.WriteLine($"failed: {ex.Message}");
    return 1;
}
