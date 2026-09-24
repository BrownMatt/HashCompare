using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class DiffToolResolverTests
{
    private const string Left = "/src/a.txt";
    private const string Right = "/dst/a.txt";

    private static DiffToolEnvironment Env(HostOs os, string[] files, Dictionary<string, string>? vars = null) =>
        new(os, files.Contains, name => vars is not null && vars.TryGetValue(name, out var v) ? v : null);

    private static DiffToolCommand? Resolve(DiffToolEnvironment env, AppConfig? config = null) =>
        DiffToolResolver.Resolve(config ?? new AppConfig(), Left, Right, env);

    [Fact]
    public void CurrentEnvironmentMatchesRunningOs()
    {
        var expected = OperatingSystem.IsWindows() ? HostOs.Windows
            : OperatingSystem.IsMacOS() ? HostOs.MacOS
            : HostOs.Linux;

        Assert.Equal(expected, DiffToolEnvironment.Current.Os);
    }

    [Fact]
    public void ConfiguredToolIsUsedWithItsArgumentTemplate()
    {
        var config = new AppConfig { DiffToolPath = "/opt/bc/bcompare", DiffToolArguments = "-l \"{left}\" -r \"{right}\"" };
        var env = Env(HostOs.Linux, ["/opt/bc/bcompare", "/usr/bin/meld"]);

        var command = Resolve(env, config);

        Assert.Equal(new DiffToolCommand("/opt/bc/bcompare", "-l \"/src/a.txt\" -r \"/dst/a.txt\""), command);
    }

    [Fact]
    public void ConfiguredToolWithBlankArgumentsUsesDefaultTemplate()
    {
        var config = new AppConfig { DiffToolPath = "/opt/tool", DiffToolArguments = "" };

        var command = Resolve(Env(HostOs.Linux, ["/opt/tool"]), config);

        Assert.Equal("\"/src/a.txt\" \"/dst/a.txt\"", command?.Arguments);
    }

    [Fact]
    public void MissingConfiguredToolFallsBackToDetection()
    {
        var config = new AppConfig { DiffToolPath = "/not/installed" };

        var command = Resolve(Env(HostOs.Linux, ["/usr/bin/meld"]), config);

        Assert.Equal("/usr/bin/meld", command?.FileName);
    }

    [Fact]
    public void WindowsPrefersWinMergeInProgramFiles()
    {
        var env = Env(HostOs.Windows,
            [@"C:\Program Files\WinMerge\WinMergeU.exe", @"C:\VSCode\bin\code.cmd"],
            new() { ["ProgramFiles"] = @"C:\Program Files", ["PATH"] = @"C:\VSCode\bin" });

        var command = Resolve(env);

        Assert.Equal(new DiffToolCommand(@"C:\Program Files\WinMerge\WinMergeU.exe", "\"/src/a.txt\" \"/dst/a.txt\""), command);
    }

    [Fact]
    public void WindowsFindsPerUserWinMergeInstall()
    {
        var env = Env(HostOs.Windows,
            [@"C:\Users\me\AppData\Local\Programs\WinMerge\WinMergeU.exe"],
            new() { ["LOCALAPPDATA"] = @"C:\Users\me\AppData\Local" });

        Assert.Equal(@"C:\Users\me\AppData\Local\Programs\WinMerge\WinMergeU.exe", Resolve(env)?.FileName);
    }

    [Fact]
    public void WindowsFallsBackToVsCodeOnPathWithDiffFlag()
    {
        var env = Env(HostOs.Windows,
            [@"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd"],
            new() { ["PATH"] = @"C:\Windows;C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin" });

        var command = Resolve(env);

        Assert.Equal(new DiffToolCommand(
            @"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd",
            "--diff \"/src/a.txt\" \"/dst/a.txt\""), command);
    }

    [Fact]
    public void MacFindsHomebrewMeldEvenWhenPathIsMinimal()
    {
        var env = Env(HostOs.MacOS,
            ["/opt/homebrew/bin/meld", "/usr/bin/opendiff"],
            new() { ["PATH"] = "/usr/bin:/bin" });

        Assert.Equal("/opt/homebrew/bin/meld", Resolve(env)?.FileName);
    }

    [Fact]
    public void MacFindsVsCodeAppBundleWithDiffFlag()
    {
        const string code = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
        var env = Env(HostOs.MacOS, [code, "/usr/bin/opendiff"]);

        Assert.Equal(new DiffToolCommand(code, "--diff \"/src/a.txt\" \"/dst/a.txt\""), Resolve(env));
    }

    [Fact]
    public void MacUsesOpenDiffOnlyAsLastResort()
    {
        var env = Env(HostOs.MacOS, ["/usr/bin/opendiff"]);

        Assert.Equal("/usr/bin/opendiff", Resolve(env)?.FileName);
    }

    [Fact]
    public void LinuxFindsMeldOnPath()
    {
        var env = Env(HostOs.Linux, ["/home/me/.local/bin/meld"], new() { ["PATH"] = "/home/me/.local/bin:/usr/bin" });

        Assert.Equal("/home/me/.local/bin/meld", Resolve(env)?.FileName);
    }

    [Fact]
    public void LinuxFallsBackToSnapVsCodeWithDiffFlag()
    {
        var env = Env(HostOs.Linux, ["/snap/bin/code"]);

        Assert.Equal(new DiffToolCommand("/snap/bin/code", "--diff \"/src/a.txt\" \"/dst/a.txt\""), Resolve(env));
    }

    [Theory]
    [InlineData(HostOs.Windows)]
    [InlineData(HostOs.MacOS)]
    [InlineData(HostOs.Linux)]
    public void ReturnsNullWhenNoToolIsInstalled(HostOs os)
    {
        Assert.Null(Resolve(Env(os, [], new() { ["PATH"] = "/usr/bin" })));
    }
}
