#include "Views/Projection/DisplayHelper.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <shellscalingapi.h>

namespace Lux::DisplayHelper {

namespace {
BOOL CALLBACK enumProc(HMONITOR mon, HDC, LPRECT, LPARAM lp)
{
    auto* list = reinterpret_cast<QList<ScreenInfo>*>(lp);
    MONITORINFOEXW mi{};
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(mon, &mi))
        return TRUE;
    ScreenInfo s;
    s.physicalLeft = mi.rcMonitor.left;
    s.physicalTop = mi.rcMonitor.top;
    s.physicalWidth = mi.rcMonitor.right - mi.rcMonitor.left;
    s.physicalHeight = mi.rcMonitor.bottom - mi.rcMonitor.top;
    s.isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
    s.deviceName = QString::fromWCharArray(mi.szDevice);
    UINT dx = 96, dy = 96;
    if (SUCCEEDED(GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, &dx, &dy))) {
        s.dpiScaleX = dx / 96.0;
        s.dpiScaleY = dy / 96.0;
    }
    list->append(s);
    return TRUE;
}
} // namespace

QList<ScreenInfo> enumerateScreens()
{
    QList<ScreenInfo> list;
    EnumDisplayMonitors(nullptr, nullptr, enumProc, reinterpret_cast<LPARAM>(&list));
    for (int i = 0; i < list.size(); ++i)
        list[i].index = i;
    return list;
}

void placePair(quintptr background, quintptr foreground, const ScreenInfo& screen)
{
    HWND bg = reinterpret_cast<HWND>(background);
    HWND fg = reinterpret_cast<HWND>(foreground);
    const UINT flags = SWP_NOACTIVATE | SWP_SHOWWINDOW;
    if (screen.isPrimary) {
        // Foreground first, then the background under it: bottom of the z-order, bg below fg.
        SetWindowPos(fg, HWND_BOTTOM, screen.physicalLeft, screen.physicalTop, screen.physicalWidth, screen.physicalHeight, flags);
        SetWindowPos(bg, HWND_BOTTOM, screen.physicalLeft, screen.physicalTop, screen.physicalWidth, screen.physicalHeight, flags);
    } else {
        SetWindowPos(bg, HWND_TOP, screen.physicalLeft, screen.physicalTop, screen.physicalWidth, screen.physicalHeight, flags);
        SetWindowPos(fg, HWND_TOP, screen.physicalLeft, screen.physicalTop, screen.physicalWidth, screen.physicalHeight, flags);
    }
}

void makeNonActivating(quintptr hwnd)
{
    HWND h = reinterpret_cast<HWND>(hwnd);
    LONG_PTR ex = GetWindowLongPtrW(h, GWL_EXSTYLE);
    ex |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
    SetWindowLongPtrW(h, GWL_EXSTYLE, ex);
}

QRect windowRect(quintptr hwnd)
{
    RECT r{};
    GetWindowRect(reinterpret_cast<HWND>(hwnd), &r);
    return QRect(r.left, r.top, r.right - r.left, r.bottom - r.top);
}

} // namespace Lux::DisplayHelper
