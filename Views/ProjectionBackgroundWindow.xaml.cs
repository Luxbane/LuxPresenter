using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class ProjectionBackgroundWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private string? _currentVideoPath;
    public bool IsBackgroundMode { get; private set; } = true;

    // Dual-Deck State: 0 = Deck A is active, 1 = Deck B is active
    private int _activeDeck = 0;
    private System.Windows.Threading.DispatcherTimer? _crossFadeTimer;

    public MpvPlayer? Player => _activeDeck == 0 ? MpvViewA.Player : MpvViewB.Player;
    public event Action<TimeSpan, TimeSpan>? VideoProgressReported;

    public ProjectionBackgroundWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        KeyDown += OnWindowKeyDown;
        Closed += OnWindowClosed;

        PreviewMouseDown += OnWindowMouseDown;
        MouseDown += OnWindowMouseDown;

        void WireDeck(Components.MpvVideoView view, int deckIdx)
        {
            view.PlayerReady += p =>
            {
                p.TimeChanged += (cur, total) =>
                {
                    if (_activeDeck == deckIdx)
                    {
                        Dispatcher.BeginInvoke(() => VideoProgressReported?.Invoke(cur, total));
                    }
                };
            };
        }

        WireDeck(MpvViewA, 0);
        WireDeck(MpvViewB, 1);
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        Application.Current?.MainWindow?.Activate();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

        // Deck A starts fully visible (255), Deck B starts hidden (0)
        MpvViewA.SetAlpha(255);
        MpvViewB.SetAlpha(0);
        MpvViewA.BringToTop();
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

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Application.Current.MainWindow?.Activate();
            Application.Current.MainWindow?.Focus();
            e.Handled = true;
            return;
        }

        // Forward remote presenter / keyboard navigation to MainWindow
        if (Application.Current.MainWindow is MainWindow mainWin && mainWin.DataContext is ViewModels.MainViewModel vm)
        {
            switch (e.Key)
            {
                case Key.PageDown:
                case Key.Space:
                case Key.Down:
                case Key.Right:
                    vm.LiveVM.NextSlideCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Up:
                case Key.Left:
                case Key.PageUp:
                    vm.LiveVM.PrevSlideCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Home:
                    vm.LiveVM.SelectFirstSlide();
                    e.Handled = true;
                    break;

                case Key.End:
                    vm.LiveVM.SelectLastSlide();
                    e.Handled = true;
                    break;

                case Key.F6:
                case Key.OemPeriod:
                    vm.BlackCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        StopVideo();
    }

    /// <summary>
    /// Plays motion background video behind lyrics (looping infinitely, audio muted, stretched to fill screen).
    /// Uses Dual-Deck crossfade so Video A seamlessly blends directly into Video B without any black flash!
    /// </summary>
    public void PlayBackgroundVideo(string filePath, int transitionDurationMs = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        IsBackgroundMode = true;
        _currentVideoPath = filePath;

        int incomingDeck = 1 - _activeDeck;
        var incomingView = incomingDeck == 0 ? MpvViewA : MpvViewB;
        var outgoingView = _activeDeck == 0 ? MpvViewA : MpvViewB;

        void LaunchPlayback()
        {
            if (incomingView.Player != null)
            {
                incomingView.Player.IsMuted = true;
                incomingView.Player.IsLooping = true;
                incomingView.Player.StretchFill = true;
                incomingView.Player.Play(filePath);

                if (transitionDurationMs > 0)
                {
                    CrossfadeDecks(incomingDeck, transitionDurationMs);
                }
                else
                {
                    _crossFadeTimer?.Stop();
                    incomingView.BringToTop();
                    incomingView.SetAlpha(255);
                    outgoingView.SetAlpha(0);
                    outgoingView.Player?.Stop();
                    _activeDeck = incomingDeck;
                }
            }
        }

        if (incomingView.Player != null)
        {
            LaunchPlayback();
        }
        else
        {
            Action<MpvPlayer>? onReady = null;
            onReady = p =>
            {
                incomingView.PlayerReady -= onReady;
                Dispatcher.BeginInvoke(new Action(LaunchPlayback));
            };
            incomingView.PlayerReady += onReady;
        }
    }

    private void CrossfadeDecks(int targetDeck, int durationMs)
    {
        _crossFadeTimer?.Stop();

        var incomingView = targetDeck == 0 ? MpvViewA : MpvViewB;
        var outgoingView = targetDeck == 0 ? MpvViewB : MpvViewA;

        // Bring the incoming deck to the top of Z-order to fade in over the outgoing deck
        incomingView.BringToTop();
        incomingView.SetAlpha(0);

        DateTime startTime = DateTime.UtcNow;
        DateTime cueStartTime = DateTime.UtcNow;
        bool isCued = false;
        byte lastAlpha = 0;

        _crossFadeTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            System.Windows.Threading.DispatcherPriority.Render,
            (s, e) =>
            {
                if (!isCued)
                {
                    // Hold alpha at 0 until frame 1 is decoded & ready on incoming deck HWND!
                    // This completely prevents the "njepat / pop tiba-tiba" on dual-core potato laptops!
                    bool ready = incomingView.Player?.IsFirstFrameReady() == true;
                    bool timedOut = (DateTime.UtcNow - cueStartTime).TotalMilliseconds >= 1200;
                    if (ready || timedOut)
                    {
                        isCued = true;
                        startTime = DateTime.UtcNow;
                    }
                    else
                    {
                        incomingView.SetAlpha(0);
                        return;
                    }
                }

                double elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                double progress = Math.Clamp(elapsedMs / durationMs, 0.0, 1.0);

                byte inAlpha = (byte)Math.Round(progress * 255.0);
                if (inAlpha != lastAlpha)
                {
                    lastAlpha = inAlpha;
                    incomingView.SetAlpha(inAlpha);
                }

                if (progress >= 1.0)
                {
                    _crossFadeTimer?.Stop();
                    incomingView.SetAlpha(255);
                    outgoingView.SetAlpha(0);
                    outgoingView.Player?.Stop();
                    _activeDeck = targetDeck;
                }
            },
            Dispatcher);

        _crossFadeTimer.Start();
    }

    /// <summary>
    /// Plays standalone media video from Media tab (unmuted audio, transport-controlled).
    /// </summary>
    public void PlayMediaVideo(string filePath, bool isPlaying, bool isLooping, int transitionDurationMs = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        IsBackgroundMode = false;
        _currentVideoPath = filePath;

        int incomingDeck = 1 - _activeDeck;
        var incomingView = incomingDeck == 0 ? MpvViewA : MpvViewB;
        var outgoingView = _activeDeck == 0 ? MpvViewA : MpvViewB;

        void LaunchPlayback()
        {
            if (incomingView.Player != null)
            {
                incomingView.Player.IsMuted = false;
                incomingView.Player.IsLooping = isLooping;
                incomingView.Player.StretchFill = false;
                incomingView.Player.Play(filePath);
                if (!isPlaying) incomingView.Player.SetPause(true);

                if (transitionDurationMs > 0)
                {
                    CrossfadeDecks(incomingDeck, transitionDurationMs);
                }
                else
                {
                    _crossFadeTimer?.Stop();
                    incomingView.BringToTop();
                    incomingView.SetAlpha(255);
                    outgoingView.SetAlpha(0);
                    outgoingView.Player?.Stop();
                    _activeDeck = incomingDeck;
                }
            }
        }

        if (incomingView.Player != null)
        {
            LaunchPlayback();
        }
        else
        {
            Action<MpvPlayer>? onReady = null;
            onReady = p =>
            {
                incomingView.PlayerReady -= onReady;
                Dispatcher.BeginInvoke(new Action(LaunchPlayback));
            };
            incomingView.PlayerReady += onReady;
        }
    }

    public void PauseVideo()
    {
        Player?.SetPause(true);
    }

    public void ResumeVideo()
    {
        Player?.Play();
    }

    public void SeekVideo(double percent)
    {
        Player?.Seek(percent);
    }

    public void SetLooping(bool isLooping)
    {
        if (Player != null)
        {
            Player.IsLooping = isLooping;
        }
    }

    public void FadeIn(int durationMs)
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
    }

    public void FadeOut(int durationMs, Action? onCompleted = null)
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
        onCompleted?.Invoke();
    }

    public void StopVideo()
    {
        _crossFadeTimer?.Stop();
        _currentVideoPath = null;
        try
        {
            MpvViewA.Player?.Stop();
            MpvViewB.Player?.Stop();
            MpvViewA.SetAlpha(0);
            MpvViewB.SetAlpha(0);
        }
        catch { }
    }
}
