using System.Diagnostics;
using System.IO;
using HashCompare.Core;

namespace HashCompare.App.Services;

/// <summary>Launches an external diff tool per-OS behavior. Returns true if launched successfully, false if no tool found.</summary>
public static class DiffToolLauncher
{
    private const string DefaultArguments = "\"{left}\" \"{right}\"";
    private const string VsCodeArguments = "--diff \"{left}\" \"{right}\"";

    /// <summary>
    /// Launches an external diff tool. Prefers the configured tool in AppConfig, then falls back to OS-probed tools.
    /// </summary>
    /// <param name="config">Application configuration with optional DiffToolPath and DiffToolArguments.</param>
    /// <param name="left">Path to the left (source) file.</param>
    /// <param name="right">Path to the right (destination) file.</param>
    /// <returns>True if a tool was launched, false if no suitable tool was found on the system.</returns>
    public static bool TryLaunch(AppConfig config, string left, string right)
    {
        // 1. Configured tool with token replacement.
        if (!string.IsNullOrWhiteSpace(config.DiffToolPath) && File.Exists(config.DiffToolPath))
        {
            var template = string.IsNullOrWhiteSpace(config.DiffToolArguments)
                ? DefaultArguments
                : config.DiffToolArguments;
            var args = template.Replace("{left}", left).Replace("{right}", right);
            Process.Start(new ProcessStartInfo(config.DiffToolPath, args) { UseShellExecute = false });
            return true;
        }

        // 2. Per-OS auto-detection.
        var tool = DetectDiffTool();
        if (tool == null)
            return false;

        var diffArgs = tool.Value.Arguments.Replace("{left}", left).Replace("{right}", right);
        Process.Start(new ProcessStartInfo(tool.Value.Path, diffArgs) { UseShellExecute = true });
        return true;
    }

    /// <summary>
    /// Probes the system for a diff tool matching the user's OS without starting any process.
    /// Returns the tool path and its argument template, or null if none found.
    /// </summary>
    private static (string Path, string Arguments)? DetectDiffTool()
    {
        // Windows: WinMerge in multiple locations, then VS Code.
        if (OperatingSystem.IsWindows())
        {
            foreach (var winMerge in new[]
                     {
                         @"C:\Program Files\WinMerge\WinMergeU.exe",
                         @"C:\Program Files (x86)\WinMerge\WinMergeU.exe"
                     })
            {
                if (File.Exists(winMerge))
                    return (winMerge, DefaultArguments);
            }

            // On Windows the VS Code launcher on PATH is code.cmd; the extensionless "code" is a shell script.
            var code = FindOnPath("code.cmd") ?? FindOnPath("code");
            return code == null ? null : (code, VsCodeArguments);
        }

        // macOS: opendiff, then Meld, then VS Code.
        if (OperatingSystem.IsMacOS())
        {
            // Apps started from Finder get a minimal PATH, so also check the Homebrew prefixes.
            var opendiff = FindOnPath("opendiff", "/usr/bin");
            if (opendiff != null)
                return (opendiff, DefaultArguments);

            var meld = FindOnPath("meld", "/opt/homebrew/bin", "/usr/local/bin");
            if (meld != null)
                return (meld, DefaultArguments);

            var code = FindOnPath("code", "/opt/homebrew/bin", "/usr/local/bin");
            return code == null ? null : (code, VsCodeArguments);
        }

        // Linux: Meld, then VS Code.
        if (OperatingSystem.IsLinux())
        {
            var meld = FindOnPath("meld", "/usr/bin", "/usr/local/bin");
            if (meld != null)
                return (meld, DefaultArguments);

            var code = FindOnPath("code", "/usr/bin", "/usr/local/bin", "/snap/bin");
            return code == null ? null : (code, VsCodeArguments);
        }

        return null;
    }

    /// <summary>
    /// Searches each PATH entry, then any extra fallback directories, for an executable file with the given name.
    /// Returns its full path, or null if not found.
    /// </summary>
    private static string? FindOnPath(string fileName, params string[] extraDirectories)
    {
        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathDirectories.Concat(extraDirectories))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory.Trim('"'), fileName);
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry; skip it.
                continue;
            }

            if (IsExecutableFile(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsExecutableFile(string path)
    {
        if (!File.Exists(path))
            return false;

        if (OperatingSystem.IsWindows())
            return true;

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & anyExecute) != 0;
    }
}
