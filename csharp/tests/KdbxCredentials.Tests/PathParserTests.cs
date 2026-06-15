using KdbxCredentials;

namespace KdbxCredentials.Tests;

public class PathParserTests
{
    [Theory]
    [InlineData("ndb/postgres-prod", new[] { "ndb" }, "postgres-prod")]
    [InlineData("a/b/c/title", new[] { "a", "b", "c" }, "title")]
    [InlineData(" NDB / Postgres-Prod ", new[] { "NDB" }, "Postgres-Prod")]
    public void SplitsGroupsAndTitle(string path, string[] expectedGroups, string expectedTitle)
    {
        PathParser.ParsedPath parsed = PathParser.Parse(path);
        Assert.Equal(expectedGroups, parsed.Groups);
        Assert.Equal(expectedTitle, parsed.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("single")]
    [InlineData("root/ndb/postgres-prod")]
    [InlineData("ROOT/ndb/x")]
    [InlineData("ndb//x")]
    [InlineData("ndb/x/")]
    public void RejectsInvalidPaths(string path)
    {
        Assert.Throws<InvalidPathException>(() => PathParser.Parse(path));
    }
}
