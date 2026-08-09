using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HashCompare.Core;

/// <summary>
/// One row in the comparison grid. <see cref="Status"/> is mutable so an action button
/// (e.g. "Copy to Dest") can update the row in place without re-running the whole comparison.
/// </summary>
public sealed class FileComparisonResult : INotifyPropertyChanged
{
    private FileStatus _status;

    /// <summary>Path relative to the folder roots, e.g. "sub\dir\file.txt".</summary>
    public required string RelativePath { get; init; }

    public required string SourceFullPath { get; init; }
    public required string DestFullPath { get; init; }

    /// <summary>Relative directory portion shown in the "Folder" column.</summary>
    public string Folder => Path.GetDirectoryName(RelativePath) ?? string.Empty;

    /// <summary>File name shown in the "File" column.</summary>
    public string File => Path.GetFileName(RelativePath);

    public required FileStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanCopyToDest));
            OnPropertyChanged(nameof(CanRemoveFromDest));
            OnPropertyChanged(nameof(CanReplaceDest));
            OnPropertyChanged(nameof(CanCompare));
        }
    }

    /// <summary>Text shown in the "Status" column.</summary>
    public string StatusText => Status.ToDisplayString();

    public bool CanCopyToDest => Status == FileStatus.Missing;
    public bool CanRemoveFromDest => Status == FileStatus.New;
    public bool CanReplaceDest => Status == FileStatus.Different;
    public bool CanCompare => Status == FileStatus.Different;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}