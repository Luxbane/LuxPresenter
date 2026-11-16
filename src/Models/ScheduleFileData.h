#pragma once

#include "Models/ScheduleItem.h"

#include <QList>

namespace Lux {

// Port of Models/ScheduleFileData.cs plus the save/load mapping in
// ScheduleViewModel. File shape: { Version, SavedAt, Items: [ScheduleItemData] }.
// Slide ContainerMargin is not persisted (as in the reference) and is reset to
// its default when loading.
struct ScheduleFile
{
    static bool save(const QString& path, const QList<ScheduleItem>& items, QString* error = nullptr);
    static bool load(const QString& path, QList<ScheduleItem>& items, QString* error = nullptr);
};

} // namespace Lux
