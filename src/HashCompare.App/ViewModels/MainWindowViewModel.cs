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

    // Command enable/disable states.
    public bool CanCompare => ShowDifferent || ShowMissing;

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
            ApplyFilter();
            UpdateStatusCounts();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ReplaceDestAsync(FileComparisonResult result)
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
    private void CompareFiles(FileComparisonResult result)
    {
        if (!DiffToolLauncher.Launch(_config, result.SourceFullPath, result.DestFullPath))
        {
            var message = $"No diff tool was found. Configure one under Config, or install WinMerge or VS Code.\n\nSource: {result.SourceFullPath}\nDestination: {result.DestFullPath}";
            Task.Run(async () => await _dialogs.ShowInfoAsync("Compare", message));
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

    // Command enable/disable states for row buttons.
    public bool CanCopyToDest => ShowMissing;
    public bool CanRemoveFromDest => ShowNew;
    public bool CanReplaceDest => ShowDifferent;
}