namespace HashCompare.Core;

/// <summary>A remembered source/destination folder pairing.</summary>
public sealed class FolderSet
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}

/// <summary>Persisted application configuration.</summary>
public sealed class AppConfig
{
    /// <summary>Most-recently-used folder pairs, newest first.</summary>
    public List<FolderSet> FolderSets { get; set; } = [];

    /// <summary>Semicolon-delimited folder name patterns (wildcards allowed) to skip.</summary>
    public string ExcludeFolders { get; set; } = string.Empty;

    /// <summary>Semicolon-delimited file extensions / name patterns (wildcards allowed) to skip.</summary>
    public string ExcludeFiles { get; set; } = string.Empty;

    /// <summary>Full path to an external diff tool used by the row "Compare" action. Empty = auto-detect.</summary>
    public string DiffToolPath { get; set; } = string.Empty;

    /// <summary>
    /// Argument template for the diff tool. The tokens <c>{left}</c> and <c>{right}</c> are replaced
    /// with the source and destination file paths.
    /// </summary>
    public string DiffToolArguments { get; set; } = "\"{left}\" \"{right}\"";
}