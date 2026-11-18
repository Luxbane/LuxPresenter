using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using LuxPresenter.Services;

namespace LuxPresenter.Views.Components;

public class MpvVideoView : HwndHost
{
    private IntPtr _hwnd = IntPtr.Zero;
    public IntPtr Hwnd => _hwnd;

    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int GWL_EXSTYLE = -20;
    private const uint LWA_ALPHA = 0x00000002;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x, int y,
        int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public MpvPlayer? Player { get; private set; }
    public event Action<MpvPlayer>? PlayerReady;

    private byte _currentAlpha = 255;

    public void SetAlpha(byte alpha)
    {
        _currentAlpha = alpha;
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
                if ((ex & WS_EX_LAYERED) == 0)
                {
                    SetWindowLong(_hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
                }
                SetLayeredWindowAttributes(_hwnd, 0, alpha, LWA_ALPHA);
            }
            catch { }
        }
    }

    public void BringToTop()
    {
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                // SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE = 0x0001 | 0x0002 | 0x0010
                SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }
            catch { }
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        int width = (int)Math.Max(1, ActualWidth > 0 ? ActualWidth : 320);
        int height = (int)Math.Max(1, ActualHeight > 0 ? ActualHeight : 240);

        _hwnd = CreateWindowEx(
            WS_EX_LAYERED,
            "STATIC",
            string.Empty,
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | 0x0000000E, // SS_BLACKRECT
            0, 0,
            width, height,
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                SetLayeredWindowAttributes(_hwnd, 0, _currentAlpha, LWA_ALPHA);
                Player = new MpvPlayer(_hwnd);
                PlayerReady?.Invoke(Player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MpvVideoView] Player init failed: {ex.Message}");
                try
                {
                    System.IO.File.AppendAllText("mpv_error.log", $"[{DateTime.Now}] MpvPlayer init error: {ex}\n");
                }
                catch { }
            }
        }

        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (Player != null)
        {
            Player.Dispose();
            Player = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
