using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Data;
using WinForms = System.Windows.Forms;

namespace HashCompare;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    private readonly ObservableCollection<FileComparisonResult> _comparisonResults = [];
    private readonly ICollectionView _resultsView;
    private readonly AppConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        // Configure the collection view for filtering
        _resultsView = CollectionViewSource.GetDefaultView(_comparisonResults);
        _resultsView.Filter = FilterResults;
        DgResults.ItemsSource = _resultsView;

        // By default, "Identical" files are not shown
        ChkIdentical.IsChecked = false;

        _config = ConfigService.Load();
        RefreshSourceHistory();

        // Pre-fill the folders used in the most recent comparison.
        var lastUsed = _config.FolderSets.FirstOrDefault();
        if (lastUsed != null)
        {
            CmbSourceFolder.Text = lastUsed.Source;
            TxtDestFolder.Text = lastUsed.Destination;
        }
    }

    // ----- Folder selection -----

    private void btnSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Select source folder" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            CmbSourceFolder.Text = dialog.SelectedPath;
    }

    private void btnDestFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Select destination folder" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            TxtDestFolder.Text = dialog.SelectedPath;
    }

    /// <summary>Picking a remembered source folder auto-fills its matching destination.</summary>
    private void CmbSourceFolder_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbSourceFolder.SelectedItem is not string source)
            return;

        var match = _config.FolderSets.FirstOrDefault(fs =>
            string.Equals(fs.Source, source, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            TxtDestFolder.Text = match.Destination;
    }

    private void RefreshSourceHistory()
    {
        var current = CmbSourceFolder.Text;
        CmbSourceFolder.ItemsSource = _config.FolderSets
            .Select(fs => fs.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        CmbSourceFolder.Text = current;
    }

    // ----- Configuration dialog -----

    private void btnConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfigWindow(CmbSourceFolder.Text, TxtDestFolder.Text, _config) { Owner = this };
        if (dialog.ShowDialog() == true)
            ConfigService.Save(_config);
    }

    // ----- Comparison -----

    private async void btnCompare_Click(object sender, RoutedEventArgs e)
    {
        var source = CmbSourceFolder.Text;
        var destination = TxtDestFolder.Text;

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            System.Windows.MessageBox.Show("Please select both source and destination folders.",
                "Missing Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(source) || !Directory.Exists(destination))
        {
            System.Windows.MessageBox.Show("Source and destination folders must both exist.",
                "Invalid Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Remember this pairing before comparing.
        ConfigService.RememberFolderSet(_config, source, destination);
        ConfigService.Save(_config);
        RefreshSourceHistory();

        // Snapshot exclude rules so the background thread doesn't touch UI/config state.
        var folderPatterns = WildcardMatcher.Compile(_config.ExcludeFolders);
        var filePatterns = WildcardMatcher.Compile(WildcardMatcher.ExpandFileExcludes(_config.ExcludeFiles));

        SetBusy(true);
        var progress = new Progress<(int done, int total)>(p =>
        {
            CompareProgress.Maximum = Math.Max(1, p.total);
            CompareProgress.Value = p.done;
            TxtProgress.Text = $"{p.done} / {p.total}";
        });

        try
        {
            var results = await Task.Run(
                () => BuildComparison(source, destination, folderPatterns, filePatterns, progress));

            _comparisonResults.Clear();
            foreach (var result in results)
                _comparisonResults.Add(result);
            _resultsView.Refresh();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Runs on a background thread: enumerates and hashes files, reporting progress, and returns the
    /// rows to display. Must not touch UI or shared mutable state.
    /// </summary>
    private static List<FileComparisonResult> BuildComparison(
        string sourceDir,
        string destDir,
        IReadOnlyList<System.Text.RegularExpressions.Regex> folderPatterns,
        IReadOnlyList<System.Text.RegularExpressions.Regex> filePatterns,
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
            .Select(f => f[sourceDir.Length..].TrimStart('\\', '/'))
            .Where(rel => !IsExcluded(rel))
            .ToList();

        var destFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories)
            .Select(f => f[destDir.Length..].TrimStart('\\', '/'))
            .Where(rel => !IsExcluded(rel))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sourceFileSet = new HashSet<string>(sourceFiles, StringComparer.OrdinalIgnoreCase);
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

            string status;
            if (!destExists)
                status = "Missing";
            else
                status = CalculateSha256(sourcePath) == CalculateSha256(destPath) ? "Identical" : "Different";

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
                Status = "New"
            });

            Report();
        }

        return results;
    }

    /// <summary>Toggles the progress UI and disables interaction while a comparison runs.</summary>
    private void SetBusy(bool busy)
    {
        BtnCompare.IsEnabled = !busy;
        BtnConfig.IsEnabled = !busy;
        BtnSourceFolder.IsEnabled = !busy;
        BtnDestFolder.IsEnabled = !busy;
        CmbSourceFolder.IsEnabled = !busy;

        var visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CompareProgress.Visibility = visibility;
        TxtProgress.Visibility = visibility;

        if (busy)
        {
            CompareProgress.Value = 0;
            TxtProgress.Text = string.Empty;
        }
    }

    private static string CalculateSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    // ----- Row actions -----

    private void CopyToDest_Click(object sender, RoutedEventArgs e)
    {
        if (GetResult(sender) is not { } result)
            return;

        try
        {
            CopyOver(result.SourceFullPath, result.DestFullPath);
            result.Status = "Identical";
            _resultsView.Refresh();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ReplaceDest_Click(object sender, RoutedEventArgs e)
    {
        if (GetResult(sender) is not { } result)
            return;

        try
        {
            CopyOver(result.SourceFullPath, result.DestFullPath);
            result.Status = "Identical";
            _resultsView.Refresh();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RemoveFromDest_Click(object sender, RoutedEventArgs e)
    {
        if (GetResult(sender) is not { } result)
            return;

        if (System.Windows.MessageBox.Show($"Delete from destination?\n\n{result.DestFullPath}",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            File.Delete(result.DestFullPath);
            _comparisonResults.Remove(result);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void CompareFiles_Click(object sender, RoutedEventArgs e)
    {
        if (GetResult(sender) is not { } result)
            return;

        if (!TryLaunchDiff(_config, result.SourceFullPath, result.DestFullPath))
        {
            System.Windows.MessageBox.Show(
                "No diff tool was found. Configure one under Config, or install WinMerge or VS Code.\n\n" +
                $"Source: {result.SourceFullPath}\nDestination: {result.DestFullPath}",
                "Compare", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static void CopyOver(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Copy(source, destination, overwrite: true);
    }

    /// <summary>
    /// Launches an external diff tool. Prefers the one configured in <see cref="AppConfig"/>, then
    /// falls back to auto-detecting WinMerge and finally VS Code.
    /// </summary>
    private static bool TryLaunchDiff(AppConfig config, string left, string right)
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

        // 2. Auto-detect WinMerge.
        foreach (var winMerge in new[]
                 {
                     @"C:\Program Files\WinMerge\WinMergeU.exe",
                     @"C:\Program Files (x86)\WinMerge\WinMergeU.exe"
                 })
        {
            if (File.Exists(winMerge))
            {
                Process.Start(new ProcessStartInfo(winMerge, $"\"{left}\" \"{right}\"") { UseShellExecute = false });
                return true;
            }
        }

        // 3. Fall back to VS Code on PATH.
        try
        {
            Process.Start(new ProcessStartInfo("code", $"--diff \"{left}\" \"{right}\"") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static FileComparisonResult? GetResult(object sender) =>
        (sender as FrameworkElement)?.DataContext as FileComparisonResult;

    private static void ShowError(Exception ex) =>
        System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);

    // ----- Filtering -----

    private bool FilterResults(object item)
    {
        if (item is FileComparisonResult result)
        {
            return result.Status switch
            {
                "Identical" => ChkIdentical.IsChecked ?? false,
                "Different" => ChkDifferent.IsChecked ?? true,
                "Missing" => ChkMissing.IsChecked ?? true,
                "New" => ChkNew.IsChecked ?? true,
                _ => true
            };
        }
        return true;
    }

    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        _resultsView?.Refresh();
    }
}
