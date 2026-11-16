#include "Services/Displays.h"

namespace Lux {

const ScreenInfo* DisplaySelection::choose(const QList<ScreenInfo>& screens, int selectedIndex)
{
    if (screens.isEmpty())
        return nullptr;
    if (selectedIndex >= 0 && selectedIndex < screens.size())
        return &screens.at(selectedIndex);
    for (const ScreenInfo& s : screens)
        if (!s.isPrimary)
            return &s;
    return &screens.first();
}

QString DisplaySelection::statusText(int displayCount, int activeScreenNumber)
{
    if (displayCount <= 1)
        return QStringLiteral("1 Display (Single Screen Test Mode)");
    if (activeScreenNumber > 0)
        return QStringLiteral("%1 Displays (Projector Output Active on Screen %2)").arg(displayCount).arg(activeScreenNumber);
    return QStringLiteral("%1 Displays Detected").arg(displayCount);
}

} // namespace Lux
