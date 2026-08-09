# Detailed Plan: Port HashCompare to Cross-Platform Avalonia

This document is a **complete, self-contained execution plan**. Follow it top to bottom. Every phase ends with a **gate** — an exact command and its expected result. Do not start a phase until the previous gate passes.

## 0. Rules of engagement (read first)

1. **Never modify or delete anything under `HashCompare/` (the existing WPF project) or the file `HashCompare.sln`.** The WPF app must keep working side-by-side with the new Avalonia app. You will *read* those files constantly (they are the behavioral spec), but never write to them.
2. **No git commits, branches, or pushes.** You only create files. The only git command you may run is `git status` (read-only) to confirm what you created.
3. All new files go in exactly three places: repo root (`HashCompare.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `PORT-STATUS.md`), `src/`, and `tests/`. A `verification/` folder is created in Phase 6 for screenshots.
4. **Working directory for every command:** `C:\Users\Matt Brown\RiderProjects\HashCompare` (the repo root).
5. **Stop condition:** if a gate fails twice after applying the Troubleshooting appendix (section 9), stop. Write what happened — the exact error, what you tried — to `PORT-STATUS.md` at the repo root and end. Do not improvise redesigns.
6. Code listings in this document marked **VERBATIM** must be written exactly as shown. Sections marked **ADAPT** describe precise edits to existing WPF files being copied into the new projects.
7. The behavioral reference is the WPF source in `HashCompare/`. When this document and the WPF source disagree on *behavior* (not structure), the WPF source wins — except for the four deliberate changes in section 2.

## 1. Headline decisions

| Decision | Choice |
|---|---|
| Target framework | `net10.0` (plain, no `-windows`) for all three new projects |
| Solution | New `HashCompare.slnx` containing **only** the three new projects. Old `HashCompare.sln` stays untouched |
| Projects | `src/HashCompare.Core` (engine/models/config — zero UI deps), `src/HashCompare.App` (Avalonia), `tests/HashCompare.Core.Tests` (xunit) |
| MVVM | CommunityToolkit.Mvvm 8.4.0 source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| Packages | Avalonia 11.3.2 (+Desktop, +Themes.Fluent, +Diagnostics Debug-only), Avalonia.Controls.DataGrid 11.3.2, CommunityToolkit.Mvvm; xunit 2.9.2 stack. Central Package Management (CPM) |
| MessageBox replacement | Custom `MessageDialog` (section 6) — no extra package |
| `ICollectionView` replacement | Master `List<FileComparisonResult>` + `FilteredResults` ObservableCollection rebuilt on filter change |
| Status strings | `FileStatus` enum in Core; `ToDisplayString()` preserves the exact display text ("Identical"/"Different"/"Missing"/"New") |
| Theme | `RequestedThemeVariant="Light"` — the status colors assume a light surface |

## 2. Deliberate behavior changes (everything else stays identical)

1. **`Path.GetRelativePath(root, f)`** replaces `f[sourceDir.Length..].TrimStart('\\','/')` (fixes trailing-separator fragility; works on all OSes).
2. **`PathComparison` policy helper**: `OrdinalIgnoreCase` on Windows/macOS, `Ordinal` on Linux — used for the dest/source path sets, MRU dedupe, and scanner sets. Hard-coded, not configurable.
3. **Per-OS diff-tool probing** (Windows behavior unchanged): configured tool → OS probe list (WinMerge ×2 on Windows; `opendiff`/Meld on macOS; `meld` on Linux) → `code --diff` fallback. `UseShellExecute=true` only for the `code` fallback on Windows, `false` otherwise.
4. **Merged row commands**: the WPF `CopyToDest_Click` and `ReplaceDest_Click` are byte-identical; the ViewModel has one `CopyToDestCommand` used by both the "Copy to Dest" and "Replace Dest" buttons.

**Explicitly NOT changing:** comparison semantics; status display strings; default checkbox states (Identical **unchecked**, others checked); counts ignore filters; ~1% progress throttling; MRU cap 25; `config.json` schema and location (`SpecialFolder.ApplicationData` → `%APPDATA%\HashCompare\config.json` on Windows, `~/.config/HashCompare/` on Unix automatically); `{left}`/`{right}` argument tokens; cell colors `#A5D6A7`/`#EF9A9A`/`#FFF59D`/White; column widths 2\*/1\*/0.4\*/0.6\*; main window 1100×650, config 640×580, scan 420×450; dest folder TextBox read-only; source ComboBox editable.

## 3. Target layout

```
HashCompare.slnx                    (new)
Directory.Build.props               (new)
Directory.Packages.props            (new)
HashCompare.sln                     (existing — DO NOT TOUCH)
HashCompare/                        (existing WPF app — DO NOT TOUCH)
src/HashCompare.Core/
    HashCompare.Core.csproj, FileStatus.cs, FileComparisonResult.cs,
    ComparisonEngine.cs, WildcardMatcher.cs, FolderScanner.cs,
    PathComparison.cs, AppConfig.cs, ConfigService.cs
src/HashCompare.App/
    HashCompare.App.csproj, Program.cs, App.axaml, App.axaml.cs,
    ViewModels/MainWindowViewModel.cs,
    Views/MainWindow.axaml(.cs), Views/ConfigWindow.axaml(.cs),
    Views/ScanSelectWindow.axaml(.cs), Views/MessageDialog.axaml(.cs),
    Converters/StatusToBrushConverter.cs,
    Services/DiffToolLauncher.cs
tests/HashCompare.Core.Tests/
    HashCompare.Core.Tests.csproj, ComparisonEngineTests.cs,
    WildcardMatcherTests.cs, ConfigServiceTests.cs
```

---

## Phase 1 — Scaffolding

Create these files exactly.

### `Directory.Build.props` (VERBATIM)

Note: this file also applies to the old WPF project (MSBuild walks up the tree). That is intentional and harmless — the WPF project already sets `Nullable`/`ImplicitUsings`, and CPM only affects projects with `PackageReference`s (the WPF project has none). The Phase 1 gate proves the old solution still builds.

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

### `Directory.Packages.props` (VERBATIM)

```xml
<Project>
  <ItemGroup>
    <!-- UI -->
    <PackageVersion Include="Avalonia" Version="11.3.2" />
    <PackageVersion Include="Avalonia.Desktop" Version="11.3.2" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="11.3.2" />
    <PackageVersion Include="Avalonia.Diagnostics" Version="11.3.2" />
    <PackageVersion Include="Avalonia.Controls.DataGrid" Version="11.3.2" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <!-- Tests -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

### `HashCompare.slnx` (VERBATIM)

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/HashCompare.Core/HashCompare.Core.csproj" />
    <Project Path="src/HashCompare.App/HashCompare.App.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/HashCompare.Core.Tests/HashCompare.Core.Tests.csproj" />
  </Folder>
</Solution>
```

### `src/HashCompare.Core/HashCompare.Core.csproj` (VERBATIM)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### `src/HashCompare.App/HashCompare.App.csproj` (VERBATIM)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Controls.DataGrid" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Avalonia.Diagnostics" Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\HashCompare.Core\HashCompare.Core.csproj" />
  </ItemGroup>
</Project>
```

### `tests/HashCompare.Core.Tests/HashCompare.Core.Tests.csproj` (VERBATIM)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\HashCompare.Core\HashCompare.Core.csproj" />
  </ItemGroup>
</Project>
```

### `src/HashCompare.App/Program.cs` (VERBATIM)

```csharp
using Avalonia;

namespace HashCompare.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

### `src/HashCompare.App/App.axaml` (VERBATIM)

The DataGrid `StyleInclude` is **mandatory** — without it the grid renders as an empty rectangle with no error.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HashCompare.App.App"
             RequestedThemeVariant="Light">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />
  </Application.Styles>
</Application>
```

### `src/HashCompare.App/App.axaml.cs` (VERBATIM)

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HashCompare.App.Views;

namespace HashCompare.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
```

### `src/HashCompare.App/Views/MainWindow.axaml` — Phase 1 placeholder (VERBATIM, replaced in Phase 3)

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="HashCompare.App.Views.MainWindow"
        Title="Folder Compare" Height="650" Width="1100">
  <TextBlock Text="Phase 1 placeholder" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Window>
```

### `src/HashCompare.App/Views/MainWindow.axaml.cs` — Phase 1 placeholder (VERBATIM, replaced in Phase 3)

```csharp
using Avalonia.Controls;

namespace HashCompare.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

### Phase 1 gate

```
dotnet build HashCompare.slnx
dotnet build HashCompare.sln
```

Both must succeed with 0 errors (warnings are fine). The second command proves the new root props files did not break the old WPF app. Then run:

```
dotnet run --project src/HashCompare.App/HashCompare.App.csproj
```

A 1100×650 window titled "Folder Compare" must open showing "Phase 1 placeholder". Close it (or kill the process after confirming it started without exceptions).

---

## Phase 2 — Core extraction + tests

### `src/HashCompare.Core/FileStatus.cs` (VERBATIM)

```csharp
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
```

### `src/HashCompare.Core/PathComparison.cs` (VERBATIM)

```csharp
namespace HashCompare.Core;

/// <summary>
/// Path string comparison policy: case-insensitive on Windows and macOS, case-sensitive on Linux.
/// </summary>
public static class PathComparison
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
```

### `src/HashCompare.Core/FileComparisonResult.cs` (VERBATIM)

Reworked from `HashCompare/FileComparisonResult.cs`: status is now the enum, and `StatusText` exposes the display string for the grid. Core stays dependency-free, so INPC is hand-written (no CommunityToolkit here).

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HashCompare.Core;

/// <summary>
/// One row in the comparison grid. <see cref="Status"/> is mutable so an action button
/// (e.g. "Copy to Dest") can update the row in place without re-running the whole comparison.
/// </summary>
public sealed class FileComparisonResult : INotifyPropertyChanged
{
    private FileStatus _status;

    /// <summary>Path relative to the folder roots, e.g. "sub\dir\file.txt".</summary>
    public required string RelativePath { get; init; }

    public required string SourceFullPath { get; init; }
    public required string DestFullPath { get; init; }

    /// <summary>Relative directory portion shown in the "Folder" column.</summary>
    public string Folder => Path.GetDirectoryName(RelativePath) ?? string.Empty;

    /// <summary>File name shown in the "File" column.</summary>
    public string File => Path.GetFileName(RelativePath);

    public required FileStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanCopyToDest));
            OnPropertyChanged(nameof(CanRemoveFromDest));
            OnPropertyChanged(nameof(CanReplaceDest));
            OnPropertyChanged(nameof(CanCompare));
        }
    }

    /// <summary>Text shown in the "Status" column.</summary>
    public string StatusText => Status.ToDisplayString();

    public bool CanCopyToDest => Status == FileStatus.Missing;
    public bool CanRemoveFromDest => Status == FileStatus.New;
    public bool CanReplaceDest => Status == FileStatus.Different;
    public bool CanCompare => Status == FileStatus.Different;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### `src/HashCompare.Core/ComparisonEngine.cs` (VERBATIM)

Ported from `MainWindow.xaml.cs` (`BuildComparison`, `CalculateSha256`, `CopyOver`) with the two deliberate changes: `Path.GetRelativePath` and `PathComparison.Comparer`.

```csharp
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

    /// <summary>Copies source over destination, creating the destination directory if needed.</summary>
    public static void CopyOver(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Copy(source, destination, overwrite: true);
    }
}
```

### `src/HashCompare.Core/FolderScanner.cs` (VERBATIM)

Extracted from `ConfigWindow.xaml.cs` (`CollectFolderNames` / `CollectFileExtensions`). Returns plain strings; the UI wraps them in checklist items.

```csharp
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
```

### `src/HashCompare.Core/WildcardMatcher.cs` (ADAPT — copy + namespace change)

Copy `HashCompare/WildcardMatcher.cs` verbatim into `src/HashCompare.Core/WildcardMatcher.cs`, changing only line 3: `namespace HashCompare;` → `namespace HashCompare.Core;`. No other edits.

### `src/HashCompare.Core/AppConfig.cs` (ADAPT — copy + namespace change)

Copy `HashCompare/AppConfig.cs` verbatim, changing only `namespace HashCompare;` → `namespace HashCompare.Core;`. Both classes (`FolderSet`, `AppConfig`) stay exactly as-is — the JSON property names must not change, so existing `config.json` files keep loading.

### `src/HashCompare.Core/ConfigService.cs` (ADAPT — copy + three precise edits)

Copy `HashCompare/ConfigService.cs`, then make exactly these edits:

1. `namespace HashCompare;` → `namespace HashCompare.Core;`
2. Add path-parameter overloads for testability. Replace the existing `Load()` and `Save(AppConfig)` bodies so the public API becomes:

```csharp
public static AppConfig Load() => Load(ConfigPath);

