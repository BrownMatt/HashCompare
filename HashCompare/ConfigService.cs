using System.IO;
using System.Text.Json;

namespace HashCompare;

/// <summary>Loads and saves <see cref="AppConfig"/> to %APPDATA%\HashCompare\config.json.</summary>
public static class ConfigService
{
    /// <summary>Maximum number of remembered folder pairs.</summary>
    public const int MaxFolderSets = 25;

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HashCompare");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crashing.
        }

        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
    }

    /// <summary>
    /// Records the given pair as most-recently-used: an existing identical pair is moved to the
    /// front, and the list is capped at <see cref="MaxFolderSets"/>.
    /// </summary>
    public static void RememberFolderSet(AppConfig config, string source, string destination)
    {
        config.FolderSets.RemoveAll(fs =>
            string.Equals(fs.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fs.Destination, destination, StringComparison.OrdinalIgnoreCase));

        config.FolderSets.Insert(0, new FolderSet { Source = source, Destination = destination });

        if (config.FolderSets.Count > MaxFolderSets)
            config.FolderSets.RemoveRange(MaxFolderSets, config.FolderSets.Count - MaxFolderSets);
    }
}
