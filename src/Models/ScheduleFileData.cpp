#include "Models/ScheduleFileData.h"

#include "Services/JsonFile.h"

#include <QDateTime>
#include <QJsonArray>
#include <QJsonDocument>

namespace Lux {

bool ScheduleFile::save(const QString& path, const QList<ScheduleItem>& items, QString* error)
{
    QJsonArray arr;
    for (const ScheduleItem& it : items) {
        QJsonObject o;
        o[QStringLiteral("Id")] = it.id;
        o[QStringLiteral("Title")] = it.title;
        o[QStringLiteral("TypeLabel")] = it.typeLabel;
        o[QStringLiteral("Category")] = static_cast<int>(it.category);
        o[QStringLiteral("Presentation")] = it.presentation.toJson(/*includeSlideMargins=*/false);
        arr.append(o);
    }
    QJsonObject root;
    root[QStringLiteral("Version")] = QStringLiteral("1.0");
    root[QStringLiteral("SavedAt")] = QDateTime::currentDateTime().toString(Qt::ISODateWithMs);
    root[QStringLiteral("Items")] = arr;
    return JsonFile::write(path, QJsonDocument(root), error);
}

bool ScheduleFile::load(const QString& path, QList<ScheduleItem>& items, QString* error)
{
    QJsonDocument doc;
    if (!JsonFile::read(path, doc, error))
        return false;
    if (!doc.isObject()) {
        if (error)
            *error = QStringLiteral("Schedule file %1 is not a JSON object.").arg(path);
        return false;
    }
    items.clear();
    const QJsonArray arr = doc.object().value(QStringLiteral("Items")).toArray();
    for (const QJsonValue& v : arr) {
        if (!v.isObject())
            continue;
        const QJsonObject o = v.toObject();
        ScheduleItem it;
        it.id = Json::str(o, QStringLiteral("Id"), newId());
        it.title = Json::str(o, QStringLiteral("Title"));
        it.typeLabel = Json::str(o, QStringLiteral("TypeLabel"), it.typeLabel);
        it.category = Json::enumValue(o, QStringLiteral("Category"), ItemCategory::Song,
                                      static_cast<int>(ItemCategory::Media));
        it.presentation = PresentationItem::fromJson(o.value(QStringLiteral("Presentation")).toObject());
        items.append(it);
    }
    return true;
}

} // namespace Lux
