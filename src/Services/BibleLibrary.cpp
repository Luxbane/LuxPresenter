#include "Services/BibleLibrary.h"
#include "Services/LyricsFormatter.h"
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMap>
#include <QRegularExpression>
#include <QSaveFile>
#include <algorithm>
#include <set>
namespace Lux {
namespace {
const char* const kAbbrev[67] = {"",
    "gen ge gn", "exo ex exod", "lev le lv", "num nu nm nb", "deut deu dt", "josh jos jsh", "judg jdg jg jdgs", "rut ru rth",
    "1sam 1sa 1sm 1s", "2sam 2sa 2sm 2s", "1kgs 1ki 1k 1kin", "2kgs 2ki 2k 2kin", "1chr 1ch 1chron", "2chr 2ch 2chron",
    "ezr", "neh ne", "esth est es", "jb", "ps psa psalm pss psm", "prov pro prv pr", "eccl ecc ec qoh",
    "song sos ss songofsongs canticles sg", "isa is", "jer je jr", "lam la", "ezek eze ezk", "dan da dn", "hos ho", "joe jl",
    "amo am", "obad oba ob", "jon jnh", "mic mc", "nah na", "hab hb", "zeph zep zp", "hag hg", "zech zec zc", "mal ml",
    "matt mat mt", "mrk mar mk mr", "luk lk", "joh jhn jn", "act ac", "rom ro rm", "1cor", "2cor", "gal ga", "eph ephes",
    "phil php pp", "col", "1thess 1thes 1th", "2thess 2thes 2th", "1tim 1ti", "2tim 2ti", "tit", "philem phm pm", "heb",
    "jas jm", "1pet 1pe 1pt 1p", "2pet 2pe 2pt 2p", "1john 1jn 1jo 1j", "2john 2jn 2jo 2j", "3john 3jn 3jo 3j", "jud jude jd",
    "rev re rv revelations"};
QString norm(const QString& s)
{
    QString out;
    for (const QChar c : s.toLower())
        if (c.isLetterOrNumber())
            out.append(c);
    return out;
}
bool validId(const QString& id)
{
    static const QRegularExpression re(QStringLiteral("^[A-Za-z0-9_-]{2,12}$"));
    return re.match(id).hasMatch();
}
// Collects messages; only the first ones are shown to the user.
struct Errors
{
    QStringList list;
    void add(const QString& path, const QString& msg) { if (list.size() < 60) list << (path.isEmpty() ? msg : path + QStringLiteral(": ") + msg); else ++more; }
    int more = 0;
};
bool asInt(const QJsonValue& v, int* out)
{
    if (!v.isDouble())
        return false;
    const double d = v.toDouble();
    if (d != std::floor(d) || d < -1e9 || d > 1e9)
        return false;
    *out = int(d);
    return true;
}
} // namespace
const QList<BookInfo>& BibleLibrary::books()
{
    struct Row { int id; const char* name; const char* shortName; int chapters; bool ot; };
    static const Row rows[] = {
    {1, "Genesis", "Gen", 50, true},
    {2, "Exodus", "Exo", 40, true},
    {3, "Leviticus", "Lev", 27, true},
    {4, "Numbers", "Num", 36, true},
    {5, "Deuteronomy", "Deu", 34, true},
    {6, "Joshua", "Jos", 24, true},
    {7, "Judges", "Jdg", 21, true},
    {8, "Ruth", "Rut", 4, true},
    {9, "1 Samuel", "1Sa", 31, true},
    {10, "2 Samuel", "2Sa", 24, true},
    {11, "1 Kings", "1Ki", 22, true},
    {12, "2 Kings", "2Ki", 25, true},
    {13, "1 Chronicles", "1Ch", 29, true},
    {14, "2 Chronicles", "2Ch", 36, true},
    {15, "Ezra", "Ezr", 10, true},
    {16, "Nehemiah", "Neh", 13, true},
    {17, "Esther", "Est", 10, true},
    {18, "Job", "Job", 42, true},
    {19, "Psalms", "Psa", 150, true},
    {20, "Proverbs", "Pro", 31, true},
    {21, "Ecclesiastes", "Ecc", 12, true},
    {22, "Song of Solomon", "Sng", 8, true},
    {23, "Isaiah", "Isa", 66, true},
    {24, "Jeremiah", "Jer", 52, true},
    {25, "Lamentations", "Lam", 5, true},
    {26, "Ezekiel", "Ezk", 48, true},
    {27, "Daniel", "Dan", 12, true},
    {28, "Hosea", "Hos", 14, true},
    {29, "Joel", "Joe", 3, true},
    {30, "Amos", "Amo", 9, true},
    {31, "Obadiah", "Oba", 1, true},
    {32, "Jonah", "Jon", 4, true},
    {33, "Micah", "Mic", 7, true},
    {34, "Nahum", "Nah", 3, true},
    {35, "Habakkuk", "Hab", 3, true},
    {36, "Zephaniah", "Zep", 3, true},
    {37, "Haggai", "Hag", 2, true},
    {38, "Zechariah", "Zec", 14, true},
    {39, "Malachi", "Mal", 4, true},
    {40, "Matthew", "Mat", 28, false},
    {41, "Mark", "Mrk", 16, false},
    {42, "Luke", "Luk", 24, false},
    {43, "John", "Jhn", 21, false},
    {44, "Acts", "Act", 28, false},
    {45, "Romans", "Rom", 16, false},
    {46, "1 Corinthians", "1Co", 16, false},
    {47, "2 Corinthians", "2Co", 13, false},
    {48, "Galatians", "Gal", 6, false},
    {49, "Ephesians", "Eph", 6, false},
    {50, "Philippians", "Php", 4, false},
    {51, "Colossians", "Col", 4, false},
    {52, "1 Thessalonians", "1Th", 5, false},
    {53, "2 Thessalonians", "2Th", 3, false},
    {54, "1 Timothy", "1Ti", 6, false},
    {55, "2 Timothy", "2Ti", 4, false},
    {56, "Titus", "Tit", 3, false},
    {57, "Filemon", "Phm", 1, false},
    {58, "Hebrews", "Heb", 13, false},
    {59, "James", "Jas", 5, false},
    {60, "1 Peter", "1Pe", 5, false},
    {61, "2 Peter", "2Pe", 3, false},
    {62, "1 John", "1Jn", 5, false},
    {63, "2 John", "2Jn", 1, false},
    {64, "3 John", "3Jn", 1, false},
    {65, "Jude", "Jud", 1, false},
    {66, "Revelation", "Rev", 22, false},
    };
    static const QList<BookInfo> list = [] {
        QList<BookInfo> l;
        for (const Row& r : rows)
            l << BookInfo{r.id, QString::fromLatin1(r.name), QString::fromLatin1(r.shortName), r.chapters, r.ot};
        return l;
    }();
    return list;
}
const BookInfo* BibleLibrary::bookInfo(int id)
{
    const auto& l = books();
    return (id >= 1 && id <= l.size()) ? &l.at(id - 1) : nullptr;
}
// ---- BibleTranslation ------------------------------------------------------------------
const BibleTranslation::Book* BibleTranslation::book(int bookId) const
{
    for (const Book& b : books)
        if (b.id == bookId)
            return &b;
    return nullptr;
}
int BibleTranslation::chapterCount(int bookId) const
{
    const Book* b = book(bookId);
    return b ? int(b->chapters.size()) : 0;
}
int BibleTranslation::verseCount(int bookId, int chapter) const
{
    const Book* b = book(bookId);
    return (b && chapter >= 1 && chapter <= b->chapters.size()) ? int(b->chapters.at(chapter - 1).size()) : 0;
}
QString BibleTranslation::verse(int bookId, int chapter, int v) const
{
    const Book* b = book(bookId);
    if (!b || chapter < 1 || chapter > b->chapters.size())
        return {};
    const QStringList& c = b->chapters.at(chapter - 1);
    return (v >= 1 && v <= c.size()) ? c.at(v - 1) : QString();
}
QList<int> BibleTranslation::versesOf(int bookId, int chapter) const
{
    QList<int> out;
    const Book* b = book(bookId);
    if (b && chapter >= 1 && chapter <= b->chapters.size())
        for (int i = 0; i < b->chapters.at(chapter - 1).size(); ++i)
            if (!b->chapters.at(chapter - 1).at(i).isEmpty())
                out << i + 1;
    return out;
}
QString BibleTranslation::bookName(int bookId) const
{
    const Book* b = book(bookId);
    return b ? b->name : (BibleLibrary::bookInfo(bookId) ? BibleLibrary::bookInfo(bookId)->name : QString());
}
int BibleTranslation::totalVerses() const
{
    int n = 0;
    for (const Book& b : books)
        for (const QStringList& c : b.chapters)
            for (const QString& v : c)
                n += v.isEmpty() ? 0 : 1;
    return n;
}
QString BibleImportResult::summary() const
{
    QStringList l = errors.mid(0, 10);
    if (errors.size() > 10)
        l << QStringLiteral("... and %1 more problem(s)").arg(errors.size() - 10);
    return l.join(QLatin1Char('\n'));
}// ---- parsing / validation ---------------------------------------------------------------
namespace {
int canonicalBookByName(const QString& text)
{
    const QString n = norm(text);
    if (n.isEmpty())
        return 0;
    for (const BookInfo& b : BibleLibrary::books())
        if (norm(b.name) == n)
            return b.id;
    for (int id = 1; id <= 66; ++id)
        if (QString::fromLatin1(kAbbrev[id]).split(QLatin1Char(' ')).contains(n))
            return id;
    return 0;
}
void finish(BibleTranslation& t, QMap<int, BibleTranslation::Book>& books)
{
    t.books.clear();
    for (auto it = books.begin(); it != books.end(); ++it)
        t.books.append(it.value());
}
BibleImportResult parseFlat(const QJsonArray& arr, BibleTranslation* out)
{
    BibleImportResult r;
    Errors e;
    if (arr.size() > 200000)
        e.add({}, QStringLiteral("too many entries (%1).").arg(arr.size()));
    QMap<int, BibleTranslation::Book> books;
    QString version;
    int count = 0;
    for (int i = 0; i < arr.size() && e.list.isEmpty(); ++i) {
        const QString at = QStringLiteral("[%1]").arg(i);
        if (!arr[i].isObject()) {
            e.add(at, QStringLiteral("expected an object with Book, Chapter, Verse, Text and Version."));
            continue;
        }
        const QJsonObject o = arr[i].toObject();
        const QString v = o.value(QStringLiteral("Version")).toString().trimmed();
        if (v.isEmpty()) {
            e.add(at + QStringLiteral(".Version"), QStringLiteral("missing."));
            continue;
        }
        if (version.isEmpty())
            version = v;
        else if (version.compare(v, Qt::CaseInsensitive) != 0) {
            e.add(at + QStringLiteral(".Version"), QStringLiteral("the file mixes several versions ('%1' and '%2'); import one version per file.").arg(version, v));
            continue;
        }
        int bookId = 0, chapter = 0, verse = 0;
        const QString bookName = o.value(QStringLiteral("Book")).toString().trimmed();
        if (!asInt(o.value(QStringLiteral("BookId")), &bookId) || bookId < 1 || bookId > 66)
            bookId = canonicalBookByName(bookName);
        if (bookId == 0) {
            e.add(at + QStringLiteral(".Book"), QStringLiteral("unknown book '%1' (give BookId 1-66 or an English book name).").arg(bookName));
            continue;
        }
        if (!asInt(o.value(QStringLiteral("Chapter")), &chapter) || chapter < 1 || chapter > 150 ||
            !asInt(o.value(QStringLiteral("Verse")), &verse) || verse < 1 || verse > 200) {
            e.add(at, QStringLiteral("Chapter must be 1-150 and Verse 1-200."));
            continue;
        }
        if (!o.value(QStringLiteral("Text")).isString()) {
            e.add(at + QStringLiteral(".Text"), QStringLiteral("expected a string."));
            continue;
        }
        BibleTranslation::Book& b = books[bookId];
        if (b.id == 0) {
            b.id = bookId;
            b.name = bookName.isEmpty() ? BibleLibrary::bookInfo(bookId)->name : bookName;
            b.shortName = BibleLibrary::bookInfo(bookId)->shortName;
        }
        while (b.chapters.size() < chapter)
            b.chapters.append(QStringList());
        QStringList& c = b.chapters[chapter - 1];
        while (c.size() < verse)
            c.append(QString());
        c[verse - 1] = o.value(QStringLiteral("Text")).toString().trimmed();
        ++count;
    }
    if (e.list.isEmpty() && count == 0)
        e.add({}, QStringLiteral("the file contains no verses."));
    r.errors = e.list;
    if (!r.errors.isEmpty())
        return r;
    if (!validId(version))
        r.errors << QStringLiteral("Version '%1': the id must be 2-12 characters (letters, digits, _ or -).").arg(version);
    if (!r.errors.isEmpty())
        return r;
    BibleTranslation t;
    t.id = version;
    t.name = version;
    finish(t, books);
    r.ok = true;
    r.id = t.id;
    r.name = t.name;
    r.books = int(t.books.size());
    r.verses = t.totalVerses();
    r.warnings << QStringLiteral("The file uses the flat list format; it was converted to the documented format.");
    if (out)
        *out = t;
    return r;
}
} // namespace
BibleImportResult BibleLibrary::parse(const QByteArray& json, BibleTranslation* out)
{
    BibleImportResult r;
    QJsonParseError pe;
    const QJsonDocument doc = QJsonDocument::fromJson(json, &pe);
    if (doc.isNull()) {
        r.errors << QStringLiteral("Not valid JSON: %1 (at byte %2).").arg(pe.errorString()).arg(pe.offset);
        return r;
    }
    if (doc.isArray())
        return parseFlat(doc.array(), out);
    if (!doc.isObject()) {
        r.errors << QStringLiteral("The file must contain a JSON object.");
        return r;
    }
    const QJsonObject o = doc.object();
    Errors e;
    int schema = 1;
    if (o.contains(QStringLiteral("schema")) && (!asInt(o.value(QStringLiteral("schema")), &schema) || schema != 1))
        e.add(QStringLiteral("schema"), QStringLiteral("unsupported schema version (this program reads schema 1)."));
    BibleTranslation t;
    t.id = o.value(QStringLiteral("id")).toString().trimmed();
    t.name = o.value(QStringLiteral("name")).toString().trimmed();
    t.language = o.value(QStringLiteral("language")).toString().trimmed();
    t.copyright = o.value(QStringLiteral("copyright")).toString().trimmed();
    if (!o.value(QStringLiteral("id")).isString() || !validId(t.id))
        e.add(QStringLiteral("id"), QStringLiteral("required: 2-12 characters, letters, digits, '_' or '-' (for example \"TB\")."));
    if (!o.value(QStringLiteral("name")).isString() || t.name.isEmpty() || t.name.size() > 80)
        e.add(QStringLiteral("name"), QStringLiteral("required: the translation's name (1-80 characters)."));
    if (t.copyright.size() > 1000)
        e.add(QStringLiteral("copyright"), QStringLiteral("too long (max 1000 characters)."));
    if (!o.value(QStringLiteral("books")).isArray() || o.value(QStringLiteral("books")).toArray().isEmpty())
        e.add(QStringLiteral("books"), QStringLiteral("required: a non-empty array of books."));
    std::set<int> seen;
    const QJsonArray books = o.value(QStringLiteral("books")).toArray();
    QMap<int, BibleTranslation::Book> map;
    for (int bi = 0; bi < books.size(); ++bi) {
        const QString bp = QStringLiteral("books[%1]").arg(bi);
        if (!books[bi].isObject()) {
            e.add(bp, QStringLiteral("expected an object."));
            continue;
        }
        const QJsonObject bo = books[bi].toObject();
        BibleTranslation::Book b;
        if (!asInt(bo.value(QStringLiteral("id")), &b.id) || b.id < 1 || b.id > 66)
            e.add(bp + QStringLiteral(".id"), QStringLiteral("must be a whole number 1-66 (1 = Genesis ... 66 = Revelation)."));
        else if (!seen.insert(b.id).second)
            e.add(bp + QStringLiteral(".id"), QStringLiteral("book %1 appears twice.").arg(b.id));
        b.name = bo.value(QStringLiteral("name")).toString().trimmed();
        if (!bo.value(QStringLiteral("name")).isString() || b.name.isEmpty() || b.name.size() > 60)
            e.add(bp + QStringLiteral(".name"), QStringLiteral("required: the book's name (1-60 characters)."));
        b.shortName = bo.value(QStringLiteral("shortName")).toString().trimmed();
        const QJsonValue chv = bo.value(QStringLiteral("chapters"));
        if (!chv.isArray() || chv.toArray().isEmpty() || chv.toArray().size() > 150) {
            e.add(bp + QStringLiteral(".chapters"), QStringLiteral("required: an array of 1-150 chapters, each an array of verse texts."));
            continue;
        }
        const QJsonArray chs = chv.toArray();
        int verses = 0;
        for (int ci = 0; ci < chs.size(); ++ci) {
            const QString cp = QStringLiteral("%1.chapters[%2]").arg(bp).arg(ci);
            if (!chs[ci].isArray() || chs[ci].toArray().size() > 200) {
                e.add(cp, QStringLiteral("expected an array of at most 200 verse texts (use null or \"\" for a missing verse)."));
                b.chapters.append(QStringList());
                continue;
            }
            QStringList verseTexts;
            const QJsonArray vs = chs[ci].toArray();
            for (int vi = 0; vi < vs.size(); ++vi) {
                if (vs[vi].isNull())
                    verseTexts << QString();
                else if (vs[vi].isString()) {
                    verseTexts << vs[vi].toString().trimmed();
                    verses += verseTexts.last().isEmpty() ? 0 : 1;
                } else {
                    e.add(QStringLiteral("%1[%2]").arg(cp).arg(vi), QStringLiteral("expected a string or null."));
                    verseTexts << QString();
                }
            }
            while (!verseTexts.isEmpty() && verseTexts.last().isEmpty())
                verseTexts.removeLast();
            b.chapters.append(verseTexts);
        }
        while (!b.chapters.isEmpty() && b.chapters.last().isEmpty())
            b.chapters.removeLast();
        if (verses == 0 && e.list.isEmpty())
            r.warnings << QStringLiteral("%1 (%2) has no verse text and was skipped.").arg(bp, b.name);
        else if (b.id >= 1 && b.id <= 66)
            map.insert(b.id, b);
    }
    r.errors = e.list;
    if (r.errors.isEmpty() && map.isEmpty())
        r.errors << QStringLiteral("books: the file contains no verse text.");
    if (!r.errors.isEmpty())
        return r;
    finish(t, map);
    r.ok = true;
    r.id = t.id;
    r.name = t.name;
    r.books = int(t.books.size());
    r.verses = t.totalVerses();
    if (out)
        *out = t;
    return r;
}
QByteArray BibleLibrary::sampleJson()
{
    return QByteArrayLiteral(
        "{\n"
        "  \"schema\": 1,\n"
        "  \"id\": \"SAMPLE\",\n"
        "  \"name\": \"Sample Translation (demo text)\",\n"
        "  \"language\": \"en\",\n"
        "  \"copyright\": \"Demo text written for LuxPresenter; it is not a real Bible translation.\",\n"
        "  \"books\": [\n"
        "    {\n"
        "      \"id\": 1,\n"
        "      \"name\": \"Genesis\",\n"
        "      \"shortName\": \"Gen\",\n"
        "      \"chapters\": [\n"
        "        [\"Sample text for Genesis chapter 1, verse 1.\", \"Sample text for Genesis chapter 1, verse 2.\", \"Sample text for Genesis chapter 1, verse 3.\"],\n"
        "        [\"Sample text for Genesis chapter 2, verse 1.\", null, \"Sample text for Genesis chapter 2, verse 3 (verse 2 is left out on purpose).\"]\n"
        "      ]\n"
        "    },\n"
        "    {\n"
        "      \"id\": 43,\n"
        "      \"name\": \"John\",\n"
        "      \"shortName\": \"Jn\",\n"
        "      \"chapters\": [\n"
        "        [\"Sample text for John chapter 1, verse 1.\", \"Sample text for John chapter 1, verse 2.\"]\n"
        "      ]\n"
        "    }\n"
        "  ]\n"
        "}\n");
}// ---- the library -----------------------------------------------------------------------
namespace {
QByteArray toSchemaJson(const BibleTranslation& t)
{
    QJsonObject o;
    o[QStringLiteral("schema")] = 1;
    o[QStringLiteral("id")] = t.id;
    o[QStringLiteral("name")] = t.name;
    o[QStringLiteral("language")] = t.language;
    o[QStringLiteral("copyright")] = t.copyright;
    QJsonArray books;
    for (const BibleTranslation::Book& b : t.books) {
        QJsonObject bo;
        bo[QStringLiteral("id")] = b.id;
        bo[QStringLiteral("name")] = b.name;
        bo[QStringLiteral("shortName")] = b.shortName;
        QJsonArray chs;
        for (const QStringList& c : b.chapters) {
            QJsonArray vs;
            for (const QString& v : c)
                vs.append(v.isEmpty() ? QJsonValue(QJsonValue::Null) : QJsonValue(v));
            chs.append(vs);
        }
        bo[QStringLiteral("chapters")] = chs;
        books.append(bo);
    }
    o[QStringLiteral("books")] = books;
    return QJsonDocument(o).toJson(QJsonDocument::Indented);
}
} // namespace
BibleLibrary::BibleLibrary(const QList<BibleVerse>& bundledVerses, const QString& userDirectory) : m_userDir(userDirectory)
{
    // bundled data (Data/bibles.json): one translation per Version found in it
    QMap<QString, std::shared_ptr<BibleTranslation>> byVersion;
    QMap<QString, QMap<int, BibleTranslation::Book>> books;
    for (const BibleVerse& v : bundledVerses) {
        if (v.bookId < 1 || v.bookId > 66 || v.chapter < 1 || v.verse < 1 || v.text.trimmed().isEmpty())
            continue;
        auto& t = byVersion[v.version];
        if (!t) {
            t = std::make_shared<BibleTranslation>();
            t->id = v.version;
            t->builtIn = v.version.compare(QLatin1String(kDefaultId), Qt::CaseInsensitive) == 0;
            t->name = t->builtIn ? QStringLiteral("King James Version") : v.version;
            t->language = t->builtIn ? QStringLiteral("en") : QString();
            t->copyright = t->builtIn ? QStringLiteral("Public domain. See licenses/KJV-NOTICE.txt.") : QString();
        }
        BibleTranslation::Book& b = books[v.version][v.bookId];
        if (b.id == 0) {
            b.id = v.bookId;
            b.name = v.book.isEmpty() ? bookInfo(v.bookId)->name : v.book;
            b.shortName = bookInfo(v.bookId)->shortName;
        }
        while (b.chapters.size() < v.chapter)
            b.chapters.append(QStringList());
        QStringList& c = b.chapters[v.chapter - 1];
        while (c.size() < v.verse)
            c.append(QString());
        c[v.verse - 1] = v.text.trimmed();
    }
    for (auto it = byVersion.begin(); it != byVersion.end(); ++it) {
        finish(*it.value(), books[it.key()]);
        m_translations.append(it.value());
    }
    std::stable_sort(m_translations.begin(), m_translations.end(), [](const auto& a, const auto& b) { return a->builtIn && !b->builtIn; });
    // the user's translations
    const QStringList files = QDir(m_userDir).entryList({QStringLiteral("*.json")}, QDir::Files, QDir::Name);
    for (const QString& f : files) {
        QFile file(QDir(m_userDir).filePath(f));
        if (!file.open(QIODevice::ReadOnly)) {
            m_loadWarnings << QStringLiteral("%1: cannot be opened.").arg(f);
            continue;
        }
        const QByteArray bytes = file.readAll();
        file.close();
        auto t = std::make_shared<BibleTranslation>();
        const BibleImportResult r = parse(bytes, t.get());
        if (!r.ok) {
            m_loadWarnings << QStringLiteral("%1: %2").arg(f, r.errors.value(0));
            continue;
        }
        bool dup = false;
        for (const auto& e : m_translations)
            dup = dup || e->id.compare(t->id, Qt::CaseInsensitive) == 0 || e->name.compare(t->name, Qt::CaseInsensitive) == 0;
        if (dup) {
            m_loadWarnings << QStringLiteral("%1: a translation with the same id or name is already installed; skipped.").arg(f);
            continue;
        }
        m_translations.append(t);
    }
}
QList<const BibleTranslation*> BibleLibrary::translations() const
{
    QList<const BibleTranslation*> l;
    for (const auto& t : m_translations)
        l << t.get();
    return l;
}
const BibleTranslation* BibleLibrary::find(const QString& id) const
{
    for (const auto& t : m_translations)
        if (t->id.compare(id, Qt::CaseInsensitive) == 0)
            return t.get();
    return nullptr;
}
const BibleTranslation* BibleLibrary::defaultTranslation() const
{
    if (const BibleTranslation* t = find(QLatin1String(kDefaultId)))
        return t;
    return m_translations.isEmpty() ? nullptr : m_translations.first().get();
}
BibleImportResult BibleLibrary::importFile(const QString& path)
{
    BibleImportResult r;
    QFile f(path);
    if (!f.exists()) {
        r.errors << QStringLiteral("The file does not exist.");
        return r;
    }
    if (f.size() > 80LL * 1024 * 1024) {
        r.errors << QStringLiteral("The file is larger than 80 MB.");
        return r;
    }
    if (!f.open(QIODevice::ReadOnly)) {
        r.errors << QStringLiteral("The file cannot be read: %1").arg(f.errorString());
        return r;
    }
    const QByteArray bytes = f.readAll();
    f.close();
    auto t = std::make_shared<BibleTranslation>();
    r = parse(bytes, t.get());
    if (!r.ok)
        return r;
    for (const auto& e : m_translations) {
        if (e->id.compare(t->id, Qt::CaseInsensitive) == 0)
            r.errors << QStringLiteral("A translation with the id '%1' is already installed ('%2'). Nothing was changed.").arg(t->id, e->name);
        else if (e->name.compare(t->name, Qt::CaseInsensitive) == 0)
            r.errors << QStringLiteral("A translation named '%1' is already installed (id '%2'). Nothing was changed.").arg(t->name, e->id);
    }
    if (!r.errors.isEmpty()) {
        r.ok = false;
        return r;
    }
    QDir().mkpath(m_userDir);
    const QString target = QDir(m_userDir).filePath(t->id.toUpper() + QStringLiteral(".json"));
    if (QFileInfo::exists(target)) {
        r.ok = false;
        r.errors << QStringLiteral("The file %1 already exists in the Bibles folder and was not replaced.").arg(QDir::toNativeSeparators(target));
        return r;
    }
    QSaveFile out(target);
    if (!out.open(QIODevice::WriteOnly) || out.write(toSchemaJson(*t)) < 0 || !out.commit()) {
        r.ok = false;
        r.errors << QStringLiteral("The translation could not be saved: %1").arg(out.errorString());
        return r;
    }
    m_translations.append(t);
    return r;
}
bool BibleLibrary::remove(const QString& id, QString* error)
{
    for (int i = 0; i < m_translations.size(); ++i) {
        if (m_translations[i]->id.compare(id, Qt::CaseInsensitive) != 0)
            continue;
        if (m_translations[i]->builtIn) {
            if (error)
                *error = QStringLiteral("The built-in translation cannot be removed.");
            return false;
        }
        const QString file = QDir(m_userDir).filePath(m_translations[i]->id.toUpper() + QStringLiteral(".json"));
        if (QFileInfo::exists(file) && !QFile::remove(file)) {
            if (error)
                *error = QStringLiteral("The file %1 could not be deleted.").arg(QDir::toNativeSeparators(file));
            return false;
        }
        m_translations.removeAt(i);
        return true;
    }
    if (error)
        *error = QStringLiteral("Unknown translation.");
    return false;
}
// ---- reference parsing and search --------------------------------------------------------
int BibleLibrary::resolveBook(const BibleTranslation& t, const QString& text, QString* error)
{
    const QString n = norm(text);
    if (n.isEmpty() || !n.at(0).isLetterOrNumber() || n.size() < 2) {
        if (error)
            *error = QStringLiteral("Unknown book.");
        return 0;
    }
    // exact names / abbreviations (the translation's own names first)
    for (const BibleTranslation::Book& b : t.books)
        if (norm(b.name) == n || (!b.shortName.isEmpty() && norm(b.shortName) == n))
            return b.id;
    const int exact = canonicalBookByName(text);
    if (exact)
        return exact;
    // unique prefix of a full name
    std::set<int> hit;
    for (const BibleTranslation::Book& b : t.books)
        if (norm(b.name).startsWith(n))
            hit.insert(b.id);
    for (const BookInfo& b : BibleLibrary::books())
        if (norm(b.name).startsWith(n))
            hit.insert(b.id);
    if (hit.size() == 1)
        return *hit.begin();
    if (error) {
        QStringList names;
        for (int id : hit)
            names << t.bookName(id);
        *error = hit.empty() ? QStringLiteral("Unknown book '%1'.").arg(text.trimmed())
                             : QStringLiteral("'%1' could mean %2.").arg(text.trimmed(), names.join(QStringLiteral(" or ")));
    }
    return 0;
}
BiblePassage BibleLibrary::passage(const BibleTranslation& t, int book, int chapter, QList<int> verses)
{
    BiblePassage p;
    std::sort(verses.begin(), verses.end());
    verses.erase(std::unique(verses.begin(), verses.end()), verses.end());
    QStringList texts;
    for (int v : verses) {
        const QString s = t.verse(book, chapter, v);
        if (!s.isEmpty()) {
            texts << s;
            p.verses << v;
        }
    }
    if (p.verses.isEmpty())
        return p;
    p.translationId = t.id;
    p.translationName = t.name;
    p.book = book;
    p.chapter = chapter;
    p.text = texts.join(QLatin1Char(' '));
    p.reference = formatReference(t.bookName(book), chapter, p.verses);
    return p;
}
PresentationItem BiblePassage::toPresentationItem(const AppSettings* settings) const
{
    const AppSettings defaults;
    PresentationItem item;
    item.title = reference;
    item.subtitle = translationId;
    item.category = ItemCategory::Scripture;
    Slide s;
    s.label = reference;
    s.title = reference;
    s.subtitle = translationId;   // shown as "(KJV)" after the verse text
    s.content = LyricsFormatter::format(text, settings ? settings : &defaults);
    s.type = SlideType::Scripture;
    s.fontSize = 13;
    item.slides.append(s);
    return item;
}QString BibleLibrary::formatReference(const QString& bookName, int chapter, QList<int> verses)
{
    std::sort(verses.begin(), verses.end());
    verses.erase(std::unique(verses.begin(), verses.end()), verses.end());
    QStringList parts;
    for (int i = 0; i < verses.size();) {
        int j = i;
        while (j + 1 < verses.size() && verses[j + 1] == verses[j] + 1)
            ++j;
        parts << (j > i ? QStringLiteral("%1-%2").arg(verses[i]).arg(verses[j]) : QString::number(verses[i]));
        i = j + 1;
    }
    return QStringLiteral("%1 %2:%3").arg(bookName).arg(chapter).arg(parts.join(QLatin1Char(',')));
}
BibleSearchResult BibleLibrary::search(const BibleTranslation& t, const QString& queryIn, int limit)
{
    BibleSearchResult r;
    const QString query = queryIn.trimmed();
    if (query.isEmpty())
        return r;
    static const QRegularExpression refRe(
        QStringLiteral("^(.+?)(?:\\s+|(?<=\\p{L})(?=\\d))(\\d+)(?:(?:\\s*[:.]\\s*|\\s+)(\\d+)(?:\\s*[-\\x{2013}]\\s*(\\d+))?)?$"));
    static const QRegularExpression bookOnly(QStringLiteral("^[1-3]?\\s*\\p{L}[\\p{L}.\\- ]*$"));
    int book = 0, chapter = 0, verse = 0, verseEnd = 0;
    QString why;
    const auto m = refRe.match(query);
    if (m.hasMatch()) {
        book = resolveBook(t, m.captured(1), &why);
        chapter = m.captured(2).toInt();
        verse = m.captured(3).toInt();
        verseEnd = m.captured(4).toInt();
    } else if (bookOnly.match(query).hasMatch()) {
        book = resolveBook(t, query, &why);
        chapter = 1;
    }
    if (book) {
        const BibleTranslation::Book* b = t.book(book);
        if (!b) {
            r.message = QStringLiteral("%1 is not in %2.").arg(t.bookName(book), t.name);
            return r;
        }
        if (chapter < 1 || chapter > b->chapters.size()) {
            r.message = QStringLiteral("%1 has chapters 1-%2.").arg(b->name).arg(b->chapters.size());
            return r;
        }
        if (verse > 0) {
            if (t.verse(book, chapter, verse).isEmpty()) {
                r.message = QStringLiteral("%1 %2:%3 is not in %4 (the chapter has %5 verses).").arg(b->name).arg(chapter).arg(verse).arg(t.name).arg(t.verseCount(book, chapter));
                return r;
            }
            if (verseEnd && verseEnd < verse) {
                r.message = QStringLiteral("The verse range %1-%2 is backwards.").arg(verse).arg(verseEnd);
                return r;
            }
        }
        r.kind = BibleSearchResult::Kind::Reference;
        r.book = book;
        r.chapter = chapter;
        r.verse = verse;
        r.verseEnd = std::min(verseEnd, t.verseCount(book, chapter));
        return r;
    }
    // full-text search: every word must occur
    const QStringList words = query.toLower().split(QRegularExpression(QStringLiteral("\\s+")), Qt::SkipEmptyParts);
    for (const BibleTranslation::Book& b : t.books)
        for (int c = 0; c < b.chapters.size(); ++c)
            for (int v = 0; v < b.chapters[c].size(); ++v) {
                const QString& text = b.chapters[c][v];
                if (text.isEmpty())
                    continue;
                const QString low = text.toLower();
                bool all = true;
                for (const QString& w : words)
                    all = all && low.contains(w);
                if (!all)
                    continue;
                if (r.hits.size() >= limit) {
                    r.truncated = true;
                    break;
                }
                r.hits.append({b.id, c + 1, v + 1, text});
            }
    r.kind = BibleSearchResult::Kind::Text;
    r.message = r.hits.isEmpty() ? QStringLiteral("No verse contains \"%1\".").arg(query)
                                 : (r.truncated ? QStringLiteral("First %1 matches (refine the search to narrow it down).").arg(r.hits.size())
                                                : QStringLiteral("%1 match(es).").arg(r.hits.size()));
    return r;
}
} // namespace Lux