#pragma once

#include "Services/JsonUtil.h"

#include <QJsonObject>
#include <QString>

namespace Lux {

enum class MediaType { Image, Video, Color, Audio };

// Port of Models/MediaItem.cs.
struct MediaItem
{
    QString id = newId();
    QString name;
    QString filePath;
    MediaType type = MediaType::Image;
    QString hexColor = QStringLiteral("#001A33"); // default deep blue
    bool isDefault = false;

    QJsonObject toJson() const;
    static MediaItem fromJson(const QJsonObject& o);
};

} // namespace Lux
