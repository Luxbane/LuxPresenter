#pragma once

#include "Services/Displays.h"

#include <QRect>

namespace Lux {

// Win32 monitor enumeration and window placement (port of Services/DisplayHelper.cs). Window handles are
// passed as quintptr (HWND) to keep <windows.h> out of headers.
namespace DisplayHelper {

// All monitors of the virtual desktop with physical-pixel rectangles and DPI scale.
QList<ScreenInfo> enumerateScreens();

// Places the projection window pair on `screen`.
//  - secondary monitor: covering the screen, above other windows (HWND_TOP), not activated.
//  - primary monitor ("single screen test mode"): covering the screen but at the bottom of the z-order so it
//    stays behind the main window and never steals the operator's screen.
// `background` sits directly below `foreground` in both cases. Windows are not activated.
void placePair(quintptr background, quintptr foreground, const ScreenInfo& screen);

// Window style helpers: tool window (no taskbar button) that never takes focus.
void makeNonActivating(quintptr hwnd);

// Physical outer rectangle of a native window (for tests/diagnostics).
QRect windowRect(quintptr hwnd);

} // namespace DisplayHelper

} // namespace Lux
