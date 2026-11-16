#include "Models/PresentationDocModels.h"

namespace Lux {

QJsonObject PresentationDocItem::toJson() const
{
    QJsonObject o;
    o[QStringLiteral("Id")] = id;
    o[QStringLiteral("Name")] = name;
    o[QStringLiteral("FilePath")] = filePath;
    o[QStringLiteral("FileExtension")] = fileExtension;
    o[QStringLiteral("SlideCount")] = slideCount;
    o[QStringLiteral("ThumbnailPath")] = thumbnailPath;
    o[QStringLiteral("DateAdded")] = dateAdded.toString(Qt::ISODateWithMs);
    return o;
}

PresentationDocItem PresentationDocItem::fromJson(const QJsonObject& o)
{
    PresentationDocItem d;
    d.id = Json::str(o, QStringLiteral("Id"), newId());
    d.name = Json::str(o, QStringLiteral("Name"));
    d.filePath = Json::str(o, QStringLiteral("FilePath"));
    d.fileExtension = Json::str(o, QStringLiteral("FileExtension"), d.fileExtension);
    d.slideCount = Json::integer(o, QStringLiteral("SlideCount"), 0);
    d.thumbnailPath = Json::str(o, QStringLiteral("ThumbnailPath"));
    const QDateTime dt = QDateTime::fromString(Json::str(o, QStringLiteral("DateAdded")), Qt::ISODate);
    if (dt.isValid())
        d.dateAdded = dt;
    return d;
}

} // namespace Lux
