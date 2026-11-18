using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuxPresenter.Models;
using LuxPresenter.Services;
using LuxPresenter.Services.Presentation;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class SlidePaneView : UserControl
{
    private SlideViewerViewModel? _vm;
    private MpvPlayer? _mpvPlayer;
    private string? _currentMpvMediaPath;
    private AppSettings _settings = new();

    public SlidePaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        MediaSlider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
        {
            StartSliderDrag();
        }), true);

        MediaSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent, new System.Windows.Controls.Primitives.DragStartedEventHandler((s, e) =>
        {
            StartSliderDrag();
        }), true);

        MediaSlider.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
        {
            FinishSliderDrag();
        }), true);

        MediaSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent, new System.Windows.Controls.Primitives.DragCompletedEventHandler((s, e) =>
        {
            FinishSliderDrag();
        }), true);

        MediaSlider.PreviewMouseUp += (s, e) =>
        {
            FinishSliderDrag();
        };

        MediaSlider.LostMouseCapture += (s, e) =>
        {
            FinishSliderDrag();
        };

        MediaSlider.ValueChanged += (s, e) =>
        {
            if (_isUserDraggingSlider && _vm != null)
            {
                _vm.CurrentPosition = e.NewValue;
                UpdateDragTimeDisplay(e.NewValue);
            }
        };

        StanzaListBox.SelectionChanged += (s, e) => ScrollSelectedSlideIntoView();

        IsVisibleChanged += (s, e) =>
        {
            if ((bool)e.NewValue == false)
            {
                PlayerMpvView.Visibility = Visibility.Collapsed;
                PlayerMediaElement.Visibility = Visibility.Collapsed;
            }
            else if (_vm != null && _vm.IsMediaMode)
            {
                UpdatePlayer();
            }
        };
    }

    private void StartSliderDrag()
    {
        if (_isUserDraggingSlider) return;
        _isUserDraggingSlider = true;
        if (_vm != null)
        {
            _vm.IsDraggingSlider = true;
            _wasPlayingBeforeDrag = _vm.IsPlaying;
            if (_wasPlayingBeforeDrag)
            {
                _vm.IsPlaying = false;
            }
        }
    }

    private void OnVmScrollRequested(int index)
    {
        ScrollSelectedSlideIntoView();
    }

    public void ScrollSelectedSlideIntoView()
    {
        if (StanzaListBox.SelectedItem != null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (StanzaListBox.SelectedItem != null)
                {
                    StanzaListBox.ScrollIntoView(StanzaListBox.SelectedItem);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private bool _isUserDraggingSlider = false;
    private bool _wasPlayingBeforeDrag = false;

    private void FinishSliderDrag()
    {
        if (!_isUserDraggingSlider) return;
        _isUserDraggingSlider = false;

        if (_vm != null)
        {
            double targetPercent = Math.Clamp(MediaSlider.Value, 0.0, 100.0);
            _vm.CurrentPosition = targetPercent;

            if (!_vm.IsLiveMode)
            {
                if (_useMediaElementFallback && PlayerMediaElement.NaturalDuration.HasTimeSpan)
                {
                    var dur = PlayerMediaElement.NaturalDuration.TimeSpan;
                    PlayerMediaElement.Position = TimeSpan.FromSeconds(dur.TotalSeconds * (targetPercent / 100.0));
                }
                else
                {
                    _mpvPlayer?.Seek(targetPercent);
                }
            }
            else
            {
                _vm.RequestSeek(targetPercent);
            }

            if (_wasPlayingBeforeDrag)
            {
                _vm.IsPlaying = true;
            }

            // Keep IsDraggingSlider true for 800ms so incoming progress polls from mpv don't overwrite
            // or fight with the slider before mpv has finished seeking to the new position.
            var targetVm = _vm;
            Task.Delay(800).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_isUserDraggingSlider && targetVm != null)
                    {
                        targetVm.IsDraggingSlider = false;
                    }
                });
            });
        }
    }

    private void UpdateDragTimeDisplay(double percent)
    {
        if (_vm == null) return;
        var p = Math.Clamp(percent, 0.0, 100.0);

        if (_vm.IsLiveMode)
        {
            var parts = _vm.TimeDisplayText.Split('/');
            if (parts.Length == 2)
            {
                string totalStr = parts[1].Trim();
                var timeParts = totalStr.Split(':');
                if (timeParts.Length == 2 &&
                    int.TryParse(timeParts[0], out int totalMin) &&
                    int.TryParse(timeParts[1], out int totalSec))
                {
                    double totalSeconds = totalMin * 60 + totalSec;
                    double curSeconds = totalSeconds * (p / 100.0);
                    int curMin = (int)curSeconds / 60;
                    int curSec = (int)curSeconds % 60;
                    _vm.TimeDisplayText = $"{curMin}:{curSec:D2} / {totalStr}";
                }
            }
        }
        else
        {
            var totalMs = _mpvPlayer?.Length ?? 0;
            if (totalMs > 0)
            {
                var cur = TimeSpan.FromMilliseconds(totalMs * (p / 100.0));
                var total = TimeSpan.FromMilliseconds(totalMs);
                _vm.TimeDisplayText = $"{cur.Minutes}:{cur.Seconds:D2} / {total.Minutes}:{total.Seconds:D2}";
            }
        }
    }

    private void OnSeekRequested(double percent)
    {
        if (_vm?.IsLiveMode == false)
        {
            _mpvPlayer?.Seek(percent);
        }
    }

    public event Action<SlidePaneView, SlideViewerViewModel>? SlideActivated;
    public event Action<SlidePaneView, SlideViewerViewModel>? SlidePrepared;

    private void OnSlideItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is Slide slide && _vm != null)
        {
            int idx = _vm.Slides.IndexOf(slide);
            if (idx >= 0)
            {
                _vm.SelectedIndex = idx;
                _vm.RequestGoLiveForSlide(slide, idx);
            }
            e.Handled = true;
        }
    }

    private void OnSlideItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) return;

        if (sender is ListBoxItem item && item.DataContext is Slide slide)
        {
            int idx = _vm.Slides.IndexOf(slide);
            if (idx >= 0)
            {
                _vm.SelectedIndex = idx;
                _vm.IsActivePane = true;
                SlideActivated?.Invoke(this, _vm);
            }
        }
    }

    private void OnSlideItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null || _vm.IsLiveMode) return;

        if (sender is ListBoxItem item && item.DataContext is Slide slide)
        {
            int idx = _vm.Slides.IndexOf(slide);
            if (idx >= 0)
            {
                // Prepare only: select target in Preview visually (gray), do NOT steal focus from Live
                _vm.SelectedIndex = idx;
                _vm.IsActivePane = false;
                SlidePrepared?.Invoke(this, _vm);

                // Crucial: mark handled so WPF does not give keyboard focus to this ListBoxItem or open menus
                e.Handled = true;
            }
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (e.ChangedButton == MouseButton.Left && _vm != null)
        {
            _vm.IsActivePane = true;
            SlideActivated?.Invoke(this, _vm);
        }
    }

    private void UpdatePresentationDoc()
    {
        // Visual updates are handled automatically via ImagePreviewPath binding on PresPreviewViewbox and PresLiveViewbox
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.SeekRequested -= OnSeekRequested;
            _vm.ScrollRequested -= OnVmScrollRequested;
            if (_vm.IsLiveMode)
            {
                AlertService.Instance.AlertStateChanged -= OnLiveAlertStateChanged;
            }
        }

        _vm = e.NewValue as SlideViewerViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.SeekRequested += OnSeekRequested;
            _vm.ScrollRequested += OnVmScrollRequested;
            if (_vm.IsLiveMode)
            {
                AlertService.Instance.AlertStateChanged += OnLiveAlertStateChanged;
                UpdateLiveAlertMarquee();
            }
            UpdateBackground();
            UpdatePlayer();
            UpdateSlideContent();
            UpdatePresentationDoc();
            UpdateClearState();
            UpdateBlackState();
            ScrollSelectedSlideIntoView();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SlideViewerViewModel.Background))
        {
            UpdateBackground();
        }
        else if (e.PropertyName == nameof(SlideViewerViewModel.CurrentSlide))
        {
            UpdateSlideContent();
        }
        else if (e.PropertyName == nameof(SlideViewerViewModel.CurrentPresentationSlide) ||
                 e.PropertyName == nameof(SlideViewerViewModel.IsPresentationDocMode))
        {
            UpdatePresentationDoc();
        }
        else if (e.PropertyName == nameof(SlideViewerViewModel.IsClear))
        {
            UpdateClearState();
        }
        else if (e.PropertyName == nameof(SlideViewerViewModel.IsBlack))
        {
            UpdateBlackState();
        }
        else if (e.PropertyName == nameof(SlideViewerViewModel.MediaFilePath) ||
                 e.PropertyName == nameof(SlideViewerViewModel.IsPlaying) ||
                 e.PropertyName == nameof(SlideViewerViewModel.IsMediaMode) ||
                 e.PropertyName == nameof(SlideViewerViewModel.Volume))
        {
            UpdatePlayer();
            if (e.PropertyName == nameof(SlideViewerViewModel.IsMediaMode) && _vm?.IsMediaMode == false)
            {
                UpdateBackground();
            }
        }
    }

    private int _activeBgLayer = 0;
    private string? _currentBg;

    private void UpdateBackground()
    {
        if (_vm == null) return;
        var bg = _vm.Background;

        if (_currentBg == bg && !string.IsNullOrEmpty(bg)) return;
        _currentBg = bg;

        if (string.IsNullOrWhiteSpace(bg) || bg.StartsWith("#"))
        {
            BackgroundImageA.Visibility = Visibility.Collapsed;
            BackgroundImageB.Visibility = Visibility.Collapsed;
            BackgroundRect.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(bg) && bg.StartsWith("#"))
            {
                try
                {
                    BackgroundRect.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
                }
                catch { }
            }
            return;
        }

        if (File.Exists(bg))
        {
            bool isVideo = ThumbnailService.IsVideoFile(bg);

            ImageSource? src = null;
            if (isVideo)
            {
                src = ThumbnailService.GetVideoThumbnail(bg);
            }
            else
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(bg, UriKind.Absolute);
                    bmp.EndInit();
                    if (bmp.CanFreeze) bmp.Freeze();
                    src = bmp;
                }
                catch { }
            }

            if (src != null)
            {
                int dur = _settings?.TransitionDurationMs > 0 ? _settings.TransitionDurationMs : 250;
                bool useBlend = _vm.IsLiveMode && string.Equals(_settings?.ScreenTransition, "Blend", StringComparison.OrdinalIgnoreCase);

                if (useBlend && dur > 0)
                {
                    int nextLayer = 1 - _activeBgLayer;
                    var incoming = nextLayer == 0 ? BackgroundImageA : BackgroundImageB;
                    var outgoing = _activeBgLayer == 0 ? BackgroundImageA : BackgroundImageB;

                    incoming.Source = src;
                    incoming.Visibility = Visibility.Visible;
                    Panel.SetZIndex(incoming, 2);
                    Panel.SetZIndex(outgoing, 1);

                    var animIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(dur))
                    {
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };
                    var animOut = new System.Windows.Media.Animation.DoubleAnimation(outgoing.Opacity, 0.0, TimeSpan.FromMilliseconds(dur))
                    {
                        FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
                    };

                    var outRef = outgoing;
                    animOut.Completed += (s, e) =>
                    {
                        outRef.BeginAnimation(UIElement.OpacityProperty, null);
                        outRef.Opacity = 0.0;
                        outRef.Visibility = Visibility.Collapsed;
                        if (_activeBgLayer != (outRef == BackgroundImageA ? 0 : 1))
                        {
                            outRef.Source = null;
                        }
                    };

                    incoming.BeginAnimation(UIElement.OpacityProperty, animIn);
                    outgoing.BeginAnimation(UIElement.OpacityProperty, animOut);
                    _activeBgLayer = nextLayer;
                }
                else
                {
                    BackgroundImageA.BeginAnimation(UIElement.OpacityProperty, null);
                    BackgroundImageB.BeginAnimation(UIElement.OpacityProperty, null);
                    BackgroundImageA.Source = src;
                    BackgroundImageA.Opacity = 1.0;
                    BackgroundImageA.Visibility = Visibility.Visible;
                    BackgroundImageB.Opacity = 0.0;
                    BackgroundImageB.Visibility = Visibility.Collapsed;
                    BackgroundImageB.Source = null;
                    _activeBgLayer = 0;
                }
                BackgroundRect.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackgroundImageA.Visibility = Visibility.Collapsed;
                BackgroundImageB.Visibility = Visibility.Collapsed;
                BackgroundRect.Visibility = Visibility.Visible;
            }
        }
        else
        {
            BackgroundImageA.Visibility = Visibility.Collapsed;
            BackgroundImageB.Visibility = Visibility.Collapsed;
            BackgroundRect.Visibility = Visibility.Visible;
        }
    }

    private void EnsureMpvPlayer()
    {
        if (_vm?.IsLiveMode == true) return;
        if (_mpvPlayer != null) return;

        if (PlayerMpvView.Player != null)
        {
            _mpvPlayer = PlayerMpvView.Player;
            WireMpvPlayerEvents();
        }
        else
        {
            PlayerMpvView.PlayerReady += p =>
            {
                _mpvPlayer = p;
                WireMpvPlayerEvents();
                if (_vm != null && _vm.IsMediaMode && !string.IsNullOrWhiteSpace(_currentMpvMediaPath))
                {
                    _mpvPlayer.IsLooping = _vm.IsLooping;
                    _mpvPlayer.IsMuted = _vm.IsLiveMode; // Mute live pane to avoid echo with projector window
                    _mpvPlayer.Play(_currentMpvMediaPath);
                    if (!_vm.IsLiveMode)
                    {
                        _mpvPlayer.SetVolume((int)Math.Clamp(_vm.Volume * 100.0, 0, 100));
                    }
                    if (!_vm.IsPlaying) _mpvPlayer.SetPause(true);
                }
            };
        }
    }

    private void WireMpvPlayerEvents()
    {
        if (_mpvPlayer == null) return;

        // In Live mode, ProjectionService is the single master clock for position and duration.
        // We do NOT attach PositionChanged or TimeChanged here in Live mode,
        // so the local preview player will NEVER overwrite the master position or fight with the slider!
        if (_vm?.IsLiveMode == true)
        {
            _mpvPlayer.EndReached += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (_vm != null && !_vm.IsLooping)
                    {
                        _vm.IsPlaying = false;
                        _vm.CurrentPosition = 0;
                    }
                });
            };
            return;
        }

        _mpvPlayer.PositionChanged += pos =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_isUserDraggingSlider && _vm != null && _vm.IsPlaying)
                {
                    _vm.CurrentPosition = pos * 100.0;
                }
            });
        };

        _mpvPlayer.TimeChanged += (cur, total) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_vm != null)
                {
                    _vm.TimeDisplayText = $"{cur.Minutes}:{cur.Seconds:D2} / {total.Minutes}:{total.Seconds:D2}";
                }
            });
        };

        _mpvPlayer.EndReached += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_vm != null && !_vm.IsLooping)
                {
                    _vm.IsPlaying = false;
                    _vm.CurrentPosition = 0;
                }
            });
        };
    }

    private bool _useMediaElementFallback = false;
    private System.Windows.Threading.DispatcherTimer? _mediaElementPollTimer;

    private void EnsureMediaElementFallback()
    {
        if (_mediaElementPollTimer == null)
        {
            _mediaElementPollTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _mediaElementPollTimer.Tick += (s, e) =>
            {
                if (_vm != null && !_isUserDraggingSlider && PlayerMediaElement.NaturalDuration.HasTimeSpan)
                {
                    var cur = PlayerMediaElement.Position;
                    var dur = PlayerMediaElement.NaturalDuration.TimeSpan;
                    if (dur.TotalSeconds > 0)
                    {
                        _vm.CurrentPosition = (cur.TotalSeconds / dur.TotalSeconds) * 100.0;
                        _vm.TimeDisplayText = $"{cur.Minutes}:{cur.Seconds:D2} / {dur.Minutes}:{dur.Seconds:D2}";
                    }
                }
            };
        }
    }

    private void UpdatePlayer()
    {
        if (_vm == null) return;
        if (_vm.IsLiveMode) return;

        if (_vm.IsMediaMode && !string.IsNullOrWhiteSpace(_vm.MediaFilePath) && File.Exists(_vm.MediaFilePath))
        {
            bool useLibMpv = string.Equals(_settings?.VideoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase);

            if (useLibMpv)
            {
                EnsureMpvPlayer();
            }

            // Fallback to MediaElement if libmpv is unavailable or disabled in settings
            if (!useLibMpv || _mpvPlayer == null)
            {
                _useMediaElementFallback = true;
                EnsureMediaElementFallback();

                PlayerMpvView.Visibility = Visibility.Collapsed;
                PlayerMediaElement.Visibility = Visibility.Visible;

                if (_currentMpvMediaPath != _vm.MediaFilePath)
                {
                    _currentMpvMediaPath = _vm.MediaFilePath;
                    PlayerMediaElement.Source = new Uri(_currentMpvMediaPath, UriKind.Absolute);
                    PlayerMediaElement.Position = TimeSpan.Zero;
                    if (_vm.IsPlaying)
                    {
                        PlayerMediaElement.Play();
                        _mediaElementPollTimer?.Start();
                    }
                    else
                    {
                        PlayerMediaElement.Pause();
                    }
                }
                else
                {
                    if (_vm.IsPlaying)
                    {
                        PlayerMediaElement.Play();
                        _mediaElementPollTimer?.Start();
                    }
                    else
                    {
                        PlayerMediaElement.Pause();
                    }
                }
                return;
            }

            // Otherwise, standard libmpv hardware-accelerated playback
            _useMediaElementFallback = false;
            PlayerMediaElement.Visibility = Visibility.Collapsed;
            PlayerMediaElement.Stop();
            _mediaElementPollTimer?.Stop();
            PlayerMpvView.Visibility = Visibility.Visible;

            if (_currentMpvMediaPath != _vm.MediaFilePath)
            {
                _currentMpvMediaPath = _vm.MediaFilePath;
                if (_mpvPlayer != null)
                {
                    _mpvPlayer.IsLooping = _vm.IsLooping;
                    _mpvPlayer.IsMuted = _vm.IsLiveMode;
                    _mpvPlayer.Play(_currentMpvMediaPath);
                    if (!_vm.IsLiveMode)
                    {
                        _mpvPlayer.SetVolume((int)Math.Clamp(_vm.Volume * 100.0, 0, 100));
                    }
                    if (!_vm.IsPlaying) _mpvPlayer.SetPause(true);
                }
            }
            else if (_mpvPlayer != null)
            {
                _mpvPlayer.IsLooping = _vm.IsLooping;
                if (!_vm.IsLiveMode)
                {
                    _mpvPlayer.SetVolume((int)Math.Clamp(_vm.Volume * 100.0, 0, 100));
                }

                if (_vm.IsPlaying && !_mpvPlayer.IsPlaying)
                {
                    _mpvPlayer.Play();
                }
                else if (!_vm.IsPlaying && _mpvPlayer.IsPlaying)
                {
                    _mpvPlayer.SetPause(true);
                }
            }
        }
        else
        {
            if (_currentMpvMediaPath != null)
            {
                _currentMpvMediaPath = null;
                _mpvPlayer?.Stop();
                PlayerMediaElement.Stop();
                PlayerMediaElement.Source = null;
                _mediaElementPollTimer?.Stop();
                MemoryHelper.TrimMemoryAsync();
            }
        }
    }

    public void ApplyFormatting(AppSettings settings)
    {
        _settings = settings;
        ApplyFormatting(settings.SongVerticalAlignment, settings.SongTextAlignment);
        UpdateSlideContent();
    }

    public void ApplyFormatting(string? verticalAlign, string? textAlign)
    {
        if (string.Equals(verticalAlign, "Top", StringComparison.OrdinalIgnoreCase))
        {
            ContentViewbox.VerticalAlignment = VerticalAlignment.Top;
        }
        else if (string.Equals(verticalAlign, "Bottom", StringComparison.OrdinalIgnoreCase))
        {
            ContentViewbox.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else
        {
            ContentViewbox.VerticalAlignment = VerticalAlignment.Center;
        }

        if (string.Equals(textAlign, "Left", StringComparison.OrdinalIgnoreCase))
        {
            ContentTextBlock.TextAlignment = TextAlignment.Left;
            ContentTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else if (string.Equals(textAlign, "Right", StringComparison.OrdinalIgnoreCase))
        {
            ContentTextBlock.TextAlignment = TextAlignment.Right;
            ContentTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            ContentTextBlock.TextAlignment = TextAlignment.Center;
            ContentTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    private void UpdateSlideContent()
    {
        if (_vm?.CurrentSlide == null)
        {
            ContentTextBlock.Text = string.Empty;
            return;
        }

        var slide = _vm.CurrentSlide;
        var formatted = LyricsFormatter.Format(slide.Content, _settings);

        void ApplySlideTypography()
        {
            var fontFamily = new FontFamily(string.IsNullOrWhiteSpace(slide.FontFamily) ? (_settings?.SongFontFamily ?? "Arial") : slide.FontFamily);
            var fontWeight = (_settings?.SongFontBold == true || slide.IsBold) ? FontWeights.Bold : FontWeights.Normal;
            var fontStyle = slide.IsItalic ? FontStyles.Italic : FontStyles.Normal;

            ContentTextBlock.FontFamily = fontFamily;
            ContentTextBlock.FontWeight = fontWeight;
            ContentTextBlock.FontStyle = fontStyle;
            ContentTextBlock.HasOutline = slide.HasOutline;
            ContentTextBlock.HasShadow = slide.HasShadow;

            int basePt = (_settings?.SongFontSize > 0) ? _settings.SongFontSize : (slide.FontSize > 0 ? slide.FontSize : 13);
            double maxTargetFs = basePt > 0 ? (basePt * 6.5) : 84.5;

            double optimalFullFs = OutlinedTextBlock.CalculateOptimalFontSize(
                formatted,
                fontFamily,
                fontWeight,
                fontStyle,
                1700,
                840,
                maxTargetFs,
                minFs: 34.0);

            // Scale precisely for preview (640 / 1920 = 1/3)
            ContentTextBlock.FontSize = optimalFullFs * (640.0 / 1920.0);
        }

        ContentViewbox.BeginAnimation(UIElement.OpacityProperty, null);
        ContentViewbox.Opacity = _vm.IsClear ? 0.0 : 1.0;
        ContentTextBlock.Text = formatted;
        ApplySlideTypography();
    }

    private void UpdateClearState()
    {
        if (_vm == null) return;
        ContentViewbox.BeginAnimation(UIElement.OpacityProperty, null);
        ContentViewbox.Opacity = _vm.IsClear ? 0.0 : 1.0;
    }

    private void UpdateBlackState()
    {
        if (_vm == null) return;
        int duration = _settings.TransitionDurationMs > 0 ? _settings.TransitionDurationMs : 250;

        if (_vm.IsBlack)
        {
            BlackOverlayRect.Visibility = Visibility.Visible;
            if (string.Equals(_settings.ScreenTransition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(duration));
                BlackOverlayRect.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            else
            {
                BlackOverlayRect.BeginAnimation(UIElement.OpacityProperty, null);
                BlackOverlayRect.Opacity = 1.0;
            }
        }
        else
        {
            if (string.Equals(_settings.ScreenTransition, "Blend", StringComparison.OrdinalIgnoreCase))
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(duration));
                anim.Completed += (s, e) =>
                {
                    if (_vm?.IsBlack == false)
                    {
                        BlackOverlayRect.Visibility = Visibility.Collapsed;
                    }
                };
                BlackOverlayRect.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            else
            {
                BlackOverlayRect.BeginAnimation(UIElement.OpacityProperty, null);
                BlackOverlayRect.Opacity = 0.0;
                BlackOverlayRect.Visibility = Visibility.Collapsed;
            }
        }
    }

    private bool _isLiveAlertAnimating = false;

    private void OnLiveAlertStateChanged()
    {
        Dispatcher.BeginInvoke(UpdateLiveAlertMarquee);
    }

    private void UpdateLiveAlertMarquee()
    {
        if (_vm == null || !_vm.IsLiveMode) return;

        var alert = AlertService.Instance;
        if (!alert.IsAlertVisible || alert.CurrentActiveMessage == null)
        {
            LiveAlertTextBlock.BeginAnimation(Canvas.LeftProperty, null);
            LiveAlertBanner.Visibility = Visibility.Collapsed;
            _isLiveAlertAnimating = false;
            return;
        }

        LiveAlertBanner.Visibility = Visibility.Visible;
        LiveAlertTextBlock.Text = alert.CurrentActiveMessage.Text;

        if (!_isLiveAlertAnimating)
        {
            StartLiveAlertAnimation();
        }
    }

    private void StartLiveAlertAnimation()
    {
        if (_vm == null || !_vm.IsLiveMode) return;
        var alert = AlertService.Instance;
        if (!alert.IsAlertVisible || alert.CurrentActiveMessage == null)
        {
            _isLiveAlertAnimating = false;
            LiveAlertBanner.Visibility = Visibility.Collapsed;
            return;
        }

        _isLiveAlertAnimating = true;
        LiveAlertTextBlock.UpdateLayout();
        double textWidth = LiveAlertTextBlock.ActualWidth > 0 ? LiveAlertTextBlock.ActualWidth : 300;
        double screenWidth = LiveAlertBanner.ActualWidth > 0 ? LiveAlertBanner.ActualWidth : 400;

        double totalDistance = screenWidth + textWidth;
        double durationSec = Math.Max(4.0, totalDistance / 90.0);

        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = screenWidth,
            To = -textWidth,
            Duration = TimeSpan.FromSeconds(durationSec)
        };

        anim.Completed += (s, e) =>
        {
            _isLiveAlertAnimating = false;
            if (AlertService.Instance.IsAlertVisible)
            {
                StartLiveAlertAnimation();
            }
        };

        LiveAlertTextBlock.BeginAnimation(Canvas.LeftProperty, anim);
    }
}
