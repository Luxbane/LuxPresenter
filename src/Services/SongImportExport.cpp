#include "Services/SongImportExport.h"

#include "Services/JsonFile.h"

#include <QFile>
#include <QJsonArray>
#include <QJsonDocument>

namespace Lux {

bool SongImportExport::exportSongs(const QString& destinationPath, const QList<Song>& songs, QString* error)
{
    QJsonArray arr;
    for (const Song& s : songs)
        arr.append(s.toJson());
    return JsonFile::write(destinationPath, QJsonDocument(arr), error);
}

bool SongImportExport::importSongs(const QString& sourcePath, QList<Song>& songs, QString* error)
{
    songs.clear();
    if (!QFile::exists(sourcePath))
        return true;
    QJsonDocument doc;
    if (!JsonFile::read(sourcePath, doc, error))
        return false;
    if (!doc.isArray()) {
        if (error)
            *error = QStringLiteral("%1 is not a songs backup (a JSON array of songs was expected).").arg(sourcePath);
        return false;
    }
    for (const QJsonValue& v : doc.array())
        if (v.isObject())
            songs.append(Song::fromJson(v.toObject()));
    return true;
}

} // namespace Lux
