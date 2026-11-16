#pragma once

#include "Services/JsonUtil.h"

#include <QJsonObject>
#include <QString>

namespace Lux {

enum class SlideType { Title, Verse, Chorus, Bridge, Ending, Scripture, General };

// Equivalent of System.Windows.Thickness (serialised as Left/Top/Right/Bottom).
struct Thickness
{
    double left = 0;
    double top = 0;
    double right = 0;
    double bottom = 0;
    bool operator==(const Thickness&) const = default;
};

// Port of Models/Slide.cs.
struct Slide
{
    QString id = newId();
    QString label = QStringLiteral("Slide"); // e.g. "Verse 1", "Chorus", "Genesis 1:1"
    QString content;
    QString title;
    QString subtitle;
    QString customBackgroundPath; // null = none
    SlideType type = SlideType::Verse;

    QString fontFamily = QStringLiteral("Tahoma");
    int fontSize = 13;
    bool isBold = true;
    bool isItalic = false;
    bool isUnderline = false;
    bool hasShadow = true;
    bool hasOutline = true;
    QString fontColor = QStringLiteral("#FFFFFF");
    Thickness containerMargin{1, 1, 1, 2};
    // Layout hint for the stanza lists (NOT serialised, JSON format unchanged): how many blank lines of the song
    // text separated this stanza from the previous one. -1 = unknown (e.g. loaded from a schedule file).
    int blankLinesBefore = -1;

    Slide clone() const; // new Id, everything else copied

    // includeMargin=false reproduces the schedule-file SlideData shape.
    QJsonObject toJson(bool includeMargin = true) const;
    static Slide fromJson(const QJsonObject& o);
};

} // namespace Lux
