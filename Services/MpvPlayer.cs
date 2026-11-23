using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace LuxPresenter.Services;

public class MpvPlayer : IDisposable
{
    private IntPtr _ctx = IntPtr.Zero;
    private readonly DispatcherTimer _pollTimer;
    private bool _isDisposed = false;
    private bool _isLooping = true;
    private bool _isMuted = false;
    private bool _stretchFill = false;
    private string? _currentFilePath;

    public bool IsPlaying { get; private set; }
    public long Length { get; private set; } = 0;
    public float Position { get; private set; } = 0f;
    public int DroppedFrames { get; private set; } = 0;
    public string? HwDecCurrent { get; private set; }
    public string? VideoCodec { get; private set; }
    public double EstimatedFps { get; private set; } = 0;

    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            _isLooping = value;
            if (_ctx != IntPtr.Zero)
            {
                MpvNative.mpv_set_property_string(_ctx, "loop-file", value ? "inf" : "no");
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_ctx != IntPtr.Zero)
            {
                MpvNative.mpv_set_property_string(_ctx, "mute", value ? "yes" : "no");
            }
        }
    }

    public bool StretchFill
    {
        get => _stretchFill;
        set
        {
            _stretchFill = value;
            if (_ctx != IntPtr.Zero)
            {
                // keepaspect=no forces video to stretch and fill entire window matching monitor aspect ratio
                MpvNative.mpv_set_property_string(_ctx, "keepaspect", value ? "no" : "yes");
            }
        }
    }

    public event Action<TimeSpan, TimeSpan>? TimeChanged;
    public event Action<float>? PositionChanged;
    public event Action? EndReached;

    public MpvPlayer(IntPtr hwnd)
    {
        _ctx = MpvNative.mpv_create();
        if (_ctx == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create libmpv instance.");
        }

        // Configure options before mpv_initialize
        MpvNative.mpv_set_option_string(_ctx, "wid", hwnd.ToInt64().ToString());
        MpvNative.mpv_set_option_string(_ctx, "vo", "gpu");
        MpvNative.mpv_set_option_string(_ctx, "gpu-context", "d3d11");
        MpvNative.mpv_set_option_string(_ctx, "hwdec", "auto-safe");
        // Ultra-low GPU and CPU overhead settings for potato laptop iGPUs (e.g. Intel HD 5500)
        MpvNative.mpv_set_option_string(_ctx, "profile", "fast");
        MpvNative.mpv_set_option_string(_ctx, "scale", "bilinear");
        MpvNative.mpv_set_option_string(_ctx, "cscale", "bilinear");
        MpvNative.mpv_set_option_string(_ctx, "dscale", "bilinear");
        MpvNative.mpv_set_option_string(_ctx, "d3d11-flip", "no");
        MpvNative.mpv_set_option_string(_ctx, "d3d11-exclusive-fs", "no");
        MpvNative.mpv_set_option_string(_ctx, "vd-lavc-threads", "2");
        MpvNative.mpv_set_option_string(_ctx, "keep-open", "yes");
        MpvNative.mpv_set_option_string(_ctx, "idle", "yes");
        MpvNative.mpv_set_option_string(_ctx, "force-window", "yes");
        MpvNative.mpv_set_option_string(_ctx, "cursor-autohide", "always");
        MpvNative.mpv_set_option_string(_ctx, "osc", "no"); // Disable default on-screen controller
        MpvNative.mpv_set_option_string(_ctx, "osd-level", "0"); // Disable OSD messages

        int initResult = MpvNative.mpv_initialize(_ctx);
        if (initResult < 0)
        {
            throw new InvalidOperationException($"mpv_initialize failed with code: {initResult}");
        }

        // Setup timer to poll position and duration
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _pollTimer.Tick += OnPollTick;
    }

    public void Play(string filePath)
    {
        if (_isDisposed || _ctx == IntPtr.Zero || string.IsNullOrWhiteSpace(filePath)) return;

        _currentFilePath = filePath;
        string normalized = filePath.Replace('\\', '/');

        // Apply looping, mute, and stretch options
        MpvNative.mpv_set_property_string(_ctx, "loop-file", _isLooping ? "inf" : "no");
        MpvNative.mpv_set_property_string(_ctx, "mute", _isMuted ? "yes" : "no");
        MpvNative.mpv_set_property_string(_ctx, "aid", _isMuted ? "no" : "auto"); // Disable audio decoding when muted to save CPU on dual-core laptops
        MpvNative.mpv_set_property_string(_ctx, "keepaspect", _stretchFill ? "no" : "yes");
        MpvNative.mpv_command_string(_ctx, $"loadfile \"{normalized}\" replace");
        MpvNative.mpv_set_property_string(_ctx, "pause", "no");

        IsPlaying = true;
        if (!_pollTimer.IsEnabled) _pollTimer.Start();
    }

    public void Play()
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;
        MpvNative.mpv_set_property_string(_ctx, "pause", "no");
        IsPlaying = true;
        if (!_pollTimer.IsEnabled) _pollTimer.Start();
    }

    public void SetPause(bool pause)
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;
        MpvNative.mpv_set_property_string(_ctx, "pause", pause ? "yes" : "no");
        IsPlaying = !pause;
    }

    public void Stop()
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;
        _pollTimer.Stop();
        MpvNative.mpv_command_string(_ctx, "stop");
        IsPlaying = false;
        Position = 0;
        Length = 0;
        _currentFilePath = null;
    }

    public bool IsFirstFrameReady()
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return false;
        string? fc = MpvNative.GetPropertyString(_ctx, "video-frame-count");
        if (!string.IsNullOrEmpty(fc) && long.TryParse(fc, out long count) && count >= 1)
        {
            return true;
        }
        string? pos = MpvNative.GetPropertyString(_ctx, "time-pos");
        if (!string.IsNullOrEmpty(pos))
        {
            return true;
        }
        return false;
    }

    private bool _isSeeking = false;
    private double _seekTargetSec = 0.0;
    private DateTime _seekTime = DateTime.MinValue;

    public void Seek(double percent)
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;
        var p = Math.Clamp(percent, 0.0, 100.0);
        _isSeeking = true;
        _seekTime = DateTime.UtcNow;
        if (Length > 0)
        {
            _seekTargetSec = (Length / 1000.0) * (p / 100.0);
        }
        MpvNative.mpv_command_string(_ctx, $"seek {p.ToString("F2", CultureInfo.InvariantCulture)} absolute-percent+exact");
    }

    public void SetVolume(int volume)
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;
        int v = Math.Clamp(volume, 0, 100);
        MpvNative.mpv_set_property_string(_ctx, "volume", v.ToString(CultureInfo.InvariantCulture));
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_isDisposed || _ctx == IntPtr.Zero) return;

        // For looping muted background video, skip unnecessary polling to keep WPF UI thread 100% free of stutter!
        bool hasListeners = TimeChanged != null || PositionChanged != null;
        if (_isMuted && _isLooping && !hasListeners)
        {
            return;
        }

        var durStr = MpvNative.GetPropertyString(_ctx, "duration");
        var timeStr = MpvNative.GetPropertyString(_ctx, "time-pos");
        var eofStr = MpvNative.GetPropertyString(_ctx, "eof-reached");

        if (double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double durSec) && durSec > 0)
        {
            Length = (long)(durSec * 1000);

            if (double.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double curSec))
            {
                if (_isSeeking)
                {
                    double elapsedSec = (DateTime.UtcNow - _seekTime).TotalSeconds;
                    // If less than 1.2s since seek and current position hasn't converged near target, ignore stale pre-seek position!
                    if (elapsedSec < 1.2 && Math.Abs(curSec - _seekTargetSec) > 2.0)
                    {
                        return;
                    }
                    _isSeeking = false;
                }

                Position = (float)Math.Clamp(curSec / durSec, 0.0, 1.0);
                TimeChanged?.Invoke(TimeSpan.FromSeconds(curSec), TimeSpan.FromSeconds(durSec));
                PositionChanged?.Invoke(Position);
            }
        }

        if (string.Equals(eofStr, "yes", StringComparison.OrdinalIgnoreCase))
        {
            if (!_isLooping)
            {
                IsPlaying = false;
                _pollTimer.Stop();
                EndReached?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _pollTimer.Stop();
        if (_ctx != IntPtr.Zero)
        {
            IntPtr ctx = _ctx;
            _ctx = IntPtr.Zero;
            try
            {
                MpvNative.mpv_terminate_destroy(ctx);
            }
            catch { }
        }
    }
}
