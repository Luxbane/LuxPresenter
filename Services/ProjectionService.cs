using System.Windows;
using LuxPresenter.Models;
using LuxPresenter.Views;

namespace LuxPresenter.Services;

public class ProjectionService : IProjectionService, IDisposable
{
    private ProjectionWindow? _window;
    private StageDisplayWindow? _stageWindow;
    private StageWebServer? _stageWebServer;
    public ProjectionState State { get; } = new();
    public event Action<ProjectionState>? StateChanged;
    public event Action<TimeSpan, TimeSpan>? VideoProgressChanged;

    public bool IsProjectorConnected => DisplayHelper.GetScreens().Count > 1;
    public int DisplayCount => DisplayHelper.GetScreens().Count;

    public ProjectionService()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var screens = DisplayHelper.GetScreens();
            if (_window != null && _window.IsVisible)
            {
                var secondaryScreen = screens.FirstOrDefault(s => !s.IsPrimary);
                if (secondaryScreen == null)
                {
                    // Monitor unplugged: fallback immediately to single monitor mode!
                    var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault();
                    if (primary != null)
                    {
                        _lastPositionedDevice = primary.DeviceName;
                        _window.HandleFallbackToSingleMonitor(primary);
                    }
                }
                else
                {
                    // Monitor plugged back in: restore projection on secondary monitor!
                    _lastPositionedDevice = null;
                    ShowProjectionWindow();
                }
            }
        });
    }

    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        try
        {
            _stageWebServer?.Dispose();
            _stageWebServer = null;
            _stageWindow?.Close();
            _stageWindow = null;
        }
        catch { }
    }

    public void SeekVideo(double percent)
    {
        _window?.SeekVideo(percent);
    }

    public void SetLooping(bool isLooping)
    {
        State.IsVideoLooping = isLooping;
        _window?.SetLooping(isLooping);
    }

    public void SetLiveSlide(Slide? slide, string? background = null, Slide? nextSlide = null, string itemTitle = "")
    {
        State.IsVideoMode = false;
        State.IsVideoPlaying = false;
        if (State.IsPresentationDocMode || State.LivePresentationDoc != null)
        {
            CloseLivePresentation();
        }
        State.IsPresentationDocMode = false;
        State.ActiveSlide = slide;
        State.ActiveNextSlide = nextSlide;
        if (!string.IsNullOrEmpty(itemTitle))
        {
            State.ActiveItemTitle = itemTitle;
        }
        if (!string.IsNullOrEmpty(background))
        {
            State.ActiveBackground = background;
        }
        else if (slide?.CustomBackgroundPath != null)
        {
            State.ActiveBackground = slide.CustomBackgroundPath;
        }

        NotifyStateChanged();
    }

    public void SetLiveMedia(string mediaPath, bool playImmediately = true)
    {
        if (State.IsPresentationDocMode || State.LivePresentationDoc != null)
        {
            CloseLivePresentation();
        }
        State.IsVideoMode = true;
        State.IsPresentationDocMode = false;
        State.MediaFilePath = mediaPath;
        State.IsVideoPlaying = playImmediately;
        NotifyStateChanged();
    }

    public void SetLivePresentation(PresentationDocument doc, int slideIndex)
    {
        State.IsVideoMode = false;
        State.IsVideoPlaying = false;
        State.IsPresentationDocMode = true;
        State.LivePresentationDoc = doc;
        State.LivePresentationSlideIndex = slideIndex;
        NotifyStateChanged();
        _window?.SetLivePresentation(doc, slideIndex);
    }

    public void NavigatePresentationSlide(int slideIndex)
    {
        State.LivePresentationSlideIndex = slideIndex;
        _window?.NavigatePresentationSlide(slideIndex);
    }

    public void CloseLivePresentation()
    {
        if (!State.IsPresentationDocMode && State.LivePresentationDoc == null)
        {
            return;
        }
        State.IsPresentationDocMode = false;
        State.LivePresentationDoc = null;
        NotifyStateChanged();
        _window?.CloseLivePresentation();
    }

    public void PlayVideo()
    {
        State.IsVideoPlaying = true;
        NotifyStateChanged();
    }

    public void PauseVideo()
    {
        State.IsVideoPlaying = false;
        NotifyStateChanged();
    }

    public void ToggleLive(bool? forceState = null)
    {
        State.IsLive = forceState ?? !State.IsLive;
        NotifyStateChanged();
    }

    public void ToggleBlack()
    {
        State.IsBlack = !State.IsBlack;
        NotifyStateChanged();
    }

    public void ToggleClear()
    {
        State.IsClear = !State.IsClear;
        NotifyStateChanged();
    }

    public void ToggleLogo()
    {
        State.IsLogo = !State.IsLogo;
        NotifyStateChanged();
    }

    public void SetBackground(string background)
    {
        State.ActiveBackground = background;
        NotifyStateChanged();
    }

    public int SelectedMonitorIndex { get; set; } = -1;
    public int MarginTop { get; set; } = 0;
    public int MarginBottom { get; set; } = 0;
    public int MarginLeft { get; set; } = 0;
    public int MarginRight { get; set; } = 0;

    private string? _lastPositionedDevice;

    public void ApplyMonitorSettings(int monitorIndex, int marginTop, int marginBottom, int marginLeft, int marginRight)
    {
        SelectedMonitorIndex = monitorIndex;
        MarginTop = marginTop;
        MarginBottom = marginBottom;
        MarginLeft = marginLeft;
        MarginRight = marginRight;

        _lastPositionedDevice = null;

        if (_window != null)
        {
            var screens = DisplayHelper.GetScreens();
            if (monitorIndex >= 0 && monitorIndex < screens.Count)
            {
                _window.SyncPositionWithScreen(screens[monitorIndex]);
                _lastPositionedDevice = screens[monitorIndex].DeviceName;
            }
            _window.UpdateMargins(MarginTop, MarginBottom, MarginLeft, MarginRight);
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        State.SongTextAlignment = settings.SongTextAlignment;
        State.SongVerticalAlignment = settings.SongVerticalAlignment;
        State.SongTransition = settings.SongTransition;
        State.SlideTransition = settings.SlideTransition;
        State.MediaTransition = settings.MediaTransition;
        State.ScreenTransition = settings.ScreenTransition;
        State.TransitionDurationMs = settings.TransitionDurationMs;
        State.VideoBackgroundEngine = settings.VideoBackgroundEngine;
        State.ShowVerseLabels = settings.ShowVerseLabels;

        State.CapitalizeAllWords = settings.CapitalizeAllWords;
        State.CapitalizeFirstCharEachLine = settings.CapitalizeFirstCharEachLine;
        State.AutoCapitalizeKeywords = settings.AutoCapitalizeKeywords;
        State.CapitalizeKeywords = settings.CapitalizeKeywords;
        State.SongFontFamily = settings.SongFontFamily;
        State.SongFontBold = settings.SongFontBold;
        State.SongFontSize = settings.SongFontSize;
        State.SongFontHeightPercent = settings.SongFontHeightPercent;

        MarginTop = settings.MarginTop;
        MarginBottom = settings.MarginBottom;
        MarginLeft = settings.MarginLeft;
        MarginRight = settings.MarginRight;
        _window?.UpdateMargins(MarginTop, MarginBottom, MarginLeft, MarginRight);

        State.IsStageDisplayEnabled = settings.EnableStageDisplay;
        State.StageMonitorIndex = settings.StageDisplayMonitorIndex;

        // Stage Web Server management
        if (settings.EnableStageWebServer)
        {
            if (_stageWebServer == null)
            {
                _stageWebServer = new StageWebServer(State, settings.StageWebServerPort);
                _stageWebServer.Start();
            }
            else if (_stageWebServer.Port != settings.StageWebServerPort)
            {
                _stageWebServer.Stop();
                _stageWebServer.Start(settings.StageWebServerPort);
            }
        }
        else
        {
            _stageWebServer?.Stop();
            _stageWebServer = null;
        }

        if (settings.EnableStageDisplay && (_stageWindow == null || !_stageWindow.IsVisible))
        {
            ShowStageDisplayWindow(settings.StageDisplayMonitorIndex);
        }

        NotifyStateChanged();
    }

    public void Prewarm()
    {
        try
        {
            if (_window == null)
            {
                _window = new ProjectionWindow();
                _window.VideoProgressReported += (pos, dur) => VideoProgressChanged?.Invoke(pos, dur);
                _window.SetLooping(State.IsVideoLooping);
                _window.Closed += (s, e) =>
                {
                    _window = null;
                    _lastPositionedDevice = null;
                    MemoryHelper.TrimMemoryAsync(300);
                };
            }

            var screens = DisplayHelper.GetScreens();
            ScreenInfo? targetScreen = null;
            if (SelectedMonitorIndex >= 0 && SelectedMonitorIndex < screens.Count)
            {
                targetScreen = screens[SelectedMonitorIndex];
            }
            else
            {
                targetScreen = screens.FirstOrDefault(s => !s.IsPrimary) ?? screens.FirstOrDefault();
            }

            if (targetScreen != null)
            {
                _lastPositionedDevice = targetScreen.DeviceName;
                _window.SyncPositionWithScreen(targetScreen);
                _window.UpdateMargins(MarginTop, MarginBottom, MarginLeft, MarginRight);
            }

            _window.Prewarm();
        }
        catch { }
    }

    public void ShowProjectionWindow()
    {
        bool isFirstOpen = _window == null;

        if (_window == null)
        {
            _window = new ProjectionWindow();
            _window.VideoProgressReported += (pos, dur) => VideoProgressChanged?.Invoke(pos, dur);
            _window.SetLooping(State.IsVideoLooping);
            _window.Closed += (s, e) =>
            {
                _window = null;
                _lastPositionedDevice = null;
                MemoryHelper.TrimMemoryAsync(300);
            };
        }

        var screens = DisplayHelper.GetScreens();
        ScreenInfo? targetScreen = null;

        if (SelectedMonitorIndex >= 0 && SelectedMonitorIndex < screens.Count)
        {
            targetScreen = screens[SelectedMonitorIndex];
        }
        else
        {
            // Auto: prefer secondary monitor (Wireless Display, Projector, HDMI)
            targetScreen = screens.FirstOrDefault(s => !s.IsPrimary) ?? screens.FirstOrDefault();
        }

        if (targetScreen != null)
        {
            // IMPORTANT: Only position the window if it's NOT already visible on that target monitor!
            // Prevents shaking, screen tearing, and Miracast stream stutter every time Go Live or Next Slide is pressed!
            if (!_window.IsVisible || _lastPositionedDevice != targetScreen.DeviceName)
            {
                _lastPositionedDevice = targetScreen.DeviceName;
                _window.SyncPositionWithScreen(targetScreen);
                _window.UpdateMargins(MarginTop, MarginBottom, MarginLeft, MarginRight);
            }
        }

        _window.UpdateState(State);
    }

    public void HideProjectionWindow()
    {
        _window?.HideWindow();
        MemoryHelper.TrimMemoryAsync(300);
    }

    public bool IsStageDisplayVisible => _stageWindow?.IsVisible == true;

    public void ShowStageDisplayWindow(int monitorIndex = -1)
    {
        if (_stageWindow == null)
        {
            _stageWindow = new StageDisplayWindow();
            _stageWindow.Closed += (s, e) =>
            {
                _stageWindow = null;
            };
        }

        var screens = DisplayHelper.GetScreens();
        if (monitorIndex >= 0 && monitorIndex < screens.Count)
        {
            _stageWindow.SyncPositionWithScreen(screens[monitorIndex]);
        }
        else
        {
            // Windowed preview mode
            _stageWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            _stageWindow.ResizeMode = ResizeMode.CanResize;
            _stageWindow.WindowState = WindowState.Normal;
        }

        _stageWindow.UpdateState(State);
        if (!_stageWindow.IsVisible)
        {
            _stageWindow.Show();
        }
    }

    public void HideStageDisplayWindow()
    {
        _stageWindow?.Hide();
    }

    public void ToggleStageDisplayWindow()
    {
        if (_stageWindow?.IsVisible == true)
        {
            HideStageDisplayWindow();
        }
        else
        {
            ShowStageDisplayWindow(State.StageMonitorIndex);
        }
    }

    public void SetStageMessage(string message)
    {
        State.StageMessage = message;
        NotifyStateChanged();
    }

    public void ClearStageMessage()
    {
        State.StageMessage = string.Empty;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        _window?.UpdateState(State);
        _stageWindow?.UpdateState(State);
        StateChanged?.Invoke(State);
    }
}
