#include "Models/MediaItem.h"

namespace Lux {

QJsonObject MediaItem::toJson() const
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

MediaItem MediaItem::fromJson(const QJsonObject& o)
{
    MediaItem m;
    m.id = Json::str(o, QStringLiteral("Id"), newId());
    m.name = Json::str(o, QStringLiteral("Name"));
    m.filePath = Json::str(o, QStringLiteral("FilePath"));
    m.type = Json::enumValue(o, QStringLiteral("Type"), MediaType::Image, static_cast<int>(MediaType::Audio));
    m.hexColor = Json::str(o, QStringLiteral("HexColor"), m.hexColor);
    m.isDefault = Json::boolean(o, QStringLiteral("IsDefault"), false);
    return m;
}

} // namespace Lux
