#pragma once

#include "Models/AppSettings.h"
#include "Models/Slide.h"

namespace Lux {

// Port of Models/Scripture.cs. The JSON shape matches Data/bibles.json
// (and user-imported Bible files): BookId, Book, Chapter, Verse, Text, Version.
struct BibleVerse
{
    int bookId = 0;
    QString book;
    int chapter = 0;
    int verse = 0;
    QString text;
    QString version = QStringLiteral("KJV");

    QString reference() const { return QStringLiteral("%1 %2:%3").arg(book).arg(chapter).arg(verse); }
    Slide toSlide(const AppSettings* settings = nullptr) const;

    QJsonObject toJson() const;
    static BibleVerse fromJson(const QJsonObject& o);
};

struct BibleBook
{
    int id = 0;
    QString name;
    QString englishName;
    QString indonesianName;
    QString shortName;
    int chapterCount = 1;
    bool isOldTestament = true;
};

} // namespace Lux
