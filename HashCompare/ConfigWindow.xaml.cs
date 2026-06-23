using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace HashCompare;

/// <summary>
/// Edits the exclude-folders / exclude-files lists. On OK the values are written back into the
/// supplied <see cref="AppConfig"/>; the caller is responsible for persisting it.
/// </summary>
public partial class ConfigWindow
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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _config.ExcludeFolders = TxtExcludeFolders.Text.Trim();
        _config.ExcludeFiles = TxtExcludeFiles.Text.Trim();
        _config.DiffToolPath = TxtDiffToolPath.Text.Trim();
        _config.DiffToolArguments = TxtDiffToolArguments.Text.Trim();
        DialogResult = true;
    }

    private void BrowseDiffTool_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "Select diff tool",
            Filter = "Executables (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            TxtDiffToolPath.Text = dialog.FileName;
    }

    private void ScanFolders_Click(object sender, RoutedEventArgs e)
    {
        var folders = CollectFolderNames(_source, _destination);
        if (folders.Count == 0)
        {
            System.Windows.MessageBox.Show("No subfolders found in the source or destination.",
                "Scan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new ScanSelectWindow("Select folder names to exclude (matched anywhere in the tree):", folders)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
            TxtExcludeFolders.Text = Append(TxtExcludeFolders.Text, dlg.SelectedItems);
    }

    private void ScanFiles_Click(object sender, RoutedEventArgs e)
    {
        var extensions = CollectFileExtensions(_source, _destination);
        if (extensions.Count == 0)
        {
            System.Windows.MessageBox.Show("No files found in the source or destination.",
                "Scan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new ScanSelectWindow("Select file extensions to exclude:", extensions)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
            TxtExcludeFiles.Text = Append(TxtExcludeFiles.Text, dlg.SelectedItems);
    }

    /// <summary>Distinct folder (leaf) names found anywhere under either root.</summary>
    private static List<CheckItem> CollectFolderNames(params string[] roots)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>Distinct file extensions (including the dot) found anywhere under either root.</summary>
    private static List<CheckItem> CollectFileExtensions(params string[] roots)
    {
        var extensions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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
