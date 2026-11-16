#pragma once

#include "Models/Song.h"

#include <QList>
#include <QString>

namespace Lux {

// Port of Services/SongImportExportService.cs: songs backup as a UTF-8 JSON array
// (the same shape as songs.json), with explicit error reporting.
struct SongImportExport
{
    static bool exportSongs(const QString& destinationPath, const QList<Song>& songs, QString* error = nullptr);
    // A missing file yields an empty list without error (as in the reference); invalid JSON
    // or a document that is not an array fails with `error` set.
    static bool importSongs(const QString& sourcePath, QList<Song>& songs, QString* error = nullptr);
};

} // namespace Lux
