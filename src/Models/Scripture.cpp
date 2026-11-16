#include "Models/Scripture.h"

#include "Services/LyricsFormatter.h"

namespace Lux {

Slide BibleVerse::toSlide(const AppSettings* settings) const
{
    const AppSettings defaults;
    Slide s;
    s.label = reference();
    s.title = reference();
    s.subtitle = version;
    s.content = LyricsFormatter::format(text, settings ? settings : &defaults);
    s.type = SlideType::Scripture;
    s.fontSize = 13;
    return s;
}

QJsonObject BibleVerse::toJson() const
{
    QJsonObject o;
    o[QStringLiteral("BookId")] = bookId;
    o[QStringLiteral("Book")] = book;
    o[QStringLiteral("Chapter")] = chapter;
    o[QStringLiteral("Verse")] = verse;
    o[QStringLiteral("Text")] = text;
    o[QStringLiteral("Version")] = version;
    return o;
}

BibleVerse BibleVerse::fromJson(const QJsonObject& o)
{
    BibleVerse v;
    v.bookId = Json::integer(o, QStringLiteral("BookId"), 0);
    v.book = Json::str(o, QStringLiteral("Book"));
    v.chapter = Json::integer(o, QStringLiteral("Chapter"), 0);
    v.verse = Json::integer(o, QStringLiteral("Verse"), 0);
    v.text = Json::str(o, QStringLiteral("Text"));
    v.version = Json::str(o, QStringLiteral("Version"), v.version);
    return v;
}

} // namespace Lux
