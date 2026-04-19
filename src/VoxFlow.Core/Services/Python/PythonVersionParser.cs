namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Parses the output of <c>python --version</c>. CPython 3.4+ writes the
/// version line to stdout; older builds wrote to stderr. Callers should pass
/// whichever stream was non-empty.
/// </summary>
internal static class PythonVersionParser
{
    private const string Prefix = "Python ";

    public static string? Parse(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }
        return trimmed[Prefix.Length..].Trim();
    }
}
