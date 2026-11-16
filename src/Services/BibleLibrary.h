#pragma once
#include "Models/PresentationItem.h"
#include "Models/Scripture.h"
#include <QHash>
#include <QList>
#include <QString>
#include <QStringList>
#include <memory>
namespace Lux {
// One of the 66 canonical books (English name, short name, chapter count in the usual versification).
struct BookInfo
{
    int id = 0;
    QString name;
    QString shortName;
    int chapters = 0;
    bool oldTestament = true;
};
// A Bible translation held in memory: book -> chapter -> verse text ("" = verse not present in this translation).
class BibleTranslation
{
public:
    struct Book
    {
        int id = 0;
        QString name;
        QString shortName;
        QList<QStringList> chapters;   // chapters[c-1][v-1]
    };
    QString id;
    QString name;
    QString language;
    QString copyright;
    bool builtIn = false;              // the bundled KJV (cannot be removed)
    QList<Book> books;                 // in canonical order, only the books that are present
    const Book* book(int bookId) const;
    int chapterCount(int bookId) const;
    int verseCount(int bookId, int chapter) const;                 // highest verse number of the chapter
    QString verse(int bookId, int chapter, int verse) const;       // "" when absent
    QList<int> versesOf(int bookId, int chapter) const;            // verse numbers that have text
    QString bookName(int bookId) const;
    int totalVerses() const;
};
struct BibleHit
{
    int book = 0, chapter = 0, verse = 0;
    QString text;
};
struct BibleSearchResult
{
    enum class Kind { None, Reference, Text };
    Kind kind = Kind::None;
    QString message;                   // why nothing was found / how many hits
    int book = 0, chapter = 0, verse = 0, verseEnd = 0;   // Reference: the target (verse 0 = whole chapter)
    QList<BibleHit> hits;              // Text
    bool truncated = false;
};
// Verses chosen in one chapter of one translation: shown as a single Scripture slide.
struct BiblePassage
{
    QString translationId, translationName;
    int book = 0, chapter = 0;
    QList<int> verses;
    QString reference;                 // "John 3:16-18"
    QString text;                      // the verses joined with a space
    bool isValid() const { return book > 0 && !verses.isEmpty(); }
    PresentationItem toPresentationItem(const AppSettings* settings = nullptr) const;
};
struct BibleImportResult
{
    bool ok = false;
    QString id, name;
    int books = 0, verses = 0;
    QStringList errors;                // path-style messages ("books[2].chapters[0][3]: ...")
    QStringList warnings;
    QString summary() const;
};
// The installed translations: the bundled KJV (from Data/bibles.json) and user-imported ones stored one file per
// translation in <Data>/Bibles/<ID>.json (never inside the executable folder, never overwritten).
class BibleLibrary
{
public:
    BibleLibrary(const QList<BibleVerse>& bundledVerses, const QString& userDirectory);
    static constexpr const char* kDefaultId = "KJV";
    static const QList<BookInfo>& books();
    static const BookInfo* bookInfo(int id);
    // Documented import format (docs/BIBLE-JSON.md): parses and validates `json`. On success fills *out.
    static BibleImportResult parse(const QByteArray& json, BibleTranslation* out);
    // A tiny example file in the documented format (original text written for this project).
    static QByteArray sampleJson();
    QList<const BibleTranslation*> translations() const;
    const BibleTranslation* find(const QString& id) const;
    const BibleTranslation* defaultTranslation() const;
    QStringList loadWarnings() const { return m_loadWarnings; }
    // Validates and installs a translation file. Refuses a duplicate id or name (never overwrites).
    BibleImportResult importFile(const QString& path);
    bool remove(const QString& id, QString* error = nullptr);
    QString userDirectory() const { return m_userDir; }
    // Reference ("John 3:16", "1 Cor 13", "Ps 23:1-3") or full-text search in one translation.
    static BibleSearchResult search(const BibleTranslation& t, const QString& query, int limit = 200);
    // Book id for a name or abbreviation as typed by the user (0 when unknown or ambiguous; *error says which).
    static int resolveBook(const BibleTranslation& t, const QString& text, QString* error = nullptr);
    // "John 3:16", "John 3:16-18", "John 3:16,18-19" for the given verse numbers of one chapter.
    static BiblePassage passage(const BibleTranslation& t, int book, int chapter, QList<int> verses);
    static QString formatReference(const QString& bookName, int chapter, QList<int> verses);
private:
    void addTranslation(std::unique_ptr<BibleTranslation> t);
    QList<std::shared_ptr<BibleTranslation>> m_translations;
    QString m_userDir;
    QStringList m_loadWarnings;
};
} // namespace Lux
