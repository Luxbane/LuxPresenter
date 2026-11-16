#include "Models/ScheduleItem.h"

namespace Lux {

ScheduleItem ScheduleItem::fromPresentation(const PresentationItem& presentation)
{
    ScheduleItem s;
    s.id = newId();
    s.title = presentation.title;
    s.typeLabel = itemCategoryName(presentation.category);
    s.category = presentation.category;
    s.presentation = presentation.clone();
    return s;
}

} // namespace Lux