public static AppConfig Load(string path)
{
    try
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
    }
    catch
    {
        // Corrupt or unreadable config: fall back to defaults rather than crashing.
    }

    return new AppConfig();
}

public static void Save(AppConfig config) => Save(config, ConfigPath);

public static void Save(AppConfig config, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
}
```

3. In `RememberFolderSet`, replace the two `StringComparison.OrdinalIgnoreCase` occurrences with `PathComparison.Comparison`.

Everything else (`MaxFolderSets = 25`, `ConfigDir`, `ConfigPath`, `Options`, the MRU move-to-front/cap logic) stays identical. The config directory stays `Environment.SpecialFolder.ApplicationData` + `"HashCompare"` — the same folder the WPF app uses, so history carries over.

### `tests/HashCompare.Core.Tests/ComparisonEngineTests.cs` (VERBATIM)

Note the progress stub: do **not** use `Progress<T>` in tests — it posts callbacks through a `SynchronizationContext` and the last report may not have arrived when the assertion runs. The inline `IProgress` implementation below reports synchronously.

```csharp
using System.Text.RegularExpressions;
using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class ComparisonEngineTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _dest;

    public ComparisonEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "HashCompareTests_" + Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _dest = Path.Combine(_root, "dest");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_dest);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteSource(string relativePath, string content) =>
        WriteFile(_source, relativePath, content);

    private void WriteDest(string relativePath, string content) =>
        WriteFile(_dest, relativePath, content);

    private static void WriteFile(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private sealed class ImmediateProgress : IProgress<(int done, int total)>
    {
        public (int done, int total) Last;
        public void Report((int done, int total) value) => Last = value;
    }

    private List<FileComparisonResult> Run(
        string folderExcludes = "", string fileExcludes = "",
        ImmediateProgress? progress = null)
    {
        var folderPatterns = WildcardMatcher.Compile(folderExcludes);
        var filePatterns = WildcardMatcher.Compile(WildcardMatcher.ExpandFileExcludes(fileExcludes));
        return ComparisonEngine.BuildComparison(
            _source, _dest, folderPatterns, filePatterns,
            progress ?? new ImmediateProgress());
    }

    [Fact]
    public void ClassifiesAllFourStatuses()
    {
        WriteSource("same.txt", "alpha");
        WriteDest("same.txt", "alpha");
        WriteSource(Path.Combine("sub", "changed.txt"), "old");
        WriteDest(Path.Combine("sub", "changed.txt"), "new");
        WriteSource("onlyInSource.txt", "s");
        WriteDest("onlyInDest.txt", "d");

        var results = Run().ToDictionary(r => r.File, r => r.Status);

        Assert.Equal(4, results.Count);
        Assert.Equal(FileStatus.Identical, results["same.txt"]);
        Assert.Equal(FileStatus.Different, results["changed.txt"]);
        Assert.Equal(FileStatus.Missing, results["onlyInSource.txt"]);
        Assert.Equal(FileStatus.New, results["onlyInDest.txt"]);
    }

    [Fact]
    public void FullPathsAndRelativePathAreConsistent()
    {
        WriteSource(Path.Combine("a", "b", "file.txt"), "x");

        var result = Assert.Single(Run());

        Assert.Equal(Path.Combine("a", "b", "file.txt"), result.RelativePath);
        Assert.Equal(Path.Combine(_source, "a", "b", "file.txt"), result.SourceFullPath);
        Assert.Equal(Path.Combine(_dest, "a", "b", "file.txt"), result.DestFullPath);
        Assert.Equal(Path.Combine("a", "b"), result.Folder);
        Assert.Equal("file.txt", result.File);
    }

    [Fact]
    public void TrailingSeparatorOnRootsDoesNotBreakRelativePaths()
    {
        WriteSource("file.txt", "x");
        WriteDest("file.txt", "x");

        var folderPatterns = WildcardMatcher.Compile("");
        var results = ComparisonEngine.BuildComparison(
            _source + Path.DirectorySeparatorChar,
            _dest + Path.DirectorySeparatorChar,
            folderPatterns, folderPatterns, new ImmediateProgress());

        var result = Assert.Single(results);
        Assert.Equal(FileStatus.Identical, result.Status);
        Assert.Equal("file.txt", result.RelativePath);
    }

    [Fact]
    public void ExcludedFolderSegmentIsSkippedInBothTrees()
    {
        WriteSource(Path.Combine("bin", "skipped.dll"), "x");
        WriteDest(Path.Combine("deep", "bin", "alsoSkipped.dll"), "y");
        WriteSource("kept.txt", "k");

        var results = Run(folderExcludes: "bin");

        var result = Assert.Single(results);
        Assert.Equal("kept.txt", result.File);
    }

    [Fact]
    public void ExcludedFileExtensionIsSkipped()
    {
        WriteSource("keep.txt", "x");
        WriteSource("drop.tmp", "x");
        WriteDest("alsoDrop.tmp", "y");

        var results = Run(fileExcludes: ".tmp");

        var result = Assert.Single(results);
        Assert.Equal("keep.txt", result.File);
    }

    [Fact]
    public void ProgressReachesTotal()
    {
        for (var i = 0; i < 5; i++)
            WriteSource($"s{i}.txt", i.ToString());
        for (var i = 0; i < 3; i++)
            WriteDest($"d{i}.txt", i.ToString());

        var progress = new ImmediateProgress();
        Run(progress: progress);

        Assert.Equal(8, progress.Last.total);
        Assert.Equal(8, progress.Last.done);
    }

    [Fact]
    public void CopyOverCreatesMissingDirectories()
    {
        WriteSource("f.txt", "content");
        var destPath = Path.Combine(_dest, "brand", "new", "dir", "f.txt");

        ComparisonEngine.CopyOver(Path.Combine(_source, "f.txt"), destPath);

        Assert.Equal("content", File.ReadAllText(destPath));
    }
}
```

### `tests/HashCompare.Core.Tests/WildcardMatcherTests.cs` (VERBATIM)

```csharp
using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class WildcardMatcherTests
{
    [Fact]
    public void CompileEmptyReturnsNoPatterns()
    {
        Assert.Empty(WildcardMatcher.Compile(""));
        Assert.Empty(WildcardMatcher.Compile("   "));
    }

    [Theory]
    [InlineData("bin", "bin", true)]
    [InlineData("bin", "BIN", true)]          // case-insensitive
    [InlineData("bin", "binaries", false)]    // whole-value match, not substring
    [InlineData("*.tmp", "file.tmp", true)]
    [InlineData("*.tmp", "file.tmp.bak", false)]
    [InlineData("fi?e.txt", "file.txt", true)]
    [InlineData("fi?e.txt", "fiile.txt", false)]
    public void IsMatchHonorsWildcards(string pattern, string value, bool expected)
    {
        var patterns = WildcardMatcher.Compile(pattern);
        Assert.Equal(expected, WildcardMatcher.IsMatch(patterns, value));
    }

    [Fact]
    public void SemicolonListMatchesAnyEntry()
    {
        var patterns = WildcardMatcher.Compile("bin; obj ;*.tmp");
        Assert.True(WildcardMatcher.IsMatch(patterns, "bin"));
        Assert.True(WildcardMatcher.IsMatch(patterns, "obj"));
        Assert.True(WildcardMatcher.IsMatch(patterns, "x.tmp"));
        Assert.False(WildcardMatcher.IsMatch(patterns, "src"));
    }

    [Theory]
    [InlineData(".tmp", "*.tmp")]      // bare dotted extension
    [InlineData("tmp", "*.tmp")]       // bare extension without dot
    [InlineData("*.tmp", "*.tmp")]     // already a wildcard: unchanged
    [InlineData("thumbs.db?", "thumbs.db?")]
    [InlineData(".tmp;log", "*.tmp;*.log")]
    [InlineData("", "")]
    public void ExpandFileExcludesNormalisesEntries(string raw, string expected)
    {
        Assert.Equal(expected, WildcardMatcher.ExpandFileExcludes(raw));
    }
}
```

### `tests/HashCompare.Core.Tests/ConfigServiceTests.cs` (VERBATIM)

```csharp
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
```

### Phase 2 gate

```
dotnet test HashCompare.slnx
```

All tests pass (expect 20+ passing, 0 failed, 0 errors). If a test fails, fix the **Core code**, not the test — the tests encode the WPF app's behavior.

---

## Phase 3 — MainWindowViewModel + MainWindow

Ordering note: the real `MessageDialog`, `ConfigWindow`, and `DiffToolLauncher` arrive in Phases 4–5. So that Phase 3 still builds and runs, this phase includes **temporary stubs** (clearly marked) that later phases replace.

### `src/HashCompare.App/ViewModels/MainWindowViewModel.cs` (VERBATIM)

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HashCompare.App.Services;
using HashCompare.Core;

namespace HashCompare.App.ViewModels;

/// <summary>
/// UI services the ViewModel needs from the view layer. Implemented by MainWindow.
/// </summary>
public interface IDialogs
{
    Task ShowInfoAsync(string title, string message);
    Task ShowErrorAsync(string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PickFolderAsync(string title);

    /// <summary>Shows the config dialog; returns true if the user pressed OK (config was edited in place).</summary>
    Task<bool> ShowConfigAsync(AppConfig config, string source, string destination);
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogs _dialogs;
    private readonly AppConfig _config;

    /// <summary>All rows from the last comparison; FilteredResults is rebuilt from this.</summary>
    private readonly List<FileComparisonResult> _allResults = [];

    [ObservableProperty] private string _sourceFolder = string.Empty;
    [ObservableProperty] private string _destFolder = string.Empty;
    [ObservableProperty] private string? _selectedHistoryItem;

    // Status filter checkboxes. Identical defaults to unchecked — same as the WPF app.
    [ObservableProperty] private bool _showIdentical;
    [ObservableProperty] private bool _showDifferent = true;
    [ObservableProperty] private bool _showMissing = true;
    [ObservableProperty] private bool _showNew = true;

    [ObservableProperty] private string _folderFilterText = string.Empty;
    [ObservableProperty] private string _fileFilterText = string.Empty;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMaximum = 1;
    [ObservableProperty] private string _progressText = string.Empty;

    // Per-status totals shown above the checkboxes. Counts ignore the active filters.
    [ObservableProperty] private int _countIdentical;
    [ObservableProperty] private int _countDifferent;
    [ObservableProperty] private int _countMissing;
    [ObservableProperty] private int _countNew;

    public ObservableCollection<string> SourceHistory { get; } = [];
    public ObservableCollection<FileComparisonResult> FilteredResults { get; } = [];

    public MainWindowViewModel(IDialogs dialogs)
    {
        _dialogs = dialogs;
        _config = ConfigService.Load();
        RefreshSourceHistory();

        // Pre-fill the folders used in the most recent comparison.
        var lastUsed = _config.FolderSets.FirstOrDefault();
        if (lastUsed != null)
        {
            SourceFolder = lastUsed.Source;
            DestFolder = lastUsed.Destination;
        }
    }

    // ----- Change reactions -----

    partial void OnShowIdenticalChanged(bool value) => ApplyFilter();
    partial void OnShowDifferentChanged(bool value) => ApplyFilter();
    partial void OnShowMissingChanged(bool value) => ApplyFilter();
    partial void OnShowNewChanged(bool value) => ApplyFilter();
    partial void OnFolderFilterTextChanged(string value) => ApplyFilter();
    partial void OnFileFilterTextChanged(string value) => ApplyFilter();

    /// <summary>Picking a remembered source folder auto-fills its matching destination.</summary>
    partial void OnSelectedHistoryItemChanged(string? value)
    {
        if (value is null)
            return;

        SourceFolder = value;
        var match = _config.FolderSets.FirstOrDefault(fs =>
            string.Equals(fs.Source, value, PathComparison.Comparison));
        if (match != null)
            DestFolder = match.Destination;
    }

    private void RefreshSourceHistory()
    {
        var current = SourceFolder;
        SourceHistory.Clear();
        foreach (var source in _config.FolderSets.Select(fs => fs.Source)
                     .Distinct(PathComparison.Comparer))
            SourceHistory.Add(source);
        SourceFolder = current;
    }

    // ----- Commands -----

    [RelayCommand]
    private void SwapFolders() => (SourceFolder, DestFolder) = (DestFolder, SourceFolder);

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var path = await _dialogs.PickFolderAsync("Select source folder");
        if (path != null)
            SourceFolder = path;
    }

    [RelayCommand]
    private async Task BrowseDestAsync()
    {
        var path = await _dialogs.PickFolderAsync("Select destination folder");
        if (path != null)
            DestFolder = path;
    }

    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        if (await _dialogs.ShowConfigAsync(_config, SourceFolder, DestFolder))
            ConfigService.Save(_config);
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        var source = SourceFolder;
        var destination = DestFolder;

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            await _dialogs.ShowInfoAsync("Missing Folders",
                "Please select both source and destination folders.");
            return;
        }

        if (!Directory.Exists(source) || !Directory.Exists(destination))
        {
            await _dialogs.ShowInfoAsync("Invalid Folders",
                "Source and destination folders must both exist.");
            return;
        }

        // Remember this pairing before comparing.
        ConfigService.RememberFolderSet(_config, source, destination);
        ConfigService.Save(_config);
        RefreshSourceHistory();

        // Snapshot exclude rules so the background thread doesn't touch UI/config state.
        var folderPatterns = WildcardMatcher.Compile(_config.ExcludeFolders);
        var filePatterns = WildcardMatcher.Compile(WildcardMatcher.ExpandFileExcludes(_config.ExcludeFiles));

        IsBusy = true;
        ProgressValue = 0;
        ProgressText = string.Empty;
        var progress = new Progress<(int done, int total)>(p =>
        {
            ProgressMaximum = Math.Max(1, p.total);
            ProgressValue = p.done;
            ProgressText = $"{p.done} / {p.total}";
        });

        try
        {
            var results = await Task.Run(
                () => ComparisonEngine.BuildComparison(source, destination, folderPatterns, filePatterns, progress));

            _allResults.Clear();
            _allResults.AddRange(results);
            ApplyFilter();
            UpdateStatusCounts();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ----- Row actions -----

    /// <summary>Used by both the "Copy to Dest" (Missing) and "Replace Dest" (Different) buttons.</summary>
    [RelayCommand]
    private async Task CopyToDestAsync(FileComparisonResult result)
    {
        try
        {
            ComparisonEngine.CopyOver(result.SourceFullPath, result.DestFullPath);
            result.Status = FileStatus.Identical;
            ApplyFilter();
            UpdateStatusCounts();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveFromDestAsync(FileComparisonResult result)
    {
        if (!await _dialogs.ConfirmAsync("Confirm Delete",
                $"Delete from destination?\n\n{result.DestFullPath}"))
            return;

        try
        {
            File.Delete(result.DestFullPath);
            _allResults.Remove(result);
            FilteredResults.Remove(result);
            UpdateStatusCounts();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CompareFilesAsync(FileComparisonResult result)
    {
        if (!DiffToolLauncher.TryLaunch(_config, result.SourceFullPath, result.DestFullPath))
        {
            await _dialogs.ShowInfoAsync("Compare",
                "No diff tool was found. Configure one under Config, or install WinMerge or VS Code.\n\n" +
                $"Source: {result.SourceFullPath}\nDestination: {result.DestFullPath}");
        }
    }

    // ----- Filtering -----

    /// <summary>Rebuilds FilteredResults from the master list using the current filters.</summary>
    private void ApplyFilter()
    {
        var folderFilter = BuildTextFilter(FolderFilterText);
        var fileFilter = BuildTextFilter(FileFilterText);

        FilteredResults.Clear();
        foreach (var result in _allResults)
        {
            var statusVisible = result.Status switch
            {
                FileStatus.Identical => ShowIdentical,
                FileStatus.Different => ShowDifferent,
                FileStatus.Missing => ShowMissing,
                FileStatus.New => ShowNew,
                _ => true
            };

            if (statusVisible
                && (folderFilter?.Invoke(result.Folder) ?? true)
                && (fileFilter?.Invoke(result.File) ?? true))
            {
                FilteredResults.Add(result);
            }
        }
    }

    /// <summary>
    /// Builds a match predicate from filter text: plain text matches as a case-insensitive
    /// substring; text containing * or ? is treated as a wildcard pattern instead.
    /// Returns null (match all) for empty text.
    /// </summary>
    private static Func<string, bool>? BuildTextFilter(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return null;

        if (text.Contains('*') || text.Contains('?'))
        {
            var patterns = WildcardMatcher.Compile(text);
            return value => WildcardMatcher.IsMatch(patterns, value);
        }

        return value => value.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Counts reflect all results regardless of filters, so hiding a status doesn't zero it.</summary>
    private void UpdateStatusCounts()
    {
        CountIdentical = _allResults.Count(r => r.Status == FileStatus.Identical);
        CountDifferent = _allResults.Count(r => r.Status == FileStatus.Different);
        CountMissing = _allResults.Count(r => r.Status == FileStatus.Missing);
        CountNew = _allResults.Count(r => r.Status == FileStatus.New);
    }
}
```

