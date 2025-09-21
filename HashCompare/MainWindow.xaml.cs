using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();
            
        // Configure the collection view for filtering
        _resultsView = CollectionViewSource.GetDefaultView(_comparisonResults);
        _resultsView.Filter = FilterResults;
        DgResults.ItemsSource = _resultsView;
            
        // By default, "Identical" files are not shown
        ChkIdentical.IsChecked = false;
    }

    private void btnSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Select source folder";

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            TxtSourceFolder.Text = dialog.SelectedPath;
        }
    }

    private void btnTargetFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Select target folder";

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            TxtTargetFolder.Text = dialog.SelectedPath;
        }
    }

    private void btnCompare_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtSourceFolder.Text) || string.IsNullOrEmpty(TxtTargetFolder.Text))
        {
            System.Windows.MessageBox.Show("Please select both source and target folders.", "Missing Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            CompareDirectories(TxtSourceFolder.Text, TxtTargetFolder.Text);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error during comparison: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CompareDirectories(string sourceDir, string targetDir)
    {
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        _comparisonResults.Clear();

        try
        {
            // Get all files in source directory (including subdirectories)
            var sourceFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                .Select(f => f.Substring(sourceDir.Length).TrimStart('\\', '/'))
                .ToList();

            // Get all files in target directory
            var targetFiles = Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories)
                .Select(f => f.Substring(targetDir.Length).TrimStart('\\', '/'))
                .ToHashSet();

            // Compare each source file
            foreach (var relativePath in sourceFiles)
            {
                var sourceFilePath = Path.Combine(sourceDir, relativePath);
                var targetFilePath = Path.Combine(targetDir, relativePath);

                var crc32Source = CalculateCrc32(sourceFilePath);
                var sha256Source = CalculateSha256(sourceFilePath);
                var targetExists = File.Exists(targetFilePath);
                var crc32Target = targetExists ? CalculateCrc32(targetFilePath) : string.Empty;
                var sha256Target = targetExists ? CalculateSha256(targetFilePath) : string.Empty;
                var status = (
                        TargetExists: targetExists,
                        CrcEquality: crc32Source == crc32Target,
                        Sha256Equality: sha256Source == sha256Target) switch
                    {
                        (TargetExists: false, _, _) => "Missing",
                        (_, CrcEquality: true, Sha256Equality: true) => "Identical",
                        _ => "Different"
                    };

                var result = new FileComparisonResult
                {
                    FilePath = relativePath,
                    // Compute source hashes
                    Crc32Source = CalculateCrc32(sourceFilePath),
                    Sha256Source = CalculateSha256(sourceFilePath),
                    Crc32Target = crc32Target,
                    Sha256Target = sha256Target,
                    Status = status
                };

                _comparisonResults.Add(result);
            }

            // Find files in target that don't exist in source
            var sourceFileSet = new HashSet<string>(sourceFiles);
            foreach (var relativePath in targetFiles)
            {
                if (!sourceFileSet.Contains(relativePath))
                {
                    var targetFilePath = Path.Combine(targetDir, relativePath);
                    var result = new FileComparisonResult
                    {
                        FilePath = relativePath,
                        Status = "New",
                        Crc32Source = string.Empty,
                        Sha256Source = string.Empty,
                        Crc32Target = CalculateCrc32(targetFilePath),
                        Sha256Target = CalculateSha256(targetFilePath)
                    };
                    _comparisonResults.Add(result);
                }
            }
                
            // Apply the filter
            _resultsView.Refresh();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static string CalculateCrc32(string filePath)
    {
        using var crc32 = new Crc32();
        using var stream = File.OpenRead(filePath);
        var hash = crc32.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    private static string CalculateSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

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

public class FileComparisonResult
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    // get is used by WPF binding
    public required string FilePath { get; init; }
    public required string Status { get; init; }
    public required string Crc32Source { get; init; }
    public required string Crc32Target { get; init; }
    public required string Sha256Source { get; init; }
    public required string Sha256Target { get; init; }
    // ReSharper restore UnusedAutoPropertyAccessor.Global
}

// CRC32 implementation (SHA256 is built-in)
public class Crc32 : HashAlgorithm
{
    private uint _crc32 = 0xFFFFFFFF;
    private readonly uint[] _crc32Table;

    public Crc32()
    {
        _crc32Table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            _crc32Table[i] = crc;
        }
            
        Initialize();
    }

    public sealed override void Initialize()
    {
        _crc32 = 0xFFFFFFFF;
    }

    protected override void HashCore(byte[] buffer, int offset, int count)
    {
        for (var i = offset; i < offset + count; i++)
        {
            _crc32 = (_crc32 >> 8) ^ _crc32Table[buffer[i] ^ (_crc32 & 0x000000FF)];
        }
    }

    protected override byte[] HashFinal()
    {
        var hash = BitConverter.GetBytes(~_crc32);
        Array.Reverse(hash);
        return hash;
    }
}
