using System.Diagnostics;
using System.IO;
using HashCompare.Core;

namespace HashCompare.App.Services;

/// <summary>Launches an external diff tool per-OS behavior. Returns true if launched successfully, false if no tool found.</summary>
public static class DiffToolLauncher
{
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
                ? "\"{left}\" \"{right}\""
                : config.DiffToolArguments;
            var args = template.Replace("{left}", left).Replace("{right}", right);
            Process.Start(new ProcessStartInfo(config.DiffToolPath, args) { UseShellExecute = false });
            return true;
        }

        // 2. Per-OS auto-detection.
        var tool = DetectDiffTool();
        if (tool == null)
            return false;

        var diffArgs = "\"{left}\" \"{right}\"".Replace("{left}", left).Replace("{right}", right);
        Process.Start(new ProcessStartInfo(tool, diffArgs) { UseShellExecute = true });
        return true;
    }

    /// <summary>Probes the system for a diff tool matching the user's OS. Returns the tool path or null if none found.</summary>
    private static string? DetectDiffTool()
    {
        var platform = Environment.OSVersion.Platform;

        // Windows: WinMerge in multiple locations, then VS Code.
        if (platform == PlatformID.Win32NT)
        {
            foreach (var winMerge in new[]
                     {
                         @"C:\Program Files\WinMerge\WinMergeU.exe",
                         @"C:\Program Files (x86)\WinMerge\WinMergeU.exe"
                     })
            {
                if (File.Exists(winMerge))
                    return winMerge;
            }

            // Fallback to VS Code on PATH.
            try
            {
                // UseShellExecute=true for VS Code as per deliberate change #3.
                Process.Start(new ProcessStartInfo("code", "--diff \"{left}\" \"{right}\"")
                    { UseShellExecute = true });
                return "code";
            }
            catch
            {
                // code not in PATH, continue to next OS probe.
            }
        }

        // macOS: opendiff, then Meld, then VS Code.
        if (platform == PlatformID.MacOSX)
        {
            if (File.Exists("/usr/bin/opendiff"))
                return "/usr/bin/opendiff";

            if (File.Exists("/usr/local/bin/meld"))
                return "/usr/local/bin/meld";

            try
            {
                Process.Start(new ProcessStartInfo("code", "--diff \"{left}\" \"{right}\"")
                    { UseShellExecute = false });
                return "code";
            }
            catch
            {
                // code not in PATH, continue to next OS probe.
            }
        }

        // Linux: Meld, then VS Code.
        if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
        {
            // Meld often installed in /usr/bin or ~/local/bin.
            if (File.Exists("/usr/bin/meld"))
                return "/usr/bin/meld";

            if (File.Exists("/usr/local/bin/meld"))
                return "/usr/local/bin/meld";

            try
            {
                Process.Start(new ProcessStartInfo("code", "--diff \"{left}\" \"{right}\"")
                    { UseShellExecute = false });
                return "code";
            }
            catch
            {
                // code not in PATH, nothing left to try.
            }
        }

        return null;
    }
}