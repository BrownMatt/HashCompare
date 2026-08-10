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