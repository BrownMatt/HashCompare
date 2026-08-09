using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public void RememberFolderSetInsertsAtFront()
    {
        var config = new AppConfig();
        ConfigService.RememberFolderSet(config, @"C:\a", @"C:\b");
        ConfigService.RememberFolderSet(config, @"C:\c", @"C:\d");

        Assert.Equal(@"C:\c", config.FolderSets[0].Source);
        Assert.Equal(@"C:\a", config.FolderSets[1].Source);
    }

    [Fact]
    public void RememberFolderSetMovesExistingPairToFront()
    {
        var config = new AppConfig();
        ConfigService.RememberFolderSet(config, @"C:\a", @"C:\b");
        ConfigService.RememberFolderSet(config, @"C:\c", @"C:\d");
        ConfigService.RememberFolderSet(config, @"C:\a", @"C:\b");

        Assert.Equal(2, config.FolderSets.Count);
        Assert.Equal(@"C:\a", config.FolderSets[0].Source);
    }

    [Fact]
    public void DedupeIsCaseInsensitiveOnWindows()
    {
        if (OperatingSystem.IsLinux())
            return; // policy is case-sensitive there by design

        var config = new AppConfig();
        ConfigService.RememberFolderSet(config, @"C:\a", @"C:\b");
        ConfigService.RememberFolderSet(config, @"C:\A", @"C:\B");

        Assert.Single(config.FolderSets);
    }

    [Fact]
    public void ListIsCappedAt25()
    {
        var config = new AppConfig();
        for (var i = 0; i < 30; i++)
            ConfigService.RememberFolderSet(config, $@"C:\src{i}", $@"C:\dst{i}");

        Assert.Equal(25, config.FolderSets.Count);
        Assert.Equal(@"C:\src29", config.FolderSets[0].Source); // newest first
    }

    [Fact]
    public void SaveAndLoadRoundTripsAllFields()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "HashCompareTests_" + Guid.NewGuid().ToString("N"), "config.json");

        try
        {
            var config = new AppConfig
            {
                ExcludeFolders = "bin;obj",
                ExcludeFiles = "*.tmp",
                DiffToolPath = @"C:\tools\diff.exe",
                DiffToolArguments = "\"{left}\" \"{right}\" /x"
            };
            ConfigService.RememberFolderSet(config, @"C:\a", @"C:\b");

            ConfigService.Save(config, path);
            var loaded = ConfigService.Load(path);

            Assert.Equal("bin;obj", loaded.ExcludeFolders);
            Assert.Equal("*.tmp", loaded.ExcludeFiles);
            Assert.Equal(@"C:\tools\diff.exe", loaded.DiffToolPath);
            Assert.Equal("\"{left}\" \"{right}\" /x", loaded.DiffToolArguments);
            var set = Assert.Single(loaded.FolderSets);
            Assert.Equal(@"C:\a", set.Source);
            Assert.Equal(@"C:\b", set.Destination);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void LoadReturnsDefaultsForMissingOrCorruptFile()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        Assert.NotNull(ConfigService.Load(missing));

        var corrupt = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(corrupt, "{ not valid json !!");
        try
        {
            var loaded = ConfigService.Load(corrupt);
            Assert.Empty(loaded.FolderSets);
        }
        finally
        {
            File.Delete(corrupt);
        }
    }
}