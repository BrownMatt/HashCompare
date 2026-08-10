namespace HashCompare.Core;

/// <summary>Scans folder trees for the Config dialog's exclude pickers.</summary>
public static class FolderScanner
{
    /// <summary>Distinct folder (leaf) names found anywhere under any of the roots, sorted.</summary>
    public static List<string> CollectFolderNames(params string[] roots)
    {
        var names = new SortedSet<string>(PathComparison.Comparer);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            try
            {
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
            catch
            {
                // Ignore folders we cannot enumerate (access denied, etc.).
            }
        }

        return names.ToList();
    }

    /// <summary>Distinct file extensions (including the dot) found anywhere under any root, sorted.</summary>
    public static List<string> CollectFileExtensions(params string[] roots)
    {
        var extensions = new SortedSet<string>(PathComparison.Comparer);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            try
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (!string.IsNullOrEmpty(ext))
                        extensions.Add(ext);
                }
            }
            catch
            {
                // Ignore folders we cannot enumerate.
            }
        }

        return extensions.ToList();
    }
}