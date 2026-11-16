#include "Models/BackgroundItem.h"

namespace Lux {

QJsonObject BackgroundItem::toJson() const
{
    QJsonObject o;
    o[QStringLiteral("Id")] = id;
    o[QStringLiteral("Name")] = name;
    o[QStringLiteral("FilePath")] = filePath;
    o[QStringLiteral("Type")] = static_cast<int>(type);
    o[QStringLiteral("HexColor")] = hexColor;
    o[QStringLiteral("IsDefault")] = isDefault;
    return o;
}

BackgroundItem BackgroundItem::fromJson(const QJsonObject& o)
{
    BackgroundItem b;
    b.id = Json::str(o, QStringLiteral("Id"), newId());
    b.name = Json::str(o, QStringLiteral("Name"));
    b.filePath = Json::str(o, QStringLiteral("FilePath"));
    b.type = Json::enumValue(o, QStringLiteral("Type"), BackgroundType::Image, static_cast<int>(BackgroundType::Color));
    b.hexColor = Json::str(o, QStringLiteral("HexColor"), b.hexColor);
    b.isDefault = Json::boolean(o, QStringLiteral("IsDefault"), false);
    return b;
}

} // namespace Lux