### `src/HashCompare.App/Services/DiffToolLauncher.cs` — Phase 3 STUB (VERBATIM, replaced in Phase 5)

```csharp
using HashCompare.Core;

namespace HashCompare.App.Services;

public static class DiffToolLauncher
{
    // Phase 3 stub so the ViewModel compiles; real per-OS implementation lands in Phase 5.
    public static bool TryLaunch(AppConfig config, string left, string right) => false;
}
```

### `src/HashCompare.App/Converters/StatusToBrushConverter.cs` (VERBATIM)

Replaces the WPF `DataGridCell` DataTriggers — Avalonia's DataGrid has no per-cell trigger styles, so the Status cell is a template column whose Border background comes from this converter. The colors and their meanings are identical to the WPF app and its legend.

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HashCompare.Core;

namespace HashCompare.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly IBrush NewBrush = new SolidColorBrush(Color.Parse("#A5D6A7"));       // Light Green
    private static readonly IBrush MissingBrush = new SolidColorBrush(Color.Parse("#EF9A9A"));   // Light Red
    private static readonly IBrush DifferentBrush = new SolidColorBrush(Color.Parse("#FFF59D")); // Light Yellow
    private static readonly IBrush IdenticalBrush = Brushes.White;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            FileStatus.New => NewBrush,
            FileStatus.Missing => MissingBrush,
            FileStatus.Different => DifferentBrush,
            FileStatus.Identical => IdenticalBrush,
            _ => Brushes.Transparent
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

