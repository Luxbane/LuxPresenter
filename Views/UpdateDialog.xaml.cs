using System;
using System.Diagnostics;
using System.Windows;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class UpdateDialog : Window
{
    private readonly GitHubReleaseInfo _release;

    public UpdateDialog(GitHubReleaseInfo release, string currentVersion)
    {
        InitializeComponent();
        _release = release;

        TxtVersionSub.Text = $"Current: v{currentVersion}  →  New: {release.TagName}";
        TxtChangelog.Text = string.IsNullOrWhiteSpace(release.Body) 
            ? $"A new version ({release.Name}) is available for download." 
            : release.Body;
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = !string.IsNullOrWhiteSpace(_release.DownloadUrl) ? _release.DownloadUrl : _release.HtmlUrl;
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open download link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
