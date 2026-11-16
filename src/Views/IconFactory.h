#pragma once

#include "Services/ThemeService.h"

#include <QPixmap>

namespace Lux {

// Toolbar icons. The vector artwork is redrawn from the path data of the
// reference application's XAML canvases (same project, MIT licence); no
// third-party icon set is used. Emoji/symbol glyphs elsewhere in the UI come
// from the Windows system fonts (Segoe UI Emoji / Segoe UI Symbol).
enum class Icon {
    Add,
    Edit,
    Display,
    Globe,
    Donate,
    Background,
    Alert,
    Logo,
    BlackSquare,
    Clear,
};

class IconFactory
{
public:
    // Renders `icon` into a pixmap of logical size `w` x `h` for the given theme.
    static QPixmap pixmap(Icon icon, int w, int h, qreal devicePixelRatio, const Theme& theme);
    static QString svg(Icon icon, const Theme& theme);
};

} // namespace Lux