### `src/HashCompare.App/Views/MainWindow.axaml` (VERBATIM — replaces the Phase 1 placeholder)

Layout mirrors the WPF `MainWindow.xaml` exactly: same rows, widths, margins, colors, and defaults. Differences forced by Avalonia are: `Watermark` replaces the WPF watermark-TextBlock hack; the Status cell is a template column with the converter; row buttons bind to the ViewModel through `#Root`.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:HashCompare.App.ViewModels"
        xmlns:conv="using:HashCompare.App.Converters"
        xmlns:core="using:HashCompare.Core"
        x:Class="HashCompare.App.Views.MainWindow"
        x:Name="Root"
        x:DataType="vm:MainWindowViewModel"
        Title="Folder Compare" Height="650" Width="1100">

  <Window.Resources>
    <conv:StatusToBrushConverter x:Key="StatusBrush" />
  </Window.Resources>

  <Grid Margin="10" RowDefinitions="Auto,Auto,Auto,*">

    <!-- Row 0: folder selection -->
    <Grid Grid.Row="0" ColumnDefinitions="*,Auto,*">

      <!-- Source Folder (editable history dropdown + Browse) -->
      <StackPanel Grid.Column="0" Margin="0,0,5,0">
        <DockPanel Margin="0,0,0,5">
          <Button DockPanel.Dock="Right" Width="110" Content="Swap folders"
                  Command="{Binding SwapFoldersCommand}" IsEnabled="{Binding !IsBusy}" />
          <TextBlock Text="Source Folder:" VerticalAlignment="Center" />
        </DockPanel>
        <DockPanel>
          <Button DockPanel.Dock="Right" Width="90" Content="Browse..."
                  Command="{Binding BrowseSourceCommand}" IsEnabled="{Binding !IsBusy}" />
          <ComboBox Name="CmbSourceFolder"
                    IsEditable="True" IsTextSearchEnabled="False"
                    HorizontalAlignment="Stretch"
                    ItemsSource="{Binding SourceHistory}"
                    SelectedItem="{Binding SelectedHistoryItem}"
                    Text="{Binding SourceFolder, Mode=TwoWay}"
                    IsEnabled="{Binding !IsBusy}" />
        </DockPanel>
      </StackPanel>

      <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Center" VerticalAlignment="Stretch" />

      <!-- Destination Folder -->
      <StackPanel Grid.Column="2" Margin="5,0,0,0">
        <TextBlock Text="Destination Folder:" Margin="0,0,0,5" />
        <DockPanel>
          <Button DockPanel.Dock="Right" Width="90" Content="Browse..."
                  Command="{Binding BrowseDestCommand}" IsEnabled="{Binding !IsBusy}" />
          <TextBox Text="{Binding DestFolder}" IsReadOnly="True" />
        </DockPanel>
      </StackPanel>
    </Grid>

    <!-- Row 1: text filters (edges) + action buttons + progress (center) -->
    <Grid Grid.Row="1" Margin="0,10" ColumnDefinitions="*,Auto,*">

      <TextBox Grid.Column="0" Width="200" HorizontalAlignment="Left" VerticalAlignment="Center"
               Watermark="Filter by folder..."
               Text="{Binding FolderFilterText, Mode=TwoWay}" />

      <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Center">
        <Button Content="Compare Folders" Padding="5" Margin="0,0,8,0"
                Command="{Binding CompareCommand}" IsEnabled="{Binding !IsBusy}" />
        <Button Content="Config" Padding="5" Width="80"
                Command="{Binding OpenConfigCommand}" IsEnabled="{Binding !IsBusy}" />
        <ProgressBar Width="220" Height="18" Margin="16,0,0,0" VerticalAlignment="Center"
                     IsVisible="{Binding IsBusy}"
                     Maximum="{Binding ProgressMaximum}" Value="{Binding ProgressValue}" />
        <TextBlock Margin="8,0,0,0" VerticalAlignment="Center"
                   IsVisible="{Binding IsBusy}" Text="{Binding ProgressText}" />
      </StackPanel>

      <TextBox Grid.Column="2" Width="200" HorizontalAlignment="Right" VerticalAlignment="Center"
               Watermark="Filter by file..."
               Text="{Binding FileFilterText, Mode=TwoWay}" />
    </Grid>

    <!-- Row 2: status filters with live counts + color legend -->
    <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,5,0,10"
                HorizontalAlignment="Left" VerticalAlignment="Bottom">
      <TextBlock Text="Filter by Status:" VerticalAlignment="Bottom" Margin="0,0,10,2" FontWeight="SemiBold" />

      <StackPanel Margin="5,0" VerticalAlignment="Bottom">
        <TextBlock Text="{Binding CountIdentical}" FontWeight="Bold" HorizontalAlignment="Center" />
        <CheckBox Content="Identical" IsChecked="{Binding ShowIdentical}" />
      </StackPanel>
      <StackPanel Margin="5,0" VerticalAlignment="Bottom">
        <TextBlock Text="{Binding CountDifferent}" FontWeight="Bold" HorizontalAlignment="Center" />
        <CheckBox Content="Different" IsChecked="{Binding ShowDifferent}" />
      </StackPanel>
      <StackPanel Margin="5,0" VerticalAlignment="Bottom">
        <TextBlock Text="{Binding CountMissing}" FontWeight="Bold" HorizontalAlignment="Center" />
        <CheckBox Content="Missing" IsChecked="{Binding ShowMissing}" />
      </StackPanel>
      <StackPanel Margin="5,0" VerticalAlignment="Bottom">
        <TextBlock Text="{Binding CountNew}" FontWeight="Bold" HorizontalAlignment="Center" />
        <CheckBox Content="New" IsChecked="{Binding ShowNew}" />
      </StackPanel>

      <!-- Color Legend -->
      <Border BorderThickness="0,0,0,1" BorderBrush="Gray" Margin="20,0,0,0" Padding="5,0,0,0">
        <StackPanel Orientation="Horizontal">
          <TextBlock Text="Legend: " FontWeight="SemiBold" VerticalAlignment="Center" />
          <Border Width="16" Height="16" Background="#A5D6A7" Margin="5,0" VerticalAlignment="Center" />
          <TextBlock Text="New" VerticalAlignment="Center" Margin="0,0,10,0" />
          <Border Width="16" Height="16" Background="#EF9A9A" Margin="5,0" VerticalAlignment="Center" />
          <TextBlock Text="Missing" VerticalAlignment="Center" Margin="0,0,10,0" />
          <Border Width="16" Height="16" Background="#FFF59D" Margin="5,0" VerticalAlignment="Center" />
          <TextBlock Text="Different" VerticalAlignment="Center" Margin="0,0,10,0" />
          <Border Width="16" Height="16" Background="White" BorderBrush="Gray" BorderThickness="1"
                  Margin="5,0" VerticalAlignment="Center" />
          <TextBlock Text="Identical" VerticalAlignment="Center" />
        </StackPanel>
      </Border>
    </StackPanel>

    <!-- Row 3: results grid -->
    <DataGrid Grid.Row="3" Margin="0,5,0,0"
              ItemsSource="{Binding FilteredResults}"
              AutoGenerateColumns="False" IsReadOnly="True"
              CanUserReorderColumns="False">
      <DataGrid.Columns>
        <!-- Folder = 1/2 of the grid, File = 1/4, Status + Actions share the last 1/4 -->
        <DataGridTemplateColumn Header="Folder" Width="2*">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="core:FileComparisonResult">
              <TextBlock Text="{Binding Folder}" FontWeight="Bold" Padding="8,0,0,0"
                         VerticalAlignment="Center" />
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <DataGridTemplateColumn Header="File" Width="1*">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="core:FileComparisonResult">
              <TextBlock Text="{Binding File}" FontWeight="Bold" VerticalAlignment="Center" />
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <DataGridTemplateColumn Header="Status" Width="0.4*">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="core:FileComparisonResult">
              <Border Background="{Binding Status, Converter={StaticResource StatusBrush}}">
                <TextBlock Text="{Binding StatusText}" VerticalAlignment="Center" Margin="4,0" />
              </Border>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <DataGridTemplateColumn Header="Actions" Width="0.6*">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="core:FileComparisonResult">
              <StackPanel Orientation="Horizontal">
                <Button Content="Copy to Dest" Margin="2,1" Padding="6,1"
                        IsVisible="{Binding CanCopyToDest}"
                        Command="{Binding #Root.((vm:MainWindowViewModel)DataContext).CopyToDestCommand}"
                        CommandParameter="{Binding}" />
                <Button Content="Remove from Dest" Margin="2,1" Padding="6,1"
                        IsVisible="{Binding CanRemoveFromDest}"
                        Command="{Binding #Root.((vm:MainWindowViewModel)DataContext).RemoveFromDestCommand}"
                        CommandParameter="{Binding}" />
                <Button Content="Replace Dest" Margin="2,1" Padding="6,1"
                        IsVisible="{Binding CanReplaceDest}"
                        Command="{Binding #Root.((vm:MainWindowViewModel)DataContext).CopyToDestCommand}"
                        CommandParameter="{Binding}" />
                <Button Content="Compare" Margin="2,1" Padding="6,1"
                        IsVisible="{Binding CanCompare}"
                        Command="{Binding #Root.((vm:MainWindowViewModel)DataContext).CompareFilesCommand}"
                        CommandParameter="{Binding}" />
              </StackPanel>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
      </DataGrid.Columns>
    </DataGrid>
  </Grid>
