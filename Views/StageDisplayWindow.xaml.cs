using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class StageDisplayWindow : Window
{
    private readonly DispatcherTimer _clockTimer;
    private bool _isCustomFullscreen = false;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;

    public StageDisplayWindow()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
        UpdateClock();

        KeyDown += OnStageWindowKeyDown;
        MouseDoubleClick += OnStageWindowMouseDoubleClick;
        Closed += OnStageWindowClosed;
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    private void UpdateClock()
    {
        TxtClock.Text = DateTime.Now.ToString("hh:mm:ss tt");
    }

    private void OnStageWindowClosed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();
    }

    private void OnStageWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isCustomFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
    }

    private void OnStageWindowMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && !IsClickOnButton(dep))
        {
            ToggleFullscreen();
        }
    }

    private bool IsClickOnButton(DependencyObject dep)
    {
        DependencyObject? current = dep;
        while (current != null)
        {
            if (current is System.Windows.Controls.Button) return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    public void ToggleFullscreen()
    {
        if (_isCustomFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    public void EnterFullscreen()
    {
        if (_isCustomFullscreen) return;
        _previousWindowState = WindowState;
        _previousWindowStyle = WindowStyle;

        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        _isCustomFullscreen = true;
        BtnFullscreen.Content = "🗗";
    }

    public void ExitFullscreen()
    {
        if (!_isCustomFullscreen) return;
        WindowStyle = _previousWindowStyle;
        WindowState = _previousWindowState;
        _isCustomFullscreen = false;
        BtnFullscreen.Content = "⛶";
    }

    private void OnFullscreenToggleClick(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnDismissAlertClick(object sender, RoutedEventArgs e)
    {
        StageAlertBanner.Visibility = Visibility.Collapsed;
    }

    public void UpdateState(ProjectionState state)
    {
        // 1. Status Badges
        BadgeBlackout.Visibility = state.IsBlack ? Visibility.Visible : Visibility.Collapsed;
        BadgeClear.Visibility = state.IsClear ? Visibility.Visible : Visibility.Collapsed;
        BadgeLive.Visibility = state.IsLive ? Visibility.Visible : Visibility.Collapsed;

        // 2. Active Item Title & Section
        if (!string.IsNullOrWhiteSpace(state.ActiveItemTitle))
        {
            TxtItemTitle.Text = state.ActiveItemTitle;
        }
        else if (state.ActiveSlide != null && !string.IsNullOrWhiteSpace(state.ActiveSlide.Label))
        {
            TxtItemTitle.Text = state.ActiveSlide.Label;
        }
        else
        {
            TxtItemTitle.Text = "Live Presentation";
        }

        if (state.ActiveSlide != null && !string.IsNullOrWhiteSpace(state.ActiveSlide.Label))
        {
            BadgeSection.Visibility = Visibility.Visible;
            TxtCurrentLabel.Text = state.ActiveSlide.Label;
        }
        else
        {
            BadgeSection.Visibility = Visibility.Collapsed;
        }

        // 3. Current Slide Lyrics
        if (state.IsBlack)
        {
            TxtCurrentSlideText.Text = "[ BLACKOUT ACTIVE ]\nScreen is currently black";
            TxtCurrentSlideText.Foreground = System.Windows.Media.Brushes.Gray;
        }
        else if (state.IsClear)
        {
            TxtCurrentSlideText.Text = "[ CLEAR ACTIVE ]\nLyrics text is cleared on screen";
            TxtCurrentSlideText.Foreground = System.Windows.Media.Brushes.Gray;
        }
        else if (state.ActiveSlide != null && !string.IsNullOrWhiteSpace(state.ActiveSlide.Content))
        {
            TxtCurrentSlideText.Text = state.ActiveSlide.Content;
            TxtCurrentSlideText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFE, 0xF0, 0x8A)); // Soft bright yellow
        }
        else if (state.IsVideoMode && !string.IsNullOrWhiteSpace(state.MediaFilePath))
        {
            TxtCurrentSlideText.Text = $"▶ [ MEDIA PLAYBACK ]\n{System.IO.Path.GetFileName(state.MediaFilePath)}";
            TxtCurrentSlideText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8)); // Sky blue
        }
        else
        {
            TxtCurrentSlideText.Text = "— No Active Slide —";
            TxtCurrentSlideText.Foreground = System.Windows.Media.Brushes.Gray;
        }

        // 4. Next Slide Preview
        if (state.ActiveNextSlide != null && !string.IsNullOrWhiteSpace(state.ActiveNextSlide.Content))
        {
            TxtNextSlideText.Text = state.ActiveNextSlide.Content;
            TxtNextSlideText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCB, 0xD5, 0xE1)); // Slate gray
            TxtNextLabelBadge.Text = !string.IsNullOrWhiteSpace(state.ActiveNextSlide.Label) ? $"[{state.ActiveNextSlide.Label}]" : "";
            TxtNextLabelBadge.Visibility = Visibility.Visible;
        }
        else
        {
            TxtNextSlideText.Text = "(End of Song / Last Slide)";
            TxtNextSlideText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B)); // Dim slate
            TxtNextLabelBadge.Visibility = Visibility.Collapsed;
        }

        // 5. Stage Alert Message
        if (!string.IsNullOrWhiteSpace(state.StageMessage))
        {
            TxtStageAlertMessage.Text = state.StageMessage;
            StageAlertBanner.Visibility = Visibility.Visible;
        }
        else
        {
            StageAlertBanner.Visibility = Visibility.Collapsed;
        }
    }

    public void SyncPositionWithScreen(ScreenInfo screen)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Normal;
        Left = screen.DipLeft;
        Top = screen.DipTop;
        Width = screen.DipWidth;
        Height = screen.DipHeight;
        _isCustomFullscreen = true;
        BtnFullscreen.Content = "🗗";

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var hwnd = helper.EnsureHandle();
        if (hwnd != IntPtr.Zero)
        {
            DisplayHelper.PositionWindowOnScreen(this, screen);
        }
    }
}
