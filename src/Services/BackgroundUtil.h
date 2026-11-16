#pragma once
#include "Models/BackgroundItem.h"
#include <QList>
#include <QString>
#include <QStringList>
namespace Lux {
// Background files and values. A "background" value is either a colour ("#RRGGBB") or the path of an image / video file.
namespace Backgrounds {
extern const char* const kDefaultColor;                  // "#001833"
QStringList imageExtensions();                           // lower case, no dot
QStringList videoExtensions();
bool isVideoPath(const QString& path);
bool isImagePath(const QString& path);
bool isColor(const QString& value);                      // "#RGB" / "#RRGGBB" / "#AARRGGBB"
QString fileDialogFilter();                              // for QFileDialog
// Item for a file the user picked (type from the extension, name from the file name). Unsupported extension: returns
// false and sets *error.
bool itemFromFile(const QString& path, BackgroundItem* out, QString* error = nullptr);
BackgroundItem itemFromColor(const QString& hex, const QString& name = {});
// Original solid colours that are always offered (not stored in backgrounds.json).
QList<BackgroundItem> builtinColors();
// The value to show for a stored background: a colour, or an existing file; anything else is `fallback`.
QString usable(const QString& background, const QString& fallback = QString::fromLatin1(kDefaultColor));
// The value an item stands for (colour or file path).
QString valueOf(const BackgroundItem& item);
} // namespace Backgrounds
} // namespace Lux
