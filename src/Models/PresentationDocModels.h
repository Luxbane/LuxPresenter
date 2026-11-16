#pragma once

#include "Services/JsonUtil.h"

#include <QDateTime>
#include <QJsonObject>
#include <QList>
#include <QString>

namespace Lux {

// Entry in the Presentation library (presentations.json).
struct PresentationDocItem
{
    QString id = newId();
    QString name;
    QString filePath;
    QString fileExtension = QStringLiteral(".pptx");
    int slideCount = 0;
    QString thumbnailPath;
    QDateTime dateAdded = QDateTime::currentDateTime();

    QJsonObject toJson() const;
    static PresentationDocItem fromJson(const QJsonObject& o);
};

// Runtime-only models used by the presentation engine (not persisted).
struct PresentationSlide
{
    int index = 0; // 1-based
    QString backgroundColor = QStringLiteral("#FFFFFF");
    QString thumbnailPath;
    QString imagePreviewPath;
    bool isSelectedInPreview = false;
    bool isCurrentlyLive = false;
};

struct PresentationDocument
{
    QString id = newId();
    QString title;
    QString filePath;
    double canvasWidth = 1920;
    double canvasHeight = 1080;
    QList<PresentationSlide> slides;
};

// Preset/running alert message (AlertMessageItem). Change notification arrives with the UI layer.
struct AlertMessageItem
{
    int count = 3;
    QString text;

    bool operator==(const AlertMessageItem& other) const
    {
        return count == other.count && text == other.text;
    }
};

} // namespace Lux
