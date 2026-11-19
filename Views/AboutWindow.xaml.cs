using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace LuxPresenter.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        TxtVersion.Text = $"Version {Services.UpdateService.CurrentVersion}";
    }

    private void OnHyperlinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }

    private void OnKofiClick(object sender, RoutedEventArgs e)
    {
        Services.UpdateService.OpenKofi();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
