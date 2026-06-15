// Open a KDBX database using the master password from the OS secret store and
// print one entry's non-secret fields. Used by the systemd-credentials
// integration test, but also a minimal usage example.
//
//   kdbx-lookup <db_path> <secret_store_key> <entry_path>
//
// On Linux the master password is read from the systemd credential named
// <secret_store_key> (the file $CREDENTIALS_DIRECTORY/<secret_store_key>), so
// this must run under a systemd unit that loaded that credential.
//
// The looked-up password is deliberately not printed; the username and URL are
// enough to prove the database was decrypted with the provisioned master
// password.

using KdbxCredentials;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: kdbx-lookup <db_path> <secret_store_key> <entry_path>");
    return 2;
}

string dbPath = args[0];
string secretStoreKey = args[1];
string entryPath = args[2];

try
{
    using Database db = Database.Open(dbPath, secretStoreKey);
    using Entry entry = db.Lookup(entryPath);

    Console.WriteLine($"OK: opened database via systemd credential '{secretStoreKey}'");
    Console.WriteLine($"username: {entry.Username ?? "<none>"}");
    Console.WriteLine($"url:      {entry.Url ?? "<none>"}");
    Console.WriteLine("password: <retrieved, not printed>");
    return 0;
}
catch (KdbxException ex)
{
    Console.Error.WriteLine($"failed: {ex.Message}");
    return 1;
}
