using System.Windows;
using Drively.App.Services;

namespace Drively.App.Views;

/// <summary>Themed confirmation / message dialog. Confirmation is never the default for destructive actions.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(ConfirmRequest request, bool showCancel = true)
    {
        InitializeComponent();
        Title = request.Title;
        TitleText.Text = request.Title;
        MessageText.Text = request.Message;
        ConfirmButton.Content = request.ConfirmText;

        if (request.Details is { Count: > 0 }) DetailsList.ItemsSource = request.Details;
        else DetailsPanel.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(request.Warning)) WarningPanel.Visibility = Visibility.Collapsed;
        else WarningText.Text = request.Warning;

        if (!showCancel)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            ConfirmButton.IsCancel = true;
        }
        else if (request.IsDestructive)
        {
            // Pressing Enter must not trigger a destructive action.
            ConfirmButton.IsDefault = false;
            ConfirmButton.Style = (Style)FindResource("DangerButton");
            CancelButton.IsDefault = true;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
