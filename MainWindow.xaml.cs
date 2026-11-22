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



    public MainWindow(Action<double, string>? startupProgress = null)

    {

        InitializeComponent();
        startupProgress?.Invoke(58, "Loading application interface...");



        _dataService = new DataService();
        startupProgress?.Invoke(68, "Loading songs, media, presentations, and Bible data...");

        _projectionService = new ProjectionService();

        _viewModel = new MainViewModel(_dataService, _projectionService);
        startupProgress?.Invoke(82, "Preparing preview and live output...");



        DataContext = _viewModel;



        MainOnlineBrowserView.CloseRequested += () =>

        {

            MainLibraryView.SelectTab(_viewModel.LastActiveLibraryTabIndex);

        };



        // Wire dialog requests

        _viewModel.OpenSongEditorRequested += OnOpenSongEditorRequested;

        _viewModel.OpenOptionsRequested += OnOpenOptionsRequested;



        // Apply saved song alignment & projection settings

        var initialSettings = _dataService.GetSettings();

        if (initialSettings.LeftPaneWidth >= 260 && initialSettings.LeftPaneWidth <= 650)
        {
            LeftPaneColumn.Width = new GridLength(initialSettings.LeftPaneWidth);
        }
        else
        {
            LeftPaneColumn.Width = new GridLength(380);
        }

        PreviewPaneView.ApplyFormatting(initialSettings);

        LivePaneView.ApplyFormatting(initialSettings);

        _projectionService.UpdateSettings(initialSettings);

        UpdateThemeUI(initialSettings.IsDarkMode);
        UpdateStageMenuState();

        // Clean up orphaned presentation caches, media, and thumbnails on startup
        CleanupOrphanedData();
        startupProgress?.Invoke(90, "Cleaning temporary presentation data...");

        // Pre-warm hardware acceleration, DirectX pipeline, and font rasterization
        _projectionService.Prewarm();
        startupProgress?.Invoke(96, "Pre-warming display shaders & hardware acceleration...");



        // Wire pane activation and prepared target events

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



        // Keyboard navigation shortcuts (Using PreviewKeyDown so Space and PageDown aren't swallowed by focused buttons/lists)

        PreviewKeyDown += OnMainWindowPreviewKeyDown;



        // Wire up Songs empty-state / quick actions

        _viewModel.SongsVM.RequestImportSongBackup += () => OnImportSongBackupClick(this, new RoutedEventArgs());

        _viewModel.SongsVM.RequestOpenSongsDirectory += () => OnOpenSongsFolderClick(this, new RoutedEventArgs());

        _viewModel.SongsVM.RequestLoadSampleSongs += () => OnLoadSampleSongsClick(this, new RoutedEventArgs());

        _viewModel.ScripturesVM.RequestImportBible += () => OnImportBibleClick(this, new RoutedEventArgs());



        // Auto-trim initial startup allocations (JIT, XAML parser caches, font tables) after 2.5 seconds of idle

        Loaded += (s, e) => MemoryHelper.TrimMemoryAsync(2500);
        Loaded += async (s, e) =>
        {
            await System.Threading.Tasks.Task.Delay(3500);
            await UpdateService.CheckForUpdatesAsync(this, silent: true);
        };

    }



    private void OnMainWindowPreviewKeyDown(object sender, KeyEventArgs e)

    {

        // Don't intercept shortcuts if user is typing text in an input box (except Ctrl shortcuts if not copying)

        var isTypingInTextBox = e.OriginalSource is TextBox || Keyboard.FocusedElement is TextBox;



        // Ctrl + T: Toggle Dark / Light Mode
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
        {
            OnToggleThemeClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl + Shift + D: Toggle Stage Display
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D)
        {
            OnToggleStageDisplayClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl + B: Black

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)

        {

            _viewModel.BlackCommand.Execute(null);

            e.Handled = true;

            return;

        }



        // Ctrl + S / Ctrl + Shift + S: Save Schedule

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)

        {

            SaveSchedule(true);

            e.Handled = true;

            return;

        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)

        {

            SaveSchedule(false);

            e.Handled = true;

            return;

        }



        // Ctrl + O: Open Schedule

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)

        {

            OpenSchedule();

            e.Handled = true;

            return;

        }



        // Ctrl + N: New Schedule

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)

        {

            NewSchedule();

            e.Handled = true;

            return;

        }



        // Ctrl + C: Clear (When NOT selecting text in an active TextBox)

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)

        {

            if (isTypingInTextBox && e.OriginalSource is TextBox tb && tb.SelectionLength > 0)

            {

                // Standard copy behavior

                return;

            }



            _viewModel.ClearCommand.Execute(null);

            e.Handled = true;

            return;

        }



        // If user is focused inside a search box, don't hijack Space, Enter, or Arrow keys

        if (isTypingInTextBox) return;



        switch (e.Key)
        {
            // Go Live: F5
            case Key.F5:
                _viewModel.GoLiveCommand.Execute(null);
                e.Handled = true;
                break;

            // Black toggle: F6 or Period (.) from presenter remote
            case Key.F6:
            case Key.OemPeriod:
                _viewModel.BlackCommand.Execute(null);
                e.Handled = true;
                break;

            // Clear toggle: F7
            case Key.F7:
                _viewModel.ClearCommand.Execute(null);
                e.Handled = true;
                break;

            // Logo toggle: F8
            case Key.F8:
                _viewModel.LogoCommand.Execute(null);
                e.Handled = true;
                break;

            // Advance live slide: PageDown, Space, Down Arrow, Right Arrow (Supports presenter remotes)
            case Key.PageDown:
            case Key.Space:
            case Key.Down:
            case Key.Right:
                _viewModel.LiveVM.NextSlideCommand.Execute(null);
                e.Handled = true;
                break;



            // Previous live slide: Up Arrow, Left Arrow, PageUp (Always operates on Live output / right preview)

            case Key.Up:

            case Key.Left:

            case Key.PageUp:

                _viewModel.LiveVM.PrevSlideCommand.Execute(null);

                e.Handled = true;

                break;



            // First slide (Top): Home

            case Key.Home:

                _viewModel.LiveVM.SelectFirstSlide();

                e.Handled = true;

                break;



            // Last slide (Bottom): End

            case Key.End:

                _viewModel.LiveVM.SelectLastSlide();

                e.Handled = true;

                break;

        }

    }



    private void OnOpenSongEditorRequested(Song? song)

    {

        var editor = new SongEditorWindow(song)

        {

            Owner = this

        };



        if (editor.ShowDialog() == true && editor.ResultSong != null)

        {

            _dataService.SaveSong(editor.ResultSong);

            _viewModel.SongsVM.ReloadSongs(editor.ResultSong.Id);



            var updatedPres = editor.ResultSong.ToPresentationItem(_viewModel.ActiveGlobalBackground);



            // Update Preview immediately if Preview was showing this song

            if (_viewModel.PreviewVM.ItemTitle.Equals(editor.ResultSong.Title, StringComparison.OrdinalIgnoreCase) ||

                (song != null && _viewModel.PreviewVM.ItemTitle.Equals(song.Title, StringComparison.OrdinalIgnoreCase)))

            {

                int curIdx = _viewModel.PreviewVM.SelectedIndex;

                _viewModel.PreviewVM.LoadItem(updatedPres, curIdx);

                _viewModel.PreviewVM.Background = _viewModel.ActiveGlobalBackground;

            }



            // Update Live immediately if Live was showing this song

            if (_viewModel.LiveVM.ItemTitle.Equals(editor.ResultSong.Title, StringComparison.OrdinalIgnoreCase) ||

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

                if (item.Title.Equals(editor.ResultSong.Title, StringComparison.OrdinalIgnoreCase) ||

                    (song != null && item.Title.Equals(song.Title, StringComparison.OrdinalIgnoreCase)))

                {

                    item.Title = editor.ResultSong.Title;

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

        var options = new OptionsWindow(_projectionService, _dataService)

        {

            Owner = this

        };



        if (options.ShowDialog() == true)

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



        var dlg = new BackgroundOptionsWindow(_dataService, previewText, currentBg)

        {

            Owner = this

        };



        if (dlg.ShowDialog() == true && dlg.SelectedBackground != null)

        {

            var bg = !string.IsNullOrWhiteSpace(dlg.SelectedBackground.FilePath)

                ? dlg.SelectedBackground.FilePath

                : dlg.SelectedBackground.HexColor;



            // Apply global background to Preview, persist, and update Live ONLY if explicitly requested

            _viewModel.SetActiveBackground(bg, dlg.IsPreviewLiveOutputChecked);

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



        var result = MessageBox.Show(

            "Are you sure you want to clear the schedule?",

            "Clear Schedule",

            MessageBoxButton.YesNo,

            MessageBoxImage.Question);



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



        var dlg = new Microsoft.Win32.OpenFileDialog

        {

            Title = "Open Schedule",

            Filter = "LuxPresenter Schedule (*.lps;*.fws)|*.lps;*.fws|JSON Schedule (*.json)|*.json|All Files (*.*)|*.*",

            DefaultExt = ".fws"

        };



        if (dlg.ShowDialog(this) == true)

        {

            try

            {

                _viewModel.ScheduleVM.LoadFromFile(dlg.FileName);

                return true;

            }

            catch (Exception ex)

            {

                MessageBox.Show($"Failed to open schedule: {ex.Message}", "LuxPresenter", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;

            }

        }



        return false;

    }



    public bool SaveSchedule(bool saveAs = false)

    {

        if (saveAs || string.IsNullOrWhiteSpace(_viewModel.ScheduleVM.CurrentScheduleFilePath))

        {

            var dlg = new Microsoft.Win32.SaveFileDialog

            {

                Title = "Save Schedule",

                Filter = "LuxPresenter Schedule (*.lps;*.fws)|*.lps;*.fws|JSON Schedule (*.json)|*.json|All Files (*.*)|*.*",

                DefaultExt = ".fws",

                FileName = string.IsNullOrWhiteSpace(_viewModel.ScheduleVM.CurrentScheduleFilePath)

                    ? $"Schedule_{DateTime.Now:yyyyMMdd}.fws"

                    : System.IO.Path.GetFileName(_viewModel.ScheduleVM.CurrentScheduleFilePath)

            };



            if (dlg.ShowDialog(this) == true)

            {

                try

                {

                    _viewModel.ScheduleVM.SaveToFile(dlg.FileName);

                    return true;

                }

                catch (Exception ex)

                {

                    MessageBox.Show($"Failed to save schedule: {ex.Message}", "LuxPresenter", MessageBoxButton.OK, MessageBoxImage.Error);

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

                MessageBox.Show($"Failed to save schedule: {ex.Message}", "LuxPresenter", MessageBoxButton.OK, MessageBoxImage.Error);

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



        var result = MessageBox.Show(

            "Do you want to save changes to the current Schedule?",

            "LuxPresenter",

            MessageBoxButton.YesNoCancel,

            MessageBoxImage.Question);



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
        CleanupOrphanedData();

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



    private void CleanupOrphanedData()
    {
        try
        {
            // 1. Presentation caches
            var activeDocs = _dataService.GetPresentationDocs().Select(d => d.FilePath);
            LuxPresenter.Services.Presentation.PresentationEngineService.CleanupOrphanedCaches(activeDocs);

            // 2. Media and Backgrounds missing from disk
            _dataService.CleanupOrphanedMedia();

            // 3. Thumbnails cache cleanup (delete any thumbnail not belonging to active media/backgrounds)
            var activeMedia = _dataService.GetMediaItems().Select(m => m.FilePath);
            var activeBackgrounds = _dataService.GetBackgrounds().Select(b => b.FilePath);
            var defaultBg = _dataService.GetSettings().LastUsedBackground;
            var allActivePaths = activeMedia.Concat(activeBackgrounds);
            if (!string.IsNullOrWhiteSpace(defaultBg))
                allActivePaths = allActivePaths.Append(defaultBg);

            LuxPresenter.Services.ThumbnailService.CleanupOrphanedThumbnails(allActivePaths);
        }
        catch { }
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

            MessageBox.Show($"Failed to open Songs folder: {ex.Message}", "LuxPresenter", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    private void OnChangeSongsFolderClick(object sender, RoutedEventArgs e)

    {

        var dlg = new Microsoft.Win32.OpenFolderDialog

        {

            Title = "Select Songs Database Folder",

            InitialDirectory = AppPaths.SongsDirectory

        };



        if (dlg.ShowDialog(this) == true)

        {

            try

            {

                _dataService.SwitchSongsDirectory(dlg.FolderName);

                _viewModel.SongsVM.ReloadSongs();

                MessageBox.Show(

                    $"Songs database folder successfully changed to:\n{dlg.FolderName}\n\nLoaded {_dataService.GetSongs().Count} songs.",

                    "Songs Database Location",

                    MessageBoxButton.OK,

                    MessageBoxImage.Information);

            }

            catch (Exception ex)

            {

                MessageBox.Show($"Failed to switch database folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }

    }



    private void OnLoadSampleSongsClick(object sender, RoutedEventArgs e)

    {

        var res = MessageBox.Show(

            "Load default sample songs into your library?\n\nThis will add sample worship songs to your library.",

            "Load Sample Songs",

            MessageBoxButton.YesNo,

            MessageBoxImage.Question);



        if (res == MessageBoxResult.Yes)

        {

            _dataService.LoadSampleSongs();

            _viewModel.SongsVM.ReloadSongs();

            MessageBox.Show("Sample songs loaded successfully!", "LuxPresenter", MessageBoxButton.OK, MessageBoxImage.Information);

        }

    }



    private void OnExportSongBackupClick(object sender, RoutedEventArgs e)

    {

        var songs = _dataService.GetSongs();

        var dlg = new Microsoft.Win32.SaveFileDialog

        {

            Title = "Export Songs Profile (Backup)",

            Filter = "LuxPresenter Songs Backup (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",

            FileName = "LuxPresenter_Songs_Backup.json",

            DefaultExt = ".json"

        };



        if (dlg.ShowDialog(this) == true)

        {

            try

            {

                SongImportExportService.ExportSongsToJson(dlg.FileName, songs);

                MessageBox.Show(

                    $"Successfully exported {songs.Count} songs to:\n{dlg.FileName}",

                    "Export Songs Backup",

                    MessageBoxButton.OK,

                    MessageBoxImage.Information);

            }

            catch (Exception ex)

            {

                MessageBox.Show($"Failed to export songs:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }

    }



    private void OnImportSongBackupClick(object sender, RoutedEventArgs e)

    {

        var dlg = new Microsoft.Win32.OpenFileDialog

        {

            Title = "Import Songs Profile (Backup)",

            Filter = "LuxPresenter Songs Backup (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",

            DefaultExt = ".json"

        };



        if (dlg.ShowDialog(this) == true)

        {

            try

            {

                var imported = SongImportExportService.ImportSongsFromJson(dlg.FileName);

                if (imported.Count == 0)

                {

                    MessageBox.Show("No songs found in the selected file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                var res = MessageBox.Show(

                    $"Found {imported.Count} songs in this backup profile.\n\n" +

                    "• Click [Yes] to Merge with your existing songs.\n" +

                    "• Click [No] to Replace all current songs with this backup.\n" +

                    "• Click [Cancel] to abort.",

                    "Import Songs Backup",

                    MessageBoxButton.YesNoCancel,

                    MessageBoxImage.Question);



                if (res == MessageBoxResult.Yes)

                {

                    _dataService.AddSongs(imported, replaceAll: false);

                    _viewModel.SongsVM.ReloadSongs();

                    MessageBox.Show($"Successfully merged {imported.Count} songs into your library!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                }

                else if (res == MessageBoxResult.No)

                {

                    _dataService.AddSongs(imported, replaceAll: true);

                    _viewModel.SongsVM.ReloadSongs();

                    MessageBox.Show($"Successfully replaced library with {imported.Count} songs from backup!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                }

            }

            catch (Exception ex)

            {

                MessageBox.Show($"Failed to import songs backup:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }

    }



    private void OnImportBibleClick(object sender, RoutedEventArgs e)

    {

        var dlg = new Microsoft.Win32.OpenFileDialog

        {

            Title = "Import Bible Translation (.json)",

            Filter = "Bible Translation JSON (*.json)|*.json|All Files (*.*)|*.*",

            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)

        };



        if (dlg.ShowDialog(this) == true)

        {

            try

            {

                int count = _dataService.ImportBible(dlg.FileName);

                if (count > 0)

                {

                    _viewModel.ScripturesVM.RefreshAfterBibleImport("TB");

                    MessageBox.Show(

                        $"Successfully imported Bible translation with {count:N0} verses!\n\nTranslation is now permanently installed and ready for church presentation.",

                        "Import Complete",

                        MessageBoxButton.OK,

                        MessageBoxImage.Information);

                }

                else

                {

                    MessageBox.Show("No valid Bible verses could be extracted from the selected JSON file.", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                }

            }

            catch (Exception ex)

            {

                MessageBox.Show($"Failed to import Bible file:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);

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
            MessageBox.Show($"Web Stage Display is now ACTIVE!\n\nMusicians and speakers can connect via:\n{url}", "Web Stage Display (Port 8080)", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Web Stage Display has been STOPPED.\nPort 8080 is now closed.", "Web Stage Display (Port 8080)", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnSendStageMessageClick(object sender, RoutedEventArgs e)
    {
        var inputDlg = new Window
        {
            Title = "Send Message to Stage Display",
            Width = 440,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("WindowBgBrush"),
            FontFamily = (FontFamily)FindResource("AppFont")
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text = "Enter stage alert message (visible only to singers/pastor on stage):",
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(lbl, 0);

        var txt = new TextBox
        {
            Height = 26,
            FontSize = 12,
            Text = _projectionService.State.StageMessage
        };
        Grid.SetRow(txt, 1);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(btnPanel, 2);

        var btnClear = new Button
        {
            Content = "Clear Alert",
            Width = 90,
            Height = 26,
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnClear.Click += (s, ev) =>
        {
            _projectionService.ClearStageMessage();
            inputDlg.Close();
        };

        var btnSend = new Button
        {
            Content = "Send to Stage",
            Width = 100,
            Height = 26,
            IsDefault = true
        };
        btnSend.Click += (s, ev) =>
        {
            _projectionService.SetStageMessage(txt.Text.Trim());
            inputDlg.Close();
        };

        btnPanel.Children.Add(btnClear);
        btnPanel.Children.Add(btnSend);

        grid.Children.Add(lbl);
        grid.Children.Add(txt);
        grid.Children.Add(btnPanel);

        inputDlg.Content = grid;
        txt.Focus();
        inputDlg.ShowDialog();
    }

    private void OnMenuAboutClick(object sender, RoutedEventArgs e)

    {

        var dlg = new AboutWindow

        {

            Owner = this

        };

        dlg.ShowDialog();

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

        Application.Current.Shutdown();

    }

}

