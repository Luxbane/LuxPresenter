using System.Threading.Tasks;
using System;



using System.Windows;



using System.Windows.Controls;



using System.Windows.Input;



using System.Windows.Media;







namespace LuxPresenter.Views.Components;







public partial class OnlineBrowserView : UserControl



{



    public event Action? CloseRequested;



    private void OnCloseOnlineClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();







    private bool _isInitialized = false;







    public OnlineBrowserView()



    {



        InitializeComponent();



        Loaded += OnLoaded;



    }







    private async void OnLoaded(object sender, RoutedEventArgs e)



    {



        if (!_isInitialized)



        {



            _isInitialized = true;



            try



            {



                string userDataFolder = System.IO.Path.Combine(



                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),



                    "LuxPresenter", "WebView2Data");



                System.IO.Directory.CreateDirectory(userDataFolder);







                // Priority search paths for WebView2 fixed runtime (Fixed Version / Portable) or System install



                string? browserExecutableFolder = null;



                string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;







                string[] searchPaths = new[]



                {



                    System.IO.Path.Combine(baseAppDir, "WebView2"),



                    System.IO.Path.Combine(baseAppDir, "runtimes", "WebView2"),



                    @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application\151.0.4129.107",



                    @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application"



                };







                foreach (var path in searchPaths)



                {



                    try



                    {



                        if (System.IO.Directory.Exists(path))



                        {



                            if (System.IO.File.Exists(System.IO.Path.Combine(path, "msedgewebview2.exe")))



                            {



                                browserExecutableFolder = path;



                                break;



                            }







                            var subDirs = System.IO.Directory.GetDirectories(path);



                            foreach (var sub in subDirs)



                            {



                                if (System.IO.File.Exists(System.IO.Path.Combine(sub, "msedgewebview2.exe")))



                                {



                                    browserExecutableFolder = sub;



                                    break;



                                }



                            }



                            if (browserExecutableFolder != null) break;



                        }



                    }



                    catch { }



                }







                var envOptions = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions

                {

                    AreBrowserExtensionsEnabled = true

                };



                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder, envOptions);

                await Browser.EnsureCoreWebView2Async(env);



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

                        string extensionPath = System.IO.Path.Combine(baseAppDir, "Extensions", "uBlockOrigin");

                        if (!System.IO.Directory.Exists(extensionPath))

                        {

                            string devPath = System.IO.Path.Combine(baseAppDir, "..", "..", "..", "Extensions", "uBlockOrigin");

                            if (System.IO.Directory.Exists(devPath)) extensionPath = System.IO.Path.GetFullPath(devPath);

                        }



                        if (System.IO.Directory.Exists(extensionPath) && System.IO.File.Exists(System.IO.Path.Combine(extensionPath, "manifest.json")))

                        {

                            await Browser.CoreWebView2.Profile.AddBrowserExtensionAsync(extensionPath);

                        }

                    }

                }

                catch (Exception ex)

                {

                    System.Diagnostics.Debug.WriteLine($"Failed to load uBlock Origin extension: {ex.Message}");

                }







                Browser.NavigationStarting += (s, args) =>



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



                };







                Browser.NavigationCompleted += (s, args) =>



                {



                    LoadingBar.Visibility = Visibility.Collapsed;



                    if (Browser.Source != null)



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



                };







                NavigateToHome();



            }



            catch (Exception ex)



            {



                MessageBox.Show($"Could not initialize Chromium WebView2 browser: {ex.Message}", "WebView2 Initialization", MessageBoxButton.OK, MessageBoxImage.Warning);



            }



        }



    }







    public void NavigateToHome()



    {



        UrlTextBox.Text = string.Empty;



        try



        {



            if (Browser.CoreWebView2 != null)



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



            if (Browser.CoreWebView2 != null)



            {



                Browser.CoreWebView2.Navigate(url);



            }



            else



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



    private void OnBtnBackClick(object sender, RoutedEventArgs e) { if (Browser.CanGoBack) Browser.GoBack(); }



    private void OnBtnForwardClick(object sender, RoutedEventArgs e) { if (Browser.CanGoForward) Browser.GoForward(); }



    private void OnBtnRefreshClick(object sender, RoutedEventArgs e)



    {



        if (string.IsNullOrWhiteSpace(UrlTextBox.Text))



        {



            NavigateToHome();



        }



        else



        {



            Browser.Reload();



        }



    }



    private void OnBtnStopClick(object sender, RoutedEventArgs e) { Browser.Stop(); }







    private void OnQuickYouTubeClick(object sender, RoutedEventArgs e) => NavigateTo("https://www.youtube.com");







    
    private bool _isCleaningUp = false;

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
            if (Browser != null && Browser.CoreWebView2 != null)
            {
                // 1. Halt any active navigation and detach current page context
                try
                {
                    Browser.CoreWebView2.Stop();
                    Browser.CoreWebView2.Navigate("about:blank");
                }
                catch { }

                // 2. Clear all website cookies across all domains
                try
                {
                    Browser.CoreWebView2.CookieManager.DeleteAllCookies();
                }
                catch { }

                // 3. Clear website browsing data categories safely without touching extension storage
                try
                {
                    var kinds = Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.Cookies |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DiskCache |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DownloadHistory |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.BrowsingHistory |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.LocalStorage |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.IndexedDb |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.ServiceWorkers |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.CacheStorage |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.WebSql |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.FileSystems |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.GeneralAutofill |
                                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.PasswordAutosave;

                    var clearTask = Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(kinds);
                    var timeoutTask = Task.Delay(3000);
                    await Task.WhenAny(clearTask, timeoutTask);
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



