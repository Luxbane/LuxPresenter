#pragma once

#include <QJsonDocument>
#include <QString>

namespace Lux {

// UTF-8 JSON file persistence. Writes are atomic (QSaveFile).
struct JsonFile
{
    // Returns false (and fills `error`) if the file is unreadable or not valid JSON.
    static bool read(const QString& path, QJsonDocument& doc, QString* error = nullptr);
    // Creates the parent directory if needed. Output is indented, UTF-8 without BOM.
    static bool write(const QString& path, const QJsonDocument& doc, QString* error = nullptr);
};

} // namespace Lux
