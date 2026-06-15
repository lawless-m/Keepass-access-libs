using KdbxCredentials;

namespace KdbxCredentials.Tests;

/// <summary>
/// Opens the committed KDBX4 fixture (<c>data/test.kdbx</c>, master password
/// <c>test</c>) and exercises lookup and error mapping. The secret-store path is
/// bypassed via the internal <c>OpenWithPassword</c> so no provisioned OS store
/// is needed.
/// </summary>
public class DatabaseTests
{
    private const string Password = "test";

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "data", "test.kdbx");

    private static Database OpenFixture() => Database.OpenWithPassword(FixturePath, Password);

    [Fact]
    public void OpensAndReadsAllFields()
    {
        using Database db = OpenFixture();
        using Entry entry = db.Lookup("ndb/postgres-prod");
        Assert.Equal("pgadmin", entry.Username);
        Assert.Equal("s3cr3t-pg", entry.Password);
        Assert.Equal("postgres://db.internal:5432/prod", entry.Url);
        Assert.Equal("Production Postgres", entry.Notes);
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        using Database db = OpenFixture();
        using Entry entry = db.Lookup("NDB/Postgres-Prod");
        Assert.Equal("pgadmin", entry.Username);
    }

    [Theory]
    [InlineData("storage/s3-backups", "AKIAEXAMPLE")]
    [InlineData("api/github", "ci-bot")]
    public void OtherGroupsResolve(string path, string expectedUser)
    {
        using Database db = OpenFixture();
        using Entry entry = db.Lookup(path);
        Assert.Equal(expectedUser, entry.Username);
    }

    [Theory]
    [InlineData("ndb/does-not-exist")]
    [InlineData("nosuchgroup/whatever")]
    public void MissingEntryOrGroupThrows(string path)
    {
        using Database db = OpenFixture();
        Assert.Throws<EntryNotFoundException>(() => db.Lookup(path));
    }

    [Fact]
    public void DuplicateTitlesAreAmbiguous()
    {
        using Database db = OpenFixture();
        Assert.Throws<AmbiguousEntryException>(() => db.Lookup("dup/duplicate"));
    }

    [Fact]
    public void WrongPasswordIsAuthenticationFailed()
    {
        Assert.Throws<AuthenticationFailedException>(
            () => Database.OpenWithPassword(FixturePath, "wrong-password"));
    }

    [Fact]
    public void MissingFileIsDatabaseNotFound()
    {
        Assert.Throws<DatabaseNotFoundException>(
            () => Database.OpenWithPassword("/no/such/file.kdbx", Password));
    }

    [Fact]
    public void NonKdbxFileIsCorrupt()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"kdbx-not-a-db-{Guid.NewGuid():N}.bin");
        File.WriteAllText(tmp, "this is definitely not a kdbx database");
        try
        {
            Assert.Throws<DatabaseCorruptException>(
                () => Database.OpenWithPassword(tmp, Password));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void ToStringDoesNotLeakSecrets()
    {
        using Database db = OpenFixture();
        using Entry entry = db.Lookup("ndb/postgres-prod");
        string rendered = entry.ToString();
        Assert.DoesNotContain("s3cr3t-pg", rendered);
        Assert.Contains("<redacted>", rendered);
    }
}