</Window>
```

Note the deliberate reuse: "Replace Dest" binds to `CopyToDestCommand` — the two WPF handlers were byte-identical (section 2, change 4).

### `src/HashCompare.App/Views/MainWindow.axaml.cs` (VERBATIM — replaces the Phase 1 placeholder; dialog stubs replaced in Phase 4)

```csharp
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HashCompare.App.ViewModels;
using HashCompare.Core;

namespace HashCompare.App.Views;

public partial class MainWindow : Window, IDialogs
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    // ----- Phase 3 stubs: replaced with MessageDialog / ConfigWindow in Phase 4 -----

    public Task ShowInfoAsync(string title, string message) => Task.CompletedTask;

    public Task ShowErrorAsync(string message) => Task.CompletedTask;

    // Returning false keeps the delete action inert until the real confirm dialog exists.
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);

    public Task<bool> ShowConfigAsync(AppConfig config, string source, string destination) =>
        Task.FromResult(false);
}
```

### Phase 3 gate

```
dotnet build HashCompare.slnx
dotnet run --project src/HashCompare.App/HashCompare.App.csproj
```

Build succeeds; the full main window opens (folder pickers, filter boxes with watermarks, four checkboxes — Identical unchecked, the rest checked — legend, empty grid with the four column headers). The Browse buttons must open the OS folder picker. Close the app. If XAML compilation fails, consult the Troubleshooting appendix (section 9) — the `#Root` cast and `x:DataType` entries cover the two most likely errors.

