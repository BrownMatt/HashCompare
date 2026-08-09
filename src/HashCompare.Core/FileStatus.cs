namespace HashCompare.Core;

/// <summary>Classification of one file across the two trees.</summary>
public enum FileStatus
{
    Identical,
    Different,
    /// <summary>Exists in source only.</summary>
    Missing,
    /// <summary>Exists in destination only.</summary>
    New
}

public static class FileStatusExtensions
{
    /// <summary>Display text — must stay exactly these strings; the UI and legend show them.</summary>
    public static string ToDisplayString(this FileStatus status) => status switch
    {
        FileStatus.Identical => "Identical",
        FileStatus.Different => "Different",
        FileStatus.Missing => "Missing",
        FileStatus.New => "New",
        _ => status.ToString()
    };
}