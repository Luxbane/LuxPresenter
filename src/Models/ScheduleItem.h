#pragma once

#include "Models/PresentationItem.h"

namespace Lux {

// Port of Models/ScheduleItem.cs.
struct ScheduleItem
{
    QString id = newId();
    QString title;
    QString typeLabel = QStringLiteral("Song");
    ItemCategory category = ItemCategory::Song;
    PresentationItem presentation;

    static ScheduleItem fromPresentation(const PresentationItem& presentation);
};

} // namespace Lux
