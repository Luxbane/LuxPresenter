#pragma once

#include "Models/AppSettings.h"
#include "Models/PresentationItem.h"

namespace Lux {

// Port of Models/Song.cs.
struct Song
{
    QString id = newId();
    QString title;
    QString author;
    QString copyright;
    QString key;
    bool isFavorite = false;
    QString lyricsRaw;
    QString defaultBackground; // null = none

    // Typography overrides
    QString fontFamily = QStringLiteral("Tahoma");
    int fontSize = 13;
    bool isBold = true;
    bool isItalic = false;
    bool isUnderline = false;
    bool hasShadow = true;
    bool hasOutline = true;
    QString fontColor = QStringLiteral("#FFFFFF");

    // Splits lyrics into slides on blank lines; recognises "[Verse 1]" and "Chorus:" headers.
    PresentationItem toPresentationItem(const AppSettings* settings = nullptr) const;

    QJsonObject toJson() const;
    static Song fromJson(const QJsonObject& o);
};

} // namespace Lux
