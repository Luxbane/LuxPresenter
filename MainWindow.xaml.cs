using System.Windows;
using System.Windows.Media;

using System.Windows.Controls;

using System.Windows.Input;

using LuxPresenter.Models;

using LuxPresenter.Services;

using LuxPresenter.ViewModels;

using LuxPresenter.Views;



namespace LuxPresenter;



public partial class MainWindow : Window

{

    private readonly MainViewModel _viewModel;

    private readonly IDataService _dataService;

    private readonly IProjectionService _projectionService;

    private readonly StartupCleanupService _cleanupService;

    private readonly KeyboardShortcutService _keyboardShortcuts;

    private readonly DialogService _dialogService;



    public MainWindow(Action<double, string>? startupProgress = null)

    {

        InitializeComponent();
        startupProgress?.Invoke(58, "Loading application interface...");



        _dataService = new DataService();
        _cleanupService = new StartupCleanupService(_dataService);
        _dialogService = new DialogService();
        startupProgress?.Invoke(68, "Loading songs, media, presentations, and Bible data...");

        _projectionService = new ProjectionService();

        _viewModel = new MainViewModel(_dataService, _projectionService);
        startupProgress?.Invoke(82, "Preparing preview and live output...");



        DataContext = _viewModel;

        var initialSettings = _dataService.GetSettings();
        ApplyInitialLayoutAndSettings(initialSettings);

        // Clean up orphaned presentation caches, media, and thumbnails on startup
        _cleanupService.CleanupOrphanedData();
        startupProgress?.Invoke(90, "Cleaning temporary presentation data...");

        // Pre-warm hardware acceleration, DirectX pipeline, and font rasterization
        _projectionService.Prewarm();
        startupProgress?.Invoke(96, "Pre-warming display shaders & hardware acceleration...");

        // Wire functional event groups
        WirePaneEvents();
        WireViewModelDialogEvents();
        WireLibraryEvents();
        _keyboardShortcuts = CreateKeyboardShortcutService();
        WireLifecycleEvents();
    }

    private void ApplyInitialLayoutAndSettings(AppSettings settings)
    {
        if (settings.LeftPaneWidth >= 260 && settings.LeftPaneWidth <= 650)
        {
            LeftPaneColumn.Width = new GridLength(settings.LeftPaneWidth);
        }
        else
        {
            LeftPaneColumn.Width = new GridLength(380);
        }

        PreviewPaneView.ApplyFormatting(settings);
        LivePaneView.ApplyFormatting(settings);
        _projectionService.UpdateSettings(settings);
        UpdateThemeUI(settings.IsDarkMode);
        UpdateStageMenuState();
    }

    private void WirePaneEvents()
    {
        PreviewPaneView.SlideActivated += (s, vm) =>
        {
            _viewModel.PreviewVM.IsActivePane = true;
            _viewModel.LiveVM.IsActivePane = false;
        };

        PreviewPaneView.SlidePrepared += (s, vm) =>
        {
            _viewModel.PreviewVM.IsActivePane = false;
            _viewModel.LiveVM.IsActivePane = true;
        };

        LivePaneView.SlideActivated += (s, vm) =>
        {
            _viewModel.LiveVM.IsActivePane = true;
            _viewModel.PreviewVM.IsActivePane = false;
        };
    }

    private void WireViewModelDialogEvents()
    {
        _viewModel.OpenSongEditorRequested += OnOpenSongEditorRequested;
        _viewModel.OpenOptionsRequested += OnOpenOptionsRequested;
    }

    private void WireLibraryEvents()
    {
        MainOnlineBrowserView.CloseRequested += () =>
        {
            MainLibraryView.SelectTab(_viewModel.LastActiveLibraryTabIndex);
        };

        _viewModel.SongsVM.RequestImportSongBackup += () => OnImportSongBackupClick(this, new RoutedEventArgs());
        _viewModel.SongsVM.RequestOpenSongsDirectory += () => OnOpenSongsFolderClick(this, new RoutedEventArgs());
        _viewModel.SongsVM.RequestLoadSampleSongs += () => OnLoadSampleSongsClick(this, new RoutedEventArgs());
        _viewModel.ScripturesVM.RequestImportBible += () => OnImportBibleClick(this, new RoutedEventArgs());
    }

