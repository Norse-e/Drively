using System.Diagnostics;
using System.Windows;
using Drively.App.Views;
using Microsoft.Win32;

namespace Drively.App.Services;

public sealed record ConfirmRequest(
    string Title,
    string Message,
    string ConfirmText,
    IReadOnlyList<KeyValuePair<string, string>>? Details = null,
    string? Warning = null,
    bool IsDestructive = false);

/// <summary>Dialogs, pickers and browser launching. Kept behind a class so view models stay testable.</summary>
public sealed class DialogService(ThemeService theme)
{
    public bool Confirm(ConfirmRequest request)
    {
        var dialog = new ConfirmDialog(request) { Owner = Application.Current.MainWindow };
        theme.ApplyTitleBar(dialog);
        return dialog.ShowDialog() == true;
    }

    public void ShowInfo(string title, string message) =>
        Show(new ConfirmRequest(title, message, "OK"), showCancel: false);

    public void ShowError(string title, string message) =>
        Show(new ConfirmRequest(title, message, "OK", IsDestructive: true), showCancel: false);

    private void Show(ConfirmRequest request, bool showCancel)
    {
        var dialog = new ConfirmDialog(request, showCancel) { Owner = Application.Current.MainWindow };
        theme.ApplyTitleBar(dialog);
        dialog.ShowDialog();
    }

    public string? PickFolder(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public string? PickSaveFile(string title, string defaultFileName, string filter, string? initialDirectory)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = defaultFileName, Filter = filter, AddExtension = true };
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            Directory.CreateDirectory(initialDirectory);
            dialog.InitialDirectory = initialDirectory;
        }
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    /// <summary>Opens an https or ms-settings link in the user's default handler. Other schemes are refused.</summary>
    public void OpenUri(string uri)
    {
        if (!uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !uri.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) return;
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    public void OpenFolder(string path)
    {
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
