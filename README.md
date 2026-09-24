# HashCompare

A cross-platform desktop app (Windows, macOS, Linux) for comparing the contents of two folders by **SHA256 hash**, so you
can verify backups, copies, and migrations with byte-level confidence instead of trusting
file sizes or timestamps. Each result row offers one-click actions to reconcile the two
folders.

Built with **[Avalonia UI](https://avaloniaui.net/)** on **.NET 10**.

---

## Requirements

- Windows, macOS, or Linux
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build (or the .NET 10
  Runtime to run a published build)
- Optional: a diff tool for the per-file **Compare** action, such as
  [WinMerge](https://winmerge.org/), [Meld](https://meldmerge.org/), or
  [VS Code](https://code.visualstudio.com/)

## Build, run & test

```sh
# from the repo root
dotnet build HashCompare.slnx -c Release
dotnet run --project src/HashCompare.App/HashCompare.App.csproj
dotnet test HashCompare.slnx
```

## Project layout

```
HashCompare.slnx                 Solution file
Directory.Build.props            Shared MSBuild settings
Directory.Packages.props         Central NuGet package versions
src/
  HashCompare.Core/              UI-independent logic: hashing and comparison engine,
                                 folder scanning, wildcard matching, config persistence
  HashCompare.App/               Avalonia desktop app (MVVM via CommunityToolkit.Mvvm)
    ViewModels/                  MainWindowViewModel
    Views/                       Main window and dialogs (Config, Scan, Message)
    Converters/                  Status-to-color converter for the results grid
    Services/                    DiffToolLauncher (per-OS diff tool detection)
tests/
  HashCompare.Core.Tests/        xUnit tests for HashCompare.Core
```

---

## How to use

1. **Pick the Source folder** — type/paste a path, choose a previous folder from the
   drop-down, or click **Browse…**. Picking a remembered source from the drop-down
   auto-fills its matching destination.
2. **Pick the Destination folder** via its **Browse…** button.
3. Click **Compare Folders**. The comparison runs in the background with a progress bar; the
   folder pair is saved to your history.
4. Review the grid and use the **filter checkboxes** to show/hide statuses. Use the per-row
   **action buttons** to copy, replace, remove, or diff files.
5. Narrow large result sets with the two **text filters** on the button row: the left box
   filters by **folder path**, the right box by **file name**. Both filter live as you type
   and combine with the status checkboxes. Plain text matches anywhere (case-insensitive);
   include `*` or `?` to switch to wildcard matching (e.g. `*.log`, `202?-report*`). The
   filters stay applied when you re-run a comparison.

On the next launch, the Source and Destination fields are pre-filled with the folders from
your most recent comparison.

### Statuses

Each file is classified by comparing SHA256 hashes. Rows are color-coded:

| Status      | Color        | Meaning                                   | Row action(s)              |
|-------------|--------------|-------------------------------------------|----------------------------|
| **Identical** | White      | Present in both, hashes match             | (none)                     |
| **Different** | Yellow     | Present in both, hashes differ            | Replace Dest · Compare     |
| **Missing**   | Red        | In source only (absent from destination)  | Copy to Dest               |
| **New**       | Green      | In destination only (absent from source)  | Remove from Dest           |

By default, **Identical** files are hidden; Different / Missing / New are shown.

### Row actions

- **Copy to Dest** — copies the source file into the destination (creating folders as needed).
- **Replace Dest** — overwrites the destination file with the source version.
- **Remove from Dest** — deletes the destination file (after a confirmation prompt).
- **Compare** — opens the two files side-by-side in your diff tool (see configuration below).

---

## Features

- SHA256 content comparison across two folder trees (recursive).
- Color-coded, filterable results grid (Folder · File · Status · Actions).
- Live folder-path and file-name text filters (substring or `*`/`?` wildcards).
- One-click reconcile actions per row (copy / replace / remove / diff).
- Background comparison with a progress bar so the UI stays responsive on large trees.
- Remembered **folder-set history** (up to 25 pairs) in an editable Source drop-down, with
  automatic pre-fill of the last-used pair on startup.
- Configurable **exclude** rules for folders and file types, with a **Scan** helper that
  lists what's actually present so you can tick items to exclude.
- Configurable external **diff tool**.

---

## Configuration

Click **Config** (next to *Compare Folders*) to open the configuration dialog. Settings are
saved to `HashCompare/config.json` under your user's application-data folder:

| OS      | Location                                                        |
|---------|-----------------------------------------------------------------|
| Windows | `%APPDATA%\HashCompare\config.json`                              |
| macOS   | `~/Library/Application Support/HashCompare/config.json`        |
| Linux   | `$XDG_CONFIG_HOME/HashCompare/config.json` (default `~/.config`) |

This same file also stores your folder-set history. It's created automatically; if it's ever
corrupted, the app falls back to defaults rather than failing to start.

### Diff tool

- **Diff tool path** — full path to the executable used by the row **Compare** action
  (use **Browse…** to select it). Leave **blank** to auto-detect one:
  - **Windows**: WinMerge (in `Program Files`), then VS Code (`code` on `PATH`)
  - **macOS / Linux**: Meld (`/usr/bin/meld` or `/usr/local/bin/meld`), then VS Code
    (`code` on `PATH`)
- **Diff tool arguments** — the argument template passed to the tool. The tokens `{left}` and
  `{right}` are replaced with the source and destination file paths.
  - WinMerge example: `"{left}" "{right}"`
  - Meld example: `"{left}" "{right}"`
  - VS Code example: `--diff "{left}" "{right}"`

### Exclude folders

A semicolon-delimited list of folder names to skip during comparison. **Wildcards** `*` and
`?` are supported, and a pattern matches **any** folder of that name anywhere in the tree.

```
bin;obj;.git;node_modules;temp*
```

Click **Scan…** above this field to list every folder name found under the current source and
destination; tick the ones you want and choose **Exclude selected** to append them.

### Exclude files

A semicolon-delimited list of file types / name patterns to skip. Bare extensions are
expanded automatically — `.tmp` (or `tmp`) becomes `*.tmp`. Entries that already contain
wildcards are used as-is.

```
.tmp;.log;~$*;Thumbs.db
```

Click **Scan…** above this field to list every file extension found under the current source
and destination; tick the ones you want and choose **Exclude selected** to append them.

---

## Notes & limitations

- Path matching follows the platform: case-insensitive on Windows and macOS,
  case-sensitive on Linux.
- The **Compare** action requires a diff tool — either the one you configure, or one of the
  auto-detected tools above installed on the machine.
- Automated tests cover the core library (`HashCompare.Core`); the UI has no automated tests.
