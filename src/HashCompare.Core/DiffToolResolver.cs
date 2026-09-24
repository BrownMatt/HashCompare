namespace HashCompare.Core;

/// <summary>An executable and argument string ready to pass to <c>ProcessStartInfo</c>.</summary>
public sealed record DiffToolCommand(string FileName, string Arguments);

/// <summary>The operating systems the diff tool probe distinguishes between.</summary>
public enum HostOs
{
    Windows,
    MacOS,
    Linux
}

/// <summary>
/// The parts of the host environment the resolver looks at. Injected so tests can simulate
/// any OS and any set of installed tools.
/// </summary>
public sealed record DiffToolEnvironment(
    HostOs Os,
    Func<string, bool> FileExists,
    Func<string, string?> GetEnvironmentVariable)
{
    /// <summary>The environment of the current process.</summary>
    public static DiffToolEnvironment Current { get; } = new(
        OperatingSystem.IsWindows() ? HostOs.Windows
            : OperatingSystem.IsMacOS() ? HostOs.MacOS
            : HostOs.Linux,
        File.Exists,
        Environment.GetEnvironmentVariable);
}

/// <summary>
/// Decides which external diff tool the row "Compare" action runs: the configured tool if it
/// exists, otherwise the first tool found by a per-OS probe. Resolving never starts a process.
/// </summary>
public static class DiffToolResolver
{
    public const string DefaultArguments = "\"{left}\" \"{right}\"";
    public const string VsCodeArguments = "--diff \"{left}\" \"{right}\"";

    // GUI apps on macOS don't inherit the shell PATH, so Homebrew locations are probed explicitly.
    private static readonly string[] MacExtraDirs = ["/opt/homebrew/bin", "/usr/local/bin"];
    private static readonly string[] LinuxExtraDirs = ["/usr/bin", "/usr/local/bin", "/snap/bin"];

    public static DiffToolCommand? Resolve(AppConfig config, string left, string right) =>
        Resolve(config, left, right, DiffToolEnvironment.Current);

    public static DiffToolCommand? Resolve(AppConfig config, string left, string right, DiffToolEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(config.DiffToolPath) && env.FileExists(config.DiffToolPath))
        {
            var template = string.IsNullOrWhiteSpace(config.DiffToolArguments)
                ? DefaultArguments
                : config.DiffToolArguments;
            return new DiffToolCommand(config.DiffToolPath, Expand(template, left, right));
        }

        var detected = Detect(env);
        return detected is null
            ? null
            : new DiffToolCommand(detected.Value.Path, Expand(detected.Value.Template, left, right));
    }

    private static string Expand(string template, string left, string right) =>
        template.Replace("{left}", left).Replace("{right}", right);

    private static (string Path, string Template)? Detect(DiffToolEnvironment env)
    {
        switch (env.Os)
        {
            case HostOs.Windows:
            {
                var winMerge = FirstExisting(env,
                    Combine(env.GetEnvironmentVariable("ProgramFiles"), @"WinMerge\WinMergeU.exe"),
                    Combine(env.GetEnvironmentVariable("ProgramFiles(x86)"), @"WinMerge\WinMergeU.exe"),
                    Combine(env.GetEnvironmentVariable("LOCALAPPDATA"), @"Programs\WinMerge\WinMergeU.exe"));
                if (winMerge is not null)
                    return (winMerge, DefaultArguments);

                var code = FindOnPath(env, [], "code.cmd", "code.exe");
                return code is null ? null : (code, VsCodeArguments);
            }

            case HostOs.MacOS:
            {
                var meld = FindOnPath(env, MacExtraDirs, "meld")
                           ?? FirstExisting(env, "/Applications/Meld.app/Contents/MacOS/Meld");
                if (meld is not null)
                    return (meld, DefaultArguments);

                var code = FindOnPath(env, MacExtraDirs, "code")
                           ?? FirstExisting(env, "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code");
                if (code is not null)
                    return (code, VsCodeArguments);

                // Last: /usr/bin/opendiff is a stub on every Mac but only works with Xcode installed.
                var openDiff = FirstExisting(env, "/usr/bin/opendiff");
                return openDiff is null ? null : (openDiff, DefaultArguments);
            }

            default:
            {
                var meld = FindOnPath(env, LinuxExtraDirs, "meld");
                if (meld is not null)
                    return (meld, DefaultArguments);

                var code = FindOnPath(env, LinuxExtraDirs, "code");
                return code is null ? null : (code, VsCodeArguments);
            }
        }
    }

    /// <summary>Searches each PATH entry, then <paramref name="extraDirs"/>, for any of the names.</summary>
    private static string? FindOnPath(DiffToolEnvironment env, string[] extraDirs, params string[] names)
    {
        var separator = env.Os == HostOs.Windows ? ';' : ':';
        var pathDirs = (env.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in pathDirs.Concat(extraDirs))
        foreach (var name in names)
        {
            var candidate = Combine(dir, name);
            if (candidate is not null && env.FileExists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FirstExisting(DiffToolEnvironment env, params string?[] candidates) =>
        candidates.FirstOrDefault(c => c is not null && env.FileExists(c));

    // Joins with the separator of the simulated OS, not the host, so tests behave the same everywhere.
    private static string? Combine(string? dir, string name)
    {
        if (string.IsNullOrEmpty(dir))
            return null;

        var sep = dir.Contains('\\') || name.Contains('\\') ? '\\' : '/';
        return dir.TrimEnd('\\', '/') + sep + name;
    }
}