    private KeyboardShortcutService CreateKeyboardShortcutService()
    {
        var shortcuts = new KeyboardShortcutService(
            _viewModel,
            toggleTheme: () => OnToggleThemeClick(this, new RoutedEventArgs()),
            toggleStageDisplay: () => OnToggleStageDisplayClick(this, new RoutedEventArgs()),
            newSchedule: () => NewSchedule(),
            openSchedule: () => OpenSchedule(),
            saveSchedule: (saveAs) => SaveSchedule(saveAs)
        );
        shortcuts.Attach(this);
        return shortcuts;
    }

    private void WireLifecycleEvents()
    {
        // Auto-trim initial startup allocations (JIT, XAML parser caches, font tables) after 2.5 seconds of idle
        Loaded += (s, e) => MemoryHelper.TrimMemoryAsync(2500);
        Loaded += async (s, e) =>
        {
            await System.Threading.Tasks.Task.Delay(3500);
            await UpdateService.CheckForUpdatesAsync(this, silent: true);
        };
    }



    private void OnOpenSongEditorRequested(Song? song)
    {
        var resultSong = _dialogService.ShowSongEditor(this, song);
        if (resultSong != null)
        {
            _dataService.SaveSong(resultSong);
            _viewModel.SongsVM.ReloadSongs(resultSong.Id);

            var updatedPres = resultSong.ToPresentationItem(_viewModel.ActiveGlobalBackground);

            // Update Preview immediately if Preview was showing this song
            if (_viewModel.PreviewVM.ItemTitle.Equals(resultSong.Title, StringComparison.OrdinalIgnoreCase) ||
                (song != null && _viewModel.PreviewVM.ItemTitle.Equals(song.Title, StringComparison.OrdinalIgnoreCase)))
            {
                int curIdx = _viewModel.PreviewVM.SelectedIndex;
                _viewModel.PreviewVM.LoadItem(updatedPres, curIdx);
                _viewModel.PreviewVM.Background = _viewModel.ActiveGlobalBackground;
            }

            // Update Live immediately if Live was showing this song
            if (_viewModel.LiveVM.ItemTitle.Equals(resultSong.Title, StringComparison.OrdinalIgnoreCase) ||
                (song != null && _viewModel.LiveVM.ItemTitle.Equals(song.Title, StringComparison.OrdinalIgnoreCase)))
            {
                int curIdx = _viewModel.LiveVM.SelectedIndex;
                _viewModel.LiveVM.LoadItem(updatedPres, curIdx);
                _viewModel.LiveVM.Background = _viewModel.ActiveGlobalBackground;
                _projectionService.SetLiveSlide(_viewModel.LiveVM.CurrentSlide, _viewModel.LiveVM.Background, _viewModel.LiveVM.NextSlide, _viewModel.LiveVM.ItemTitle);
            }

            // Also update Schedule items matching this song if present
            foreach (var item in _viewModel.ScheduleVM.Items)
            {
                if (item.Title.Equals(resultSong.Title, StringComparison.OrdinalIgnoreCase) ||
                    (song != null && item.Title.Equals(song.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Title = resultSong.Title;
                    item.Presentation = updatedPres.Clone();
                }
            }
        }

        MemoryHelper.TrimMemoryAsync(300);
    }



    private MessageAlertWindow? _alertWindow;



    private void OnAlertBtnClick(object sender, RoutedEventArgs e)

    {

        if (_alertWindow == null || !_alertWindow.IsLoaded)

        {

            _alertWindow = new MessageAlertWindow(_dataService)

            {

                Owner = this

            };

            _alertWindow.Closed += (s, ev) => _alertWindow = null;

            _alertWindow.Show();

        }

        else

        {

            _alertWindow.Activate();

            _alertWindow.Focus();

        }

    }



    private void OnOpenOptionsRequested()
    {
        if (_dialogService.ShowOptions(this, _projectionService, _dataService))
        {
            var settings = _dataService.GetSettings();
            _viewModel.UpdateSettings(settings);
            _viewModel.SongsVM.ReloadSongs();
            PreviewPaneView.ApplyFormatting(settings);
            LivePaneView.ApplyFormatting(settings);
            _projectionService.UpdateSettings(settings);
            UpdateStageMenuState();
            _viewModel.ReloadCurrentPresentation();
        }

        MemoryHelper.TrimMemoryAsync(300);
    }

    private void OnBackgroundBtnClick(object sender, RoutedEventArgs e)
    {
        // Get preview text from current preview slide for the dialog monitor preview
        var previewText = _viewModel.PreviewVM.CurrentSlide?.Content
                       ?? _viewModel.PreviewVM.ItemTitle
                       ?? string.Empty;
        var currentBg = _viewModel.PreviewVM.Background;

        var (accepted, bg, applyToLive) = _dialogService.ShowBackgroundDialog(this, _dataService, previewText, currentBg);
        if (accepted && bg != null)
        {
            // Apply global background to Preview, persist, and update Live ONLY if explicitly requested
            _viewModel.SetActiveBackground(bg, applyToLive);
        }
    }



    private void OnGlobeButtonClick(object sender, RoutedEventArgs e)

    {

        if (_viewModel.IsOnlineMode)

        {

            MainLibraryView.SelectTab(_viewModel.LastActiveLibraryTabIndex);

        }

        else

        {

            MainLibraryView.SelectTab(4); // Online tab (index 4)

        }

    }



    private void OnNewScheduleClick(object sender, RoutedEventArgs e)

    {

        NewSchedule();

    }



    private void OnOpenScheduleClick(object sender, RoutedEventArgs e)

    {

        OpenSchedule();

    }



    private void OnSaveScheduleClick(object sender, RoutedEventArgs e)

    {

        SaveSchedule(false);

    }



    private void OnSaveScheduleAsClick(object sender, RoutedEventArgs e)

    {

        SaveSchedule(true);

    }



    private void OnClearScheduleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ScheduleVM.Items.Count == 0) return;

        var result = _dialogService.ShowConfirmation(
            this,
            "Are you sure you want to clear the schedule?",
            "Clear Schedule");

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ScheduleVM.ClearSchedule();
        }
    }



    public bool NewSchedule()

    {

        if (!PromptSaveIfModified())

        {

            return false;

        }



        _viewModel.ScheduleVM.ClearSchedule();

        return true;

    }



    public bool OpenSchedule()
    {
        if (!PromptSaveIfModified())
        {
            return false;
        }

        var fileName = _dialogService.ShowOpenScheduleDialog(this);
        if (fileName != null)
        {
            try
            {
                _viewModel.ScheduleVM.LoadFromFile(fileName);
                return true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to open schedule: {ex.Message}");
                return false;
            }
        }

        return false;
    }



    public bool SaveSchedule(bool saveAs = false)
    {
        if (saveAs || string.IsNullOrWhiteSpace(_viewModel.ScheduleVM.CurrentScheduleFilePath))
        {
            var fileName = _dialogService.ShowSaveScheduleDialog(this, _viewModel.ScheduleVM.CurrentScheduleFilePath);
            if (fileName != null)
            {
                try
                {
                    _viewModel.ScheduleVM.SaveToFile(fileName);
                    return true;
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError(this, $"Failed to save schedule: {ex.Message}");
                    return false;
                }
            }
            return false;
        }
        else
        {
            try
            {
                _viewModel.ScheduleVM.SaveToFile(_viewModel.ScheduleVM.CurrentScheduleFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to save schedule: {ex.Message}");
                return false;
            }
        }
    }

    private bool PromptSaveIfModified()
    {
        if (!_viewModel.ScheduleVM.HasUnsavedChanges || _viewModel.ScheduleVM.Items.Count == 0)
        {
            return true;
        }

        var result = _dialogService.ShowConfirmation(
            this,
            "Do you want to save changes to the current Schedule?",
            "LuxPresenter",
            MessageBoxButton.YesNoCancel);

        if (result == MessageBoxResult.Yes)
        {
            return SaveSchedule(false);
        }
        else if (result == MessageBoxResult.No)
        {
            return true;
        }
        else
        {
            return false;
        }
    }



    private bool _isShuttingDown = false;

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var settings = _dataService.GetSettings();
            double currentWidth = LeftPaneColumn.ActualWidth > 0 ? LeftPaneColumn.ActualWidth : LeftPaneColumn.Width.Value;
            if (currentWidth >= 260 && currentWidth <= 650)
            {
                settings.LeftPaneWidth = currentWidth;
                _dataService.SaveSettings(settings);
            }
        }
        catch { }

        // Clean up orphaned presentation caches, media, and thumbnails on shutdown
        _cleanupService.CleanupOrphanedData();

        if (!_isShuttingDown)
        {
            if (!PromptSaveIfModified())
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            _isShuttingDown = true;
            Hide();

            try
            {
                if (MainOnlineBrowserView != null)
                {
                    await MainOnlineBrowserView.CleanupOnShutdownAsync();
                }
            }
            catch { }

            Close();
            return;
        }

        base.OnClosing(e);
    }



    private void OnMenuExitClick(object sender, RoutedEventArgs e)

    {

        Close();

    }



    private void OnOpenSongsFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = AppPaths.SongsDirectory;
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(this, $"Failed to open Songs folder: {ex.Message}");
        }
    }

    private void OnChangeSongsFolderClick(object sender, RoutedEventArgs e)
    {
        var folderName = _dialogService.ShowSelectFolderDialog(this, "Select Songs Database Folder", AppPaths.SongsDirectory);
        if (folderName != null)
        {
            try
            {
                _dataService.SwitchSongsDirectory(folderName);
                _viewModel.SongsVM.ReloadSongs();
                _dialogService.ShowInformation(
                    this,
                    $"Songs database folder successfully changed to:\n{folderName}\n\nLoaded {_dataService.GetSongs().Count} songs.",
                    "Songs Database Location");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to switch database folder: {ex.Message}", "Error");
            }
        }
    }

    private void OnLoadSampleSongsClick(object sender, RoutedEventArgs e)
    {
        var res = _dialogService.ShowConfirmation(
            this,
            "Load default sample songs into your library?\n\nThis will add sample worship songs to your library.",
            "Load Sample Songs");

        if (res == MessageBoxResult.Yes)
        {
            _dataService.LoadSampleSongs();
            _viewModel.SongsVM.ReloadSongs();
            _dialogService.ShowInformation(this, "Sample songs loaded successfully!");
        }
    }



    private void OnExportSongBackupClick(object sender, RoutedEventArgs e)
    {
        var songs = _dataService.GetSongs();
        var fileName = _dialogService.ShowExportSongBackupDialog(this);
        if (fileName != null)
        {
            try
            {
                SongImportExportService.ExportSongsToJson(fileName, songs);
                _dialogService.ShowInformation(
                    this,
                    $"Successfully exported {songs.Count} songs to:\n{fileName}",
                    "Export Songs Backup");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to export songs:\n{ex.Message}", "Export Error");
            }
        }
    }

    private void OnImportSongBackupClick(object sender, RoutedEventArgs e)
    {
        var fileName = _dialogService.ShowImportSongBackupDialog(this);
        if (fileName != null)
        {
            try
            {
                var imported = SongImportExportService.ImportSongsFromJson(fileName);
                if (imported.Count == 0)
                {
                    _dialogService.ShowWarning(this, "No songs found in the selected file.", "Import Error");
                    return;
                }

                var res = _dialogService.ShowConfirmation(
                    this,
                    $"Found {imported.Count} songs in this backup profile.\n\n" +
                    "• Click [Yes] to Merge with your existing songs.\n" +
                    "• Click [No] to Replace all current songs with this backup.\n" +
                    "• Click [Cancel] to abort.",
                    "Import Songs Backup",
                    MessageBoxButton.YesNoCancel);

                if (res == MessageBoxResult.Yes)
                {
                    _dataService.AddSongs(imported, replaceAll: false);
                    _viewModel.SongsVM.ReloadSongs();
                    _dialogService.ShowInformation(this, $"Successfully merged {imported.Count} songs into your library!", "Import Complete");
                }
                else if (res == MessageBoxResult.No)
                {
                    _dataService.AddSongs(imported, replaceAll: true);
                    _viewModel.SongsVM.ReloadSongs();
                    _dialogService.ShowInformation(this, $"Successfully replaced library with {imported.Count} songs from backup!", "Import Complete");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to import songs backup:\n{ex.Message}", "Import Error");
            }
        }
    }

    private void OnImportBibleClick(object sender, RoutedEventArgs e)
    {
        var fileName = _dialogService.ShowImportBibleDialog(this);
        if (fileName != null)
        {
            try
            {
                int count = _dataService.ImportBible(fileName);
                if (count > 0)
                {
                    _viewModel.ScripturesVM.RefreshAfterBibleImport("TB");
                    _dialogService.ShowInformation(
                        this,
                        $"Successfully imported Bible translation with {count:N0} verses!\n\nTranslation is now permanently installed and ready for church presentation.",
                        "Import Complete");
                }
                else
                {
                    _dialogService.ShowWarning(this, "No valid Bible verses could be extracted from the selected JSON file.", "Import Warning");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(this, $"Failed to import Bible file:\n{ex.Message}", "Import Error");
            }
        }
    }



    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        bool force = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                     System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
        await UpdateService.CheckForUpdatesAsync(this, silent: false, forceShowDialog: force);
    }

    private void OnDonateKofiClick(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenKofi();
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        var settings = _dataService.GetSettings();
        settings.IsDarkMode = !settings.IsDarkMode;
        _dataService.SaveSettings(settings);
        ThemeService.ApplyTheme(settings.IsDarkMode);
        UpdateThemeUI(settings.IsDarkMode);
    }

    private void UpdateThemeUI(bool isDark)
    {
        if (MenuThemeToggle != null)
        {
            MenuThemeToggle.IsChecked = isDark;
            MenuThemeToggle.Header = isDark ? "☀️ Light Mode" : "🌙 Dark Mode";
        }
        if (TxtThemeIcon != null && TxtThemeLabel != null)
        {
            TxtThemeIcon.Text = isDark ? "☀️" : "🌙";
            TxtThemeIcon.Foreground = isDark
                ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
                : new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
            TxtThemeLabel.Text = isDark ? "Light" : "Dark";
            BtnThemeToggle.ToolTip = isDark ? "Switch to Light Mode (Ctrl+T)" : "Switch to Dark Mode (Ctrl+T)";
        }
    }

    private void OnToggleStageDisplayClick(object sender, RoutedEventArgs e)
    {
        _projectionService.ToggleStageDisplayWindow();
        if (MenuStageDisplayToggle != null)
        {
            MenuStageDisplayToggle.IsChecked = _projectionService.IsStageDisplayVisible;
        }
    }

    private void UpdateStageMenuState()
    {
        bool isRunning = _projectionService.IsStageWebServerRunning;
        if (MenuWebStageServerToggle != null)
        {
            MenuWebStageServerToggle.IsChecked = isRunning;
        }
        if (MenuViewWebStageServerToggle != null)
        {
            MenuViewWebStageServerToggle.IsChecked = isRunning;
        }
    }

    private void OnToggleWebStageServerClick(object sender, RoutedEventArgs e)
    {
        var settings = _dataService.GetSettings();
        bool isRunning = _projectionService.ToggleStageWebServer(settings.StageWebServerPort);
        settings.EnableStageWebServer = isRunning;
        _dataService.SaveSettings(settings);

        UpdateStageMenuState();

        if (isRunning)
        {
            string url = $"http://{StageWebServer.GetLocalIPAddress()}:{settings.StageWebServerPort}/stage";
            _dialogService.ShowInformation(this, $"Web Stage Display is now ACTIVE!\n\nMusicians and speakers can connect via:\n{url}", "Web Stage Display (Port 8080)");
        }
        else
        {
            _dialogService.ShowInformation(this, "Web Stage Display has been STOPPED.\nPort 8080 is now closed.", "Web Stage Display (Port 8080)");
        }
    }

    private void OnSendStageMessageClick(object sender, RoutedEventArgs e)
    {
        var (confirmed, message, clear) = _dialogService.ShowSendStageMessageDialog(this, _projectionService.State.StageMessage);
        if (confirmed)
        {
            if (clear)
            {
                _projectionService.ClearStageMessage();
            }
            else
            {
                _projectionService.SetStageMessage(message);
            }
        }
    }

    private void OnMenuAboutClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAbout(this);
    }



    protected override void OnClosed(EventArgs e)

    {

        base.OnClosed(e);

        if (!string.IsNullOrWhiteSpace(_viewModel.ActiveGlobalBackground))

        {

            var settings = _dataService.GetSettings();

            settings.LastUsedBackground = _viewModel.ActiveGlobalBackground;

            _dataService.SaveSettings(settings);

        }

        _projectionService.HideProjectionWindow();
        (_projectionService as IDisposable)?.Dispose();

        try

        {

            LuxPresenter.Services.Presentation.LibreOfficeSlideshowService.Instance.Dispose();

        }

        catch { }

        _keyboardShortcuts?.Detach();
        Application.Current.Shutdown();

    }

}

