// Milestone 3 core tests: song search, schedule service, song backup import/export, sample songs
// and compatibility with the JSON shapes written by the reference application (System.Text.Json).

#include "Models/ScheduleFileData.h"
#include "Models/Song.h"
#include "Services/AppPaths.h"
#include "Services/DataService.h"
#include "Services/ScheduleService.h"
#include "Services/SongImportExport.h"
#include "Services/SongSearch.h"
#include "Services/TextRepair.h"

#include <QDir>
#include <QFile>
#include <QJsonArray>
#include <QJsonDocument>
#include <QTemporaryDir>
#include <QtTest>

using namespace Lux;

namespace {

struct Sandbox
{
    Sandbox()
    {
        QDir().mkpath(QStringLiteral(LUX_TEST_TMP));
        dir = std::make_unique<QTemporaryDir>(QStringLiteral(LUX_TEST_TMP "/m3-XXXXXX"));
        AppPaths::setBaseDirectoryOverride(dir->path());
    }
    ~Sandbox() { AppPaths::setBaseDirectoryOverride(QString()); }
    QString path(const QString& rel) const { return QDir(dir->path()).filePath(rel); }
    std::unique_ptr<QTemporaryDir> dir;
};

Song makeSong(const QString& title, const QString& author, const QString& lyrics, bool fav = false)
{
    Song s;
    s.title = title;
    s.author = author;
    s.lyricsRaw = lyrics;
    s.isFavorite = fav;
    return s;
}

PresentationItem presentation(const QString& title, int slides = 2)
{
    PresentationItem p;
    p.title = title;
    for (int i = 0; i < slides; ++i) {
        Slide s;
        s.label = QStringLiteral("Verse %1").arg(i + 1);
        s.content = QStringLiteral("%1 line %2").arg(title).arg(i + 1);
        p.slides.append(s);
    }
    return p;
}

QStringList titles(const QList<Song>& songs)
{
    QStringList t;
    for (const Song& s : songs)
        t << s.title;
    return t;
}

QStringList scheduleTitles(const ScheduleService& s)
{
    QStringList t;
    for (const ScheduleItem& i : s.items())
        t << i.title;
    return t;
}

} // namespace

class SongsScheduleTests : public QObject
{
    Q_OBJECT

private slots:
    // ---- search ----------------------------------------------------------------------
    void normalizeStripsPunctuationAndQuotes()
    {
        QCOMPARE(SongSearch::normalize(QStringLiteral("Dib'ri \u2014 Allah!  Besar")), QStringLiteral("dibri allah besar"));
        QCOMPARE(SongSearch::normalize(QStringLiteral("It\u2019s \u201Cwell\u201D, with-my soul")), QStringLiteral("its well with my soul"));
        QCOMPARE(SongSearch::normalize(QStringLiteral("   ")), QString());
        QCOMPARE(SongSearch::normalize(QStringLiteral("O  Come\tAll")), QStringLiteral("o come all"));
    }

    void titleSearchRanksExactThenPrefixThenContains()
    {
        const SongSearch idx({makeSong("Amazing Grace", "John Newton", "Amazing grace how sweet the sound"),
                              makeSong("Grace", "X", "plain"),
                              makeSong("Grace Alone", "Y", "alone"),
                              makeSong("How Great Is Our God", "Chris", "great is our God")});
        QCOMPARE(titles(idx.filter(QStringLiteral("grace"), QString(), false)),
                 (QStringList{"Grace", "Grace Alone", "Amazing Grace"})); // exact 2000, prefix 1600, contains 1200
        QCOMPARE(titles(idx.filter(QStringLiteral("AMAZING"), QString(), false)), (QStringList{"Amazing Grace"}));
        QVERIFY(idx.filter(QStringLiteral("zzz"), QString(), false).isEmpty());
        // compact matching ignores spaces/punctuation: "howgreat" finds "How Great Is Our God"
        QCOMPARE(titles(idx.filter(QStringLiteral("how-great"), QString(), false)), (QStringList{"How Great Is Our God"}));
    }

