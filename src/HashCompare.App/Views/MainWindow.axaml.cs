using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HashCompare.App.ViewModels;
using HashCompare.Core;

namespace HashCompare.App.Views;

public partial class MainWindow : Window, IDialogs
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    // ----- Phase 3 stubs: replaced with MessageDialog / ConfigWindow in Phase 4 -----

    public Task ShowInfoAsync(string title, string message) =>
        MessageDialog.Info(this, title, message);

    public Task ShowErrorAsync(string message) =>
        MessageDialog.Error(this, message);

    public Task<bool> ConfirmAsync(string title, string message) =>
        MessageDialog.Confirm(this, title, message);

    public async Task<bool> ShowConfigAsync(AppConfig config, string source, string destination)
    {
        var dialog = new ConfigWindow(source, destination, config);
        return await dialog.ShowDialog<bool?>(this) == true;
    }
}