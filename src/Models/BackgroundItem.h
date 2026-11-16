#pragma once

#include "Services/JsonUtil.h"

#include <QJsonObject>
#include <QString>

namespace Lux {

enum class BackgroundType { Image, Video, Color };

// Port of Models/BackgroundItem.cs.
struct BackgroundItem
{
    QString id = newId();
    QString name;
    QString filePath;
    BackgroundType type = BackgroundType::Image;
    QString hexColor = QStringLiteral("#001833");
    bool isDefault = false;

    QJsonObject toJson() const;
    static BackgroundItem fromJson(const QJsonObject& o);
};

} // namespace Lux
