#pragma once

#include "Models/Slide.h"

#include <QList>

namespace Lux {

enum class ItemCategory { Song, Scripture, Presentation, Media };

// Port of Models/PresentationItem.cs.
struct PresentationItem
{
    QString id = newId();
    QString title;
    QString subtitle;
    ItemCategory category = ItemCategory::Song;
    QString backgroundPath; // null = none
    QList<Slide> slides;

    PresentationItem clone() const; // new Id and new slide Ids

    // includeSlideMargins=false reproduces the schedule-file PresentationData shape.
    QJsonObject toJson(bool includeSlideMargins = true) const;
    static PresentationItem fromJson(const QJsonObject& o);
};

QString itemCategoryName(ItemCategory c); // "Song", "Scripture", ... (C# enum ToString)

} // namespace Lux
