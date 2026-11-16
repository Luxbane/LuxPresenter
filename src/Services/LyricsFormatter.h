#pragma once

#include "Models/AppSettings.h"

#include <QString>

namespace Lux {

// Port of Services/LyricsFormatter.cs (word-formatting options from AppSettings).
// Note: like the reference, "capitalize all words" short-circuits the other options.
class LyricsFormatter
{
public:
    static QString format(const QString& raw, const AppSettings* settings);
};

} // namespace Lux
