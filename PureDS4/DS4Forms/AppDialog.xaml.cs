using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms;

public partial class AppDialog : Window
{
    private MessageBoxResult result = MessageBoxResult.None;

    private AppDialog(string message, string caption, MessageBoxButton buttons,
        MessageBoxImage image, MessageBoxResult defaultResult)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(caption) ? "DS4Windows Reworked" : caption;
        headingText.Text = Title;
        messageText.Text = message ?? string.Empty;
        ConfigureState(image);
        ConfigureButtons(buttons, defaultResult);
    }

    public static MessageBoxResult Show(string message) =>
        Show(null, message, "DS4Windows Reworked", MessageBoxButton.OK,
            MessageBoxImage.None, MessageBoxResult.OK);

    public static MessageBoxResult Show(string message, string caption) =>
        Show(null, message, caption, MessageBoxButton.OK,
            MessageBoxImage.None, MessageBoxResult.OK);

    public static MessageBoxResult Show(string message, string caption,
        MessageBoxButton buttons) =>
        Show(null, message, caption, buttons, MessageBoxImage.None,
            DefaultFor(buttons));

    public static MessageBoxResult Show(string message, string caption,
        MessageBoxButton buttons, MessageBoxImage image) =>
        Show(null, message, caption, buttons, image, DefaultFor(buttons));

    public static MessageBoxResult Show(string message, string caption,
        MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult) =>
        Show(null, message, caption, buttons, image, defaultResult);

    public static MessageBoxResult Show(Window owner, string message,
        string caption, MessageBoxButton buttons, MessageBoxImage image) =>
        Show(owner, message, caption, buttons, image, DefaultFor(buttons));

    public static MessageBoxResult Show(Window owner, string message,
        string caption, MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult)
    {
        var dialog = new AppDialog(message, caption, buttons, image,
            defaultResult);
        if (owner?.IsVisible == true) dialog.Owner = owner;
        else if (Application.Current?.MainWindow?.IsVisible == true)
            dialog.Owner = Application.Current.MainWindow;
        dialog.ShowDialog();
        return dialog.result == MessageBoxResult.None
            ? DefaultForClose(buttons)
            : dialog.result;
    }

    private void ConfigureState(MessageBoxImage image)
    {
        (string glyph, string brush) = image switch
        {
            MessageBoxImage.Error => ("✕", "StateErrorBrush"),
            MessageBoxImage.Warning => ("⚠", "StateWarningBrush"),
            MessageBoxImage.Question => ("?", "StateRecoveryBrush"),
            MessageBoxImage.Information => ("i", "StateRecoveryBrush"),
            _ => ("i", "StateNeutralBrush"),
        };
        stateGlyph.Text = glyph;
        stateGlyph.Foreground = (Brush)FindResource(brush);
    }

    private void ConfigureButtons(MessageBoxButton buttons,
        MessageBoxResult defaultResult)
    {
        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                AddButton("Cancel", MessageBoxResult.Cancel, defaultResult);
                AddButton("OK", MessageBoxResult.OK, defaultResult, true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("No", MessageBoxResult.No, defaultResult);
                AddButton("Yes", MessageBoxResult.Yes, defaultResult, true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("Cancel", MessageBoxResult.Cancel, defaultResult);
                AddButton("No", MessageBoxResult.No, defaultResult);
                AddButton("Yes", MessageBoxResult.Yes, defaultResult, true);
                break;
            default:
                AddButton("OK", MessageBoxResult.OK, defaultResult, true);
                break;
        }
    }

    private void AddButton(string label, MessageBoxResult value,
        MessageBoxResult defaultResult, bool primary = false)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 86,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = value == defaultResult,
            IsCancel = value == MessageBoxResult.Cancel ||
                (value == MessageBoxResult.No && defaultResult == MessageBoxResult.No),
            Style = (Style)FindResource(primary
                ? "FoundationPrimaryButtonStyle"
                : "FoundationButtonStyle"),
        };
        button.Click += (_, _) =>
        {
            result = value;
            DialogResult = true;
        };
        buttonPanel.Children.Add(button);
    }

    private static MessageBoxResult DefaultFor(MessageBoxButton buttons) =>
        buttons switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.OK,
        };

    private static MessageBoxResult DefaultForClose(MessageBoxButton buttons) =>
        buttons switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.OK,
        };
}