---

## Phase 4 — Dialogs (MessageDialog, ConfigWindow, ScanSelectWindow)

### `src/HashCompare.App/Views/MessageDialog.axaml` (VERBATIM)

Adapted from NoBigDiff's proven `MessageDialog`. **Adaptation applied:** NoBigDiff's version references `ChromeBackgroundBrush`/`ChromeForegroundBrush` StaticResources that only exist in NoBigDiff's app resources — those are removed here; the dialog uses the theme's defaults.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="HashCompare.App.Views.MessageDialog"
        Width="440" SizeToContent="Height"
        CanResize="False"
        ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner">

  <StackPanel Margin="20" Spacing="16">
    <TextBlock Name="MessageText" TextWrapping="Wrap" FontSize="13" />
    <StackPanel Name="Buttons" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" />
  </StackPanel>
</Window>
```

### `src/HashCompare.App/Views/MessageDialog.axaml.cs` (VERBATIM)

The generic `ShowAsync` is NoBigDiff's; the `Info`/`Error`/`Confirm` helpers map the six WPF `MessageBox` call sites.

```csharp
using Avalonia.Controls;
using Avalonia.Input;

namespace HashCompare.App.Views;

/// <summary>
/// One modal used for every prompt in the app: plain messages and yes/no confirmations.
/// Buttons are supplied by the caller, and the result is whichever button's value was
/// chosen (default when closed by the title bar).
/// </summary>
public partial class MessageDialog : Window
{
    private object? _result;

    public MessageDialog()
    {
        InitializeComponent();
    }

    public static async Task<T> ShowAsync<T>(
        Window owner,
        string title,
        string message,
        IReadOnlyList<(string Label, T Value, bool IsDefault)> buttons,
        T closedResult)
    {
        var dialog = new MessageDialog
        {
            Title = title,
            _result = closedResult,
        };

        dialog.MessageText.Text = message;

        foreach (var (label, value, isDefault) in buttons)
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 88,
                Padding = new Avalonia.Thickness(12, 6),
                IsDefault = isDefault,
            };

            button.Click += (_, _) =>
            {
                dialog._result = value;
                dialog.Close();
            };

            dialog.Buttons.Children.Add(button);
        }

        await dialog.ShowDialog(owner);
        return (T)dialog._result!;
    }

    // ----- Convenience helpers matching the WPF MessageBox call sites -----

    public static Task Info(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, [("OK", true, true)], true);

    public static Task Error(Window owner, string message) =>
        ShowAsync(owner, "Error", $"Error: {message}", [("OK", true, true)], true);

    /// <summary>Yes/No confirmation; closing the dialog means No.</summary>
    public static Task<bool> Confirm(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, [("Yes", true, false), ("No", false, true)], false);

    /// <summary>Escape dismisses, answering exactly as closing from the title bar does.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape) return;

        e.Handled = true;
        Close();
    }
}
```

### `src/HashCompare.App/Views/ScanSelectWindow.axaml` (VERBATIM)

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="using:HashCompare.App.Views"
        x:Class="HashCompare.App.Views.ScanSelectWindow"
        Title="Scan" Height="450" Width="420"
        WindowStartupLocation="CenterOwner">
  <DockPanel Margin="10">
    <TextBlock Name="TxtPrompt" DockPanel.Dock="Top" Margin="0,0,0,8" TextWrapping="Wrap" />

    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal"
                HorizontalAlignment="Right" Margin="0,10,0,0" Spacing="8">
      <Button Content="Exclude selected" Width="130" Click="ExcludeSelected_Click" />
      <Button Content="Cancel" Width="80" Click="Cancel_Click" />
    </StackPanel>

    <Border BorderBrush="Gray" BorderThickness="1">
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ItemsControl Name="ItemsList" Margin="6">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="local:CheckItem">
              <CheckBox Content="{Binding Name}" IsChecked="{Binding IsChecked}" Margin="2" />
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </Border>
  </DockPanel>
</Window>
```

### `src/HashCompare.App/Views/ScanSelectWindow.axaml.cs` (VERBATIM)

`CheckItem` lives here in the App (it is UI state); Core's `FolderScanner` returns plain strings. WPF's `DialogResult = true` becomes Avalonia's `Close(result)` — the caller awaits `ShowDialog<bool?>`.

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HashCompare.App.Views;

/// <summary>A checklist item shown in <see cref="ScanSelectWindow"/>.</summary>
public sealed class CheckItem
{
    public required string Name { get; init; }
    public bool IsChecked { get; set; }
}

/// <summary>
/// Generic checkbox picker used by the Config dialog to choose folders or file extensions to
/// exclude. Returns the checked names when closed via "Exclude selected".
/// </summary>
public partial class ScanSelectWindow : Window
{
    private readonly List<CheckItem> _items;

    // Parameterless ctor required by the XAML loader; not used at runtime.
    public ScanSelectWindow() : this("", []) { }

    public ScanSelectWindow(string prompt, List<CheckItem> items)
    {
        InitializeComponent();
        _items = items;
        TxtPrompt.Text = prompt;
        ItemsList.ItemsSource = _items;
    }

    /// <summary>Names of the checked items; populated when the dialog is accepted.</summary>
    public IReadOnlyList<string> SelectedItems { get; private set; } = [];

