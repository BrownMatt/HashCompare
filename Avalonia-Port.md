# Write Avalonia-Port.md — port plan for HashCompare → cross-platform Avalonia

## Context

HashCompare is a small WPF desktop app (`net10.0-windows`, ~1,200 lines across 11 files, **zero NuGet packages**) that compares two folder trees by SHA256 hash. The user asked for a plan to port it to cross-platform Avalonia, written to `Avalonia-Port.md`. **The deliverable of this task is the planning document itself, committed to the repo — not the port.**

Key exploration findings that shaped the plan:
- The comparison core is already decoupled: `BuildComparison` (MainWindow.xaml.cs:166) is a pure static function taking primitives + compiled `Regex[]` + `IProgress<(int,int)>`; together with `CalculateSha256`, `WildcardMatcher.cs`, `AppConfig.cs`, `ConfigService.cs`, `FileComparisonResult.cs` (~250–350 lines) it lifts into a Core library nearly unchanged.
- The Windows-coupled surface: WinForms `FolderBrowserDialog`/`OpenFileDialog` (3 sites), `System.Windows.MessageBox` (6 sites), WPF `DataTrigger` styles (status cell colors, watermark hack), `ICollectionView` filtering, `BooleanToVisibilityConverter`, WPF `ShowDialog bool?` pattern, hard-coded WinMerge paths, `OrdinalIgnoreCase` path sets, `f[sourceDir.Length..]` relative-path fragility.
- Workspace precedent (NoBigDiff, FreeSheets, WebTester): Avalonia 11 + CommunityToolkit.Mvvm, `.slnx`, `Directory.Packages.props` CPM. NoBigDiff has a proven `MessageDialog.axaml` to adapt; WebTester's `Directory.Packages.props` is the version template.

## Implementation

Single step: create `C:\Users\Matt Brown\RiderProjects\HashCompare\Avalonia-Port.md` containing the port plan below, then commit and push on a worktree branch (background-session isolation).

1. `EnterWorktree` (required before edits in this session).
2. Write `Avalonia-Port.md` with the content outlined in "Document content" below.
3. Commit ("Add Avalonia cross-platform port plan") and push the branch (never master).
4. Optionally note in the report that `docs/hashcompare/Knowledge.md` is stale (predates swap-button/status-counts/legend commits) — mention only, don't fix here.

## Document content (Avalonia-Port.md)

The document will contain the following plan, structured as phased executable steps:

