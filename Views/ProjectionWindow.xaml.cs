using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LuxPresenter.Models;
using LuxPresenter.Services;
using LuxPresenter.Services.Presentation;
using LuxPresenter.Views.Components;

namespace LuxPresenter.Views;

public partial class ProjectionWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private ProjectionBackgroundWindow? _bgVideoWindow;
    private string _videoBackgroundEngine = "libmpv";
    private string? _currentMpvMediaPath;
    private bool _isLooping = true;
    private bool _isAlertAnimating = false;
    private bool _isFirstRender = true;

    // Dual-Deck WPF MediaElement state (Smooth 60fps Blend Transition matching libmpv)
    private int _activeMediaElementDeck = 0;
    private System.Windows.Threading.DispatcherTimer? _mediaElementCrossfadeTimer;
    private System.Windows.Threading.DispatcherTimer? _mediaElementProgressTimer;
    private MediaElement ActiveMediaElement => _activeMediaElementDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;

    public ProjectionBackgroundWindow? BackgroundVideoWindow => _bgVideoWindow;

    // Two-buffer crossfader state: 0 = Layer A is active/visible, 1 = Layer B is active/visible
    private int _activeLayer = 0;
    private string? _lastSlideId;
    private string? _lastRenderedText;
    private string? _lastVerticalAlignment;
    private string? _lastTextAlignment;
    private bool? _lastShowVerseLabels;
    private int? _lastFontSize;
    private string? _lastFontFamily;
    private bool _isCurrentlyClear = false;
    private bool _isCurrentlyBlack = false;
    private bool _isCurrentlyLogo = false;
    private bool _isCurrentlyInVideoMode = false;
    private ProjectionState? _lastState;

    public event Action<TimeSpan, TimeSpan>? VideoProgressReported;

    public void SetLooping(bool isLooping)
    {
        _isLooping = isLooping;
        _bgVideoWindow?.SetLooping(isLooping);
    }

    public void SeekVideo(double percent)
    {
        _bgVideoWindow?.SeekVideo(percent);

        if (!string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
        {
            var active = ActiveMediaElement;
            if (active.NaturalDuration.HasTimeSpan)
            {
                var total = active.NaturalDuration.TimeSpan;
                active.Position = TimeSpan.FromMilliseconds(total.TotalMilliseconds * Math.Clamp(percent, 0.0, 1.0));
            }
        }
    }

    public void SyncPositionWithScreen(ScreenInfo screen)
    {
        if (_bgVideoWindow != null)
        {
            DisplayHelper.PositionProjectionPair(_bgVideoWindow, this, screen);
        }
        else
        {
            DisplayHelper.PositionWindowOnScreen(this, screen);
        }
    }

    public void HandleFallbackToSingleMonitor(ScreenInfo primaryScreen)
    {
        Topmost = false;
        if (_bgVideoWindow != null)
        {
            _bgVideoWindow.Topmost = false;
        }

        SyncPositionWithScreen(primaryScreen);

        if (Application.Current?.MainWindow != null)
        {
            var mainWin = Application.Current.MainWindow;
            var mainHwnd = new System.Windows.Interop.WindowInteropHelper(mainWin).Handle;
            var frontHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var bgHwnd = _bgVideoWindow != null ? new System.Windows.Interop.WindowInteropHelper(_bgVideoWindow).Handle : IntPtr.Zero;

            if (bgHwnd != IntPtr.Zero)
            {
                DisplayHelper.DemoteFromTopmostAndSendBehind(bgHwnd, IntPtr.Zero);
            }
            if (frontHwnd != IntPtr.Zero)
            {
                DisplayHelper.DemoteFromTopmostAndSendBehind(frontHwnd, mainHwnd);
            }

            mainWin.Activate();
            mainWin.Focus();
        }
    }

    public void EnsureWindowOrder()
    {
        if (_bgVideoWindow?.IsVisible == true && IsVisible)
        {
            var frontHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var bgHwnd = new System.Windows.Interop.WindowInteropHelper(_bgVideoWindow).Handle;
            DisplayHelper.SetWindowBehind(bgHwnd, frontHwnd);
        }
    }

    public void HideWindow()
    {
        _bgVideoWindow?.Hide();
        Hide();
        _isFirstRender = true;
    }

    public ProjectionWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        KeyDown += OnProjectionWindowKeyDown;
        Loaded += OnProjectionWindowLoaded;
        Closed += OnProjectionWindowClosed;

        PreviewMouseDown += (s, e) =>
        {
            EnsureWindowOrder();
            Application.Current?.MainWindow?.Activate();
        };

        _bgVideoWindow = new ProjectionBackgroundWindow();
        _bgVideoWindow.VideoProgressReported += (cur, total) =>
        {
            Dispatcher.BeginInvoke(() => VideoProgressReported?.Invoke(cur, total));
        };

        BackgroundVideoElementA.MediaOpened += (s, e) =>
        {
            File.AppendAllText("media_log.txt", $"[Background A] MediaOpened: {BackgroundVideoElementA.Source}, Duration: {BackgroundVideoElementA.NaturalDuration}\n");
        };
        BackgroundVideoElementA.MediaFailed += (s, e) =>
        {
            File.AppendAllText("media_log.txt", $"[Background A] MediaFailed: {e.ErrorException?.Message}\n");
        };

        BackgroundVideoElementB.MediaOpened += (s, e) =>
        {
            File.AppendAllText("media_log.txt", $"[Background B] MediaOpened: {BackgroundVideoElementB.Source}, Duration: {BackgroundVideoElementB.NaturalDuration}\n");
        };
        BackgroundVideoElementB.MediaFailed += (s, e) =>
        {
            File.AppendAllText("media_log.txt", $"[Background B] MediaFailed: {e.ErrorException?.Message}\n");
        };

        // Listen for Alert changes
        AlertService.Instance.AlertStateChanged += OnAlertStateChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    private void OnProjectionWindowLoaded(object sender, RoutedEventArgs e)
    {
        UpdateAlertMarquee();
        _isFirstRender = false;
    }

    private void OnProjectionWindowClosed(object? sender, EventArgs e)
    {
        AlertService.Instance.AlertStateChanged -= OnAlertStateChanged;
        _mediaElementCrossfadeTimer?.Stop();
        _mediaElementProgressTimer?.Stop();
        try
        {
            _bgVideoWindow?.Close();
            _bgVideoWindow = null;
        }
        catch { }
    }

    private void OnAlertStateChanged()
    {
        Dispatcher.BeginInvoke(UpdateAlertMarquee);
    }

    private void UpdateAlertMarquee()
    {
        var alert = AlertService.Instance;
        if (!alert.IsAlertVisible || alert.CurrentActiveMessage == null)
        {
            AlertTextBlock.BeginAnimation(Canvas.LeftProperty, null);
            AlertBanner.Visibility = Visibility.Collapsed;
            _isAlertAnimating = false;
            return;
        }

        AlertBanner.Visibility = Visibility.Visible;
        AlertTextBlock.Text = alert.CurrentActiveMessage.Text;

        if (!_isAlertAnimating)
        {
            StartAlertAnimation();
        }
    }

    private void StartAlertAnimation()
    {
        var alert = AlertService.Instance;
        if (!alert.IsAlertVisible || alert.CurrentActiveMessage == null)
        {
            _isAlertAnimating = false;
            AlertBanner.Visibility = Visibility.Collapsed;
            return;
        }

        _isAlertAnimating = true;
        AlertTextBlock.UpdateLayout();

        double textWidth = AlertTextBlock.ActualWidth > 0 ? AlertTextBlock.ActualWidth : 1000;
        double screenWidth = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : 1920;

        double totalDistance = screenWidth + textWidth;
        double durationSec = Math.Max(5.0, totalDistance / 180.0);

        var anim = new DoubleAnimation
        {
            From = screenWidth,
            To = -textWidth,
            Duration = TimeSpan.FromSeconds(durationSec)
        };

        anim.Completed += (s, e) =>
        {
            _isAlertAnimating = false;
            AlertService.Instance.NotifyPassCompleted();
            if (AlertService.Instance.IsAlertVisible)
            {
                StartAlertAnimation();
            }
        };

        AlertTextBlock.BeginAnimation(Canvas.LeftProperty, anim);
    }

    private void OnProjectionWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Activate();
                Application.Current.MainWindow.Focus();
            }
            e.Handled = true;
            return;
        }

        // Forward remote presenter / keyboard navigation to MainWindow
        if (Application.Current.MainWindow is MainWindow mainWin && mainWin.DataContext is ViewModels.MainViewModel vm)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.PageDown:
                case System.Windows.Input.Key.Space:
                case System.Windows.Input.Key.Down:
                case System.Windows.Input.Key.Right:
                    vm.LiveVM.NextSlideCommand.Execute(null);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Up:
                case System.Windows.Input.Key.Left:
                case System.Windows.Input.Key.PageUp:
                    vm.LiveVM.PrevSlideCommand.Execute(null);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Home:
                    vm.LiveVM.SelectFirstSlide();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.End:
                    vm.LiveVM.SelectLastSlide();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.F6:
                case System.Windows.Input.Key.OemPeriod:
                    vm.BlackCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    public void UpdateMargins(int top, int bottom, int left, int right)
    {
        var margin = new Thickness(
            Math.Max(10, 50 + left * 3),
            Math.Max(5, 10 + top * 2),
            Math.Max(10, 50 + right * 3),
            Math.Max(10, 30 + bottom * 3)
        );
        SlideContentGridA.Margin = margin;
        SlideContentGridB.Margin = margin;

        double contentW = 1920 - margin.Left - margin.Right;
        if (contentW > 200)
        {
            TxtContentA.Width = contentW;
            TxtContentB.Width = contentW;
        }
    }

    public void UpdateState(ProjectionState state)
    {
        _lastState = state;
        _videoBackgroundEngine = state.VideoBackgroundEngine ?? "libmpv";
        int duration = state.TransitionDurationMs > 0 ? state.TransitionDurationMs : 250;

        // 1. Black overlay
        bool shouldBlack = !state.IsLive || state.IsBlack;
        SetBlackOverlay(shouldBlack, state.ScreenTransition, duration);

        if (shouldBlack)
        {
            SetLogoOverlay(false, state.ScreenTransition, duration);
            _bgVideoWindow?.PauseVideo();
            if (BackgroundVideoElementA.Visibility == Visibility.Visible) BackgroundVideoElementA.Pause();
            if (BackgroundVideoElementB.Visibility == Visibility.Visible) BackgroundVideoElementB.Pause();
            if (ActiveMediaElement.Visibility == Visibility.Visible) ActiveMediaElement.Pause();
        }
        else
        {
            if (_isCurrentlyInVideoMode && state.IsVideoPlaying)
            {
                if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                    _bgVideoWindow?.ResumeVideo();
                else
                    ActiveMediaElement.Play();
            }
            else if (_isCurrentlyInVideoMode && !state.IsVideoPlaying)
            {
                if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                    _bgVideoWindow?.PauseVideo();
                else
                    ActiveMediaElement.Pause();
            }
            else if (!_isCurrentlyInVideoMode)
            {
                if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                {
                    if (_bgVideoWindow?.IsVisible == true) _bgVideoWindow?.ResumeVideo();
                }
                else
                {
                    if (ActiveMediaElement.Visibility == Visibility.Visible) ActiveMediaElement.Play();
                }
            }
        }

        bool isHiddenByOverlay = shouldBlack || state.IsLogo;
        int effectiveDuration = isHiddenByOverlay ? 0 : duration;
        string effectiveScreenTransition = isHiddenByOverlay ? "Cut" : state.ScreenTransition;
        string effectiveSongTransition = isHiddenByOverlay ? "Cut" : state.SongTransition;
        string effectiveSlideTransition = isHiddenByOverlay ? "Cut" : state.SlideTransition;
        string effectiveMediaTransition = isHiddenByOverlay ? "Cut" : state.MediaTransition;

        // Standalone Video Mode (Media Tab Video with Smooth Fade Transition)
        if (state.IsVideoMode && !string.IsNullOrEmpty(state.MediaFilePath) && File.Exists(state.MediaFilePath))
        {
            if (!_isCurrentlyInVideoMode)
            {
                _isCurrentlyInVideoMode = true;
                string? prevBg = _currentActiveBg;
                _currentActiveBg = null; // Force reset active background so returning to song will re-apply properly
                SetLogoOverlay(false, state.ScreenTransition, duration);

                bool useMediaBlend = (string.Equals(effectiveMediaTransition, "Blend", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(effectiveSongTransition, "Blend", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(effectiveScreenTransition, "Blend", StringComparison.OrdinalIgnoreCase)) && effectiveDuration > 0;

                StopBackgroundVideo();
                RootGrid.Background = Brushes.Transparent;

                bool isPlaying = shouldBlack ? false : state.IsVideoPlaying;

                if (useMediaBlend && effectiveDuration > 0 && !_isFirstRender)
                {
                    PlayMediaOnProjector(state.MediaFilePath, isPlaying, effectiveDuration);
                    AnimateFade(SlideContentGridA, SlideContentGridA.Opacity, 0.0, effectiveDuration);
                    AnimateFade(SlideContentGridB, SlideContentGridB.Opacity, 0.0, effectiveDuration);
                    if (BackgroundImageA.Source != null && BackgroundImageA.Opacity > 0.01)
                    {
                        AnimateFade(BackgroundImageA, BackgroundImageA.Opacity, 0.0, effectiveDuration, () => BackgroundImageA.Source = null);
                    }
                    if (BackgroundImageB.Source != null && BackgroundImageB.Opacity > 0.01)
                    {
                        AnimateFade(BackgroundImageB, BackgroundImageB.Opacity, 0.0, effectiveDuration, () => BackgroundImageB.Source = null);
                    }
                }
                else
                {
                    PlayMediaOnProjector(state.MediaFilePath, isPlaying, 0);
                    SlideContentGridA.BeginAnimation(UIElement.OpacityProperty, null);
                    SlideContentGridB.BeginAnimation(UIElement.OpacityProperty, null);
                    BackgroundImageA.BeginAnimation(UIElement.OpacityProperty, null);
                    BackgroundImageB.BeginAnimation(UIElement.OpacityProperty, null);
                    SlideContentGridA.Opacity = 0.0;
                    SlideContentGridB.Opacity = 0.0;
                    BackgroundImageA.Opacity = 0.0;
                    BackgroundImageB.Opacity = 0.0;
                    BackgroundImageA.Source = null;
                    BackgroundImageB.Source = null;
                }
            }
            else
            {
                // Already in video mode, handle play/pause or new media
                bool isPlaying = shouldBlack ? false : state.IsVideoPlaying;
                if (_currentMpvMediaPath != state.MediaFilePath)
                {
                    PlayMediaOnProjector(state.MediaFilePath, isPlaying, effectiveDuration);
                }
                else
                {
                    if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isPlaying) _bgVideoWindow?.ResumeVideo();
                        else _bgVideoWindow?.PauseVideo();
                    }
                    else
                    {
                        if (isPlaying) ActiveMediaElement.Play();
                        else ActiveMediaElement.Pause();
                    }
                }
            }
            if (shouldBlack)
            {
                _bgVideoWindow?.PauseVideo();
                ActiveMediaElement.Pause();
            }
            _isFirstRender = false;
            return;
        }
        else
        {
            bool returningFromVideo = _isCurrentlyInVideoMode;
            if (returningFromVideo)
            {
                _isCurrentlyInVideoMode = false;
                _currentMpvMediaPath = null;
                _currentActiveBg = null; // Force reload song background!
                StopMediaElementProgressTimer();
                StopMpvBackgroundVideo();
                StopBackgroundVideo();
            }
        }

        // 2. Logo overlay
        SetLogoOverlay(state.IsLogo, state.ScreenTransition, duration);

        // Presentation Document Mode
        if (state.IsPresentationDocMode && _activeLiveDoc != null)
        {
            StopMpvBackgroundVideo();
            StopBackgroundVideo();
            RootGrid.Background = Brushes.Black;
            SlideViewbox.Visibility = Visibility.Collapsed;
            PresentationImage.Visibility = Visibility.Visible;
            _isFirstRender = false;
            return;
        }

        // 3. Background Color / Image / Video (with smooth blend transition)
        ApplyBackground(state.ActiveBackground, effectiveSongTransition, effectiveDuration);
        if (shouldBlack)
        {
            _bgVideoWindow?.PauseVideo();
            if (BackgroundVideoElementA.Visibility == Visibility.Visible) BackgroundVideoElementA.Pause();
            if (BackgroundVideoElementB.Visibility == Visibility.Visible) BackgroundVideoElementB.Pause();
            if (ActiveMediaElement.Visibility == Visibility.Visible) ActiveMediaElement.Pause();
        }

        // 4. Clear text mode (Blend in/out for Clear)
        bool wasCleared = _isCurrentlyClear;
        if (state.IsClear)
        {
            if (!_isCurrentlyClear || SlideContentGridA.Opacity > 0.01 || SlideContentGridB.Opacity > 0.01)
            {
                _isCurrentlyClear = true;
                if (string.Equals(effectiveScreenTransition, "Blend", StringComparison.OrdinalIgnoreCase) && effectiveDuration > 0)
                {
                    AnimateFade(SlideContentGridA, SlideContentGridA.Opacity, 0.0, effectiveDuration);
                    AnimateFade(SlideContentGridB, SlideContentGridB.Opacity, 0.0, effectiveDuration);
                }
                else
                {
                    SlideContentGridA.BeginAnimation(UIElement.OpacityProperty, null);
                    SlideContentGridB.BeginAnimation(UIElement.OpacityProperty, null);
                    SlideContentGridA.Opacity = 0.0;
                    SlideContentGridB.Opacity = 0.0;
                }
            }
            return;
        }
        else
        {
            if (_isCurrentlyClear)
            {
                _isCurrentlyClear = false;
                wasCleared = true;
            }
        }

        // 5. Normal Slide Display & Double-Buffer Crossfade
        var slide = state.ActiveSlide;
        if (slide == null)
        {
            // Slide removed / blank -> fade both layers out (Blend Out)
            if (string.Equals(effectiveSlideTransition, "Blend", StringComparison.OrdinalIgnoreCase) && effectiveDuration > 0)
            {
                AnimateFade(SlideContentGridA, SlideContentGridA.Opacity, 0.0, effectiveDuration);
                AnimateFade(SlideContentGridB, SlideContentGridB.Opacity, 0.0, effectiveDuration);
            }
            else
            {
                SlideContentGridA.BeginAnimation(UIElement.OpacityProperty, null);
                SlideContentGridB.BeginAnimation(UIElement.OpacityProperty, null);
                SlideContentGridA.Opacity = 0.0;
                SlideContentGridB.Opacity = 0.0;
            }
            _lastSlideId = null;
            _lastRenderedText = null;
            return;
        }

        // Format Lyrics Text (Respecting Word Formatting Options & Capitalization)
        var formattedContent = LyricsFormatter.Format(slide.Content, new AppSettings
        {
            CapitalizeAllWords = state.CapitalizeAllWords,
            CapitalizeFirstCharEachLine = state.CapitalizeFirstCharEachLine,
            AutoCapitalizeKeywords = state.AutoCapitalizeKeywords,
            CapitalizeKeywords = state.CapitalizeKeywords
        });

        // Determine if slide, text, or typography alignment changed
        bool hasChanged = wasCleared ||
                          _lastSlideId != slide.Id ||
                          _lastRenderedText != formattedContent ||
                          _lastVerticalAlignment != state.SongVerticalAlignment ||
                          _lastTextAlignment != state.SongTextAlignment ||
                          _lastShowVerseLabels != state.ShowVerseLabels ||
                          _lastFontSize != slide.FontSize ||
                          _lastFontFamily != slide.FontFamily;
        if (!hasChanged)
        {
            return;
        }

        _lastSlideId = slide.Id;
        _lastRenderedText = formattedContent;
        _lastVerticalAlignment = state.SongVerticalAlignment;
        _lastTextAlignment = state.SongTextAlignment;
        _lastShowVerseLabels = state.ShowVerseLabels;
        _lastFontSize = slide.FontSize;
        _lastFontFamily = slide.FontFamily;

        // Choose incoming (alternate) vs outgoing (current) layer
        int nextLayer = 1 - _activeLayer;
        var incomingGrid = nextLayer == 0 ? SlideContentGridA : SlideContentGridB;
        var outgoingGrid = _activeLayer == 0 ? SlideContentGridA : SlideContentGridB;
        var incomingText = nextLayer == 0 ? TxtContentA : TxtContentB;
        var incomingFooter = nextLayer == 0 ? TxtFooterA : TxtFooterB;
        var incomingHeader = nextLayer == 0 ? TxtHeaderA : TxtHeaderB;
        var incomingViewbox = nextLayer == 0 ? ContentViewboxA : ContentViewboxB;

        // Determine final display text
        string finalDisplayText;
        if (slide.Type == SlideType.Scripture)
        {
            incomingHeader.Visibility = Visibility.Collapsed;
            var versionSuffix = string.IsNullOrWhiteSpace(slide.Subtitle) ? string.Empty : $" ({slide.Subtitle})";
            finalDisplayText = LyricsFormatter.Format($"{slide.Content}{versionSuffix}", new AppSettings
            {
                CapitalizeAllWords = state.CapitalizeAllWords,
                CapitalizeFirstCharEachLine = state.CapitalizeFirstCharEachLine,
                AutoCapitalizeKeywords = state.AutoCapitalizeKeywords,
                CapitalizeKeywords = state.CapitalizeKeywords
            });
            incomingFooter.Text = slide.Title;
        }
        else
        {
            // Always hide header for songs
            incomingHeader.Visibility = Visibility.Collapsed;
            finalDisplayText = formattedContent;
            if (state.ShowVerseLabels && !string.IsNullOrWhiteSpace(slide.Label))
            {
                incomingFooter.Visibility = Visibility.Visible;
                incomingFooter.Text = slide.Label;
            }
            else
            {
                incomingFooter.Visibility = Visibility.Collapsed;
                incomingFooter.Text = string.Empty;
            }
        }

        incomingText.Text = finalDisplayText;

        var fontFamily = new FontFamily(string.IsNullOrWhiteSpace(slide.FontFamily) ? (state.SongFontFamily ?? "Arial") : slide.FontFamily);
        var fontWeight = (state.SongFontBold || slide.IsBold) ? FontWeights.Bold : FontWeights.Normal;
        var fontStyle = slide.IsItalic ? FontStyles.Italic : FontStyles.Normal;

        incomingText.FontFamily = fontFamily;
        incomingText.FontWeight = fontWeight;
        incomingText.FontStyle = fontStyle;
        incomingText.HasOutline = slide.HasOutline;
        incomingText.HasShadow = slide.HasShadow;

        // Apply Vertical Alignment (align both SlideViewbox viewport and incomingViewbox to monitor edge)
        if (string.Equals(state.SongVerticalAlignment, "Top", StringComparison.OrdinalIgnoreCase))
        {
            SlideViewbox.VerticalAlignment = VerticalAlignment.Top;
            incomingViewbox.VerticalAlignment = VerticalAlignment.Top;
        }
        else if (string.Equals(state.SongVerticalAlignment, "Bottom", StringComparison.OrdinalIgnoreCase))
        {
            SlideViewbox.VerticalAlignment = VerticalAlignment.Bottom;
            incomingViewbox.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else
        {
            SlideViewbox.VerticalAlignment = VerticalAlignment.Center;
            incomingViewbox.VerticalAlignment = VerticalAlignment.Center;
        }

        // Apply Text Alignment
        if (string.Equals(state.SongTextAlignment, "Left", StringComparison.OrdinalIgnoreCase))
            incomingText.TextAlignment = TextAlignment.Left;
        else if (string.Equals(state.SongTextAlignment, "Right", StringComparison.OrdinalIgnoreCase))
            incomingText.TextAlignment = TextAlignment.Right;
        else
            incomingText.TextAlignment = TextAlignment.Center;

        // Dynamic optimal font size:
        // Automatically calculates the ideal font size so long verses/lyrics span the full width
        // and fit comfortably without being crushed into a narrow vertical column!
        double availW = incomingText.Width > 0 ? incomingText.Width : (1920 - incomingGrid.Margin.Left - incomingGrid.Margin.Right);
        if (availW <= 100) availW = 1720;
        double availH = 1080 - incomingGrid.Margin.Top - incomingGrid.Margin.Bottom - 80;
        if (availH <= 100) availH = 840;

        int basePt = state.SongFontSize > 0 ? state.SongFontSize : (slide.FontSize > 0 ? slide.FontSize : 13);
        double maxTargetFs = basePt > 0 ? (basePt * 6.5) : 84.5;

        incomingText.FontSize = OutlinedTextBlock.CalculateOptimalFontSize(
            finalDisplayText,
            fontFamily,
            fontWeight,
            fontStyle,
            availW - 20,
            availH,
            maxTargetFs,
            minFs: 34.0);

        // Crossfade Transition between outgoing and incoming layers
        Panel.SetZIndex(incomingGrid, 2);
        Panel.SetZIndex(outgoingGrid, 1);

        bool isInitial = _isFirstRender || !IsLoaded;
        bool useBlend = (string.Equals(effectiveSlideTransition, "Blend", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(effectiveSongTransition, "Blend", StringComparison.OrdinalIgnoreCase)) && !isInitial && effectiveDuration > 0;

        if (useBlend && effectiveDuration > 0)
        {
            incomingGrid.Opacity = 0.0;
            double curOut = wasCleared ? 0.0 : (outgoingGrid.Opacity > 0.05 ? outgoingGrid.Opacity : 1.0);
            AnimateFade(incomingGrid, 0.0, 1.0, effectiveDuration);
            if (curOut > 0.01)
            {
                AnimateFade(outgoingGrid, curOut, 0.0, effectiveDuration);
            }
            else
            {
                outgoingGrid.BeginAnimation(UIElement.OpacityProperty, null);
                outgoingGrid.Opacity = 0.0;
            }
        }
        else
        {
            incomingGrid.BeginAnimation(UIElement.OpacityProperty, null);
            outgoingGrid.BeginAnimation(UIElement.OpacityProperty, null);
            incomingGrid.Opacity = 1.0;
            outgoingGrid.Opacity = 0.0;
        }

        _activeLayer = nextLayer;
        _isFirstRender = false;
    }

    private static void AnimateFade(UIElement element, double from, double to, int durationMs, Action? onCompleted = null)
    {
        if (durationMs <= 0)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = to;
            onCompleted?.Invoke();
            return;
        }

        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            FillBehavior = FillBehavior.HoldEnd
        };
        anim.Completed += (s, e) =>
        {
            element.Opacity = to;
            onCompleted?.Invoke();
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void SetBlackOverlay(bool show, string transition, int durationMs)
    {
        if (show == _isCurrentlyBlack) return;
        _isCurrentlyBlack = show;

        int duration = durationMs > 0 ? durationMs : 250;
        if (show)
        {
            BlackOverlay.Visibility = Visibility.Visible;
            if (string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                AnimateFade(BlackOverlay, 0.0, 1.0, duration);
            }
            else
            {
                BlackOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                BlackOverlay.Opacity = 1.0;
            }
        }
        else
        {
            if (string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                AnimateFade(BlackOverlay, BlackOverlay.Opacity, 0.0, duration, () =>
                {
                    if (!_isCurrentlyBlack)
                    {
                        BlackOverlay.Visibility = Visibility.Collapsed;
                    }
                });
            }
            else
            {
                BlackOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                BlackOverlay.Opacity = 0.0;
                BlackOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SetLogoOverlay(bool show, string transition, int durationMs)
    {
        if (show == _isCurrentlyLogo) return;
        _isCurrentlyLogo = show;

        int duration = durationMs > 0 ? durationMs : 250;
        if (show)
        {
            LogoOverlay.Visibility = Visibility.Visible;
            if (string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                AnimateFade(LogoOverlay, 0.0, 1.0, duration);
            }
            else
            {
                LogoOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                LogoOverlay.Opacity = 1.0;
            }
        }
        else
        {
            if (string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                AnimateFade(LogoOverlay, LogoOverlay.Opacity, 0.0, duration, () =>
                {
                    if (!_isCurrentlyLogo)
                    {
                        LogoOverlay.Visibility = Visibility.Collapsed;
                    }
                });
            }
            else
            {
                LogoOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                LogoOverlay.Opacity = 0.0;
                LogoOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private int _activeBgLayer = 0;
    private string? _currentActiveBg;

    private void ApplyBackground(string bg, string transition = "Blend", int durationMs = 350)
    {
        if (string.IsNullOrWhiteSpace(bg))
        {
            if (_currentActiveBg != null)
            {
                _currentActiveBg = null;
                AnimateFade(BackgroundImageA, BackgroundImageA.Opacity, 0.0, durationMs, () => BackgroundImageA.Source = null);
                AnimateFade(BackgroundImageB, BackgroundImageB.Opacity, 0.0, durationMs, () => BackgroundImageB.Source = null);
                FadeOutActiveVideos(durationMs);
                StopMpvBackgroundVideo();
            }
            RootGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            return;
        }

        if (_currentActiveBg == bg && !_isFirstRender && !string.IsNullOrEmpty(bg))
        {
            return;
        }

        string? prevBg = _currentActiveBg;
        _currentActiveBg = bg;

        if (bg.StartsWith("#"))
        {
            AnimateFade(BackgroundImageA, BackgroundImageA.Opacity, 0.0, durationMs, () => BackgroundImageA.Source = null);
            AnimateFade(BackgroundImageB, BackgroundImageB.Opacity, 0.0, durationMs, () => BackgroundImageB.Source = null);
            FadeOutActiveVideos(durationMs);
            StopMpvBackgroundVideo();
            try
            {
                RootGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            }
            catch
            {
                RootGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            }
        }
        else if (File.Exists(bg))
        {
            bool isVideo = ThumbnailService.IsVideoFile(bg);

            if (isVideo)
            {
                if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                {
                    // === Hardware Accelerated LibMPV Video Background ===
                    StopBackgroundVideo(); // Stop MediaElement fallback

                    bool useBlend = string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase) && !_isFirstRender && IsLoaded;

                    // If transitioning with Blend from another video or image:
                    if (useBlend && durationMs > 0)
                    {
                        RootGrid.Background = Brushes.Transparent;
                        if (_bgVideoWindow != null)
                        {
                            if (!_bgVideoWindow.IsVisible) _bgVideoWindow.Show();
                            _bgVideoWindow.PlayBackgroundVideo(bg, durationMs);
                            EnsureWindowOrder();
                        }

                        FadeOutActiveImages(durationMs);
                    }
                    else
                    {
                        RootGrid.Background = Brushes.Transparent;
                        if (_bgVideoWindow != null)
                        {
                            if (!_bgVideoWindow.IsVisible) _bgVideoWindow.Show();
                            _bgVideoWindow.PlayBackgroundVideo(bg, 0);
                            EnsureWindowOrder();
                            _bgVideoWindow.BeginAnimation(UIElement.OpacityProperty, null);
                            _bgVideoWindow.Opacity = 1.0;
                        }

                        BackgroundImageA.BeginAnimation(UIElement.OpacityProperty, null);
                        BackgroundImageB.BeginAnimation(UIElement.OpacityProperty, null);
                        BackgroundImageA.Opacity = 0.0;
                        BackgroundImageB.Opacity = 0.0;
                        BackgroundImageA.Source = null;
                        BackgroundImageB.Source = null;
                    }
                }
                else
                {
                    // === Dual-Deck WPF MediaElement Engine (Smooth Blend matching libmpv) ===
                    StopMpvBackgroundVideo();
                    RootGrid.Background = Brushes.Transparent;

                    bool useBlend = string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase) && !_isFirstRender && IsLoaded;
                    PlayBackgroundVideoMediaElement(bg, useBlend ? durationMs : 0);
                }
            }
            else
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(bg, UriKind.Absolute);
                    bitmap.EndInit();
                    if (bitmap.CanFreeze) bitmap.Freeze();

                    int nextLayer = 1 - _activeBgLayer;
                    var incomingImg = nextLayer == 0 ? BackgroundImageA : BackgroundImageB;
                    var outgoingImg = _activeBgLayer == 0 ? BackgroundImageA : BackgroundImageB;

                    incomingImg.BeginAnimation(UIElement.OpacityProperty, null);
                    incomingImg.Source = bitmap;
                    Panel.SetZIndex(incomingImg, 4);
                    Panel.SetZIndex(outgoingImg, 3);
                    Panel.SetZIndex(BackgroundVideoElementA, 2);
                    Panel.SetZIndex(BackgroundVideoElementB, 2);

                    bool isInitial = _isFirstRender || !IsLoaded;
                    bool useBlend = string.Equals(transition, "Blend", StringComparison.OrdinalIgnoreCase) && !isInitial;

                    if (useBlend && durationMs > 0)
                    {
                        incomingImg.Opacity = 0.0;
                        AnimateFade(incomingImg, 0.0, 1.0, durationMs, onCompleted: () =>
                        {
                            StopBackgroundVideo();
                            StopMpvBackgroundVideo();
                        });

                        if (outgoingImg.Source != null && outgoingImg.Opacity > 0.01)
                        {
                            var outRef = outgoingImg;
                            int targetLayerIdx = _activeBgLayer;
                            AnimateFade(outRef, outRef.Opacity, 0.0, durationMs, () =>
                            {
                                if (_activeBgLayer != targetLayerIdx)
                                {
                                    outRef.Source = null;
                                    outRef.Opacity = 0.0;
                                }
                            });
                        }
                    }
                    else
                    {
                        incomingImg.BeginAnimation(UIElement.OpacityProperty, null);
                        outgoingImg.BeginAnimation(UIElement.OpacityProperty, null);
                        incomingImg.Opacity = 1.0;
                        outgoingImg.Opacity = 0.0;
                        outgoingImg.Source = null;
                        StopBackgroundVideo();
                        StopMpvBackgroundVideo();
                    }

                    _activeBgLayer = nextLayer;
                }
                catch
                {
                    RootGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
                    AnimateFade(BackgroundImageA, BackgroundImageA.Opacity, 0.0, durationMs, () => BackgroundImageA.Source = null);
                    AnimateFade(BackgroundImageB, BackgroundImageB.Opacity, 0.0, durationMs, () => BackgroundImageB.Source = null);
                    StopBackgroundVideo();
                    StopMpvBackgroundVideo();
                }
            }
        }
        else
        {
            RootGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            AnimateFade(BackgroundImageA, BackgroundImageA.Opacity, 0.0, durationMs, () => BackgroundImageA.Source = null);
            AnimateFade(BackgroundImageB, BackgroundImageB.Opacity, 0.0, durationMs, () => BackgroundImageB.Source = null);
            StopBackgroundVideo();
            StopMpvBackgroundVideo();
        }
    }

    private void PlayBackgroundVideoMediaElement(string filePath, int durationMs)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        int incomingDeck = 1 - _activeMediaElementDeck;
        var incoming = incomingDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;
        var outgoing = _activeMediaElementDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;

        _mediaElementCrossfadeTimer?.Stop();

        incoming.IsMuted = true;
        incoming.LoadedBehavior = MediaState.Manual;
        incoming.UnloadedBehavior = MediaState.Stop;
        incoming.Source = new Uri(filePath, UriKind.Absolute);
        incoming.Stretch = Stretch.Fill;
        incoming.HorizontalAlignment = HorizontalAlignment.Stretch;
        incoming.VerticalAlignment = VerticalAlignment.Stretch;
        incoming.Visibility = Visibility.Visible;
        incoming.Opacity = 0.0;
        Panel.SetZIndex(incoming, 4);
        Panel.SetZIndex(outgoing, 3);
        Panel.SetZIndex(BackgroundImageA, 2);
        Panel.SetZIndex(BackgroundImageB, 2);

        incoming.Play();

        bool hasActiveOutgoing = outgoing.Visibility == Visibility.Visible && outgoing.Source != null;

        if (durationMs > 0 && hasActiveOutgoing && !_isFirstRender && IsLoaded)
        {
            CrossfadeMediaElements(incomingDeck, durationMs);
        }
        else
        {
            incoming.Opacity = 1.0;
            outgoing.Opacity = 0.0;
            outgoing.Stop();
            outgoing.Visibility = Visibility.Collapsed;
            outgoing.Source = null;
            _activeMediaElementDeck = incomingDeck;
        }

        FadeOutActiveImages(durationMs);
    }

    private void CrossfadeMediaElements(int incomingDeck, int durationMs)
    {
        _mediaElementCrossfadeTimer?.Stop();

        var incoming = incomingDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;
        var outgoing = incomingDeck == 0 ? BackgroundVideoElementB : BackgroundVideoElementA;

        Panel.SetZIndex(incoming, 4);
        Panel.SetZIndex(outgoing, 3);
        incoming.Opacity = 0.0;
        outgoing.Opacity = 1.0;

        DateTime startTime = DateTime.UtcNow;
        _mediaElementCrossfadeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps smooth crossfade identical to libmpv
        };

        _mediaElementCrossfadeTimer.Tick += (s, e) =>
        {
            double elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / durationMs, 0.0, 1.0);

            incoming.Opacity = progress;

            if (progress >= 1.0)
            {
                _mediaElementCrossfadeTimer.Stop();
                incoming.Opacity = 1.0;
                outgoing.Opacity = 0.0;
                outgoing.Stop();
                outgoing.Visibility = Visibility.Collapsed;
                outgoing.Source = null;
                _activeMediaElementDeck = incomingDeck;
            }
        };

        _mediaElementCrossfadeTimer.Start();
    }

    public void PlayMediaElementVideo(string filePath, bool isPlaying, bool isLooping, int durationMs = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        _currentMpvMediaPath = filePath;

        int incomingDeck = 1 - _activeMediaElementDeck;
        var incoming = incomingDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;
        var outgoing = _activeMediaElementDeck == 0 ? BackgroundVideoElementA : BackgroundVideoElementB;

        _mediaElementCrossfadeTimer?.Stop();

        incoming.IsMuted = false;
        incoming.LoadedBehavior = MediaState.Manual;
        incoming.UnloadedBehavior = MediaState.Stop;
        incoming.Source = new Uri(filePath, UriKind.Absolute);
        incoming.Stretch = Stretch.Uniform;
        incoming.HorizontalAlignment = HorizontalAlignment.Center;
        incoming.VerticalAlignment = VerticalAlignment.Center;
        incoming.Visibility = Visibility.Visible;
        incoming.Opacity = 0.0;
        Panel.SetZIndex(incoming, 4);
        Panel.SetZIndex(outgoing, 3);
        Panel.SetZIndex(BackgroundImageA, 2);
        Panel.SetZIndex(BackgroundImageB, 2);

        incoming.Play();
        if (!isPlaying) incoming.Pause();

        bool hasActiveOutgoing = outgoing.Visibility == Visibility.Visible && outgoing.Source != null;

        if (durationMs > 0 && hasActiveOutgoing && !_isFirstRender && IsLoaded)
        {
            CrossfadeMediaElements(incomingDeck, durationMs);
        }
        else
        {
            incoming.Opacity = 1.0;
            outgoing.Opacity = 0.0;
            outgoing.Stop();
            outgoing.Visibility = Visibility.Collapsed;
            outgoing.Source = null;
            _activeMediaElementDeck = incomingDeck;
        }

        StartMediaElementProgressTimer();
    }

    private void FadeOutActiveImages(int durationMs)
    {
        void FadeImg(Image img, int layerIdx)
        {
            if (img.Source != null && img.Opacity > 0.01)
            {
                if (durationMs > 0)
                {
                    AnimateFade(img, img.Opacity, 0.0, durationMs, () =>
                    {
                        if (_activeBgLayer != layerIdx)
                        {
                            img.Source = null;
                            img.Opacity = 0.0;
                        }
                    });
                }
                else
                {
                    img.BeginAnimation(UIElement.OpacityProperty, null);
                    img.Opacity = 0.0;
                    img.Source = null;
                }
            }
        }
        FadeImg(BackgroundImageA, 0);
        FadeImg(BackgroundImageB, 1);
    }

    private void FadeOutActiveVideos(int durationMs, Action? onCompleted = null)
    {
        _mediaElementCrossfadeTimer?.Stop();
        var active = ActiveMediaElement;
        if (active.Visibility == Visibility.Visible && active.Opacity > 0.01 && durationMs > 0)
        {
            AnimateFade(active, active.Opacity, 0.0, durationMs, () =>
            {
                StopBackgroundVideo();
                onCompleted?.Invoke();
            });
        }
        else
        {
            StopBackgroundVideo();
            onCompleted?.Invoke();
        }
    }

    private void StartMediaElementProgressTimer()
    {
        if (_mediaElementProgressTimer == null)
        {
            _mediaElementProgressTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _mediaElementProgressTimer.Tick += (s, e) =>
            {
                if (_isCurrentlyInVideoMode && !string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
                {
                    var active = ActiveMediaElement;
                    if (active.NaturalDuration.HasTimeSpan)
                    {
                        VideoProgressReported?.Invoke(active.Position, active.NaturalDuration.TimeSpan);
                    }
                }
            };
        }
        _mediaElementProgressTimer.Start();
    }

    private void StopMediaElementProgressTimer()
    {
        _mediaElementProgressTimer?.Stop();
    }

    private void StopMpvBackgroundVideo()
    {
        if (_bgVideoWindow != null && !_isCurrentlyInVideoMode)
        {
            _bgVideoWindow.StopVideo();
            // Keep _bgVideoWindow visible as solid black backdrop! Do NOT call _bgVideoWindow.Hide()!
        }
    }

    private void StopBackgroundVideo()
    {
        _mediaElementCrossfadeTimer?.Stop();
        try
        {
            BackgroundVideoElementA.Stop();
            BackgroundVideoElementA.Visibility = Visibility.Collapsed;
            BackgroundVideoElementA.Source = null;
            BackgroundVideoElementA.Opacity = 0.0;

            BackgroundVideoElementB.Stop();
            BackgroundVideoElementB.Visibility = Visibility.Collapsed;
            BackgroundVideoElementB.Source = null;
            BackgroundVideoElementB.Opacity = 0.0;
        }
        catch { }
    }

    private void PlayMediaOnProjector(string filePath, bool isPlaying, int durationMs = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (string.Equals(_videoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
        {
            if (_bgVideoWindow == null) return;
            _currentMpvMediaPath = filePath;
            if (!_bgVideoWindow.IsVisible) _bgVideoWindow.Show();
            _bgVideoWindow.PlayMediaVideo(filePath, isPlaying, _isLooping, durationMs);
            EnsureWindowOrder();
        }
        else
        {
            StopMpvBackgroundVideo();
            PlayMediaElementVideo(filePath, isPlaying, _isLooping, durationMs);
        }
    }

    private void OnBackgroundVideoEndedA(object sender, RoutedEventArgs e)
    {
        if (_isLooping || !_isCurrentlyInVideoMode)
        {
            BackgroundVideoElementA.Position = TimeSpan.Zero;
            BackgroundVideoElementA.Play();
        }
    }

    private void OnBackgroundVideoEndedB(object sender, RoutedEventArgs e)
    {
        if (_isLooping || !_isCurrentlyInVideoMode)
        {
            BackgroundVideoElementB.Position = TimeSpan.Zero;
            BackgroundVideoElementB.Play();
        }
    }

    private PresentationDocument? _activeLiveDoc;

    public void SetLivePresentation(PresentationDocument doc, int slideIndex)
    {
        try
        {
            _activeLiveDoc = doc;
            _currentActiveBg = null;

            // Stop motion video background completely so presentation is never overlaid on top of video
            StopMpvBackgroundVideo();
            StopBackgroundVideo();

            // Clear any background image sources
            BackgroundImageA.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImageB.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImageA.Opacity = 0.0;
            BackgroundImageB.Opacity = 0.0;
            BackgroundImageA.Source = null;
            BackgroundImageB.Source = null;

            // Solid black backdrop behind the presentation slides
            RootGrid.Background = Brushes.Black;

            SlideViewbox.Visibility = Visibility.Collapsed;
            PresentationImage.Visibility = Visibility.Visible;

            if (slideIndex >= 0 && slideIndex < doc.Slides.Count)
            {
                var slide = doc.Slides[slideIndex];
                if (!string.IsNullOrEmpty(slide.ImagePreviewPath) && File.Exists(slide.ImagePreviewPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(slide.ImagePreviewPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    PresentationImage.Source = bmp;
                }
            }
        }
        catch { }
    }

    public void NavigatePresentationSlide(int slideIndex)
    {
        if (_activeLiveDoc != null && slideIndex >= 0 && slideIndex < _activeLiveDoc.Slides.Count)
        {
            SetLivePresentation(_activeLiveDoc, slideIndex);
        }
    }

    public void CloseLivePresentation()
    {
        if (_activeLiveDoc == null && PresentationImage.Visibility == Visibility.Collapsed)
        {
            return;
        }

        PresentationImage.Visibility = Visibility.Collapsed;
        PresentationImage.Source = null;
        SlideViewbox.Visibility = Visibility.Visible;
        _activeLiveDoc = null;
        _currentActiveBg = null;

        // Reapply song/scripture background
        if (_lastState != null)
        {
            ApplyBackground(_lastState.ActiveBackground, _lastState.SongTransition, 250);
        }
    }
}
