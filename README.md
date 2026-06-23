# HashCompare

A Windows desktop app for comparing the contents of two folders by **SHA256 hash**, so you
can verify backups, copies, and migrations with byte-level confidence instead of trusting
file sizes or timestamps. Each result row offers one-click actions to reconcile the two
folders.

Built with **WPF** on **.NET 10** (Windows-only).

---

## Requirements

- Windows
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  (`Microsoft.WindowsDesktop.App 10.x`)
- Optional: a diff tool ([WinMerge](https://winmerge.org/) or
  [VS Code](https://code.visualstudio.com/)) for the per-file **Compare** action

## Build & run

```sh
# from the repo root
dotnet build HashCompare.sln -c Release
dotnet run --project HashCompare/HashCompare.csproj
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
saved to:

```
%APPDATA%\HashCompare\config.json
```

This same file also stores your folder-set history. It's created automatically; if it's ever
corrupted, the app falls back to defaults rather than failing to start.

### Diff tool

- **Diff tool path** — full path to the executable used by the row **Compare** action
  (use **Browse…** to select it). Leave **blank** to auto-detect WinMerge, then VS Code.
- **Diff tool arguments** — the argument template passed to the tool. The tokens `{left}` and
  `{right}` are replaced with the source and destination file paths.
  - WinMerge example: `"{left}" "{right}"`
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

- Windows-only (uses WPF + a WinForms folder/file picker).
- The **Compare** action requires a diff tool — either the one you configure, or WinMerge /
  VS Code installed on the machine.
- There are currently no automated tests.
