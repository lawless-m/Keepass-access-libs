// Print one entry's password to stdout, so a script can capture it and pass it
// to another program. Companion to the Lookup example for the batch-file use
// case:
//
//   @echo off
//   for /f "usebackq delims=" %%p in (`kdbx-getpass db.kdbx kdbx-master grp\title`) do set "PW=%%p"
//   program --password %PW% --log c:\tmp
//   set "PW="
//
// Unlike Lookup, this DELIBERATELY prints the password. Use it only where
// exposing the secret to the calling shell (its stdout, the parent process's
// environment, and the launched program's command line) is acceptable — it
// moves the secret out of plaintext config, but does not hide it at runtime.

using KdbxCredentials;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: kdbx-getpass <db_path> <secret_store_key> <entry_path>");
    return 2;
}

string dbPath = args[0];
string secretStoreKey = args[1];
string entryPath = args[2];

try
{
    using Database db = Database.Open(dbPath, secretStoreKey);
    using Entry entry = db.Lookup(entryPath);

    if (entry.Password is null)
    {
        Console.Error.WriteLine($"entry '{entryPath}' has no password");
        return 1;
    }

    // Just the password, nothing else, so `for /f` captures it cleanly.
    Console.WriteLine(entry.Password);
    return 0;
}
catch (KdbxException ex)
{
    Console.Error.WriteLine($"failed: {ex.Message}");
    return 1;
}
