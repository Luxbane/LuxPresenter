using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuxPresenter.Services;

public class ScreenInfo
{
    public int Index { get; set; }
    public double DipLeft { get; set; }
    public double DipTop { get; set; }
    public double DipWidth { get; set; }
    public double DipHeight { get; set; }

    public int PhysicalLeft { get; set; }
    public int PhysicalTop { get; set; }
    public int PhysicalWidth { get; set; }
    public int PhysicalHeight { get; set; }

    public double DpiScaleX { get; set; } = 1.0;
    public double DpiScaleY { get; set; } = 1.0;

    public bool IsPrimary { get; set; }
    public string DeviceName { get; set; } = string.Empty;

    public int Left => (int)Math.Round(DipLeft);
    public int Top => (int)Math.Round(DipTop);
    public int Width => (int)Math.Round(DipWidth);
    public int Height => (int)Math.Round(DipHeight);

    public string DisplayName => IsPrimary
        ? $"Primary Monitor ({PhysicalWidth}x{PhysicalHeight})"
        : $"Secondary Monitor ({PhysicalWidth}x{PhysicalHeight})";
}

public static class DisplayHelper
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private const uint MONITORINFOF_PRIMARY = 1;

    public static List<ScreenInfo> GetScreens()
    {
        var screens = new List<ScreenInfo>();
        int index = 0;

        uint sysDpi = 96;
        try { sysDpi = GetDpiForSystem(); } catch { }
        if (sysDpi == 0) sysDpi = 96;
        double primaryScaleX = sysDpi / 96.0;
        double primaryScaleY = sysDpi / 96.0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                uint monDpiX = sysDpi;
                uint monDpiY = sysDpi;
                try
                {
                    if (GetDpiForMonitor(hMonitor, 0, out uint dx, out uint dy) == 0 && dx > 0 && dy > 0)
                    {
                        monDpiX = dx;
                        monDpiY = dy;
                    }
                }
                catch { }

                double monScaleX = monDpiX / 96.0;
                double monScaleY = monDpiY / 96.0;

                int physLeft = mi.rcMonitor.Left;
                int physTop = mi.rcMonitor.Top;
                int physWidth = mi.rcMonitor.Right - mi.rcMonitor.Left;
                int physHeight = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

                // In WPF's coordinate space, virtual desktop offsets are scaled relative to primary monitor DPI:
                double dipLeft = physLeft / primaryScaleX;
                double dipTop = physTop / primaryScaleY;

                // Window dimensions on the target monitor are scaled by the target monitor's DPI:
                double dipWidth = physWidth / monScaleX;
                double dipHeight = physHeight / monScaleY;

                screens.Add(new ScreenInfo
                {
                    Index = index++,
                    DipLeft = dipLeft,
                    DipTop = dipTop,
                    DipWidth = dipWidth,
                    DipHeight = dipHeight,
                    PhysicalLeft = physLeft,
                    PhysicalTop = physTop,
                    PhysicalWidth = physWidth,
                    PhysicalHeight = physHeight,
                    DpiScaleX = monScaleX,
                    DpiScaleY = monScaleY,
                    IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    DeviceName = mi.szDevice
                });
            }
            return true;
        }, IntPtr.Zero);

        return screens;
    }

    /// <summary>
    /// Positions and shows a WPF window on the target monitor pixel-perfect,
    /// ensuring WPF initializes its visual rendering tree and Win32 attaches to the monitor coordinates.
    /// </summary>
    public static void PositionWindowOnScreen(Window window, ScreenInfo screen)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowState = WindowState.Normal;
        window.Left = screen.DipLeft;
        window.Top = screen.DipTop;
        window.Width = screen.DipWidth;
        window.Height = screen.DipHeight;

        // On secondary monitor, set Topmost so it projects cleanly over desktop/taskbar of that monitor.
        // On primary monitor (single screen test), keep Topmost false so MainWindow stays on top.
        window.Topmost = !screen.IsPrimary;

        var helper = new WindowInteropHelper(window);
        var hwnd = helper.EnsureHandle();

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (screen.IsPrimary)
        {
            // Position behind MainWindow on primary screen (desktop background layer)
            SetWindowPos(hwnd, HWND_BOTTOM, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                Application.Current.MainWindow.Activate();
            }
        }
        else
        {
            // Position pixel-perfect across the secondary monitor (Physical Win32 coordinates)
            SetWindowPos(hwnd, HWND_TOP, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// Positions and synchronizes the Background Video Window and Foreground Lyrics Window together,
    /// guaranteeing correct Z-order on both single-monitor and dual-monitor projector setups.
    /// </summary>
    public static void PositionProjectionPair(Window bgWindow, Window frontWindow, ScreenInfo screen)
    {
        bgWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        bgWindow.WindowStyle = WindowStyle.None;
        bgWindow.ResizeMode = ResizeMode.NoResize;
        bgWindow.WindowState = WindowState.Normal;
        bgWindow.Topmost = !screen.IsPrimary;
        bgWindow.Left = screen.DipLeft;
        bgWindow.Top = screen.DipTop;
        bgWindow.Width = screen.DipWidth;
        bgWindow.Height = screen.DipHeight;

        frontWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        frontWindow.WindowStyle = WindowStyle.None;
        frontWindow.ResizeMode = ResizeMode.NoResize;
        frontWindow.WindowState = WindowState.Normal;
        frontWindow.Topmost = !screen.IsPrimary;
        frontWindow.Left = screen.DipLeft;
        frontWindow.Top = screen.DipTop;
        frontWindow.Width = screen.DipWidth;
        frontWindow.Height = screen.DipHeight;

        var bgHelper = new WindowInteropHelper(bgWindow);
        var frontHelper = new WindowInteropHelper(frontWindow);

        var bgHwnd = bgHelper.EnsureHandle();
        var frontHwnd = frontHelper.EnsureHandle();

        // Background window MUST always be visible as solid black backdrop on target screen!
        if (!bgWindow.IsVisible) bgWindow.Show();
        if (!frontWindow.IsVisible) frontWindow.Show();

        const uint flags = SWP_NOACTIVATE | SWP_SHOWWINDOW;

        if (screen.IsPrimary)
        {
            bgWindow.Topmost = false;
            frontWindow.Topmost = false;

            SetWindowPos(frontHwnd, HWND_BOTTOM, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, flags);
            SetWindowPos(bgHwnd, HWND_BOTTOM, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, flags);

            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                Application.Current.MainWindow.Activate();
            }
        }
        else
        {
            // Secondary monitor: bg placed at TOP, then front placed at TOP (directly over bg)
            bgWindow.Topmost = true;
            frontWindow.Topmost = true;
            SetWindowPos(bgHwnd, HWND_TOP, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, flags);
            SetWindowPos(frontHwnd, HWND_TOP, screen.PhysicalLeft, screen.PhysicalTop, screen.PhysicalWidth, screen.PhysicalHeight, flags);
        }
    }

    /// <summary>
    /// Explicitly strips the WS_EX_TOPMOST flag from hWnd and places it behind hWndFront (or at HWND_BOTTOM).
    /// Used when falling back to a single display so the projection window never locks out the user.
    /// </summary>
    public static void DemoteFromTopmostAndSendBehind(IntPtr hWnd, IntPtr hWndFront)
    {
        if (hWnd != IntPtr.Zero)
        {
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            if (hWndFront != IntPtr.Zero)
            {
                SetWindowPos(hWndFront, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                SetWindowPos(hWnd, hWndFront, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
    }

    /// <summary>
    /// Places hWnd directly behind hWndFront in the Win32 window Z-order.
    /// Used to place libmpv video background window immediately behind transparent ProjectionWindow.
    /// </summary>
    public static void SetWindowBehind(IntPtr hWnd, IntPtr hWndFront)
    {
        if (hWnd != IntPtr.Zero && hWndFront != IntPtr.Zero)
        {
            SetWindowPos(hWndFront, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            SetWindowPos(hWnd, hWndFront, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // Only bring MainWindow to HWND_TOP when in Single Monitor Test Mode.
            // On dual-monitor / projector mode, touching MainWindow across displays forces cross-monitor
            // DWM composition sync stalls between different refresh rates, causing stutter on low-end laptops.
            var screens = GetScreens();
            if (screens.Count <= 1 && Application.Current?.MainWindow != null)
            {
                var mainHwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                if (mainHwnd != IntPtr.Zero)
                {
                    SetWindowPos(mainHwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }
    }
}
