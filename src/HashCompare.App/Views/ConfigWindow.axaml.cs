using Avalonia.Controls;
using System.IO;
using HashCompare.App.ViewModels;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HashCompare.App.Services;

namespace HashCompare.App.Views;

/// <summary>
/// Edits the exclude-folders / exclude-files lists. On OK the values are written back into the
/// supplied <see cref="AppConfig"/>; the caller is responsible for persisting it.
/// </summary>
public partial class ConfigWindow : Window
{
    private readonly string _source;
    private readonly string _destination;
    private readonly AppConfig _config;

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

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _config.ExcludeFolders = TxtExcludeFolders.Text.Trim();
        _config.ExcludeFiles = TxtExcludeFiles.Text.Trim();
        _config.DiffToolPath = TxtDiffToolPath.Text.Trim();
        _config.DiffToolArguments = TxtDiffToolArguments.Text.Trim();
        Close(true);
    }

    private async void BrowseDiffTool_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select diff tool",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executables")
                {
                    Patterns = new[] { "*.exe", "*.cmd", "*.bat" }
                },
                new FilePickerFileType("All files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (file.Count > 0)
        {
            var path = file[0];
            if (path.TryGetLocalPath(out var localPath))
                TxtDiffToolPath.Text = localPath;
        }
    }

    private async void ScanFolders_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await CollectFolderNamesAsync(_source, _destination);
        if (folders.Count == 0)
        {
            await ShowMessageDialogAsync("No subfolders found in the source or destination.", "Scan");
            return;
        }

        var dlg = new ScanSelectWindow("Select folder names to exclude (matched anywhere in the tree):", folders)
        {
            Owner = this
        };
        if (await dlg.ShowDialog<bool>() == true)
            TxtExcludeFolders.Text = Append(TxtExcludeFolders.Text, dlg.SelectedItems);
    }

    private async void ScanFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var extensions = await CollectFileExtensionsAsync(_source, _destination);
        if (extensions.Count == 0)
        {
            await ShowMessageDialogAsync("No files found in the source or destination.", "Scan");
            return;
        }

        var dlg = new ScanSelectWindow("Select file extensions to exclude:", extensions)
        {
            Owner = this
        };
        if (await dlg.ShowDialog<bool>() == true)
            TxtExcludeFiles.Text = Append(TxtExcludeFiles.Text, dlg.SelectedItems);
    }

    private static async Task<List<CheckItem>> CollectFolderNamesAsync(params string[] roots)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        return names.Select(n => new CheckItem { Name = n }).ToList();
    }

    private static async Task<List<CheckItem>> CollectFileExtensionsAsync(params string[] roots)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        return extensions.Select(ext => new CheckItem { Name = ext }).ToList();
    }

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

    private Task ShowMessageDialogAsync(string message, string title)
    {
        var dialog = new MessageDialog(title, message);
        dialog.Owner = this;
        return dialog.ShowDialog<bool>();
    }
}