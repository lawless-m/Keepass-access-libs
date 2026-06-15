namespace KdbxCredentials;

/// <summary>
/// Parses and validates <c>/</c>-separated entry paths per <c>SPEC.md</c>:
/// the implied root group must be omitted, matching is case-insensitive, and
/// leading/trailing whitespace on each segment is ignored.
/// </summary>
internal static class PathParser
{
    /// <summary>A validated, trimmed group/title split.</summary>
    internal readonly record struct ParsedPath(IReadOnlyList<string> Groups, string Title);

    /// <summary>
    /// Parse and validate <paramref name="path"/>.
    /// </summary>
    /// <exception cref="InvalidPathException">
    /// Thrown for empty, single-segment, empty-segment, or root-prefixed paths.
    /// </exception>
    internal static ParsedPath Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidPathException(path, "path is empty");
        }

        string[] segments = path.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = segments[i].Trim();
        }

        if (segments.Any(string.IsNullOrEmpty))
        {
            throw new InvalidPathException(path, "path contains an empty segment");
        }

        if (segments.Length < 2)
        {
            throw new InvalidPathException(path, "path has a single segment — a group is required");
        }

        if (string.Equals(segments[0], "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPathException(path, "the root group must not be included in the path");
        }

        string title = segments[^1];
        string[] groups = segments[..^1];
        return new ParsedPath(groups, title);
    }
}
