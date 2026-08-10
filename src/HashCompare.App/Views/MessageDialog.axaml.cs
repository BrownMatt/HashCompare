using Avalonia.Controls;
using Avalonia.Input;

namespace HashCompare.App.Views;

/// <summary>
/// One modal used for every prompt in the app: plain messages and yes/no confirmations.
/// Buttons are supplied by the caller, and the result is whichever button's value was
/// chosen (default when closed by the title bar).
/// </summary>
public partial class MessageDialog : Window
{
    private object? _result;

    public MessageDialog()
    {
        InitializeComponent();
    }

    public static async Task<T> ShowAsync<T>(
        Window owner,
        string title,
        string message,
        IReadOnlyList<(string Label, T Value, bool IsDefault)> buttons,
        T closedResult)
    {
        var dialog = new MessageDialog
        {
            Title = title,
            _result = closedResult,
        };

        dialog.MessageText.Text = message;

        foreach (var (label, value, isDefault) in buttons)
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 88,
                Padding = new Avalonia.Thickness(12, 6),
                IsDefault = isDefault,
            };

            button.Click += (_, _) =>
            {
                dialog._result = value;
                dialog.Close();
            };

            dialog.Buttons.Children.Add(button);
        }

        await dialog.ShowDialog(owner);
        return (T)dialog._result!;
    }

    // ----- Convenience helpers matching the WPF MessageBox call sites -----

    public static Task Info(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, [("OK", true, true)], true);

    public static Task Error(Window owner, string message) =>
        ShowAsync(owner, "Error", $"Error: {message}", [("OK", true, true)], true);

    /// <summary>Yes/No confirmation; closing the dialog means No.</summary>
    public static Task<bool> Confirm(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, [("Yes", true, false), ("No", false, true)], false);

    /// <summary>Escape dismisses, answering exactly as closing from the title bar does.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape) return;

        e.Handled = true;
        Close();
    }
}