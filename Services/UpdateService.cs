using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using LuxPresenter.Views;

namespace LuxPresenter.Services;

public class GitHubReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
}

public static class UpdateService
{
    public const string CurrentVersion = "1.2.1";
    public const string GitHubRepo = "luxbane/LuxPresenter";
    public const string KofiUrl = "https://ko-fi.com/luxbane";

    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task CheckForUpdatesAsync(Window? owner = null, bool silent = false, bool forceShowDialog = false)
    {
        try
        {
            var release = await FetchLatestReleaseAsync();
            if (release == null)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        owner ?? Application.Current.MainWindow,
                        $"You are using the latest version of LuxPresenter (v{CurrentVersion}).",
                        "Check for Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            if (forceShowDialog || IsNewerVersion(CurrentVersion, release.TagName))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dlg = new UpdateDialog(release, CurrentVersion)
                    {
                        Owner = owner ?? Application.Current.MainWindow
                    };
                    dlg.ShowDialog();
                });
            }
            else
            {
                if (!silent)
                {
                    MessageBox.Show(
                        owner ?? Application.Current.MainWindow,
                        $"You are using the latest version of LuxPresenter (v{CurrentVersion}).",
                        "Check for Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }
        catch
        {
            if (!silent)
            {
                MessageBox.Show(
                    owner ?? Application.Current.MainWindow,
                    "Unable to check for updates.\nPlease check your internet connection and try again later.",
                    "Check for Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    public static async Task<GitHubReleaseInfo?> FetchLatestReleaseAsync()
    {
        string url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", $"LuxPresenter-{CurrentVersion}");
        request.Headers.Add("Accept", "application/vnd.github.v3+json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
        string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
        string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
        string htmlUrl = root.TryGetProperty("html_url", out var htmlElem) ? htmlElem.GetString() ?? "" : "";

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsElem.EnumerateArray())
            {
                string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var bUrl) ? bUrl.GetString() : null;
                    break;
                }
            }
        }

        return new GitHubReleaseInfo
        {
            TagName = tagName,
            Name = string.IsNullOrWhiteSpace(name) ? tagName : name,
            Body = body,
            HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? $"https://github.com/{GitHubRepo}/releases" : htmlUrl,
            DownloadUrl = downloadUrl ?? htmlUrl
        };
    }

    public static bool IsNewerVersion(string currentVerStr, string remoteVerStr)
    {
        var curVer = NormalizeVersion(currentVerStr);
        var remVer = NormalizeVersion(remoteVerStr);
        if (curVer == null || remVer == null) return false;
        return remVer > curVer;
    }

    private static Version? NormalizeVersion(string verStr)
    {
        if (string.IsNullOrWhiteSpace(verStr)) return null;
        var match = Regex.Match(verStr, @"\d+(\.\d+)+");
        if (match.Success && Version.TryParse(match.Value, out var v))
        {
            return v;
        }
        return null;
    }

    public static void OpenKofi()
    {
        try
        {
            Process.Start(new ProcessStartInfo(KofiUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
