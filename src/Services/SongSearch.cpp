#include "Services/SongSearch.h"

#include "Services/TextRepair.h"

#include <QHash>
#include <QRegularExpression>
#include <QStringList>

#include <algorithm>

namespace Lux {

namespace {

const QHash<QString, QString>& synonyms()
{
    static const QHash<QString, QString> map{
        {"dibri", "diberi"},   {"diberi", "dibri"},   {"sbab", "sebab"},     {"sebab", "sbab"},
        {"tlah", "telah"},     {"telah", "tlah"},     {"karna", "karena"},   {"karena", "karna"},
        {"sgala", "segala"},   {"segala", "sgala"},   {"bsar", "besar"},     {"besar", "bsar"},
        {"prisai", "perisai"}, {"perisai", "prisai"}, {"blajar", "belajar"}, {"belajar", "blajar"},
        {"sperti", "seperti"}, {"seperti", "sperti"}, {"slalu", "selalu"},   {"selalu", "slalu"},
    };
    return map;
}

bool has(const QString& haystack, const QString& needle)
{
    return haystack.contains(needle, Qt::CaseInsensitive);
}

QString withoutSpaces(QString s)
{
    s.remove(QLatin1Char(' '));
    return s;
}

} // namespace

QString SongSearch::normalize(const QString& input)
{
    // Compare what the user sees: legacy Windows-1252 control characters become their real punctuation.
    const QString text = displayText(input);
    if (text.trimmed().isEmpty())
        return QString();
    QString sb;
    sb.reserve(text.size());
    for (const QChar c : text) {
        const char16_t u = c.unicode();
        if (u == 39 || u == 96 || u == 0x2019 || u == 0x2018 || u == 0x201C || u == 0x201D ||
            (u >= 0x91 && u <= 0x94))
            continue; // stripped without a space: dib'ri -> dibri
        if (c.isPunct() || c.isSymbol() || u == 0x2013 || u == 0x2014)
            sb.append(QLatin1Char(' '));
        else
            sb.append(c.toLower());
    }
    return sb.split(QRegularExpression(QStringLiteral("\\s+")), Qt::SkipEmptyParts).join(QLatin1Char(' '));
}

SongSearch::SongSearch(const QList<Song>& songs)
{
    m_entries.reserve(songs.size());
    for (const Song& s : songs) {
        Entry e;
        e.song = s;
        e.cleanTitle = normalize(s.title);
        e.compactTitle = withoutSpaces(e.cleanTitle);
        e.cleanAuthor = normalize(s.author);
        e.cleanLyrics = normalize(s.lyricsRaw);
        e.compactLyrics = withoutSpaces(e.cleanLyrics);
        e.cleanFull = e.cleanTitle + QLatin1Char(' ') + e.cleanAuthor + QLatin1Char(' ') + e.cleanLyrics;
        e.compactFull = e.compactTitle + withoutSpaces(e.cleanAuthor) + e.compactLyrics;
        m_entries.append(std::move(e));
    }
}

int SongSearch::matchingStanza(const QStringList& stanzas, const QString& lyricsQuery)
{
    const QString normQ = normalize(lyricsQuery);
    if (normQ.isEmpty())
        return -1;
    const QString compactQ = withoutSpaces(normQ);
    const QStringList tokens = normQ.split(QLatin1Char(' '), Qt::SkipEmptyParts);
    auto tokenMatches = [](const QString& clean, const QString& compact, const QString& token) {
        if (has(clean, token) || has(compact, token))
            return true;
        const auto it = synonyms().constFind(token.toLower());
        return it != synonyms().cend() && (has(clean, *it) || has(compact, *it));
    };
    QStringList clean, compact;
    for (const QString& s : stanzas) {
        clean << normalize(s);
        compact << withoutSpaces(clean.last());
    }
    for (qsizetype i = 0; i < clean.size(); ++i)   // 1. the whole phrase
        if (has(clean[i], normQ) || has(compact[i], compactQ))
            return int(i);
    int best = -1, bestCount = 0;                  // 2. the stanza with the most query words
    for (qsizetype i = 0; i < clean.size(); ++i) {
        int matched = 0;
        for (const QString& t : tokens)
            if (tokenMatches(clean[i], compact[i], t))
                ++matched;
        if (matched > bestCount) {
            bestCount = matched;
            best = int(i);
        }
    }
    return best;
}

QList<Song> SongSearch::filter(const QString& titleQuery, const QString& lyricsQuery, bool favoritesOnly) const
{
    const QString titleQ = titleQuery.trimmed();
    const QString lyricsQ = lyricsQuery.trimmed();
    const bool hasTitle = !titleQ.isEmpty();
    const bool hasLyrics = !lyricsQ.isEmpty();

    auto byTitle = [](const Song& a, const Song& b) { return QString::localeAwareCompare(a.title, b.title) < 0; };

    QList<Song> results;
    if (!hasTitle && !hasLyrics) {
        for (const Entry& e : m_entries)
            if (!favoritesOnly || e.song.isFavorite)
                results.append(e.song);
        std::stable_sort(results.begin(), results.end(), byTitle);
        return results;
    }

    const QString normQ = normalize(hasTitle ? titleQ : lyricsQ);
    const QString compactQ = withoutSpaces(normQ);
    const QStringList tokens = normQ.split(QLatin1Char(' '), Qt::SkipEmptyParts);

    auto tokenMatches = [&](const QString& clean, const QString& compact, const QString& token) {
        if (has(clean, token) || has(compact, token))
            return true;
        const auto it = synonyms().constFind(token.toLower());
        return it != synonyms().cend() && (has(clean, *it) || has(compact, *it));
    };

    QList<QPair<int, Song>> scored;
    for (const Entry& e : m_entries) {
        if (favoritesOnly && !e.song.isFavorite)
            continue;
        int score = 0;
        if (hasTitle) {
            if (e.cleanTitle == normQ) score += 2000;
            else if (e.cleanTitle.startsWith(normQ, Qt::CaseInsensitive)) score += 1600;
            else if (e.compactTitle.startsWith(compactQ, Qt::CaseInsensitive)) score += 1400;
            else if (has(e.cleanTitle, normQ)) score += 1200;
            else if (has(e.compactTitle, compactQ)) score += 1000;
            else {
                int matched = 0;
                for (const QString& t : tokens)
                    if (tokenMatches(e.cleanTitle, e.compactTitle, t))
                        ++matched;
                if (matched == tokens.size()) score += 700;
            }
        } else {
            if (e.cleanTitle == normQ) score += 2000;
            else if (e.cleanTitle.startsWith(normQ, Qt::CaseInsensitive)) score += 1600;
            else if (e.compactTitle.startsWith(compactQ, Qt::CaseInsensitive)) score += 1400;
            else if (has(e.cleanTitle, normQ)) score += 1200;
            else if (has(e.compactTitle, compactQ)) score += 1000;
            else if (has(e.cleanLyrics, normQ)) score += 800;
            else if (has(e.compactLyrics, compactQ)) score += 700;
            int matched = 0;
            for (const QString& t : tokens)
                if (tokenMatches(e.cleanFull, e.compactFull, t))
                    ++matched;
            if (matched == tokens.size())
                score += 500;
        }
        if (score > 0)
            scored.append({score, e.song});
    }
    std::stable_sort(scored.begin(), scored.end(), [&](const auto& a, const auto& b) {
        if (a.first != b.first)
            return a.first > b.first;
        return byTitle(a.second, b.second);
    });
    for (const auto& p : std::as_const(scored))
        results.append(p.second);
    return results;
}

} // namespace Lux
