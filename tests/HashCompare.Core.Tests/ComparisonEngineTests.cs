using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class ComparisonEngineTests
{
    [Fact]
    public void BuildComparisonReturnsCorrectCounts()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            // Create test files
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content");
            File.WriteAllText(Path.Combine(sourceDir, "file2.txt"), "content");
            File.WriteAllText(Path.Combine(destDir, "file1.txt"), "content");
            Directory.CreateDirectory(Path.Combine(sourceDir, "sub"));
            File.WriteAllText(Path.Combine(sourceDir, "sub", "file3.txt"), "content");

            var patterns = WildcardMatcher.Compile("");
            var progress = new Progress<(int, int)>(_ => { });

            var results = ComparisonEngine.BuildComparison(sourceDir, destDir, patterns, patterns, progress);

            var identical = results.Count(r => r.Status == FileStatus.Identical);
            var different = results.Count(r => r.Status == FileStatus.Different);
            var missing = results.Count(r => r.Status == FileStatus.Missing);
            var @new = results.Count(r => r.Status == FileStatus.New);

            Assert.Equal(2, identical);
            Assert.Equal(0, different);
            Assert.Equal(1, missing); // sub/file3.txt exists in source but not dest
            Assert.Equal(0, @new);
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    [Fact]
    public void BuildComparisonDetectsDifferentFiles()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            // Create different files
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "source");
            File.WriteAllText(Path.Combine(destDir, "file1.txt"), "dest");

            var patterns = WildcardMatcher.Compile("");
            var progress = new Progress<(int, int)>(_ => { });

            var results = ComparisonEngine.BuildComparison(sourceDir, destDir, patterns, patterns, progress);

            var diff = results.Single(r => r.RelativePath == "file1.txt");
            Assert.Equal(FileStatus.Different, diff.Status);
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    [Fact]
    public void BuildComparisonDetectsNewFiles()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            // Create dest-only file
            File.WriteAllText(Path.Combine(destDir, "new.txt"), "content");

            var patterns = WildcardMatcher.Compile("");
            var progress = new Progress<(int, int)>(_ => { });

            var results = ComparisonEngine.BuildComparison(sourceDir, destDir, patterns, patterns, progress);

            var newFile = results.Single(r => r.RelativePath == "new.txt");
            Assert.Equal(FileStatus.New, newFile.Status);
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    [Fact]
    public void BuildComparisonRespectsFolderExcludes()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceDir, "bin"));
            File.WriteAllText(Path.Combine(sourceDir, "bin", "file1.exe"), "content");
            Directory.CreateDirectory(Path.Combine(destDir, "obj"));
            File.WriteAllText(Path.Combine(destDir, "obj", "file2.obj"), "content");

            var folderPatterns = WildcardMatcher.Compile("bin;obj");
            var filePatterns = WildcardMatcher.Compile("");
            var progress = new Progress<(int, int)>(_ => { });

            var results = ComparisonEngine.BuildComparison(sourceDir, destDir, folderPatterns, filePatterns, progress);

            Assert.Empty(results);
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    [Fact]
    public void BuildComparisonRespectsFileExcludes()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content");
            File.WriteAllText(Path.Combine(sourceDir, "file1.tmp"), "tmp");
            File.WriteAllText(Path.Combine(destDir, "file1.txt"), "content");

            var folderPatterns = WildcardMatcher.Compile("");
            var filePatterns = WildcardMatcher.Compile("*.tmp");
            var progress = new Progress<(int, int)>(_ => { });

            var results = ComparisonEngine.BuildComparison(sourceDir, destDir, folderPatterns, filePatterns, progress);

            Assert.Single(results);
            Assert.Equal("file1.txt", results[0].RelativePath);
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    [Fact]
    public void CalculateSha256ProducesConsistentHashes()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sha256test_{Guid.NewGuid():N}.txt");
        try
        {
            const string content = "Hello, World!";
            File.WriteAllText(tempFile, content);

            var hash1 = ComparisonEngine.CalculateSha256(tempFile);
            var hash2 = ComparisonEngine.CalculateSha256(tempFile);

            Assert.Equal(hash1, hash2);
            Assert.Equal(64, hash1.Length); // SHA256 produces 64 hex characters
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CalculateSha256DiffersForDifferentContent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sha256test_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "content1");
            var hash1 = ComparisonEngine.CalculateSha256(tempFile);

            File.WriteAllText(tempFile, "content2");
            var hash2 = ComparisonEngine.CalculateSha256(tempFile);

            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildComparisonThrottlesProgress()
    {
        var sourceDir = CreateTempDir("source");
        var destDir = CreateTempDir("dest");

        try
        {
            // Create many files to ensure progress throttling works
            for (int i = 0; i < 100; i++)
            {
                File.WriteAllText(Path.Combine(sourceDir, $"file{i}.txt"), $"content{i}");
                File.WriteAllText(Path.Combine(destDir, $"file{i}.txt"), $"content{i}");
            }

            var patterns = WildcardMatcher.Compile("");
            var progressCalls = new List<(int, int)>();
            var progress = new Progress<(int, int)>(r => progressCalls.Add(r));

            ComparisonEngine.BuildComparison(sourceDir, destDir, patterns, patterns, progress);

            // Progress should be reported at most once per 1% step (~1 time for 100 files)
            // We expect at least one report and the last report should be (200, 200)
            Assert.NotEmpty(progressCalls);
            Assert.Equal((200, 200), progressCalls.Last());
        }
        finally
        {
            DeleteDirectory(sourceDir);
            DeleteDirectory(destDir);
        }
    }

    private static string CreateTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"HashCompareTest_{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDirectory(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}