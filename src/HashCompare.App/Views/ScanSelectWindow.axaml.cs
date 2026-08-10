using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HashCompare.App.Views;

/// <summary>A checklist item shown in <see cref="ScanSelectWindow"/>.</summary>
public sealed class CheckItem
{
    public required string Name { get; init; }
    public bool IsChecked { get; set; }
}

/// <summary>
/// Generic checkbox picker used by the Config dialog to choose folders or file extensions to
/// exclude. Returns the checked names when closed via "Exclude selected".
/// </summary>
public partial class ScanSelectWindow : Window
{
    private readonly List<CheckItem> _items;

    // Parameterless ctor required by the XAML loader; not used at runtime.
    public ScanSelectWindow() : this("", []) { }

    public ScanSelectWindow(string prompt, List<CheckItem> items)
    {
        InitializeComponent();
        _items = items;
        TxtPrompt.Text = prompt;
        ItemsList.ItemsSource = _items;
    }

    /// <summary>Names of the checked items; populated when the dialog is accepted.</summary>
    public IReadOnlyList<string> SelectedItems { get; private set; } = [];

    private void ExcludeSelected_Click(object? sender, RoutedEventArgs e)
    {
        SelectedItems = _items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}