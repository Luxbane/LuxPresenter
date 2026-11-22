using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace LuxPresenter.Views.Components;

public partial class OnlineBrowserView : UserControl
{
    public event Action? CloseRequested;

    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isCleaningUp = false;

    public OnlineBrowserView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnCloseOnlineClick(object sender, RoutedEventArgs e)
    {
        CloseOnline();
        CloseRequested?.Invoke();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            if (!_isInitialized)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, async () =>
                {
                    if (Visibility == Visibility.Visible && !_isInitialized)
                    {
                        await InitializeBrowserAsync();
                    }
                });
            }
            else
            {
                NavigateToHome();
            }
        }
        else
        {
            CloseOnline();
        }
    }

    public void CloseOnline()
    {
        try
        {
            if (_isInitialized && Browser != null)
            {
                try
                {
                    if (Browser.CoreWebView2 != null)
                    {
                        Browser.CoreWebView2.Stop();
                        Browser.NavigateToString("about:blank");
                    }
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            UrlTextBox.Text = string.Empty;
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private void RecreateBrowserControl()
    {
        try
        {
            if (Browser != null)
            {
                BrowserHostContainer.Children.Remove(Browser);
                Browser.Dispose();
            }
        }
        catch { }

        Browser = new Microsoft.Web.WebView2.Wpf.WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        BrowserHostContainer.Children.Insert(0, Browser);
    }

    /// <summary>
    /// Locates the bundled WebView2 Fixed Version runtime folder.
    /// Strictly ignores Program Files and only resolves local bundled runtime paths.
    /// </summary>
    private static string? GetBundledWebView2Folder()
    {
        string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] searchPaths = new[]
        {
            Path.Combine(baseAppDir, "WebView2"),
            Path.Combine(baseAppDir, "runtimes", "WebView2"),
            Path.Combine(baseAppDir, "..", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "Portable", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "Build", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "..", "Portable", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "..", "Build", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "..", "..", "Portable", "WebView2"),
            Path.Combine(baseAppDir, "..", "..", "..", "..", "..", "Build", "WebView2"),
            @"E:\FreeWorship\Portable\WebView2",
            @"E:\FreeWorship\Build\WebView2"
        };

        foreach (var path in searchPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    if (File.Exists(Path.Combine(path, "msedgewebview2.exe")))
                    {
                        return Path.GetFullPath(path);
                    }

                    foreach (var sub in Directory.GetDirectories(path))
                    {
                        if (File.Exists(Path.Combine(sub, "msedgewebview2.exe")))
                        {
                            return Path.GetFullPath(sub);
                        }
                    }
                }
            }
            catch { }
        }

        return null;
    }

    private async Task InitializeBrowserAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            LoadingBar.Visibility = Visibility.Visible;
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LuxPresenter", "WebView2Data");
            Directory.CreateDirectory(userDataFolder);

            // Clean up any stale shutdown crash flags from previous abrupt terminations
            try
            {
                string crashFile = Path.Combine(userDataFolder, "EBWebView", "Crashpad", "temp", "edge_shutdown_crash.txt");
                if (File.Exists(crashFile)) File.Delete(crashFile);
            }
            catch { }

            string? browserExecutableFolder = GetBundledWebView2Folder();
            string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;

            var envOptions = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true
            };

            var env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder, envOptions);

            try
            {
                await Browser.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex) when (ex.Message.Contains("crashed", StringComparison.OrdinalIgnoreCase) || ex is InvalidOperationException)
            {
                // Control entered crashed state from an earlier session; replace with fresh instance
                RecreateBrowserControl();
                await Browser.EnsureCoreWebView2Async(env);
            }

            Browser.NavigationStarting -= OnNavigationStarting;
            Browser.NavigationStarting += OnNavigationStarting;
            Browser.NavigationCompleted -= OnNavigationCompleted;
            Browser.NavigationCompleted += OnNavigationCompleted;

            if (Browser.CoreWebView2 != null)
            {
                Browser.CoreWebView2.ProcessFailed -= OnBrowserProcessFailed;
                Browser.CoreWebView2.ProcessFailed += OnBrowserProcessFailed;

                // Load uBlock Origin Adblocker Extension
                try
                {
                    bool isInstalled = false;
                    var existingExts = await Browser.CoreWebView2.Profile.GetBrowserExtensionsAsync();
                    foreach (var ext in existingExts)
                    {
                        if (ext.Name != null && ext.Name.Contains("uBlock", StringComparison.OrdinalIgnoreCase))
                        {
                            isInstalled = true;
                            if (!ext.IsEnabled)
                            {
                                await ext.EnableAsync(true);
                            }
                            break;
                        }
                    }

                    if (!isInstalled)
                    {
                        string extensionPath = Path.Combine(baseAppDir, "Extensions", "uBlockOrigin");
                        if (!Directory.Exists(extensionPath))
                        {
                            string devPath = Path.Combine(baseAppDir, "..", "..", "..", "Extensions", "uBlockOrigin");
                            if (Directory.Exists(devPath)) extensionPath = Path.GetFullPath(devPath);
                        }

                        if (Directory.Exists(extensionPath) && File.Exists(Path.Combine(extensionPath, "manifest.json")))
                        {
                            await Browser.CoreWebView2.Profile.AddBrowserExtensionAsync(extensionPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load uBlock Origin extension: {ex.Message}");
                }
            }

            _isInitialized = true;
            NavigateToHome();
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            MessageBox.Show($"Could not initialize Chromium WebView2 browser: {ex.Message}", "WebView2 Initialization", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            _initLock.Release();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        LoadingBar.Visibility = Visibility.Visible;
        if (args.Uri != null && (args.Uri == "about:blank" || args.Uri.StartsWith("data:text/html")))
        {
            UrlTextBox.Text = string.Empty;
        }
        else
        {
            UrlTextBox.Text = args.Uri;
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        LoadingBar.Visibility = Visibility.Collapsed;
        try
        {
            if (Browser?.Source != null)
            {
                string src = Browser.Source.ToString();
                if (src == "about:blank" || src.StartsWith("data:text/html"))
                {
                    UrlTextBox.Text = string.Empty;
                }
                else
                {
                    UrlTextBox.Text = src;
                }
            }
        }
        catch { }
    }

    private void OnBrowserProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"WebView2 ProcessFailed: {args.ProcessFailedKind}, reason: {args.Reason}");
        _isInitialized = false;
        Dispatcher.BeginInvoke(() =>
        {
            RecreateBrowserControl();
        });
    }

    public void NavigateToHome()
    {
        UrlTextBox.Text = string.Empty;
        try
        {
            if (Browser?.CoreWebView2 != null)
            {
                Browser.NavigateToString(GetInPrivateHomeHtml());
            }
        }
        catch { }
    }

    public void NavigateTo(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || 
            url.Equals("about:blank", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            NavigateToHome();
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (url.Contains(".") && !url.Contains(" "))
            {
                url = "https://" + url;
            }
            else
            {
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
            }
        }

        UrlTextBox.Text = url;
        try
        {
            if (Browser?.CoreWebView2 != null)
            {
                Browser.CoreWebView2.Navigate(url);
            }
            else if (Browser != null)
            {
                Browser.Source = new Uri(url);
            }
        }
        catch { }
    }

    private void OnUrlTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(UrlTextBox.Text);
            e.Handled = true;
        }
    }

    private void OnBtnGoClick(object sender, RoutedEventArgs e) => NavigateTo(UrlTextBox.Text);

    private void OnBtnHomeClick(object sender, RoutedEventArgs e) => NavigateToHome();

    private void OnBtnBackClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Browser != null && Browser.CanGoBack) Browser.GoBack();
        }
        catch { }
    }

    private void OnBtnForwardClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Browser != null && Browser.CanGoForward) Browser.GoForward();
        }
        catch { }
    }

    private void OnBtnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                NavigateToHome();
            }
            else
            {
                Browser?.Reload();
            }
        }
        catch { }
    }

    private void OnBtnStopClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Browser?.Stop();
        }
        catch { }
    }

    private void OnQuickYouTubeClick(object sender, RoutedEventArgs e) => NavigateTo("https://www.youtube.com");

    /// <summary>
    /// Clears website browsing data (cookies, cache, history, DOM storage, indexedDB, service workers)
    /// while strictly preserving uBlock Origin extension and its settings/filter rules.
    /// Safely halts active navigations and disposes WebView2 resources.
    /// </summary>
    public async Task CleanupOnShutdownAsync()
    {
        if (_isCleaningUp) return;
        _isCleaningUp = true;

        try
        {
            if (Browser != null)
            {
                try
                {
                    if (Browser.CoreWebView2 != null)
                    {
                        try { Browser.CoreWebView2.Stop(); } catch { }
                        try { Browser.CoreWebView2.Navigate("about:blank"); } catch { }
                        try { Browser.CoreWebView2.CookieManager.DeleteAllCookies(); } catch { }
                        try
                        {
                            var kinds = CoreWebView2BrowsingDataKinds.Cookies |
                                        CoreWebView2BrowsingDataKinds.DiskCache |
                                        CoreWebView2BrowsingDataKinds.DownloadHistory |
                                        CoreWebView2BrowsingDataKinds.BrowsingHistory |
                                        CoreWebView2BrowsingDataKinds.LocalStorage |
                                        CoreWebView2BrowsingDataKinds.IndexedDb |
                                        CoreWebView2BrowsingDataKinds.ServiceWorkers |
                                        CoreWebView2BrowsingDataKinds.CacheStorage |
                                        CoreWebView2BrowsingDataKinds.WebSql |
                                        CoreWebView2BrowsingDataKinds.FileSystems |
                                        CoreWebView2BrowsingDataKinds.GeneralAutofill |
                                        CoreWebView2BrowsingDataKinds.PasswordAutosave;

                            var clearTask = Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(kinds);
                            var timeoutTask = Task.Delay(2000);
                            await Task.WhenAny(clearTask, timeoutTask);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            try
            {
                Browser?.Dispose();
            }
            catch { }
        }
    }

    private static string GetInPrivateHomeHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Online Search</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background-color: #202124;
      color: #9AA0A6;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      font-size: 14px;
      line-height: 1.5;
      min-height: 100vh;
      display: flex;
      justify-content: center;
      align-items: center;
      padding: 40px 24px;
      user-select: none;
    }
    .container {
      max-width: 580px;
      width: 100%;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .icon-container {
      width: 56px;
      height: 56px;
      border-radius: 50%;
      background-color: #3C4043;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 24px;
    }
    .icon-container svg {
      width: 32px;
      height: 32px;
      fill: #E8EAED;
    }
    h1 {
      color: #E8EAED;
      font-size: 24px;
      font-weight: 500;
      margin-bottom: 12px;
      letter-spacing: 0.2px;
    }
    .subtitle {
      color: #9AA0A6;
      font-size: 14px;
      line-height: 1.5;
    }
  </style>
</head>
<body>
  <div class="container">
    <div class="icon-container">
      <svg viewBox="0 0 24 24">
        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
      </svg>
    </div>
    <h1>Online Lyrics Search</h1>
    <p class="subtitle">Search with Google or enter web address on search bar</p>
    <div style="margin-top: 14px; display: inline-block; padding: 4px 12px; background: #1E3A8A; color: #93C5FD; border-radius: 12px; font-size: 11px; font-weight: 600;">🛡️ AdBlock Active (uBlock Origin)</div>
  </div>
</body>
</html>
""";
    }
}
