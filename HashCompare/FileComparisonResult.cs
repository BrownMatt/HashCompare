using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HashCompare;

/// <summary>
/// One row in the comparison grid. <see cref="Status"/> is mutable so an action button
/// (e.g. "Copy to Dest") can update the row in place without re-running the whole comparison.
/// </summary>
public sealed class FileComparisonResult : INotifyPropertyChanged
{
    private string _status = string.Empty;

    /// <summary>Path relative to the folder roots, e.g. "sub\dir\file.txt".</summary>
    public required string RelativePath { get; init; }

    public required string SourceFullPath { get; init; }
    public required string DestFullPath { get; init; }

    /// <summary>Relative directory portion shown in the "Folder" column.</summary>
    public string Folder => Path.GetDirectoryName(RelativePath) ?? string.Empty;

    /// <summary>File name shown in the "File" column.</summary>
    public string File => Path.GetFileName(RelativePath);

    public required string Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCopyToDest));
            OnPropertyChanged(nameof(CanRemoveFromDest));
            OnPropertyChanged(nameof(CanReplaceDest));
            OnPropertyChanged(nameof(CanCompare));
        }
    }

    public bool CanCopyToDest => Status == "Missing";     // exists in source only
    public bool CanRemoveFromDest => Status == "New";     // exists in destination only
    public bool CanReplaceDest => Status == "Different";
    public bool CanCompare => Status == "Different";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
