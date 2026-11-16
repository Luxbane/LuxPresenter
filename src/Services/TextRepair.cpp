#include "Services/TextRepair.h"

namespace Lux {

namespace {

// Windows-1252 0x80..0x9F -> Unicode (0 = undefined in Windows-1252).
constexpr char16_t kCp1252[32] = {
    0x20AC, 0,      0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, // 80-87
    0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0,      0x017D, 0,      // 88-8F
    0,      0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, // 90-97
    0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0,      0x017E, 0x0178, // 98-9F
};

inline bool isC1(QChar c)
{
    return c.unicode() >= 0x80 && c.unicode() <= 0x9F;
}

} // namespace

bool hasLegacyControlChars(const QString& text)
{
    for (const QChar c : text)
        if (isC1(c))
            return true;
    return false;
}

QString displayText(const QString& stored)
{
    if (!hasLegacyControlChars(stored))
        return stored;
    QString out;
    out.reserve(stored.size());
    for (const QChar c : stored) {
        if (!isC1(c)) {
            out.append(c);
            continue;
        }
        const char16_t mapped = kCp1252[c.unicode() - 0x80];
        if (mapped != 0)
            out.append(QChar(mapped));
    }
    return out;
}

} // namespace Lux
