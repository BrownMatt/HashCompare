using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WinForms = System.Windows.Forms;

namespace HashCompare
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileComparisonResult> _comparisonResults = new();

        public MainWindow()
        {
            InitializeComponent();
            dgResults.ItemsSource = _comparisonResults;
        }

        private void btnSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select source folder"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtSourceFolder.Text = dialog.SelectedPath;
            }
        }

        private void btnTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select target folder"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtTargetFolder.Text = dialog.SelectedPath;
            }
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSourceFolder.Text) || string.IsNullOrEmpty(txtTargetFolder.Text))
            {
                System.Windows.MessageBox.Show("Please select both source and target folders.", "Missing Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CompareDirectories(txtSourceFolder.Text, txtTargetFolder.Text);
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
                    string sourceFilePath = System.IO.Path.Combine(sourceDir, relativePath);
                    string targetFilePath = System.IO.Path.Combine(targetDir, relativePath);

                    var result = new FileComparisonResult
                    {
                        FilePath = relativePath
                    };

                    // Compute source hashes
                    result.CRC32Source = CalculateCRC32(sourceFilePath);
                    result.SHA256Source = CalculateSHA256(sourceFilePath);

                    // Check if file exists in target
                    if (targetFiles.Contains(relativePath))
                    {
                        // Calculate target hashes
                        result.CRC32Target = CalculateCRC32(targetFilePath);
                        result.SHA256Target = CalculateSHA256(targetFilePath);

                        // Compare hashes
                        if (result.CRC32Source == result.CRC32Target && 
                            result.SHA256Source == result.SHA256Target)
                        {
                            result.Status = "Identical";
                        }
                        else
                        {
                            result.Status = "Different";
                        }
                    }
                    else
                    {
                        result.Status = "Missing";
                    }

                    _comparisonResults.Add(result);
                }

                // Find files in target that don't exist in source
                var sourceFileSet = new HashSet<string>(sourceFiles);
                foreach (var relativePath in targetFiles)
                {
                    if (!sourceFileSet.Contains(relativePath))
                    {
                        string targetFilePath = System.IO.Path.Combine(targetDir, relativePath);
                        var result = new FileComparisonResult
                        {
                            FilePath = relativePath,
                            Status = "New",
                            CRC32Target = CalculateCRC32(targetFilePath),
                            SHA256Target = CalculateSHA256(targetFilePath)
                        };
                        _comparisonResults.Add(result);
                    }
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private string CalculateCRC32(string filePath)
        {
            using var crc32 = new Crc32();
            using var stream = File.OpenRead(filePath);
            byte[] hash = crc32.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        private string CalculateSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }

    public class FileComparisonResult
    {
        public string FilePath { get; set; }
        public string Status { get; set; }
        public string CRC32Source { get; set; }
        public string CRC32Target { get; set; }
        public string SHA256Source { get; set; }
        public string SHA256Target { get; set; }
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
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
                _crc32Table[i] = crc;
            }
            
            Initialize();
        }

        public override void Initialize()
        {
            _crc32 = 0xFFFFFFFF;
        }

        protected override void HashCore(byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
            {
                _crc32 = (_crc32 >> 8) ^ _crc32Table[buffer[i] ^ (_crc32 & 0x000000FF)];
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] hash = BitConverter.GetBytes(~_crc32);
            Array.Reverse(hash);
            return hash;
        }
    }
}