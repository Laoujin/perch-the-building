using System.Windows;

using Wpf.Ui.Controls;

namespace Perch.Desktop.Views;

public partial class CrashWindow : FluentWindow
{
    public CrashWindow(Exception exception)
    {
        InitializeComponent();

        ErrorMessageText.Text = exception.Message;
        ErrorDetailsBox.Text = exception.ToString();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ErrorDetailsBox.Text);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
