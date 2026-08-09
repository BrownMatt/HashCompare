using Avalonia.Controls;
using System.Threading.Tasks;

namespace HashCompare.App.Views;

/// <summary>Custom replacement for MessageBox.</summary>
public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TxtMessage.Text = message;
    }

    public async Task<bool> ShowDialog()
    {
        return await ShowDialog<bool>();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}