#pragma once

#include <QString>

namespace Lux {

// Display-time repair of legacy Windows-1252 text.
//
// Song databases created from Windows-1252 sources and read as Latin-1 contain the C1 control
// characters U+0080..U+009F where the author typed typographic punctuation (U+0092 for the
// apostrophe, U+0096 for the en dash, ...). They are valid UTF-8/JSON, but no font has glyphs for
// control characters, so they render as empty boxes. The stored text is never modified; every
// place that shows text maps those code points to the characters they stood for
// (Windows-1252 table). The five code points Windows-1252 leaves undefined carry no visible
// meaning and are omitted from the display.
QString displayText(const QString& stored);

// True when `text` contains any U+0080..U+009F code point.
bool hasLegacyControlChars(const QString& text);

} // namespace Lux
