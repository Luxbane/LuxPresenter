#pragma once

#include "Models/Song.h"

#include <QList>
#include <QString>
#include <QStringList>

namespace Lux {

// Port of the search logic in ViewModels/SongsViewModel.cs: punctuation-insensitive,
// token-based scoring over title / author / lyrics, with the Indonesian contraction
// synonyms of the reference (e.g. "karna" <-> "karena").
class SongSearch
{
public:
    explicit SongSearch(const QList<Song>& songs = {});

    // Lower-cases, strips apostrophes/smart quotes, turns punctuation into spaces and
    // collapses whitespace.
    static QString normalize(const QString& input);

    // Title-only query wins over a lyrics query (the UI keeps at most one non-empty).
    // Without a query: all songs (favourites only if requested) ordered by title.
    QList<Song> filter(const QString& titleQuery, const QString& lyricsQuery, bool favoritesOnly) const;

    // Index of the stanza of a song (given as the plain text of each stanza) that a lyrics query refers to, -1 when no
    // stanza matches (e.g. the song was found by title/author only). Same normalisation, phrase/compact/token rules and
    // synonyms as filter(). Preference: first stanza containing the whole phrase; otherwise the stanza containing the most
    // query words (first one on ties).
    static int matchingStanza(const QStringList& stanzas, const QString& lyricsQuery);
    int songCount() const { return static_cast<int>(m_entries.size()); }

private:
    struct Entry
    {
        Song song;
        QString cleanTitle, compactTitle, cleanAuthor, cleanLyrics, compactLyrics, cleanFull, compactFull;
    };
    QList<Entry> m_entries;
};

} // namespace Lux