    private void ExcludeSelected_Click(object? sender, RoutedEventArgs e)
    {
        SelectedItems = _items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
```

### `src/HashCompare.App/Views/ConfigWindow.axaml` (VERBATIM)

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="HashCompare.App.Views.ConfigWindow"
        Title="Configuration" Height="580" Width="640"
        WindowStartupLocation="CenterOwner">
  <Grid Margin="12"
        RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto,Auto,*,Auto,Auto,*,Auto">

    <!-- Diff tool -->
    <TextBlock Grid.Row="0" TextWrapping="Wrap" Margin="0,0,0,4"
               Text="Diff tool (used by the row &quot;Compare&quot; action; leave blank to auto-detect WinMerge / VS Code):" />
    <DockPanel Grid.Row="1">
      <Button DockPanel.Dock="Right" Width="90" Margin="6,0,0,0" Content="Browse..."
              Click="BrowseDiffTool_Click" />
      <TextBox Name="TxtDiffToolPath" />
    </DockPanel>
    <TextBlock Grid.Row="2" Margin="0,8,0,4"
               Text="Diff tool arguments ({left} and {right} are replaced with the file paths):" />
    <TextBox Grid.Row="3" Name="TxtDiffToolArguments" />

    <Separator Grid.Row="4" Margin="0,14,0,8" />

    <!-- Exclude folders -->
    <Button Grid.Row="5" Content="Scan..." Width="90" HorizontalAlignment="Left"
            Click="ScanFolders_Click" />
    <TextBlock Grid.Row="6" Margin="0,6,0,4"
               Text="Exclude folders (semicolon-delimited, wildcards allowed):" />
    <TextBox Grid.Row="7" Name="TxtExcludeFolders" AcceptsReturn="True"
             TextWrapping="Wrap" MinHeight="80" />

    <!-- Exclude files -->
    <Button Grid.Row="8" Content="Scan..." Width="90" HorizontalAlignment="Left"
            Margin="0,14,0,0" Click="ScanFiles_Click" />
    <TextBlock Grid.Row="9" Margin="0,6,0,4"
               Text="Exclude files (extensions or name patterns, semicolon-delimited):" />
    <TextBox Grid.Row="10" Name="TxtExcludeFiles" AcceptsReturn="True"
             TextWrapping="Wrap" MinHeight="80" />

    <!-- Dialog buttons -->
    <StackPanel Grid.Row="11" Orientation="Horizontal" HorizontalAlignment="Right"
                Margin="0,14,0,0" Spacing="8">
      <Button Content="OK" Width="80" IsDefault="True" Click="Ok_Click" />
      <Button Content="Cancel" Width="80" IsCancel="True" Click="Cancel_Click" />
    </StackPanel>
  </Grid>
</Window>
```

### `src/HashCompare.App/Views/ConfigWindow.axaml.cs` (VERBATIM)

Same responsibilities as the WPF version; the tree-scanning moved to `Core.FolderScanner`, the file browse uses `OpenFilePickerAsync` with an "All files" type listed first (Unix-friendly — diff tools there have no extension).

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HashCompare.Core;

namespace HashCompare.App.Views;

/// <summary>
/// Edits the diff tool and exclude lists. On OK the values are written back into the supplied
/// <see cref="AppConfig"/> and the dialog closes with true; the caller persists the config.
/// </summary>
public partial class ConfigWindow : Window
{
    private readonly string _source;
    private readonly string _destination;
    private readonly AppConfig _config;

    // Parameterless ctor required by the XAML loader; not used at runtime.
    public ConfigWindow() : this("", "", new AppConfig()) { }

    public ConfigWindow(string source, string destination, AppConfig config)
    {
        InitializeComponent();
        _source = source;
        _destination = destination;
        _config = config;

        TxtExcludeFolders.Text = config.ExcludeFolders;
        TxtExcludeFiles.Text = config.ExcludeFiles;
        TxtDiffToolPath.Text = config.DiffToolPath;
        TxtDiffToolArguments.Text = config.DiffToolArguments;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        _config.ExcludeFolders = (TxtExcludeFolders.Text ?? "").Trim();
        _config.ExcludeFiles = (TxtExcludeFiles.Text ?? "").Trim();
        _config.DiffToolPath = (TxtDiffToolPath.Text ?? "").Trim();
        _config.DiffToolArguments = (TxtDiffToolArguments.Text ?? "").Trim();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private async void BrowseDiffTool_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select diff tool",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("All files") { Patterns = ["*"] },
                new FilePickerFileType("Executables") { Patterns = ["*.exe", "*.cmd", "*.bat"] },
            ]
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path != null)
            TxtDiffToolPath.Text = path;
    }

    private async void ScanFolders_Click(object? sender, RoutedEventArgs e)
    {
        var folders = FolderScanner.CollectFolderNames(_source, _destination);
        if (folders.Count == 0)
        {
            await MessageDialog.Info(this, "Scan", "No subfolders found in the source or destination.");
            return;
        }

        var items = folders.Select(n => new CheckItem { Name = n }).ToList();
        var dlg = new ScanSelectWindow("Select folder names to exclude (matched anywhere in the tree):", items);
        if (await dlg.ShowDialog<bool?>(this) == true)
            TxtExcludeFolders.Text = Append(TxtExcludeFolders.Text ?? "", dlg.SelectedItems);
    }

    private async void ScanFiles_Click(object? sender, RoutedEventArgs e)
    {
        var extensions = FolderScanner.CollectFileExtensions(_source, _destination);
        if (extensions.Count == 0)
        {
            await MessageDialog.Info(this, "Scan", "No files found in the source or destination.");
            return;
        }

        var items = extensions.Select(ext => new CheckItem { Name = ext }).ToList();
        var dlg = new ScanSelectWindow("Select file extensions to exclude:", items);
        if (await dlg.ShowDialog<bool?>(this) == true)
            TxtExcludeFiles.Text = Append(TxtExcludeFiles.Text ?? "", dlg.SelectedItems);
    }

    /// <summary>Appends new entries to a semicolon-delimited list, de-duplicating case-insensitively.</summary>
    private static string Append(string existing, IReadOnlyList<string> additions)
    {
        var parts = existing
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var addition in additions)
        {
            if (!parts.Contains(addition, StringComparer.OrdinalIgnoreCase))
                parts.Add(addition);
        }

        return string.Join(";", parts);
    }
}
```

### Replace the Phase 3 stubs in `src/HashCompare.App/Views/MainWindow.axaml.cs` (ADAPT)

Replace everything from the `// ----- Phase 3 stubs` comment to the end of the class with:

```csharp
    public Task ShowInfoAsync(string title, string message) =>
        MessageDialog.Info(this, title, message);

    public Task ShowErrorAsync(string message) =>
        MessageDialog.Error(this, message);

    public Task<bool> ConfirmAsync(string title, string message) =>
        MessageDialog.Confirm(this, title, message);

    public async Task<bool> ShowConfigAsync(AppConfig config, string source, string destination)
    {
        var dialog = new ConfigWindow(source, destination, config);
        return await dialog.ShowDialog<bool?>(this) == true;
    }
}
```

### Phase 4 gate

```
dotnet build HashCompare.slnx
dotnet run --project src/HashCompare.App/HashCompare.App.csproj
```

Build succeeds; the app opens. Config dialog check: click Config, click OK, then confirm `%APPDATA%\HashCompare\config.json` still parses as JSON and has the same five top-level properties (`FolderSets`, `ExcludeFolders`, `ExcludeFiles`, `DiffToolPath`, `DiffToolArguments`):

```powershell
Get-Content "$env:APPDATA\HashCompare\config.json" | ConvertFrom-Json | Format-List
```

(If the file doesn't exist yet, that's fine — it appears after the first OK/Compare. The schema check then applies.)

---

## Phase 5 — DiffToolLauncher (real implementation)

### `src/HashCompare.App/Services/DiffToolLauncher.cs` (VERBATIM — replaces the Phase 3 stub)

Windows behavior is unchanged from the WPF app (configured tool → WinMerge ×2 → `code --diff`). macOS/Linux get equivalent probe lists. `UseShellExecute = true` only for the `code` fallback on Windows — that is what resolves the `code.cmd` shim; everywhere else it stays `false`.

```csharp
using System.Diagnostics;
using HashCompare.Core;

namespace HashCompare.App.Services;

/// <summary>
/// Launches an external diff tool. Prefers the one configured in <see cref="AppConfig"/>, then
/// falls back to a per-OS probe list, and finally VS Code on PATH.
/// </summary>
public static class DiffToolLauncher
{
    public static bool TryLaunch(AppConfig config, string left, string right)
    {
        // 1. Configured tool.
        if (!string.IsNullOrWhiteSpace(config.DiffToolPath) && File.Exists(config.DiffToolPath))
        {
            var template = string.IsNullOrWhiteSpace(config.DiffToolArguments)
                ? "\"{left}\" \"{right}\""
                : config.DiffToolArguments;
            var args = template.Replace("{left}", left).Replace("{right}", right);
            Process.Start(new ProcessStartInfo(config.DiffToolPath, args) { UseShellExecute = false });
            return true;
        }

        // 2. Auto-detect a well-known tool for this OS.
        foreach (var candidate in GetProbeCandidates())
        {
            if (File.Exists(candidate))
            {
                Process.Start(new ProcessStartInfo(candidate, $"\"{left}\" \"{right}\"")
                {
                    UseShellExecute = false
                });
                return true;
            }
        }

        // 3. Fall back to VS Code on PATH.
        try
        {
            Process.Start(new ProcessStartInfo("code", $"--diff \"{left}\" \"{right}\"")
            {
                // On Windows, shell execution resolves the code.cmd shim; on Unix, "code" is a
                // real executable on PATH and shell execution would break argument quoting.
                UseShellExecute = OperatingSystem.IsWindows()
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> GetProbeCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\Program Files\WinMerge\WinMergeU.exe";
            yield return @"C:\Program Files (x86)\WinMerge\WinMergeU.exe";
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/usr/bin/opendiff";
            yield return "/Applications/Meld.app/Contents/MacOS/Meld";
            yield return "/opt/homebrew/bin/meld";
        }
        else
        {
            yield return "/usr/bin/meld";
            yield return "/usr/local/bin/meld";
        }
    }
}
```

