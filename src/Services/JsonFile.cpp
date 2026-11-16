#include "Services/JsonFile.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonParseError>
#include <QSaveFile>

namespace Lux {

bool JsonFile::read(const QString& path, QJsonDocument& doc, QString* error)
{
    QFile f(path);
    if (!f.open(QIODevice::ReadOnly)) {
        if (error)
            *error = QStringLiteral("Cannot open %1: %2").arg(path, f.errorString());
        return false;
    }
    QByteArray data = f.readAll();
    f.close();
    if (data.startsWith("\xEF\xBB\xBF"))
        data.remove(0, 3);

    QJsonParseError pe;
    doc = QJsonDocument::fromJson(data, &pe);
    if (pe.error != QJsonParseError::NoError) {
        if (error)
            *error = QStringLiteral("Invalid JSON in %1 at offset %2: %3")
                         .arg(path).arg(pe.offset).arg(pe.errorString());
        return false;
    }
    return true;
}

bool JsonFile::write(const QString& path, const QJsonDocument& doc, QString* error)
{
    QDir().mkpath(QFileInfo(path).absolutePath());
    QSaveFile f(path);
    if (!f.open(QIODevice::WriteOnly)) {
        if (error)
            *error = QStringLiteral("Cannot write %1: %2").arg(path, f.errorString());
        return false;
    }
    f.write(doc.toJson(QJsonDocument::Indented));
    if (!f.commit()) {
        if (error)
            *error = QStringLiteral("Cannot commit %1: %2").arg(path, f.errorString());
        return false;
    }
    return true;
}

} // namespace Lux
