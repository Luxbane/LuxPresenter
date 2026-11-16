#pragma once

#include <QList>
#include <QString>

namespace Lux {

// One monitor of the virtual desktop (port of ScreenInfo in Services/DisplayHelper.cs). Rectangles are
// physical pixels in Win32 virtual-desktop coordinates; windows are positioned with them directly.
struct ScreenInfo
{
    int index = 0;
    int physicalLeft = 0;
    int physicalTop = 0;
    int physicalWidth = 0;
    int physicalHeight = 0;
    double dpiScaleX = 1.0;
    double dpiScaleY = 1.0;
    bool isPrimary = false;
    QString deviceName;

    QString displayName() const
    {
        return QStringLiteral("%1 Monitor (%2x%3)")
            .arg(isPrimary ? QStringLiteral("Primary") : QStringLiteral("Secondary"))
            .arg(physicalWidth)
            .arg(physicalHeight);
    }
    bool operator==(const ScreenInfo&) const = default;
};

struct DisplaySelection
{
    // ProjectionService.ShowProjectionWindow: an explicit valid index wins; otherwise "auto" prefers a
    // secondary monitor (projector / HDMI / wireless display) and falls back to the first monitor.
    static const ScreenInfo* choose(const QList<ScreenInfo>& screens, int selectedIndex);
    static bool isProjectorConnected(const QList<ScreenInfo>& screens) { return screens.size() > 1; }
    // "1 Display (Single Screen Test Mode)" / "N Displays Detected" / "... (Projector Output Active on Screen K)"
    static QString statusText(int displayCount, int activeScreenNumber /*1-based, 0 = none*/);
};

} // namespace Lux
