namespace HashCompare.Core;

/// <summary>
/// Path string comparison policy: case-insensitive on Windows and macOS, case-sensitive on Linux.
/// </summary>
public static class PathComparison
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Compares two paths for equality using the OS-appropriate case sensitivity.</summary>
    public static bool Equal(string a, string b) =>
        string.Equals(a, b, Comparison);

    /// <summary>Determines if the path 'a' should be considered equal to 'b' (used for MRU deduplication).</summary>
    public static bool IsSame(string a, string b) => Equal(a, b);
}