### Headline decisions
| Decision | Choice |
|---|---|
| Target framework | net10.0 (app is already on net10; fall back to net8.0 only if tooling misbehaves) |
| Solution | New `HashCompare.slnx` + `Directory.Build.props` + `Directory.Packages.props` (CPM), modeled on WebTester; old WPF project stays on disk until final cleanup phase |
| Projects | `src/HashCompare.Core` (engine, models, config, wildcard, folder scanner — zero UI deps), `src/HashCompare.App` (Avalonia), `tests/HashCompare.Core.Tests` (xunit) |
| MVVM | CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) — matches all three of the user's Avalonia apps |
| Packages | Avalonia 11.3.x (+Desktop, +Themes.Fluent, +Diagnostics Debug-only), Avalonia.Controls.DataGrid, CommunityToolkit.Mvvm; xunit stack for tests |
| MessageBox | Custom `MessageDialog` adapted from NoBigDiff (`Info`/`Error`/`Confirm` helpers) — no extra package, keeps the app's minimal-dependency character |
| ICollectionView | Manual filtered rebuild: master `List<FileComparisonResult>` + `FilteredResults` ObservableCollection swapped on filter change (row counts are small; DynamicData is overkill) |
| Status strings | `FileStatus` enum in Core replaces the "Identical"/"Different"/"Missing"/"New" magic strings (8+ literal comparisons); `ToString()` preserves display text; not persisted, so no config compat issue |
| Theme | `RequestedThemeVariant="Light"` — legend/status colors (#A5D6A7/#EF9A9A/#FFF59D/White) assume a light surface |

### Deliberate behavior changes (everything else identical)
1. `Path.GetRelativePath` replaces `f[sourceDir.Length..].TrimStart('\\','/')` (fixes trailing-separator fragility, works on all OSes).
2. `PathComparison` policy helper: `OrdinalIgnoreCase` on Windows/macOS, `Ordinal` on Linux — used for the dest/source path sets and MRU dedupe. Hard-coded policy, not configurable.
3. Per-OS diff-tool probing (Windows behavior unchanged): configured tool → OS probe list (WinMerge ×2 on Windows; `opendiff`/Meld on macOS; `meld` on Linux) → `code --diff` fallback; `UseShellExecute=true` only on Windows (resolves the `code.cmd` shim), `false` on Unix.
4. Collapse the byte-identical `CopyToDest_Click`/`ReplaceDest_Click` into one command.

Explicitly NOT changing: comparison semantics, status display strings, default checkbox states (Identical unchecked), counts-ignore-filters behavior, ~1% progress throttling, MRU cap 25, config.json schema/location (`SpecialFolder.ApplicationData` → `~/.config/HashCompare` on Unix automatically), `{left}`/`{right}` tokens, cell colors, column proportions 2*/1*/0.4*/0.6*, window sizes, dest TextBox read-only.

### Target layout
```
HashCompare.slnx, Directory.Build.props, Directory.Packages.props, Avalonia-Port.md
src/HashCompare.Core/      FileStatus, FileComparisonResult (hand-written INPC, Core stays dep-free),
                           ComparisonEngine (BuildComparison+CalculateSha256+CopyOver), WildcardMatcher,
                           FolderScanner (extracted from ConfigWindow), PathComparison, AppConfig, ConfigService
src/HashCompare.App/       Program, App.axaml (FluentTheme + DataGrid StyleInclude + Light),
                           ViewModels/MainWindowViewModel, Views/{MainWindow,ConfigWindow,ScanSelectWindow,MessageDialog},
                           Converters/StatusToBrushConverter, Services/DiffToolLauncher
tests/HashCompare.Core.Tests/  ComparisonEngineTests, WildcardMatcherTests, ConfigServiceTests
```

### Phases (each with a build/test/run gate)
1. **Scaffolding** — props files (copy WebTester's versions), three projects, slnx; App.axaml must include `avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml` or the grid renders empty. Gate: builds, empty window launches.
2. **Core extraction + tests** — move the five portable files; extract `ComparisonEngine` with the two path changes; `FileStatus` enum; `PathComparison`; `FolderScanner`. Tests: engine against temp fixture trees (all four statuses, excludes, progress completion), WildcardMatcher (`Compile`, `ExpandFileExcludes`), ConfigService MRU (move-to-front/dedupe/cap-25). Gate: `dotnet test` green.
3. **MainWindowViewModel + MainWindow.axaml** — observable props (folders, history, 4 status toggles, 2 text filters, busy/progress, counts, FilteredResults), commands (Compare async, Swap, Browse ×2 via `StorageProvider.OpenFolderPickerAsync`, Config, row commands via `#Root` compiled-binding cast + `CommandParameter="{Binding}"`), small `IDialogs` interface implemented by the window (NoBigDiff pattern). XAML translation table: `TextBox.Watermark` replaces the DataTrigger watermark hack; `IsVisible="{Binding CanX}"` replaces BooleanToVisibilityConverter; status cell = `DataGridTemplateColumn` + `StatusToBrushConverter` (WPF `DataGridCell` DataTriggers don't exist); bold Folder/File columns become template columns; editable ComboBox history with `AutoCompleteBox` as fallback if flaky. Gate: compare runs, filters/counts/row buttons behave.
4. **Dialogs** — `MessageDialog` (adapt NoBigDiff's) replaces all six MessageBox sites; `ConfigWindow`/`ScanSelectWindow` use Avalonia's `Close(result)` + `await ShowDialog<bool?>(owner)`; diff-tool browse via `OpenFilePickerAsync` ("All files" listed first for Unix friendliness). Gate: config round-trips to the same config.json.
5. **Cross-platform behaviors** — `DiffToolLauncher` per-OS probe/fallback as above.
6. **Verification** — Release build + tests; run against small fixture trees; drive the exe with the **avalonia-drive** skill: default checkbox states, legend/cell colors, busy-state progress, counts ignore filters, wildcard text filters + watermarks, per-status row actions (Copy→Identical in place, Remove with confirm, Replace, Compare launches diff tool), Browse/Swap/history-autofill, MRU persistence and schema-identical reuse of existing `%APPDATA%\HashCompare\config.json`, config dialog round-trip + scan pickers. Optional: WSL smoke build.
7. **Cleanup** — `git rm` the WPF project + old .sln, update README; one commit per phase.

### Risks
- Editable `ComboBox` (`IsEditable`, new in 11.3) — fallback to `AutoCompleteBox` without VM changes.
- DataGrid package quirks (mandatory StyleInclude, star-width/template differences) — visual check in phase 6.
- Compiled-binding friction (`x:DataType` on cell templates, `#Root` casts) — expected first-build XAML errors.
- Async RelayCommands change exception routing — keep try/catch-around-everything so errors surface via the error dialog.

## Verification (of this task)

- `Avalonia-Port.md` exists at the repo root, renders cleanly as Markdown, and its phase gates/file lists are consistent with the actual source files (line refs spot-checked: BuildComparison at MainWindow.xaml.cs:166, OrdinalIgnoreCase sets at :193–195, csproj UseWPF+UseWindowsForms confirmed).
- Committed on a worktree branch and pushed (repo has a GitHub remote).
