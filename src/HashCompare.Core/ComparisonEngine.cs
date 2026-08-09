using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HashCompare.Core;

/// <summary>
/// The comparison core. Pure static functions safe to run on a background thread:
/// no UI, no shared mutable state.
/// </summary>
public static class ComparisonEngine
{
    /// <summary>
    /// Enumerates and hashes files under both roots, reporting progress, and returns the rows
    /// to display: every source file classified as Identical/Different/Missing, and every
    /// destination-only file as New.
    /// </summary>
    public static List<FileComparisonResult> BuildComparison(
        string sourceDir,
        string destDir,
        IReadOnlyList<Regex> folderPatterns,
        IReadOnlyList<Regex> filePatterns,
        IProgress<(int done, int total)> progress)
    {
        bool IsExcluded(string relativePath)
        {
            if (WildcardMatcher.IsMatch(filePatterns, Path.GetFileName(relativePath)))
                return true;

            var dir = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrEmpty(dir))
                return false;

            return dir.Split('\\', '/').Any(segment => WildcardMatcher.IsMatch(folderPatterns, segment));
        }

        var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(sourceDir, f))
            .Where(rel => !IsExcluded(rel))
            .ToList();

        var destFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(destDir, f))
            .Where(rel => !IsExcluded(rel))
            .ToHashSet(PathComparison.Comparer);

        var sourceFileSet = new HashSet<string>(sourceFiles, PathComparison.Comparer);
        var results = new List<FileComparisonResult>(sourceFiles.Count + destFiles.Count);

        var total = sourceFiles.Count + destFiles.Count;
        var done = 0;
        var step = Math.Max(1, total / 100); // throttle progress updates to ~1% increments

        void Report()
        {
            done++;
            if (done % step == 0 || done == total)
                progress.Report((done, total));
        }

        // Source-side pass: classify each source file against its destination counterpart.
        foreach (var relativePath in sourceFiles)
        {
            var sourcePath = Path.Combine(sourceDir, relativePath);
            var destPath = Path.Combine(destDir, relativePath);
            var destExists = destFiles.Contains(relativePath);

            FileStatus status;
            if (!destExists)
                status = FileStatus.Missing;
            else
                status = CalculateSha256(sourcePath) == CalculateSha256(destPath)
                    ? FileStatus.Identical
                    : FileStatus.Different;

            results.Add(new FileComparisonResult
            {
                RelativePath = relativePath,
                SourceFullPath = sourcePath,
                DestFullPath = destPath,
                Status = status
            });

            Report();
        }

        // Destination-only files are "New".
        foreach (var relativePath in destFiles)
        {
            if (sourceFileSet.Contains(relativePath))
            {
                Report();
                continue;
            }

            results.Add(new FileComparisonResult
            {
                RelativePath = relativePath,
                SourceFullPath = Path.Combine(sourceDir, relativePath),
                DestFullPath = Path.Combine(destDir, relativePath),
                Status = FileStatus.New
            });

            Report();
        }

        return results;
    }

    public static string CalculateSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    public static void CopyOver(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Copy(source, destination, overwrite: true);
    }
}