### Phase 5 gate

```
dotnet build HashCompare.slnx
```

Build succeeds. (Behavioral testing of the launcher is part of Matt's manual checklist — Phase 7.)

---

## Phase 6 — Final verification and status report

### 6.1 Release build and tests

```
dotnet build HashCompare.slnx -c Release
dotnet test HashCompare.slnx -c Release
dotnet build HashCompare.sln
```

All three must succeed (the last one re-confirms the WPF app is untouched and still builds).

### 6.2 Fixture trees

Create `verification/` at the repo root, then run this PowerShell script (save as `verification/make-fixtures.ps1`, then run it):

```powershell
$root = Join-Path $env:TEMP "HashCompareFixtures"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$src = Join-Path $root "source"
$dst = Join-Path $root "dest"

New-Item -ItemType Directory -Force (Join-Path $src "sub") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $dst "sub") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $src "bin") | Out-Null

Set-Content (Join-Path $src "identical.txt") "same content"
Set-Content (Join-Path $dst "identical.txt") "same content"
Set-Content (Join-Path $src "sub\different.txt") "source version"
Set-Content (Join-Path $dst "sub\different.txt") "dest version"
Set-Content (Join-Path $src "missing.txt") "only in source"
Set-Content (Join-Path $dst "new.txt") "only in dest"
Set-Content (Join-Path $src "bin\excludable.dll") "binary-ish"

Write-Host "Source: $src"
Write-Host "Dest:   $dst"
```

### 6.3 Launch and screenshot

Start the Release exe:

```powershell
Start-Process "src\HashCompare.App\bin\Release\net10.0\HashCompare.App.exe"
```

Wait ~3 seconds, then capture the window (save as `verification/take-screenshot.ps1` and run):

```powershell
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$proc = Get-Process HashCompare.App -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1

Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public class Win32 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

[Win32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500
$rect = New-Object RECT
[Win32]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$bmp.Save("verification\launch.png", [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Saved verification\launch.png"
```

Inspect `verification/launch.png` (Read it as an image). It must show: the full window chrome, both folder rows, the filter/status row with all four checkboxes and the legend, and the DataGrid **with visible column headers** (Folder / File / Status / Actions). If the grid area is a blank rectangle, the DataGrid StyleInclude is missing — see Troubleshooting. Then stop the process:

```powershell
Stop-Process -Name HashCompare.App -ErrorAction SilentlyContinue
```

### 6.4 Write `PORT-STATUS.md`

Write a report at the repo root covering: what was built (project list, test count and result), the gate results per phase, the fixture paths from 6.2 for Matt's manual pass, the screenshot location, anything skipped or failed, and a pointer to Phase 7 (Matt's checklist). Also restate: **no commits were made; the WPF project is untouched; deleting it is Matt's decision after real-world use.**

---

## Phase 7 — Manual verification checklist (FOR MATT — not the executing model's job)

The executing model stops after Phase 6. Matt runs the app against the Phase 6 fixture trees (`%TEMP%\HashCompareFixtures\source` / `dest`) and checks:

- [ ] Compare run produces exactly: `identical.txt` Identical, `sub\different.txt` Different, `missing.txt` Missing, `new.txt` New; `bin\excludable.dll` appears unless `bin` is in Exclude folders
- [ ] Default checkboxes: Identical **unchecked**, Different/Missing/New checked; identical row hidden until Identical is checked
- [ ] Counts above the checkboxes show 1/1/1/1(+1 with the dll) and **do not change** when checkboxes are toggled
- [ ] Status cell colors match the legend (green New, red Missing, yellow Different, white Identical)
- [ ] Text filters: plain substring (case-insensitive) and `*`/`?` wildcards, watermarks show when empty
- [ ] Row actions: Copy to Dest (Missing→Identical in place), Remove from Dest (confirm dialog, row disappears, file deleted), Replace Dest (Different→Identical), Compare launches the diff tool (WinMerge or `code --diff`)
- [ ] Diff-tool "not found" info dialog appears when neither a configured tool nor WinMerge/VS Code exists
- [ ] Browse buttons open folder pickers; Swap folders swaps the two boxes; dest box is read-only
- [ ] Source history dropdown lists prior sources; picking one auto-fills its destination
- [ ] MRU: existing pre-port `%APPDATA%\HashCompare\config.json` history appears (schema-identical reuse); new compares insert at front, capped at 25
- [ ] Config dialog: values round-trip; Scan… buttons list folder names / extensions from both trees and append selections semicolon-delimited
- [ ] Progress bar + "n / m" text appear during a large compare and controls disable while busy
- [ ] Optional cross-platform smoke: `dotnet build HashCompare.slnx` inside WSL

**Deferred to Matt (deliberately not done by the executing model):** deleting the WPF project + `HashCompare.sln`, all git commits/pushes, and updating `docs/hashcompare/` in the RiderProjects docs vault.

---

## 9. Troubleshooting appendix (symptom → fix)

| Symptom | Fix |
|---|---|
| DataGrid area renders as an empty/blank rectangle, no error | The `StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"` line is missing from `App.axaml`, or the `Avalonia.Controls.DataGrid` package reference is missing. |
| XAML compile error "Unable to resolve property … on object of type 'object'" inside a cell template | The `DataTemplate` lacks `x:DataType="core:FileComparisonResult"` (compiled bindings are on by default via the csproj). |
| XAML compile error on the row-button `Command` bindings | The cast syntax must be exactly `{Binding #Root.((vm:MainWindowViewModel)DataContext).XxxCommand}` — note the double parentheses around the cast and that the Window has `x:Name="Root"`. |
| Compile error: command `CopyToDestCommand` not found | CommunityToolkit generates the command name from the method: `CopyToDestAsync` → `CopyToDestCommand` (the `Async` suffix is dropped). Check the method names match this document. |
| Editable ComboBox misbehaves (text resets, can't type a path) | Known 11.3 rough edge. Swap the ComboBox for `AutoCompleteBox`: `<AutoCompleteBox Name="CmbSourceFolder" Text="{Binding SourceFolder, Mode=TwoWay}" ItemsSource="{Binding SourceHistory}" SelectedItem="{Binding SelectedHistoryItem}" FilterMode="None" MinimumPrefixLength="0" />`. No ViewModel changes needed. |
| Folder/file picker returns something but the path is null | Use `TryGetLocalPath()` on the `IStorageFolder`/`IStorageFile` (already in the listings); `Path` alone is a URI. |
| CS0104 ambiguous reference `Path`/`File`/`Directory` | A file `using`s both `System.IO` (implicit) and an Avalonia namespace exposing a same-named type (e.g. `Avalonia.Controls.Shapes.Path`). Fully qualify as `System.IO.Path` in that file, or remove the offending `using`. |
| Exception inside an async `[RelayCommand]` silently disappears | Expected — async void routing. Every command body in the listings already wraps risky work in try/catch that surfaces via `ShowErrorAsync`. Keep it that way; don't remove the try/catch to "simplify". |
| `dotnet run` fails with "A project was not found" | You are not in the repo root, or the `--project` path is wrong. All commands in this document assume `C:\Users\Matt Brown\RiderProjects\HashCompare`. |
| Old `HashCompare.sln` fails to build after Phase 1 | Should not happen (CPM only affects projects with `PackageReference`s). If it does, the new root props files are the cause — report it in `PORT-STATUS.md` and stop; do **not** edit the WPF project to compensate. |
| Test `ProgressReachesTotal` flaky | You used `Progress<T>` instead of the synchronous `ImmediateProgress` stub from the listing. `Progress<T>` posts via SynchronizationContext. |
| `slnx` not recognized | Requires a current .NET SDK (9.0.200+). Run `dotnet --version`; if the SDK is older, report in `PORT-STATUS.md` — do not convert to `.sln`. |

*End of plan. Generated 2026-08-08 from the WPF source at commit `6d6481e` and the approved outline in `Avalonia-Port.md`.*