    void lyricsSearchFindsWordsAnywhere()
    {
        const SongSearch idx({makeSong("Amazing Grace", "John Newton", "Amazing grace how sweet the sound\nThat saved a wretch"),
                              makeSong("Other", "Someone", "nothing relevant here")});
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("sweet sound"), false)), (QStringList{"Amazing Grace"}));
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("a wretch"), false)), (QStringList{"Amazing Grace"}));
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("newton"), false)), (QStringList{"Amazing Grace"})); // author is searched too
        QVERIFY(idx.filter(QString(), QStringLiteral("sweet missingword"), false).isEmpty());
    }

    void searchUsesContractionSynonyms()
    {
        const SongSearch idx({makeSong("Karena Kasih", "A", "karena kasih-Nya yang besar"), makeSong("Lain", "B", "lain")});
        QCOMPARE(titles(idx.filter(QStringLiteral("karna kasih"), QString(), false)), (QStringList{"Karena Kasih"}));
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("bsar"), false)), (QStringList{"Karena Kasih"}));
    }

    void noQueryListsAllByTitleAndFavoritesFilter()
    {
        const SongSearch idx({makeSong("beta", "", "", true), makeSong("Alpha", "", ""), makeSong("Gamma", "", "", true)});
        QCOMPARE(titles(idx.filter(QString(), QString(), false)), (QStringList{"Alpha", "beta", "Gamma"}));
        QCOMPARE(titles(idx.filter(QString(), QString(), true)), (QStringList{"beta", "Gamma"}));
        QCOMPARE(titles(idx.filter(QStringLiteral("a"), QString(), true)), (QStringList{"beta", "Gamma"})); // favourites + query
    }

    // ---- schedule service ------------------------------------------------------------
    void scheduleAddSelectsAndRequestsPreview()
    {
        ScheduleService s;
        QSignalSpy preview(&s, &ScheduleService::itemSelectedForPreview);
        s.addToSchedule(presentation("One"));
        s.addToSchedule(presentation("Two"));
        QCOMPARE(scheduleTitles(s), (QStringList{"One", "Two"}));
        QCOMPARE(s.selectedIndex(), 1);
        QCOMPARE(preview.count(), 2);
        QVERIFY(s.hasUnsavedChanges());
        QCOMPARE(s.items()[0].typeLabel, QStringLiteral("Song"));
        QVERIFY(s.items()[0].presentation.id != presentation("One").id); // stored as a clone
        // SelectedItem semantics: no repeat preview for the same row, but UserSelect always previews
        s.select(1);
        QCOMPARE(preview.count(), 2);
        s.userSelect(1);
        QCOMPARE(preview.count(), 3);
        s.select(0);
        QCOMPARE(preview.count(), 4);
        QCOMPARE(preview.last().first().toInt(), 0);
    }

    void scheduleReorderRemoveAndInsert()
    {
        ScheduleService s;
        for (const char* t : {"A", "B", "C", "D"})
            s.addToSchedule(presentation(QString::fromLatin1(t)));
        QSignalSpy preview(&s, &ScheduleService::itemSelectedForPreview);

        s.select(1); // B
        preview.clear();
        QVERIFY(s.moveUp());
        QCOMPARE(scheduleTitles(s), (QStringList{"B", "A", "C", "D"}));
        QCOMPARE(s.selectedIndex(), 0);      // the moved item stays selected
        QCOMPARE(preview.count(), 0);        // ... without re-previewing
        QVERIFY(!s.moveUp());                // already first
        QVERIFY(s.moveDown());
        QVERIFY(s.moveDown());
        QCOMPARE(scheduleTitles(s), (QStringList{"A", "C", "B", "D"}));
        QCOMPARE(s.selectedIndex(), 2);
        s.select(3);
        QVERIFY(!s.moveDown());              // already last

        // remove selects the neighbour at the same index (clamped) and previews it
        s.select(3);
        preview.clear();
        QVERIFY(s.removeSelected());
        QCOMPARE(scheduleTitles(s), (QStringList{"A", "C", "B"}));
        QCOMPARE(s.selectedIndex(), 2);
        QCOMPARE(preview.count(), 1);
        s.select(0);
        QVERIFY(s.removeSelected());
        QCOMPARE(scheduleTitles(s), (QStringList{"C", "B"}));
        QCOMPARE(s.selectedIndex(), 0);

        // insert at a position (drag & drop), clamped, selects the new item
        ScheduleItem x = ScheduleItem::fromPresentation(presentation("X"));
        s.insertItem(1, x);
        QCOMPARE(scheduleTitles(s), (QStringList{"C", "X", "B"}));
        QCOMPARE(s.selectedIndex(), 1);
        s.insertItem(99, ScheduleItem::fromPresentation(presentation("Z")));
        QCOMPARE(scheduleTitles(s).last(), QStringLiteral("Z"));
        // arbitrary move (drag reorder) keeps the list consistent
        QVERIFY(s.move(0, 3));
        QCOMPARE(scheduleTitles(s), (QStringList{"X", "B", "Z", "C"}));
        QVERIFY(!s.move(0, 0));
        QVERIFY(!s.move(-1, 2));

        s.select(-1);
        QVERIFY(!s.removeSelected());
    }

    void scheduleClearAndTitleReplacement()
    {
        ScheduleService s;
        s.addToSchedule(presentation("Old Song"));
        s.addToSchedule(presentation("Other"));
        s.replaceByTitle(QStringLiteral("old song"), QStringLiteral("New Song"), presentation("New Song", 3));
        QCOMPARE(scheduleTitles(s), (QStringList{"New Song", "Other"}));
        QCOMPARE(s.items()[0].presentation.slides.size(), 3);
        QVERIFY(s.hasUnsavedChanges()); // from the adds; replacing alone never sets the flag

        Sandbox sb;
        QString err;
        QVERIFY2(s.saveToFile(sb.path("a.fws"), &err), qPrintable(err));
        QVERIFY(!s.hasUnsavedChanges());
        s.replaceByTitle(QString(), QStringLiteral("Other"), presentation("Other", 1));
        QVERIFY(!s.hasUnsavedChanges());

        s.clear();
        QCOMPARE(s.count(), 0);
        QCOMPARE(s.selectedIndex(), -1);
        QVERIFY(s.currentFilePath().isNull());
        QVERIFY(!s.hasUnsavedChanges());
    }

    void schedulePersistence()
    {
        Sandbox sb;
        ScheduleService s;
        Song song = makeSong("Amazing Grace", "John Newton", "[Verse 1]\nAmazing grace\nhow sweet\n\n\n[Chorus]\nLa la");
        AppSettings st;
        st.capitalizeAllWords = false;
        s.addToSchedule(song.toPresentationItem(&st));
        s.addToSchedule(presentation("Second"));
        const QString file = sb.path("service.fws");
        QString err;
        QVERIFY2(s.saveToFile(file, &err), qPrintable(err));
        QCOMPARE(s.currentFilePath(), file);
        QVERIFY(!s.hasUnsavedChanges());

        ScheduleService loaded;
        QSignalSpy preview(&loaded, &ScheduleService::itemSelectedForPreview);
        QVERIFY2(loaded.loadFromFile(file, &err), qPrintable(err));
        QCOMPARE(scheduleTitles(loaded), (QStringList{"Amazing Grace", "Second"}));
        QCOMPARE(loaded.items()[0].id, s.items()[0].id);
        QCOMPARE(loaded.items()[0].presentation.slides.size(), 2);
        QCOMPARE(loaded.items()[0].presentation.slides[0].label, QStringLiteral("Verse 1"));
        QCOMPARE(loaded.items()[0].presentation.slides[1].type, SlideType::Chorus);
        QCOMPARE(loaded.selectedIndex(), 0);   // first item selected after opening
        QCOMPARE(preview.count(), 1);          // and previewed
        QVERIFY(!loaded.hasUnsavedChanges());
        QCOMPARE(loaded.currentFilePath(), file);
        loaded.moveDown();
        QVERIFY(loaded.hasUnsavedChanges());

        QVERIFY(!loaded.loadFromFile(sb.path("missing.fws"), &err));
        QVERIFY(!err.isEmpty());
        QFile bad(sb.path("bad.fws"));
        QVERIFY(bad.open(QIODevice::WriteOnly));
        bad.write("{ not json");
        bad.close();
        QVERIFY(!loaded.loadFromFile(sb.path("bad.fws"), &err));
        QCOMPARE(loaded.count(), 2); // a failed open keeps the current schedule
    }

    // ---- reference JSON compatibility ----------------------------------------------------
    void readsReferenceSongsJson()
    {
        Sandbox sb;
        QDir().mkpath(sb.path("Database"));
        QFile f(sb.path("Database/songs.json"));
        // Shape produced by System.Text.Json (WriteIndented): PascalCase, CRLF lyrics, null background,
        // plus a field this version does not know about.
        QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/reference_songs.json"), f.fileName()));
        DataService ds;
        QVERIFY2(ds.lastError().isEmpty(), qPrintable(ds.lastError()));
        QCOMPARE(ds.songs().size(), 1);
        const Song s = ds.songs().first();
        QCOMPARE(s.id, QStringLiteral("11111111-2222-3333-4444-555555555555"));
        QVERIFY(s.isFavorite);
        QVERIFY(s.defaultBackground.isNull());
        QVERIFY(s.lyricsRaw.contains(QStringLiteral("\r\n")));

        AppSettings st;
        st.capitalizeAllWords = false;
        const PresentationItem p = s.toPresentationItem(&st);
        QCOMPARE(p.slides.size(), 2); // CRLF blank line still splits stanzas
        QCOMPARE(p.slides[0].content, QStringLiteral("Line one\nLine two"));
        QCOMPARE(p.slides[1].label, QStringLiteral("Chorus"));

        // Saving keeps the id and the reference key names.
        QVERIFY(ds.saveSong(s));
        QFile again(sb.path("Database/songs.json"));
        QVERIFY(again.open(QIODevice::ReadOnly));
        const QJsonObject o = QJsonDocument::fromJson(again.readAll()).array().first().toObject();
        for (const char* k : {"Id", "Title", "Author", "Copyright", "Key", "IsFavorite", "LyricsRaw", "DefaultBackground",
                              "FontFamily", "FontSize", "IsBold", "IsItalic", "IsUnderline", "HasShadow", "HasOutline", "FontColor"})
            QVERIFY2(o.contains(QString::fromLatin1(k)), k);
        QCOMPARE(o.value("Id").toString(), s.id);
    }

    void readsReferenceScheduleFile()
    {
        Sandbox sb;
        QFile f(sb.path("ref.fws"));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/reference_schedule.fws"), f.fileName()));
        ScheduleService s;
        QString err;
        QVERIFY2(s.loadFromFile(sb.path("ref.fws"), &err), qPrintable(err));
        QCOMPARE(s.count(), 1);
        QCOMPARE(s.items()[0].title, QStringLiteral("Ref Item"));
        QCOMPARE(s.items()[0].presentation.subtitle, QStringLiteral("By Someone"));
        QCOMPARE(s.items()[0].presentation.slides[0].content, QStringLiteral("HELLO\nWORLD"));
        QCOMPARE(s.items()[0].presentation.slides[0].type, SlideType::Verse);
        QCOMPARE(s.items()[0].presentation.slides[0].containerMargin, (Thickness{1, 1, 1, 2}));

        // What we write has the same top-level shape.
        QVERIFY(s.saveToFile(sb.path("out.fws")));
        QFile out(sb.path("out.fws"));
        QVERIFY(out.open(QIODevice::ReadOnly));
        const QJsonObject root = QJsonDocument::fromJson(out.readAll()).object();
        QCOMPARE(root.value("Version").toString(), QStringLiteral("1.0"));
        const QJsonObject item = root.value("Items").toArray().first().toObject();
        for (const char* k : {"Id", "Title", "TypeLabel", "Category", "Presentation"})
            QVERIFY2(item.contains(QString::fromLatin1(k)), k);
        const QJsonObject slide = item.value("Presentation").toObject().value("Slides").toArray().first().toObject();
        QVERIFY(!slide.contains(QStringLiteral("ContainerMargin"))); // SlideData has no margin
        QCOMPARE(slide.value("Type").toInt(), 1);
    }

    // ---- backup import / export ----------------------------------------------------------
    void songBackupRoundTripAndErrors()
    {
        Sandbox sb;
        const QList<Song> songs{makeSong("Zeta \u00e9", "\u00c0", "[Verse 1]\nline \u2014 \u0e01"), makeSong("Alpha", "B", "x")};
        QString err;
        QVERIFY2(SongImportExport::exportSongs(sb.path("backup.json"), songs, &err), qPrintable(err));
        QList<Song> back;
        QVERIFY2(SongImportExport::importSongs(sb.path("backup.json"), back, &err), qPrintable(err));
        QCOMPARE(back.size(), 2);
        QCOMPARE(back[0].title, songs[0].title);
        QCOMPARE(back[0].lyricsRaw, songs[0].lyricsRaw);
        QCOMPARE(back[0].id, songs[0].id);

        QVERIFY(SongImportExport::importSongs(sb.path("missing.json"), back, &err)); // missing => empty, like the reference
        QVERIFY(back.isEmpty());
        QFile bad(sb.path("bad.json"));
        QVERIFY(bad.open(QIODevice::WriteOnly));
        bad.write("{\"not\":\"an array\"}");
        bad.close();
        QVERIFY(!SongImportExport::importSongs(sb.path("bad.json"), back, &err));
        QVERIFY(!err.isEmpty());
        QVERIFY(bad.open(QIODevice::WriteOnly | QIODevice::Truncate));
        bad.write("[ broken");
        bad.close();
        QVERIFY(!SongImportExport::importSongs(sb.path("bad.json"), back, &err));
    }

    void importMergeAndReplaceSemantics()
    {
        Sandbox sb;
        DataService ds;
        Song mine = makeSong("Same Title", "Mine", "mine lyrics");
        QVERIFY(ds.saveSong(mine));
        QVERIFY(ds.saveSong(makeSong("Only Mine", "", "")));

        QList<Song> imported{makeSong("same title", "Theirs", "their lyrics"), makeSong("New One", "N", "n")};
        QVERIFY(ds.addSongs(imported, /*replaceAll=*/false)); // merge: matching titles are updated, others added
        QCOMPARE(ds.songs().size(), 3);
        Song updated;
        QVERIFY(ds.songById(mine.id, &updated));
        QCOMPARE(updated.lyricsRaw, QStringLiteral("their lyrics"));
        QCOMPARE(updated.author, QStringLiteral("Theirs"));

        QVERIFY(ds.addSongs(imported, /*replaceAll=*/true)); // replace: only the backup remains
        QCOMPARE(titles(ds.songs()), (QStringList{"New One", "same title"}));
    }

    // ---- sample songs ------------------------------------------------------------------
    void sampleSongsAreOptionalAndPublicDomain()
    {
        Sandbox sb;
        {
            DataService ds;
            QCOMPARE(ds.songs().size(), 0); // never loaded automatically
            QCOMPARE(ds.loadSampleSongs(), 5);
            QCOMPARE(ds.songs().size(), 5);
            for (const Song& s : ds.songs()) {
                QCOMPARE(s.copyright, QStringLiteral("Public Domain"));
                QVERIFY(!s.lyricsRaw.trimmed().isEmpty());
                QVERIFY(!s.author.isEmpty());
            }
            QVERIFY(titles(ds.songs()).contains(QStringLiteral("Amazing Grace")));
            QCOMPARE(ds.loadSampleSongs(), 5); // idempotent: titles match, nothing is duplicated
            QCOMPARE(ds.songs().size(), 5);
        }
        DataService reloaded; // and they were persisted
        QCOMPARE(reloaded.songs().size(), 5);

        AppSettings st;
        st.capitalizeAllWords = false;
        Song grace;
        for (const Song& s : reloaded.songs())
            if (s.title == QStringLiteral("Amazing Grace"))
                grace = s;
        const PresentationItem p = grace.toPresentationItem(&st);
        QVERIFY(p.slides.size() >= 4);
        QCOMPARE(p.subtitle, QStringLiteral("By John Newton"));
    }

    // ---- legacy Windows-1252 characters (U+0080..U+009F) --------------------------------
    void textRepairFollowsWindows1252()
    {
        struct Row { char16_t from; char16_t to; };
        const Row table[] = {
            {0x80, 0x20AC}, {0x82, 0x201A}, {0x83, 0x0192}, {0x84, 0x201E}, {0x85, 0x2026}, {0x86, 0x2020},
            {0x87, 0x2021}, {0x88, 0x02C6}, {0x89, 0x2030}, {0x8A, 0x0160}, {0x8B, 0x2039}, {0x8C, 0x0152},
            {0x8E, 0x017D}, {0x91, 0x2018}, {0x92, 0x2019}, {0x93, 0x201C}, {0x94, 0x201D}, {0x95, 0x2022},
            {0x96, 0x2013}, {0x97, 0x2014}, {0x98, 0x02DC}, {0x99, 0x2122}, {0x9A, 0x0161}, {0x9B, 0x203A},
            {0x9C, 0x0153}, {0x9E, 0x017E}, {0x9F, 0x0178},
        };
        for (const Row& r : table)
            QCOMPARE(displayText(QString(QChar(r.from))), QString(QChar(r.to)));
        for (char16_t undefined : {0x81, 0x8D, 0x8F, 0x90, 0x9D})
            QCOMPARE(displayText(QStringLiteral("a") + QChar(undefined) + QStringLiteral("b")), QStringLiteral("ab"));

        // Everything that is not a C1 control passes through untouched (same string, no copy needed)
        const QString plain = QStringLiteral("Caf\u00e9 \u00b4 \u2019 \u0e01\u0e32 \u4e2d\u6587 \U0001F3B5 \u00a0 end\r\n");
        QVERIFY(!hasLegacyControlChars(plain));
        QCOMPARE(displayText(plain), plain);
        QVERIFY(hasLegacyControlChars(QStringLiteral("x\u0092")));
        QCOMPARE(displayText(displayText(QStringLiteral("a\u0092\u0096b"))), QStringLiteral("a\u2019\u2013b")); // idempotent
        QCOMPARE(displayText(QString()), QString());
    }

    void storedTextIsPreservedExactly()
    {
        Sandbox sb;
        const QString title = QStringLiteral("Bapa\u0092s \u0096 Caf\u00e9 \u0e01\u0e32 \U0001F3B5");
        const QString lyrics = QStringLiteral("[Verse 1]\r\nKau sgalanya bagi\u0092ku\u0085\r\n\r\n[Chorus]\r\n\u0091quoted\u0092 \u00b4 \u2019");
        Song s = makeSong(title, QStringLiteral("O\u0092Neil"), lyrics);
        {
            DataService ds;
            QVERIFY(ds.saveSong(s));
        }
        // 1. The file is plain UTF-8 and holds the original code points (C2 92 for U+0092), not repaired ones
        QFile f(sb.path("Database/songs.json"));
        QVERIFY(f.open(QIODevice::ReadOnly));
        const QByteArray raw = f.readAll();
        QVERIFY(!raw.startsWith("\xEF\xBB\xBF"));
        QVERIFY(raw.contains("\xC2\x92"));
        QVERIFY(raw.contains("\xC2\x96"));
        QVERIFY(!raw.contains("\xE2\x80\x99 ")); // no U+2019 written in place of U+0092 (the one in the lyrics is followed by the end)
        QVERIFY(raw.contains("\xF0\x9F\x8E\xB5")); // emoji survives
        f.close();

        // 2. Reloading yields exactly the same strings
        DataService again;
        QCOMPARE(again.songs().size(), 1);
        QCOMPARE(again.songs()[0].title, title);
        QCOMPARE(again.songs()[0].author, QStringLiteral("O\u0092Neil"));
        QCOMPARE(again.songs()[0].lyricsRaw, lyrics);

        // 3. Backups and schedules keep it too
        QVERIFY(SongImportExport::exportSongs(sb.path("b.json"), again.songs()));
        QList<Song> back;
        QVERIFY(SongImportExport::importSongs(sb.path("b.json"), back));
        QCOMPARE(back[0].lyricsRaw, lyrics);
        AppSettings st;
        ScheduleService sched;
        sched.addToSchedule(again.songs()[0].toPresentationItem(&st));
        QVERIFY(sched.saveToFile(sb.path("s.fws")));
        ScheduleService loaded;
        QVERIFY(loaded.loadFromFile(sb.path("s.fws")));
        QCOMPARE(loaded.items()[0].title, title);
        QVERIFY(loaded.items()[0].presentation.slides[0].content.contains(QChar(0x92)));
        // 4. Presentation conversion does not touch the characters (repair happens when drawing)
        QVERIFY(again.songs()[0].toPresentationItem(&st).slides[0].content.contains(QChar(0x92)));
    }

    void searchTreatsLegacyCharactersLikeTheRealOnes()
    {
        QCOMPARE(SongSearch::normalize(QStringLiteral("Dib\u0092ri")), SongSearch::normalize(QStringLiteral("Dib\u2019ri")));
        QCOMPARE(SongSearch::normalize(QStringLiteral("Dib\u0092ri")), QStringLiteral("dibri"));
        QCOMPARE(SongSearch::normalize(QStringLiteral("A\u0096B")), SongSearch::normalize(QStringLiteral("A\u2013B")));
        QCOMPARE(SongSearch::normalize(QStringLiteral("A\u0096B")), QStringLiteral("a b"));
        const SongSearch idx({makeSong(QStringLiteral("Apapun Terjadi \u0096 Welyar"), QStringLiteral("x"), QStringLiteral("don\u0092t stop")),
                              makeSong(QStringLiteral("Other"), QString(), QString())});
        QCOMPARE(titles(idx.filter(QStringLiteral("terjadi welyar"), QString(), false)), (QStringList{"Apapun Terjadi \u0096 Welyar"}));
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("don\u2019t"), false)), (QStringList{"Apapun Terjadi \u0096 Welyar"}));
        QCOMPARE(titles(idx.filter(QString(), QStringLiteral("dont stop"), false)), (QStringList{"Apapun Terjadi \u0096 Welyar"}));
    }

    // ---- presentation conversion / formatting --------------------------------------------
    void presentationConversionFollowsSettings()
    {
        Song s = makeSong("Hymn", "Author", "[Verse 1]\nfirst line\nsecond line\n\nChorus:\nsing along");
        s.fontFamily = QStringLiteral("Verdana");
        s.fontSize = 20;
        s.isBold = false;
        s.hasShadow = false;

        AppSettings st; // defaults: capitalise all words, Tahoma 13 bold
        PresentationItem p = s.toPresentationItem(&st);
        QCOMPARE(p.slides[0].content, QStringLiteral("FIRST LINE\nSECOND LINE"));
        QCOMPARE(p.slides[1].label, QStringLiteral("Chorus"));
        QCOMPARE(p.slides[1].type, SlideType::Chorus);
        QCOMPARE(p.slides[0].fontFamily, QStringLiteral("Tahoma")); // global song font wins over the song's own
        QVERIFY(p.slides[0].isBold);                                // SongFontBold || song.IsBold
        QVERIFY(!p.slides[0].hasShadow);                            // per-song flags are kept

        st.capitalizeAllWords = false;
        st.songFontFamily = QString();
        st.songFontSize = 0;
        st.songFontBold = false;
        p = s.toPresentationItem(&st);
        QCOMPARE(p.slides[0].content, QStringLiteral("first line\nsecond line"));
        QCOMPARE(p.slides[0].fontFamily, QStringLiteral("Verdana")); // falls back to the song's font
        QCOMPARE(p.slides[0].fontSize, 20);
        QVERIFY(!p.slides[0].isBold);

        s.defaultBackground = QStringLiteral("#0B132B");
        QCOMPARE(s.toPresentationItem(&st).backgroundPath, QStringLiteral("#0B132B"));
        s.defaultBackground = QStringLiteral("C:/definitely/missing.png");
        QVERIFY(s.toPresentationItem(&st).backgroundPath.trimmed().isEmpty()); // missing file ignored
    }
};

QTEST_GUILESS_MAIN(SongsScheduleTests)
#include "test_songs_schedule.moc"
