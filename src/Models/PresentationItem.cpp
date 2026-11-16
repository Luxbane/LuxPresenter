#include "Models/PresentationItem.h"

#include <QJsonArray>

namespace Lux {

QString itemCategoryName(ItemCategory c)
{
    switch (c) {
    case ItemCategory::Song: return QStringLiteral("Song");
    case ItemCategory::Scripture: return QStringLiteral("Scripture");
    case ItemCategory::Presentation: return QStringLiteral("Presentation");
    case ItemCategory::Media: return QStringLiteral("Media");
    }
    return QStringLiteral("Song");
}

PresentationItem PresentationItem::clone() const
{
    PresentationItem c;
    c.id = newId();
    c.title = title;
    c.subtitle = subtitle;
    c.category = category;
    c.backgroundPath = backgroundPath;
    for (const Slide& s : slides)
        c.slides.append(s.clone());
    return c;
}

QJsonObject PresentationItem::toJson(bool includeSlideMargins) const
{
    QJsonArray arr;
    for (const Slide& s : slides)
        arr.append(s.toJson(includeSlideMargins));

    QJsonObject o;
    o[QStringLiteral("Id")] = id;
    o[QStringLiteral("Title")] = title;
    o[QStringLiteral("Subtitle")] = subtitle;
    o[QStringLiteral("Category")] = static_cast<int>(category);
    o[QStringLiteral("BackgroundPath")] = Json::fromNullable(backgroundPath);
    o[QStringLiteral("Slides")] = arr;
    return o;
}

PresentationItem PresentationItem::fromJson(const QJsonObject& o)
{
    PresentationItem p;
    p.id = Json::str(o, QStringLiteral("Id"), newId());
    p.title = Json::str(o, QStringLiteral("Title"));
    p.subtitle = Json::str(o, QStringLiteral("Subtitle"));
    p.category = Json::enumValue(o, QStringLiteral("Category"), ItemCategory::Song, static_cast<int>(ItemCategory::Media));
    p.backgroundPath = Json::nullableStr(o, QStringLiteral("BackgroundPath"));
    const QJsonArray arr = o.value(QStringLiteral("Slides")).toArray();
    for (const QJsonValue& v : arr)
        if (v.isObject())
            p.slides.append(Slide::fromJson(v.toObject()));
    return p;
}

} // namespace Lux
