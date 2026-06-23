using System.Text.RegularExpressions;

namespace HashCompare;

/// <summary>Compiles semicolon-delimited wildcard patterns (* and ?) into case-insensitive regexes.</summary>
public static class WildcardMatcher
{
    public static IReadOnlyList<Regex> Compile(string patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
            return [];

        var compiled = new List<Regex>();
        foreach (var raw in patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pattern = "^" + Regex.Escape(raw).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            compiled.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        return compiled;
    }

    public static bool IsMatch(IReadOnlyList<Regex> patterns, string value) =>
        patterns.Any(r => r.IsMatch(value));

    /// <summary>
    /// Normalises file-exclude entries into filename wildcards. Bare extensions (".tmp" or "tmp")
    /// become "*.tmp"; entries that already contain wildcards are left as-is.
    /// </summary>
    public static string ExpandFileExcludes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var expanded = raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p =>
                p.Contains('*') || p.Contains('?') ? p
                : p.StartsWith('.') ? "*" + p
                : "*." + p);

        return string.Join(';', expanded);
    }
}
