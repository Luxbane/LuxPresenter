// Milestone 5 tests: PDF engine (PDFium), LibreOffice conversion (against a fake soffice), the presentation service,
// the presentation page on the projector, panes, library tab, schedule items and MainWindow integration.
// Offscreen platform: no windows are shown. Real windows / WebView2 are covered by luxwindows_tests.

#include "FakeOutput.h"
#include "LoWatch.h"
#include "Models/ScheduleItem.h"
#include "Services/AppPaths.h"
#include "Services/DataService.h"
#include "Services/PageImageCache.h"
#include "Services/PdfDocument.h"
#include "Services/Presentation/OfficeConverter.h"
#include "Services/PresentationService.h"
#include "Services/ProjectionComposer.h"
#include "Services/ProjectionService.h"
#include "Services/ThemeService.h"
#include "Services/UrlUtil.h"
#include "Views/Components/ClassicButton.h"
#include "Views/Components/LibraryView.h"
#include "Views/Components/PageView.h"
#include "Views/Components/PresentationTab.h"
#include "Views/Components/ScheduleView.h"
#include "Views/Components/SlidePaneView.h"
#include "Views/Components/SongsTab.h"
#include "Views/MainWindow.h"
#include "Views/Projection/ProjectionWindow.h"

#include <QComboBox>
#include <QLabel>
#include <QLineEdit>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonObject>
#include <QJsonDocument>
#include <QListWidget>
#include <QPageSize>
#include <QPainter>
#include <QPdfWriter>
#include <QPushButton>
#include <QSignalSpy>
#include <QTemporaryDir>
#include <QtTest>

using namespace Lux;
using LuxTest::FakeOutput;

namespace {

struct PageSpec
{
    QSizeF size;      // points
    QColor colour;    // solid page colour
};

const QList<PageSpec> kPages{{{960, 540}, QColor(0, 0, 255)},      // 16:9 slide
                             {{595, 842}, QColor(0, 128, 0)},      // A4 portrait
                             {{800, 600}, QColor(200, 0, 0)}};     // 4:3

// Writes a PDF whose every page is one solid colour with its own size (so scaling/aspect handling is observable).
bool makePdf(const QString& path, const QList<PageSpec>& pages = kPages)
{
    QPdfWriter w(path);
    w.setResolution(72);
    w.setPageMargins(QMarginsF(0, 0, 0, 0));
    QPainter p;
    for (int i = 0; i < pages.size(); ++i) {
        w.setPageSize(QPageSize(pages[i].size, QPageSize::Point));
        w.setPageMargins(QMarginsF(0, 0, 0, 0));
        if (i == 0) {
            if (!p.begin(&w))
                return false;
        } else {
            w.newPage();
        }
        p.fillRect(QRectF(-20, -20, pages[i].size.width() + 40, pages[i].size.height() + 40), pages[i].colour);
        p.setPen(Qt::white);
        QFont f(QStringLiteral("Arial"));
        f.setPixelSize(48);
        p.setFont(f);
        p.drawText(QRectF(0, 0, pages[i].size.width(), pages[i].size.height()), Qt::AlignCenter, QStringLiteral("Slide %1").arg(i + 1));
    }
    return p.end();
}

QRgb px(const QImage& img, int x, int y) { return img.pixel(x, y) | 0xFF000000; }
bool closeTo(QRgb a, const QColor& c, int tol = 6)
{
    return qAbs(qRed(a) - c.red()) <= tol && qAbs(qGreen(a) - c.green()) <= tol && qAbs(qBlue(a) - c.blue()) <= tol;
}

struct Env
{
    Env()
    {
        QDir().mkpath(QStringLiteral(LUX_TEST_TMP));
        dir = std::make_unique<QTemporaryDir>(QStringLiteral(LUX_TEST_TMP "/pres-XXXXXX"));
        AppPaths::setBaseDirectoryOverride(dir->path());
        data = std::make_unique<DataService>();
        ThemeService::instance().apply(false);
        QVERIFY2(PdfiumApi::instance().load(QStringLiteral(LUX_PDFIUM_DLL)), qPrintable(PdfiumApi::instance().lastError()));
    }
    ~Env()
    {
        window.reset();
        data.reset();
        PageImageCache::instance().clear();
        AppPaths::setBaseDirectoryOverride(QString());
        for (const char* v : {"FAKE_SOFFICE_PDF", "FAKE_SOFFICE_MODE", "FAKE_SOFFICE_LOG"})
            qunsetenv(v);
    }
    MainWindow& mainWindow()
    {
        if (!window) {
            window = std::make_unique<MainWindow>(*data);
            window->projection()->setOutputFactory(LuxTest::fakeFactory());
            window->setDialogsSuppressed(true);
        }
        return *window;
    }
    QString path(const QString& rel) const { return QDir(dir->path()).filePath(rel); }
    QString pdf(const QString& name, const QList<PageSpec>& pages = kPages) const
    {
        const QString p = path(name);
        if (!makePdf(p, pages))
            qFatal("could not write test pdf");
        return p;
    }
    QString touch(const QString& name, const QByteArray& content = "x") const
    {
        QFile f(path(name));
        f.open(QIODevice::WriteOnly);
        QByteArray c = content;
        const QString ext = QFileInfo(name).suffix().toLower();
        if (c == "x" && (ext == QLatin1String("pptx") || ext == QLatin1String("ppsx") || ext == QLatin1String("odp")))
            c = QByteArray("PK\x03\x04", 4) + QByteArray(60, 0);   // looks like a package (the converter checks the signature)
        f.write(c);
        return f.fileName();
    }

    std::unique_ptr<QTemporaryDir> dir;
    std::unique_ptr<DataService> data;
    std::unique_ptr<MainWindow> window;
};

ProjectionState baseState()
{
    ProjectionState st;
    st.transitionDurationMs = 500;
    st.songTransition = st.slideTransition = st.screenTransition = st.mediaTransition = QStringLiteral("Blend");
    return st;
}

struct Clocked
{
    qint64 now = 0;
    ProjectionComposer c;
    Clocked() { c.setClock([this] { return now; }); }
};

} // namespace

class PresentationTests : public QObject
{
    Q_OBJECT
private:
    // Runs a conversion that is expected to fail and reports its message.
    void failedConversion(Env& env, const QString& input, const QString& mode, int timeout, const QString& soffice, QString* message)
    {
        qputenv("FAKE_SOFFICE_MODE", mode.toLocal8Bit());
        OfficeConverter conv;
        conv.setSofficeOverride(soffice);
        conv.setTimeoutMs(timeout);
        QSignalSpy spy(&conv, &OfficeConverter::finished);
        conv.convert(input, env.path(QStringLiteral("o")), env.path(QStringLiteral("p")));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 20000);
        QVERIFY(!spy[0][0].toBool());
        *message = spy[0][2].toString();
    }

private slots:
    void cleanup() { qunsetenv("FAKE_SOFFICE_MODE"); }

    // ---- PDFium ------------------------------------------------------------------------------
    void pdfEngineRendersPagesAtTheRequestedSizeKeepingAspect()
    {
        Env env;
        const QString file = env.pdf(QStringLiteral("deck.pdf"));
        PdfDocument doc;
        QVERIFY2(doc.open(file), qPrintable(doc.errorMessage()));
        QCOMPARE(doc.pageCount(), 3);
        QCOMPARE(doc.pageSize(0), QSizeF(960, 540));
        QCOMPARE(doc.pageSize(1), QSizeF(595, 842));
        QVERIFY(doc.pageSize(7).isEmpty());

        struct Case { int page, boxW, boxH, expW, expH; };
        for (const Case& c : QList<Case>{{0, 400, 400, 400, 225}, {1, 400, 400, 282, 400}, {2, 400, 400, 400, 300},
                                         {0, 1920, 1200, 1920, 1080}, {0, 1920, 1080, 1920, 1080}, {1, 1920, 1200, 848, 1200}}) {
            const PdfDocument::Bitmap b = doc.render(c.page, c.boxW, c.boxH);
            QVERIFY(!b.isNull());
            QVERIFY2(qAbs(b.width - c.expW) <= 1 && qAbs(b.height - c.expH) <= 1,
                     qPrintable(QStringLiteral("page %1 into %2x%3 -> %4x%5").arg(c.page).arg(c.boxW).arg(c.boxH).arg(b.width).arg(b.height)));
            QImage img(reinterpret_cast<const uchar*>(b.data.constData()), b.width, b.height, b.stride, QImage::Format_RGB32);
            QVERIFY2(closeTo(px(img, b.width / 2, 3), kPages[c.page].colour) && closeTo(px(img, 2, b.height - 3), kPages[c.page].colour),
                     "page colour fills the rendered bitmap");
        }
        QVERIFY(doc.render(5, 100, 100).isNull());       // no such page
        QVERIFY(doc.render(0, 0, 100).isNull());
    }

    void invalidPdfFilesAreReportedNotCrashed()
    {
        Env env;
        PdfDocument doc;
        QVERIFY(!doc.open(env.path(QStringLiteral("missing.pdf"))));
        QCOMPARE(doc.error(), PdfDocument::Error::NotFound);
        QVERIFY(!doc.errorMessage().isEmpty());

        QVERIFY(!doc.open(env.touch(QStringLiteral("empty.pdf"), QByteArray())));
        QCOMPARE(doc.error(), PdfDocument::Error::Empty);

        QVERIFY(!doc.open(env.touch(QStringLiteral("garbage.pdf"), "this is not a pdf at all, just text\n")));
        QCOMPARE(doc.error(), PdfDocument::Error::Invalid);

        const QString good = env.pdf(QStringLiteral("good.pdf"));
        QFile f(good);
        QVERIFY(f.open(QIODevice::ReadOnly));
        const QByteArray bytes = f.readAll();
        f.close();
        QVERIFY(!doc.open(env.touch(QStringLiteral("truncated.pdf"), bytes.left(bytes.size() / 3))));
        QVERIFY(doc.error() == PdfDocument::Error::Invalid || doc.error() == PdfDocument::Error::NoPages);

        QVERIFY(!doc.open(env.path(QString())));          // a directory
        QVERIFY(!doc.isOpen());
        QVERIFY(doc.open(good));                          // the object is reusable after failures
        QVERIFY(doc.isOpen());
        doc.close();
        QVERIFY(!doc.isOpen() && doc.pageCount() == 0);

        // the file is not locked while a document is open (it is read into memory)
        QVERIFY(doc.open(good));
        QVERIFY(QFile::remove(good));
    }

    void pageCacheRendersOncePerSizeAndSurvivesInvalidFiles()
    {
        Env env;
        const QString file = env.pdf(QStringLiteral("deck.pdf"));
        PageImageCache& cache = PageImageCache::instance();
        cache.clear();
        QString err;
        QCOMPARE(cache.pageCount(file, &err), 3);
        const int before = cache.renderCount();
        const QImage a = cache.page(file, 0, QSize(640, 480));
        QCOMPARE(a.size(), QSize(640, 360));
        QCOMPARE(cache.renderCount(), before + 1);
        cache.page(file, 0, QSize(640, 480));
        cache.page(file, 0, QSize(640, 400));                 // same resulting size would be shared only if equal
        QVERIFY(cache.renderCount() <= before + 2);
        const int afterRepeat = cache.renderCount();
        for (int i = 0; i < 10; ++i)
            cache.page(file, 0, QSize(640, 480));
        QCOMPARE(cache.renderCount(), afterRepeat);           // repeated requests are served from memory
        QVERIFY(cache.page(file, 9, QSize(640, 480)).isNull());
        QCOMPARE(cache.pageCount(env.path(QStringLiteral("nope.pdf")), &err), 0);
        QVERIFY(!err.isEmpty());
        cache.invalidate(file);
        cache.page(file, 0, QSize(640, 480));
        QVERIFY(cache.renderCount() > afterRepeat);
    }

    // ---- LibreOffice conversion ---------------------------------------------------------------
    void sofficeIsLocatedInTheLibreOfficeFolderOnly()
    {
        Env env;
        QCOMPARE(OfficeConverter::findSoffice(env.path(QStringLiteral("NoLibreOffice"))), QString());
        QDir().mkpath(env.path(QStringLiteral("LO/program")));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FAKE_SOFFICE), env.path(QStringLiteral("LO/program/soffice.exe"))));
        QVERIFY(OfficeConverter::findSoffice(env.path(QStringLiteral("LO"))).endsWith(QStringLiteral("soffice.exe")));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FAKE_SOFFICE), env.path(QStringLiteral("LO/program/soffice.com"))));
        QVERIFY(OfficeConverter::findSoffice(env.path(QStringLiteral("LO"))).endsWith(QStringLiteral("soffice.com")));   // console build preferred

        QVERIFY(OfficeConverter::isOfficeFile(QStringLiteral("a.PPTX")) && OfficeConverter::isOfficeFile(QStringLiteral("a.odp")) &&
                OfficeConverter::isOfficeFile(QStringLiteral("a.ppsx")) && OfficeConverter::isOfficeFile(QStringLiteral("a.pps")));
        QVERIFY(!OfficeConverter::isOfficeFile(QStringLiteral("a.pdf")) && !OfficeConverter::isOfficeFile(QStringLiteral("a.docx")));
    }

    void conversionRunsSofficeWithTheReferenceCommandLine()
    {
        Env env;
        const QString deck = env.pdf(QStringLiteral("source-deck.pdf"));
        qputenv("FAKE_SOFFICE_PDF", deck.toLocal8Bit());
        const QString log = env.path(QStringLiteral("soffice.log"));
        qputenv("FAKE_SOFFICE_LOG", log.toLocal8Bit());
        const QString input = env.touch(QStringLiteral("Sunday Service.pptx"));

        OfficeConverter conv;
        conv.setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
        QSignalSpy spy(&conv, &OfficeConverter::finished);
        conv.convert(input, env.path(QStringLiteral("out dir")), env.path(QStringLiteral("profile")));
        QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 20000);
        QVERIFY2(spy[0][0].toBool(), qPrintable(spy[0][2].toString()));
        const QString pdf = spy[0][1].toString();
        QVERIFY(pdf.endsWith(QStringLiteral("Sunday Service.pdf")) && QFile::exists(pdf));

        QFile lf(log);
        QVERIFY(lf.open(QIODevice::ReadOnly));
        const QStringList args = QString::fromLocal8Bit(lf.readAll()).remove(QLatin1Char('\r')).split(QLatin1Char('\n'), Qt::SkipEmptyParts);
        for (const char* a : {"--headless", "--norestore", "--nologo", "--nodefault", "--convert-to", "pdf:impress_pdf_Export", "--outdir"})
            QVERIFY2(args.contains(QLatin1String(a)), a);
        QVERIFY(args.contains(QDir::toNativeSeparators(input)));
        QVERIFY(args.contains(QDir::toNativeSeparators(env.path(QStringLiteral("out dir")))));
        QVERIFY2(std::any_of(args.begin(), args.end(), [](const QString& s) { return s.startsWith(QStringLiteral("-env:UserInstallation=file:///")); }),
                 "private user profile");
    }

    void conversionFailuresAreReported()
    {
        Env env;
        const QString input = env.touch(QStringLiteral("deck.pptx"));
        qputenv("FAKE_SOFFICE_PDF", env.pdf(QStringLiteral("d.pdf")).toLocal8Bit());
        QString fail, none, hang, missing;
        failedConversion(env, input, QStringLiteral("fail"), 20000, QStringLiteral(LUX_FAKE_SOFFICE), &fail);
        QVERIFY2(fail.contains(QStringLiteral("exit code 3")) && fail.contains(QStringLiteral("source file could not be loaded")), qPrintable(fail));
        failedConversion(env, input, QStringLiteral("nooutput"), 20000, QStringLiteral(LUX_FAKE_SOFFICE), &none);
        QVERIFY2(none.contains(QStringLiteral("could not convert")), qPrintable(none));
        failedConversion(env, input, QStringLiteral("hang"), 400, QStringLiteral(LUX_FAKE_SOFFICE), &hang);
        QVERIFY2(hang.contains(QStringLiteral("in time")), qPrintable(hang));
        failedConversion(env, input, QString(), 20000, env.path(QStringLiteral("not/installed/soffice.exe")), &missing);
        QVERIFY2(missing.contains(QStringLiteral("LibreOffice")), qPrintable(missing));
    }

    // ---- presentation service -------------------------------------------------------------------
    void serviceOpensPdfsAndRejectsBadFiles()
    {
        Env env;
        PresentationService svc;
        QSignalSpy opened(&svc, &PresentationService::opened);
        QSignalSpy failed(&svc, &PresentationService::failed);

        svc.open(env.pdf(QStringLiteral("deck.pdf")));
        QTRY_COMPARE(opened.size(), 1);
        const auto doc = opened[0][0].value<PresentationService::Document>();
        QCOMPARE(doc.title, QStringLiteral("deck"));
        QCOMPARE(doc.pageCount, 3);
        QCOMPARE(doc.pdfPath, doc.filePath);
        QVERIFY(svc.document(doc.filePath) != nullptr);

        svc.open(env.path(QStringLiteral("nothing.pdf")));
        svc.open(env.touch(QStringLiteral("notes.docx")));
        svc.open(env.touch(QStringLiteral("bad.pdf"), "%PDF-1.4 but not really"));
        svc.open(env.touch(QStringLiteral("empty.pdf"), QByteArray()));
        QTRY_COMPARE(failed.size(), 4);
        QVERIFY(failed[0][1].toString().contains(QStringLiteral("not found")));
        QVERIFY(failed[1][1].toString().contains(QStringLiteral("not supported")));
        QVERIFY(!failed[2][1].toString().isEmpty());
        QVERIFY(failed[3][1].toString().contains(QStringLiteral("empty")));
        QCOMPARE(opened.size(), 1);
    }

    void officeDocumentsAreConvertedOnceAndCached()
    {
        Env env;
        qputenv("FAKE_SOFFICE_PDF", env.pdf(QStringLiteral("converted.pdf")).toLocal8Bit());
        const QString log = env.path(QStringLiteral("soffice.log"));
        qputenv("FAKE_SOFFICE_LOG", log.toLocal8Bit());
        const QString pptx = env.touch(QStringLiteral("Hymns.pptx"));
        auto invocations = [&] {
            QFile f(log);
            return f.open(QIODevice::ReadOnly) ? QString::fromLocal8Bit(f.readAll()).count(QStringLiteral("---")) : 0;
        };

        {
            PresentationService svc;
            svc.setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
            QSignalSpy opened(&svc, &PresentationService::opened);
            QSignalSpy busy(&svc, &PresentationService::busyChanged);
            svc.open(pptx);
            QTRY_COMPARE_WITH_TIMEOUT(opened.size(), 1, 20000);
            const auto d = opened[0][0].value<PresentationService::Document>();
            QCOMPARE(d.pageCount, 3);
            QVERIFY(d.pdfPath.contains(QStringLiteral("PresentationCache")) && d.pdfPath != d.filePath);
            QCOMPARE(invocations(), 1);
            QVERIFY(busy.size() >= 2 && busy.first().first().toBool() && !busy.last().first().toBool());
            svc.open(pptx);                                  // same service: served from memory
            QTRY_COMPARE(opened.size(), 2);
            QCOMPARE(invocations(), 1);
        }
        {
            PresentationService fresh;                       // a new session reuses the cached PDF
            fresh.setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
            QSignalSpy opened(&fresh, &PresentationService::opened);
            fresh.open(pptx);
            QTRY_COMPARE(opened.size(), 1);
            QCOMPARE(invocations(), 1);
        }
        {
            // the source changed after the cached conversion: converted again
            QFile src(pptx);
            QVERIFY(src.open(QIODevice::ReadWrite));
            QVERIFY(src.setFileTime(QDateTime::currentDateTime().addSecs(120), QFileDevice::FileModificationTime));
            src.close();
            PresentationService fresh;
            fresh.setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
            QSignalSpy opened(&fresh, &PresentationService::opened);
            fresh.open(pptx);
            QTRY_COMPARE_WITH_TIMEOUT(opened.size(), 1, 20000);
            QCOMPARE(invocations(), 2);
        }
    }

    void officeConversionProblemsReachTheCaller()
    {
        Env env;
        const QString pptx = env.touch(QStringLiteral("Deck.pptx"));
        PresentationService svc;
        QSignalSpy failed(&svc, &PresentationService::failed);
        svc.setSofficeOverride(env.path(QStringLiteral("not/installed.exe")));          // LibreOffice missing
        svc.open(pptx);
        QTRY_COMPARE(failed.size(), 1);
        QVERIFY2(failed[0][1].toString().contains(QStringLiteral("LibreOffice was not found")), qPrintable(failed[0][1].toString()));

        qputenv("FAKE_SOFFICE_MODE", "fail");                                           // conversion fails
        svc.setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
        svc.open(pptx);
        QTRY_COMPARE_WITH_TIMEOUT(failed.size(), 2, 20000);

        qunsetenv("FAKE_SOFFICE_MODE");                                                  // converter returns a broken PDF
        qputenv("FAKE_SOFFICE_PDF", env.touch(QStringLiteral("broken.pdf"), "garbage").toLocal8Bit());
        svc.open(pptx);
        QTRY_COMPARE_WITH_TIMEOUT(failed.size(), 3, 20000);
        QVERIFY(!svc.isBusy());

        // several requests are processed one after the other
        qputenv("FAKE_SOFFICE_PDF", env.pdf(QStringLiteral("ok.pdf")).toLocal8Bit());
        QSignalSpy opened(&svc, &PresentationService::opened);
        svc.open(env.touch(QStringLiteral("One.pptx")));
        svc.open(env.touch(QStringLiteral("Two.odp")));
        QTRY_COMPARE_WITH_TIMEOUT(opened.size(), 2, 30000);
    }

    void browserInputIsNormalisedLikeTheReference()
    {
        QCOMPARE(normalizeBrowserInput(QString()), QString());
        QCOMPARE(normalizeBrowserInput(QStringLiteral("  ")), QString());
        QCOMPARE(normalizeBrowserInput(QStringLiteral("about:blank")), QString());
        QCOMPARE(normalizeBrowserInput(QStringLiteral("Home")), QString());
        QCOMPARE(normalizeBrowserInput(QStringLiteral("https://a.example/x")), QStringLiteral("https://a.example/x"));
        QCOMPARE(normalizeBrowserInput(QStringLiteral("HTTP://a.example")), QStringLiteral("HTTP://a.example"));
        QCOMPARE(normalizeBrowserInput(QStringLiteral("example.com/path")), QStringLiteral("https://example.com/path"));
        QCOMPARE(normalizeBrowserInput(QStringLiteral("amazing grace lyrics")), QStringLiteral("https://www.google.com/search?q=amazing%20grace%20lyrics"));
        QCOMPARE(normalizeBrowserInput(QStringLiteral("hymn")), QStringLiteral("https://www.google.com/search?q=hymn"));
        QVERIFY(isHomeUrl(QString()) && isHomeUrl(QStringLiteral("about:blank")) && isHomeUrl(QStringLiteral("data:text/html;charset=utf-8,x")));
        QVERIFY(!isHomeUrl(QStringLiteral("https://x.y")));
    }

    // ---- the bundled LibreOffice (real conversions, not the stand-in) -------------------------------------------
    void bundledLibreOfficeIsCompleteAndLocatable()
    {
        Env env;
        const QString lo = QStringLiteral(LUX_BUNDLED_LO);
        QVERIFY2(QDir(lo).exists(), "the build bundles LibreOffice into Portable\\Engines\\LibreOffice");
        QCOMPARE(OfficeConverter::installProblem(lo), QString());
        const OfficeConverter::Engine e = OfficeConverter::locate(lo, {});
        QVERIFY(e.bundled && e.problem.isEmpty());
        QVERIFY2(e.soffice.endsWith(QStringLiteral("program/soffice.com")), qPrintable(e.soffice));
        for (const char* dll : {"vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll"})
            QVERIFY2(QFileInfo::exists(QDir(lo).filePath(QStringLiteral("program/") + QLatin1String(dll))), dll);   // app-local C++ runtime
        for (const char* f : {"license.txt", "LICENSE.html", "NOTICE", "CREDITS.fodt"})
            QVERIFY2(QFileInfo::exists(QDir(lo).filePath(QLatin1String(f))), f);                                   // licence texts ship with it
        QVERIFY(!QFileInfo::exists(QDir(lo).filePath(QStringLiteral("LibreOffice_26.8.3_Win_x86-64.msi"))));       // no installer copy
        QVERIFY(QFileInfo::exists(QDir(lo).filePath(QStringLiteral("../../licenses/libreoffice-NOTICE.txt"))));

        // an incomplete copy is detected and reported, not run
        QDir().mkpath(env.path(QStringLiteral("Broken/program")));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FAKE_SOFFICE), env.path(QStringLiteral("Broken/program/soffice.exe"))));
        const QString problem = OfficeConverter::installProblem(env.path(QStringLiteral("Broken")));
        QVERIFY2(problem.contains(QStringLiteral("soffice.bin")) && problem.contains(QStringLiteral("mergedlo.dll")), qPrintable(problem));
        const OfficeConverter::Engine broken = OfficeConverter::locate(env.path(QStringLiteral("Broken")), {});
        QVERIFY(broken.soffice.isEmpty());
        QVERIFY2(broken.problem.contains(QStringLiteral("incomplete or damaged")) && broken.problem.contains(QStringLiteral("PDF files can still be used")),
                 qPrintable(broken.problem));
        // ... and a usable system installation is preferred over a damaged bundled one
        const OfficeConverter::Engine fallback = OfficeConverter::locate(env.path(QStringLiteral("Broken")), {lo});
        QVERIFY(!fallback.bundled && fallback.soffice.endsWith(QStringLiteral("soffice.com")));
        // nothing at all
        const OfficeConverter::Engine none = OfficeConverter::locate(env.path(QStringLiteral("Nope")), {});
        QVERIFY(none.soffice.isEmpty() && none.problem.contains(QStringLiteral("not found")));
    }

    void bundledLibreOfficeConvertsPptxAndOdpInvisibly()
    {
        Env env;
        const QString lo = QStringLiteral(LUX_BUNDLED_LO);
        const OfficeConverter::Engine engine = OfficeConverter::locate(lo, {});
        QVERIFY2(!engine.soffice.isEmpty(), qPrintable(engine.problem));
        LuxTest::WindowWatcher watcher(lo);

        struct Case { const char* file; };
        for (const char* name : {"source_deck.pptx", "source_deck.odp"}) {
            const QString input = env.path(QString::fromLatin1(name));
            QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/") + QLatin1String(name), input));
            OfficeConverter conv;
            conv.setSofficeOverride(engine.soffice);
            conv.setTimeoutMs(240000);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            const QString outDir = env.path(QStringLiteral("out-") + QLatin1String(name));
            conv.convert(input, outDir, env.path(QStringLiteral("lo-profile")));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 250000);
            QVERIFY2(spy[0][0].toBool(), qPrintable(QLatin1String(name) + QStringLiteral(": ") + spy[0][2].toString()));

            // a valid PDF with the three coloured 16:9 slides
            PdfDocument pdf;
            QVERIFY2(pdf.open(spy[0][1].toString()), qPrintable(pdf.errorMessage()));
            QCOMPARE(pdf.pageCount(), 3);
            QVERIFY(qAbs(pdf.pageSize(0).width() / pdf.pageSize(0).height() - 16.0 / 9.0) < 0.01);
            const QColor colours[3] = {QColor(0, 0, 255), QColor(0, 128, 0), QColor(200, 0, 0)};
            for (int p = 0; p < 3; ++p) {
                const PdfDocument::Bitmap b = pdf.render(p, 640, 480);
                QVERIFY(!b.isNull());
                QCOMPARE(b.width, 640);
                QCOMPARE(b.height, 360);
                const QImage img(reinterpret_cast<const uchar*>(b.data.constData()), b.width, b.height, b.stride, QImage::Format_RGB32);
                QVERIFY2(closeTo(px(img, 20, 330), colours[p], 14), qPrintable(QStringLiteral("%1 page %2 colour").arg(QLatin1String(name)).arg(p + 1)));
                bool text = false;                                  // the white caption was rendered
                for (int y = 20; y < 120 && !text; y += 2)
                    for (int x = 80; x < 560; x += 2)
                        if (qRed(px(img, x, y)) > 200 && qGreen(px(img, x, y)) > 200 && qBlue(px(img, x, y)) > 200) { text = true; break; }
                QVERIFY2(text, "slide text rendered");
            }
            // only the PDF is left in the output folder (no lock files / temp documents)
            const QStringList left = QDir(outDir).entryList(QDir::AllEntries | QDir::NoDotAndDotDot | QDir::Hidden);
            QCOMPARE(left, QStringList{QStringLiteral("source_deck.pdf")});
            QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);   // nothing keeps running
        }
        QVERIFY2(watcher.pollsWithProcess() > 0, "the window watcher overlapped a running LibreOffice");
        QVERIFY2(watcher.seen().isEmpty(), qPrintable(QStringLiteral("LibreOffice showed a window: ") + watcher.seen().join(QStringLiteral("; "))));
        QVERIFY(QDir(env.path(QStringLiteral("lo-profile"))).exists());
    }

    void libreOfficeFailuresTimeoutsAndCancelLeaveNothingBehind()
    {
        Env env;
        const QString lo = QStringLiteral(LUX_BUNDLED_LO);
        const QString soffice = OfficeConverter::locate(lo, {}).soffice;
        QVERIFY(!soffice.isEmpty());
        LuxTest::WindowWatcher watcher(lo);

        // a document LibreOffice cannot load
        {
            OfficeConverter conv;
            conv.setSofficeOverride(soffice);
            conv.setTimeoutMs(240000);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            conv.convert(env.touch(QStringLiteral("broken.pptx"), "this is not a presentation"), env.path(QStringLiteral("o1")), env.path(QStringLiteral("profile")));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 250000);
            QVERIFY(!spy[0][0].toBool());
            QVERIFY2(spy[0][2].toString().contains(QStringLiteral("not a valid presentation")), qPrintable(spy[0][2].toString()));
            QVERIFY(!QFile::exists(env.path(QStringLiteral("o1/broken.pdf"))));
            QVERIFY(LuxTest::processesUnder(lo).isEmpty());   // refused without starting LibreOffice
        }
        // a package that LibreOffice itself cannot load (valid ZIP, no presentation inside)
        {
            QByteArray zip("PK\x03\x04", 4);
            zip += QByteArray(60, 0);
            OfficeConverter conv;
            conv.setSofficeOverride(soffice);
            conv.setTimeoutMs(240000);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            conv.convert(env.touch(QStringLiteral("hollow.pptx"), zip), env.path(QStringLiteral("o1b")), env.path(QStringLiteral("profile")));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 250000);
            QVERIFY(!spy[0][0].toBool());
            QVERIFY2(spy[0][2].toString().contains(QStringLiteral("could not convert")), qPrintable(spy[0][2].toString()));
            QVERIFY(!QFile::exists(env.path(QStringLiteral("o1b/hollow.pdf"))));
            QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
        }
        // a conversion that takes longer than the timeout: the whole process tree is ended, the profile is discarded
        {
            OfficeConverter conv;
            conv.setSofficeOverride(soffice);
            conv.setTimeoutMs(150);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            const QString profile = env.path(QStringLiteral("fresh-profile"));
            conv.convert(env.path(QStringLiteral("source_deck.pptx")).isEmpty() ? QString() : QStringLiteral(LUX_FIXTURES "/source_deck.pptx"),
                         env.path(QStringLiteral("o2")), profile);
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 20000);
            QVERIFY(!spy[0][0].toBool());
            QVERIFY2(spy[0][2].toString().contains(QStringLiteral("in time")), qPrintable(spy[0][2].toString()));
            QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
            QVERIFY(!QFile::exists(env.path(QStringLiteral("o2/source_deck.pdf"))));
            QVERIFY2(!QDir(profile).exists(), "a killed conversion does not leave a half-written profile");
            QTest::qWait(300);
            QCOMPARE(spy.size(), 1);                                // reported exactly once
        }
        // cancelled
        {
            OfficeConverter conv;
            conv.setSofficeOverride(soffice);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            conv.convert(QStringLiteral(LUX_FIXTURES "/source_deck.odp"), env.path(QStringLiteral("o3")), env.path(QStringLiteral("profile3")));
            QTRY_VERIFY_WITH_TIMEOUT(!LuxTest::processesUnder(lo).isEmpty() || spy.size() == 1, 20000);
            conv.cancel();
            QTRY_COMPARE(spy.size(), 1);
            QVERIFY(!spy[0][0].toBool() || QFile::exists(env.path(QStringLiteral("o3/source_deck.pdf"))));
            QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
        }
        // closing LuxPresenter (destroying the converter) while LibreOffice works ends it too
        {
            auto* conv = new OfficeConverter;
            conv->setSofficeOverride(soffice);
            conv->setTimeoutMs(240000);
            conv->convert(QStringLiteral(LUX_FIXTURES "/source_deck.pptx"), env.path(QStringLiteral("o4")), env.path(QStringLiteral("profile4")));
            QTRY_VERIFY_WITH_TIMEOUT(!LuxTest::processesUnder(lo).isEmpty(), 30000);
            delete conv;
            QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
        }
        // and the next conversion works normally
        {
            OfficeConverter conv;
            conv.setSofficeOverride(soffice);
            conv.setTimeoutMs(240000);
            QSignalSpy spy(&conv, &OfficeConverter::finished);
            conv.convert(QStringLiteral(LUX_FIXTURES "/source_deck.odp"), env.path(QStringLiteral("o5")), env.path(QStringLiteral("profile5")));
            QTRY_COMPARE_WITH_TIMEOUT(spy.size(), 1, 250000);
            QVERIFY2(spy[0][0].toBool(), qPrintable(spy[0][2].toString()));
        }
        QVERIFY2(watcher.seen().isEmpty(), qPrintable(QStringLiteral("LibreOffice showed a window: ") + watcher.seen().join(QStringLiteral("; "))));
    }

    void importPreviewScheduleAndLiveUseTheBundledEngineWithoutAnyLibreOfficeWindow()
    {
        Env env;
        const QString lo = QStringLiteral(LUX_BUNDLED_LO);
        MainWindow& w = env.mainWindow();
        w.presentations()->setSofficeOverride(OfficeConverter::locate(lo, {}).soffice);
        w.presentations()->setConversionTimeoutMs(240000);
        LuxTest::WindowWatcher watcher(lo);
        PresentationTab* tab = w.library()->presentationTab();

        // two PowerPoint/OpenDocument imports in a row are queued and converted one after the other
        const QString pptx = env.path(QStringLiteral("Service.pptx")), odp = env.path(QStringLiteral("Hymns.odp"));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/source_deck.pptx"), pptx));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/source_deck.odp"), odp));
        QSignalSpy opened(w.presentations(), &PresentationService::opened);
        QVERIFY(tab->importPath(pptx));
        QTRY_COMPARE_WITH_TIMEOUT(tab->slideGrid()->count(), 3, 250000);
        QVERIFY(tab->importPath(odp));
        QTRY_COMPARE_WITH_TIMEOUT(opened.size(), 2, 250000);
        QCOMPARE(env.data->presentationDocs().size(), 2);

        // Preview, Schedule, Live: PDFium shows the converted pages
        tab->selectDocumentByPath(pptx);
        QTRY_COMPARE_WITH_TIMEOUT(tab->slideGrid()->count(), 3, 20000);
        emit tab->slideGrid()->itemPressed(tab->slideGrid()->item(1));
        QVERIFY(w.previewPane()->hasDocument());
        const QImage pv = w.previewPane()->pageView()->currentImage();
        QVERIFY(!pv.isNull());
        QVERIFY(closeTo(px(pv, 6, pv.height() - 6), QColor(0, 128, 0), 14));        // page 2 is green
        w.addPreviewToSchedule();
        QCOMPARE(w.scheduleService().count(), 1);
        w.scheduleService().userSelect(0);
        QTRY_VERIFY(w.previewPane()->hasDocument());
        w.goLive();
        auto* fo = dynamic_cast<FakeOutput*>(w.projection()->output());
        QVERIFY(fo && fo->updates.last().isPresentationMode);
        QVERIFY(fo->updates.last().presentationPdfPath.contains(QStringLiteral("PresentationCache")));
        w.liveNext();
        w.liveNext();
        QCOMPARE(fo->updates.last().presentationPage, 2);
        const QImage page3 = PageImageCache::instance().page(fo->updates.last().presentationPdfPath, 2, QSize(640, 480));
        QVERIFY(closeTo(px(page3, 6, page3.height() - 6), QColor(200, 0, 0), 14));   // page 3 is red

        // a second open of the same document uses the cache: LibreOffice is not started again
        const int pollsBefore = watcher.pollsWithProcess();
        QSignalSpy again(w.presentations(), &PresentationService::opened);
        w.presentations()->open(pptx);
        QTRY_COMPARE(again.size(), 1);
        QTest::qWait(400);
        QCOMPARE(watcher.pollsWithProcess(), pollsBefore);

        QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
        QVERIFY2(watcher.seen().isEmpty(), qPrintable(QStringLiteral("LibreOffice showed a window: ") + watcher.seen().join(QStringLiteral("; "))));
        QVERIFY(watcher.pollsWithProcess() > 0);
        QVERIFY(!QFile::exists(QDir(AppPaths::presentationCacheDirectory()).filePath(QStringLiteral("_user_profile/user/.lock"))) ||
                LuxTest::processesUnder(lo).isEmpty());
    }
    // ---- projector ------------------------------------------------------------------------------
    void serviceCarriesPresentationState()
    {
        ProjectionService svc;
        svc.setOutputFactory(LuxTest::fakeFactory());
        svc.showWindow();
        auto* fo = dynamic_cast<FakeOutput*>(svc.output());
        QVERIFY(fo);
        svc.setLivePresentation(QStringLiteral("C:/x/deck.pdf"), 2);
        QVERIFY(fo->updates.last().isPresentationMode);
        QCOMPARE(fo->updates.last().presentationPdfPath, QStringLiteral("C:/x/deck.pdf"));
        QCOMPARE(fo->updates.last().presentationPage, 2);
        const int n = fo->updates.size();
        svc.navigatePresentation(2);                      // same page: nothing to do
        QCOMPARE(fo->updates.size(), n);
        svc.navigatePresentation(0);
        QCOMPARE(fo->updates.last().presentationPage, 0);
        svc.toggleBlack(true);
        QVERIFY(fo->updates.last().isBlack && fo->updates.last().isPresentationMode);   // black keeps the page underneath
        svc.toggleBlack(false);
        Slide s;
        s.content = QStringLiteral("text");
        svc.setLiveSlide(s, QStringLiteral("#001833"));   // a song/slide replaces the presentation
        QVERIFY(!fo->updates.last().isPresentationMode && fo->updates.last().presentationPdfPath.isEmpty());
        svc.setLivePresentation(QStringLiteral("C:/x/deck.pdf"), 1);
        svc.closePresentation();
        QVERIFY(!svc.state().isPresentationMode);
        svc.setLivePresentation(QStringLiteral("C:/x/deck.pdf"), 1);
        svc.playMedia(QStringLiteral("C:/x/v.mp4"), true);
        QVERIFY(!svc.state().isPresentationMode && svc.state().isVideoMode);
    }

    void composerShowsPresentationOnBlackAndRestoresSlides()
    {
        QTemporaryDir dir;
        QFile vf(dir.filePath(QStringLiteral("bg.mp4")));
        vf.open(QIODevice::WriteOnly);
        vf.write("x");
        vf.close();
        Clocked k;
        int prepared = 0;
        k.c.setPresentationPreparer([&] { ++prepared; });
        ProjectionState st = baseState();
        st.activeBackground = vf.fileName();                         // background video running
        Slide s;
        s.content = QStringLiteral("hello");
        st.activeSlide = s;
        k.c.applyState(st);
        QVERIFY(k.c.videoShowsThrough());
        k.c.takeVideoCommands();

        k.now = 1000;
        st.isPresentationMode = true;
        st.presentationPdfPath = QStringLiteral("C:/x/deck.pdf");
        st.presentationPage = 0;
        k.c.applyState(st);
        QVERIFY(k.c.presentationActive());
        QCOMPARE(prepared, 1);
        QCOMPARE(k.c.rootBackground(), QStringLiteral("#000000"));
        QVERIFY(!k.c.videoShowsThrough());
        const auto cmds = k.c.takeVideoCommands();
        QCOMPARE(cmds.size(), 1);
        QVERIFY(cmds[0].kind == VideoCommand::Kind::Stop);           // the background video stops
        QCOMPARE(k.c.slideOpacity(0, 1000) + k.c.slideOpacity(1, 1000), 0.0);
        const int serial = k.c.presentationSerial();

        k.now = 1100;
        k.c.applyState(st);                                          // nothing changed
        QCOMPARE(prepared, 1);
        QCOMPARE(k.c.presentationSerial(), serial);
        st.presentationPage = 3;                                     // navigation
        k.c.applyState(st);
        QCOMPARE(prepared, 2);
        QCOMPARE(k.c.presentationPage(), 3);
        QVERIFY(k.c.presentationSerial() > serial);

        k.now = 2000;                                                // logo and black sit above the page
        st.isLogo = true;
        k.c.applyState(st);
        QVERIFY(k.c.logoVisible());
        QVERIFY(k.c.presentationActive());
        st.presentationPage = 4;                                     // page changes are applied even under the logo
        k.c.applyState(st);
        QCOMPARE(k.c.presentationPage(), 4);
        st.isLogo = false;
        k.now = 3000;
        k.c.applyState(st);
        st.isBlack = true;
        k.now = 4000;
        k.c.applyState(st);
        QVERIFY(k.c.blackVisible() && k.c.presentationActive());
        st.isBlack = false;

        k.now = 5000;                                                // back to the song
        st.isPresentationMode = false;
        k.c.applyState(st);
        QVERIFY(!k.c.presentationActive());
        QVERIFY(k.c.videoShowsThrough());                            // the background video starts again
        bool play = false;
        for (const VideoCommand& c : k.c.takeVideoCommands())
            play |= c.kind == VideoCommand::Kind::PlayBackground;
        QVERIFY(play);
        QVERIFY(k.c.slideLayer(k.c.activeSlideLayer()).valid);
        QVERIFY(k.c.slideOpacity(k.c.activeSlideLayer(), 6000) > 0.99);
        k.c.reset();
        QVERIFY(!k.c.presentationActive());
    }

    void presentationPageIsFittedInTheTargetMonitor()
    {
        Env env;
        const QString file = env.pdf(QStringLiteral("deck.pdf"));
        Clocked k;
        ProjectionWindow win(&k.c);
        win.resize(1600, 1000);
        k.c.setPresentationPreparer([&] { win.preparePresentation(); });
        ProjectionState st = baseState();
        st.isPresentationMode = true;
        st.presentationPdfPath = file;

        auto render = [&](int w, int h, qreal dpr = 1.0) {
            QImage img(int(w * dpr), int(h * dpr), QImage::Format_ARGB32_Premultiplied);
            img.setDevicePixelRatio(dpr);
            img.fill(Qt::transparent);
            QPainter p(&img);
            win.paintScene(&p, QRectF(0, 0, w, h), 0);
            p.end();
            return img;
        };

        // 16:9 page on a 16:10 monitor: full width, letterboxed top/bottom in black
        st.presentationPage = 0;
        k.c.applyState(st);
        QImage img = render(1600, 1000);
        QVERIFY(closeTo(px(img, 800, 200), kPages[0].colour));      // (the caption "Slide N" is white, in the middle)
        QVERIFY(closeTo(px(img, 2, 52), kPages[0].colour) && closeTo(px(img, 1597, 948), kPages[0].colour));   // page edges: 1600x900 at y=50..950
        QCOMPARE(px(img, 800, 20), qRgb(0, 0, 0));
        QCOMPARE(px(img, 800, 980), qRgb(0, 0, 0));
        QCOMPARE(px(img, 0, 0), qRgb(0, 0, 0));

        // A4 portrait: pillarboxed, aspect preserved (width = 1000 * 595/842 = 707)
        st.presentationPage = 1;
        k.c.applyState(st);
        img = render(1600, 1000);
        QVERIFY(closeTo(px(img, 800, 200), kPages[1].colour));
        QCOMPARE(px(img, 400, 500), qRgb(0, 0, 0));
        QCOMPARE(px(img, 1200, 500), qRgb(0, 0, 0));
        int left = 0, right = 0;
        for (int x = 0; x < 1600; ++x)
            if (closeTo(px(img, x, 200), kPages[1].colour)) { if (!left) left = x; right = x; }
        QVERIFY2(qAbs((right - left + 1) - 707) <= 2, qPrintable(QString::number(right - left + 1)));
        QVERIFY(qAbs((left + right) / 2 - 800) <= 1);

        // other monitors / scale factors: rendered for the target's own device pixels
        for (const QSize& sz : {QSize(1920, 1080), QSize(1280, 720), QSize(1024, 768), QSize(2560, 1080)}) {
            st.presentationPage = 2;                                    // 4:3
            k.c.applyState(st);
            win.resize(sz);
            win.preparePresentation();
            for (qreal dpr : {1.0, 1.5}) {
                const QImage r = render(sz.width(), sz.height(), dpr);
                const double scale = qMin(sz.width() * dpr / 800.0, sz.height() * dpr / 600.0);
                const int pageW = qRound(800 * scale), pageH = qRound(600 * scale);
                const int cx = r.width() / 2, cy = r.height() / 2;
                const int cy2 = cy - pageH / 4;                       // above the caption
                QVERIFY(closeTo(px(r, cx, cy2), kPages[2].colour));
                QVERIFY(closeTo(px(r, cx - pageW / 2 + 2, cy2), kPages[2].colour) && closeTo(px(r, cx + pageW / 2 - 3, cy2), kPages[2].colour));
                QVERIFY(closeTo(px(r, cx, cy - pageH / 2 + 2), kPages[2].colour) && closeTo(px(r, cx, cy + pageH / 2 - 3), kPages[2].colour));
                if (pageW + 8 < r.width()) {
                    QCOMPARE(px(r, cx - pageW / 2 - 4, cy), qRgb(0, 0, 0));
                    QCOMPARE(px(r, cx + pageW / 2 + 4, cy), qRgb(0, 0, 0));
                }
                if (pageH + 8 < r.height()) {
                    QCOMPARE(px(r, cx, cy - pageH / 2 - 4), qRgb(0, 0, 0));
                    QCOMPARE(px(r, cx, cy + pageH / 2 + 4), qRgb(0, 0, 0));
                }
            }
        }

        // an unreadable/missing PDF leaves the (black) output intact instead of crashing
        st.presentationPdfPath = env.path(QStringLiteral("gone.pdf"));
        k.c.applyState(st);
        win.resize(640, 360);
        img = render(640, 360);
        QCOMPARE(px(img, 320, 180), qRgb(0, 0, 0));
    }

    // ---- panes -----------------------------------------------------------------------------------
    void paneDocumentModeNavigatesPages()
    {
        Env env;
        PresentationService::Document doc;
        doc.title = QStringLiteral("deck");
        doc.filePath = doc.pdfPath = env.pdf(QStringLiteral("deck.pdf"));
        doc.pageCount = 3;

        SlidePaneView pane(QStringLiteral("Preview"), false);
        pane.resize(520, 400);
        pane.show();
        QVERIFY(QTest::qWaitForWindowExposed(&pane));
        QVERIFY(!pane.docPrevButton()->isEnabled() && !pane.docNextButton()->isEnabled());
        QSignalSpy changed(&pane, &SlidePaneView::documentPageChanged);

        pane.loadDocument(doc, 0);
        QCOMPARE(pane.mode(), SlidePaneView::Mode::PresentationDoc);
        QVERIFY(pane.hasDocument());
        QCOMPARE(pane.headerText(), QStringLiteral("Preview - deck"));
        QCOMPARE(pane.docStatus()->text(), QStringLiteral("Slide 1 of 3"));
        QVERIFY(!pane.docPrevButton()->isEnabled() && pane.docNextButton()->isEnabled());
        QCOMPARE(changed.size(), 0);                          // loading is not navigation

        pane.docNextButton()->click();
        QCOMPARE(pane.documentPage(), 1);
        QCOMPARE(changed.size(), 1);
        QCOMPARE(changed.last().first().toInt(), 1);
        QCOMPARE(pane.docStatus()->text(), QStringLiteral("Slide 2 of 3"));
        QVERIFY(pane.docPrevButton()->isEnabled() && pane.docNextButton()->isEnabled());
        pane.setDocumentPage(99);                              // clamped
        QCOMPARE(pane.documentPage(), 2);
        QVERIFY(!pane.docNextButton()->isEnabled());
        pane.setDocumentPage(2);
        QCOMPARE(changed.size(), 2);                           // unchanged page: no signal
        pane.docPrevButton()->click();
        QCOMPARE(pane.documentPage(), 1);

        // the page is rendered for the pane: sharp, aspect preserved
        const QImage shown = pane.pageView()->currentImage();
        QVERIFY(!shown.isNull());
        QVERIFY(shown.width() <= pane.pageView()->width() * pane.pageView()->devicePixelRatioF() + 1);
        QVERIFY(qAbs(shown.width() / double(shown.height()) - 595.0 / 842.0) < 0.01);

        // back to slides / error display
        PresentationItem item;
        Slide s;
        s.content = QStringLiteral("x");
        item.title = QStringLiteral("song");
        item.slides.append(s);
        pane.loadItem(item, QStringLiteral("#001833"));
        QCOMPARE(pane.mode(), SlidePaneView::Mode::Slides);
        QVERIFY(!pane.hasDocument());
        QVERIFY(!pane.docPrevButton()->isEnabled() && !pane.docNextButton()->isEnabled());
        pane.showDocumentError(QStringLiteral("Lost"), QStringLiteral("The file was not found."));
        QCOMPARE(pane.mode(), SlidePaneView::Mode::PresentationDoc);
        QVERIFY(!pane.hasDocument());
        QCOMPARE(pane.pageView()->message(), QStringLiteral("The file was not found."));
        QCOMPARE(pane.headerText(), QStringLiteral("Preview - Lost"));
    }

    // ---- library tab -----------------------------------------------------------------------------
    void libraryTabImportsStagesAndRemovesDocuments()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        PresentationTab* tab = w.library()->presentationTab();
        QSignalSpy staged(tab, &PresentationTab::slideStaged);
        QSignalSpy activated(tab, &PresentationTab::slideActivated);
        QSignalSpy messages(tab, &PresentationTab::messageRequested);

        const QString file = env.pdf(QStringLiteral("Sunday.pdf"));
        QVERIFY(tab->importPath(file));
        QTRY_COMPARE(tab->slideGrid()->count(), 3);
        QCOMPARE(tab->documentCombo()->count(), 1);
        QCOMPARE(tab->documentCombo()->currentText(), QStringLiteral("Sunday"));
        QVERIFY(tab->deleteButton()->isEnabled());
        QCOMPARE(env.data->presentationDocs().size(), 1);
        QCOMPARE(env.data->presentationDocs().first().slideCount, 3);
        QCOMPARE(env.data->presentationDocs().first().fileExtension, QStringLiteral(".pdf"));
        QVERIFY(!tab->loadingLabel()->isVisible());

        // stored in presentations.json (same file/format as before)
        QFile pf(QDir(AppPaths::dataDirectory()).filePath(QStringLiteral("presentations.json")));
        QVERIFY(pf.open(QIODevice::ReadOnly));
        const QJsonArray arr = QJsonDocument::fromJson(pf.readAll()).array();
        pf.close();
        QCOMPARE(arr.size(), 1);
        QVERIFY(arr[0].toObject().contains(QStringLiteral("FilePath")) && arr[0].toObject().contains(QStringLiteral("SlideCount")));

        // click stages in Preview, double-click goes live
        staged.clear();
        emit tab->slideGrid()->itemPressed(tab->slideGrid()->item(1));
        QCOMPARE(staged.size(), 1);
        QCOMPARE(staged.last()[1].toInt(), 1);
        QCOMPARE(w.previewPane()->mode(), SlidePaneView::Mode::PresentationDoc);
        QCOMPARE(w.previewPane()->documentPage(), 1);
        QCOMPARE(w.previewSource(), MainWindow::PreviewSource::Presentation);
        QCOMPARE(w.statusText(), QStringLiteral("Preview Presentation: Sunday - Slide 2"));
        emit tab->slideGrid()->itemDoubleClicked(tab->slideGrid()->item(2));
        QCOMPARE(activated.size(), 1);
        QVERIFY(w.isLiveOutputActive());
        QCOMPARE(w.livePane()->documentPage(), 2);
        QCOMPARE(w.statusText(), QStringLiteral("Live Presentation: Sunday - Slide 3"));

        // refusals do not touch the library
        QVERIFY(!tab->importPath(env.touch(QStringLiteral("old.ppt"))));
        QVERIFY(tab->message().contains(QStringLiteral("Legacy PowerPoint")));
        QVERIFY(!tab->importPath(env.touch(QStringLiteral("old.pps"))));
        QVERIFY(!tab->importPath(env.touch(QStringLiteral("letter.docx"))));
        QVERIFY(!tab->importPath(env.path(QStringLiteral("missing.pdf"))));
        QCOMPARE(messages.size(), 4);
        QCOMPARE(env.data->presentationDocs().size(), 1);

        // a broken PDF is reported and not added
        messages.clear();
        QVERIFY(tab->importPath(env.touch(QStringLiteral("broken.pdf"), "nope")));
        QTRY_COMPARE(messages.size(), 1);
        QVERIFY(messages[0][2].toBool());
        QCOMPARE(env.data->presentationDocs().size(), 1);
        QCOMPARE(tab->slideGrid()->count(), 0);

        // LibreOffice missing: PowerPoint import is refused with a clear message, the app carries on
        messages.clear();
        env.window->presentations()->setSofficeOverride(env.path(QStringLiteral("nope/soffice.exe")));
        QVERIFY(tab->importPath(env.touch(QStringLiteral("deck.pptx"))));
        QTRY_COMPARE_WITH_TIMEOUT(messages.size(), 1, 20000);
        QVERIFY(messages[0][1].toString().contains(QStringLiteral("LibreOffice was not found")));
        QCOMPARE(env.data->presentationDocs().size(), 1);

        // filter + selection + removal
        tab->selectDocumentByPath(file);
        QTRY_COMPARE(tab->slideGrid()->count(), 3);
        tab->filterBox()->setText(QStringLiteral("zzz"));
        QCOMPARE(tab->documentCombo()->count(), 0);
        tab->filterBox()->setText(QStringLiteral("sun"));
        QCOMPARE(tab->documentCombo()->count(), 1);
        tab->filterBox()->clear();
        QVERIFY(tab->documentCombo()->currentIndex() >= 0);
        QVERIFY2(tab->removeDocument(tab->documentCombo()->currentData().toString()), qPrintable(env.data->lastError()));
        QCOMPARE(tab->documentCombo()->count(), 0);
        QCOMPARE(tab->slideGrid()->count(), 0);
        QVERIFY(!tab->deleteButton()->isEnabled());
        QCOMPARE(env.data->presentationDocs().size(), 0);
        QVERIFY(QFile::exists(file));                      // the user's file is never deleted
    }

    void convertedPowerPointJoinsTheLibrary()
    {
        Env env;
        qputenv("FAKE_SOFFICE_PDF", env.pdf(QStringLiteral("from-lo.pdf")).toLocal8Bit());
        MainWindow& w = env.mainWindow();
        w.presentations()->setSofficeOverride(QStringLiteral(LUX_FAKE_SOFFICE));
        PresentationTab* tab = w.library()->presentationTab();
        const QString pptx = env.touch(QStringLiteral("Easter.pptx"));
        QVERIFY(tab->importPath(pptx));
        QTRY_COMPARE_WITH_TIMEOUT(tab->slideGrid()->count(), 3, 20000);
        QCOMPARE(env.data->presentationDocs().first().fileExtension, QStringLiteral(".pptx"));
        QCOMPARE(tab->currentDocument()->title, QStringLiteral("Easter"));
        QVERIFY(tab->currentDocument()->pdfPath.contains(QStringLiteral("PresentationCache")));
    }

    // ---- workflow: Preview / Live / projector / schedule ----------------------------------------------
    void documentGoesLivePageByPageThroughThePanes()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        PresentationTab* tab = w.library()->presentationTab();
        QVERIFY(tab->importPath(env.pdf(QStringLiteral("Sunday.pdf"))));
        QTRY_COMPARE(tab->slideGrid()->count(), 3);
        const QString pdf = tab->currentDocument()->pdfPath;

        emit tab->slideGrid()->itemPressed(tab->slideGrid()->item(0));
        w.goLiveButton()->click();
        auto* fo = dynamic_cast<FakeOutput*>(w.projection()->output());
        QVERIFY(fo && fo->shown);
        QVERIFY(fo->updates.last().isPresentationMode);
        QCOMPARE(fo->updates.last().presentationPdfPath, pdf);
        QCOMPARE(fo->updates.last().presentationPage, 0);
        QCOMPARE(w.livePane()->mode(), SlidePaneView::Mode::PresentationDoc);
        QCOMPARE(w.livePane()->headerText(), QStringLiteral("Live - Sunday"));
        QCOMPARE(w.livePane()->docStatus()->text(), QStringLiteral("LIVE \u25CF Slide 1 of 3"));
        QVERIFY(tab->slideGrid()->item(0)->data(Qt::UserRole + 1).toBool());          // LIVE badge on slide 1

        // Live pane buttons, keyboard and API all drive the projector
        w.livePane()->docNextButton()->click();
        QCOMPARE(fo->updates.last().presentationPage, 1);
        QVERIFY(!tab->slideGrid()->item(0)->data(Qt::UserRole + 1).toBool());
        QVERIFY(tab->slideGrid()->item(1)->data(Qt::UserRole + 1).toBool());
        auto key = [&](int k) { QKeyEvent e(QEvent::KeyPress, k, Qt::NoModifier); return w.handleProjectionKey(&e); };
        QVERIFY(key(Qt::Key_Space));
        QCOMPARE(fo->updates.last().presentationPage, 2);
        QCOMPARE(w.statusText(), QStringLiteral("Live Presentation: Sunday - Slide 3"));
        QVERIFY(key(Qt::Key_Space));                           // clamps at the last page
        QCOMPARE(w.livePane()->documentPage(), 2);
        QVERIFY(key(Qt::Key_Home));
        QCOMPARE(fo->updates.last().presentationPage, 0);
        QVERIFY(key(Qt::Key_End));
        QCOMPARE(fo->updates.last().presentationPage, 2);
        QVERIFY(key(Qt::Key_PageUp));
        QCOMPARE(w.livePane()->documentPage(), 1);
        QCOMPARE(w.previewPane()->documentPage(), 0);          // Preview and Live are independent

        // Black / Logo work over a presentation; Preview navigation does not move the projector
        w.blackButton()->click();
        QVERIFY(fo->updates.last().isBlack && fo->updates.last().isPresentationMode);
        w.blackButton()->click();
        w.previewPane()->docNextButton()->click();
        QCOMPARE(w.previewPane()->documentPage(), 1);
        QCOMPARE(fo->updates.last().presentationPage, 1);
        QCOMPARE(tab->slideGrid()->currentRow(), 1);           // the grid follows the Preview selection

        // Go Live again from the Preview page that is staged
        w.previewPane()->setDocumentPage(2);
        w.goLive();
        QCOMPARE(w.livePane()->documentPage(), 2);
        QCOMPARE(fo->updates.last().presentationPage, 2);

        // a song replaces the presentation on the projector
        Song song;
        song.title = QStringLiteral("Grace");
        song.lyricsRaw = QStringLiteral("Amazing grace");
        env.data->saveSong(song);
        w.songsTab()->reload();
        w.songsTab()->userSelect(song.id);
        w.goLive();
        QVERIFY(!fo->updates.last().isPresentationMode);
        QVERIFY(fo->updates.last().activeSlide.has_value());
        QCOMPARE(w.livePane()->mode(), SlidePaneView::Mode::Slides);
        QVERIFY(!tab->slideGrid()->item(2)->data(Qt::UserRole + 1).toBool());   // no stale LIVE badge
        // and the document can come back
        emit tab->slideGrid()->itemDoubleClicked(tab->slideGrid()->item(1));
        QVERIFY(fo->updates.last().isPresentationMode);
        QCOMPARE(fo->updates.last().presentationPage, 1);
    }

    void presentationItemsLiveInTheSchedule()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        PresentationTab* tab = w.library()->presentationTab();
        const QString file = env.pdf(QStringLiteral("Order of Service.pdf"));
        QVERIFY(tab->importPath(file));
        QTRY_COMPARE(tab->slideGrid()->count(), 3);

        // add through the tab's menu and through the schedule's "+" with the document in Preview
        tab->actionAddToSchedule()->trigger();
        QCOMPARE(w.scheduleService().count(), 1);
        emit tab->slideGrid()->itemPressed(tab->slideGrid()->item(1));
        w.addPreviewToSchedule();
        QCOMPARE(w.scheduleService().count(), 2);
        const ScheduleItem& item = w.scheduleService().items().first();
        QCOMPARE(item.category, ItemCategory::Presentation);
        QCOMPARE(item.title, QStringLiteral("Order of Service"));
        QCOMPARE(item.presentation.slides.size(), 3);
        QCOMPARE(item.presentation.slides[1].label, QStringLiteral("Slide 2"));
        QCOMPARE(item.presentation.slides[1].content, QStringLiteral("2"));
        QCOMPARE(QFileInfo(item.presentation.slides[0].subtitle), QFileInfo(file));

        // saved in the unchanged schedule format and read back
        const QString sched = env.path(QStringLiteral("service.fws"));
        QString err;
        QVERIFY2(w.saveScheduleFile(sched, &err), qPrintable(err));
        QFile sf(sched);
        QVERIFY(sf.open(QIODevice::ReadOnly));
        const QJsonObject root = QJsonDocument::fromJson(sf.readAll()).object();
        sf.close();
        QStringList rootKeys = root.keys();
        rootKeys.sort();
        QCOMPARE(rootKeys, (QStringList{"Items", "SavedAt", "Version"}));
        const QJsonObject jitem = root[QStringLiteral("Items")].toArray()[0].toObject();
        QCOMPARE(jitem[QStringLiteral("Category")].toInt(), 2);                  // ItemCategory.Presentation
        QStringList itemKeys = jitem.keys();
        itemKeys.sort();
        QCOMPARE(itemKeys, (QStringList{"Category", "Id", "Presentation", "Title", "TypeLabel"}));
        QStringList slideKeys = jitem[QStringLiteral("Presentation")].toObject()[QStringLiteral("Slides")].toArray()[0].toObject().keys();
        slideKeys.sort();
        QCOMPARE(slideKeys, (QStringList{"Content", "CustomBackgroundPath", "FontColor", "FontFamily", "FontSize", "HasOutline", "HasShadow", "Id",
                                         "IsBold", "IsItalic", "IsUnderline", "Label", "Subtitle", "Title", "Type"}));
        w.newScheduleNoPrompt();
        QCOMPARE(w.scheduleService().count(), 0);
        QVERIFY2(w.openScheduleFile(sched, &err), qPrintable(err));
        QCOMPARE(w.scheduleService().count(), 2);
        QCOMPARE(w.scheduleService().items().first().category, ItemCategory::Presentation);

        // selecting the item previews the document; double-clicking it goes live
        w.scheduleService().userSelect(0);
        QTRY_VERIFY(w.previewPane()->hasDocument());
        QCOMPARE(w.previewPane()->headerText(), QStringLiteral("Preview - Order of Service"));
        QCOMPARE(w.previewSource(), MainWindow::PreviewSource::Schedule);
        QCOMPARE(w.previewPane()->documentPage(), 0);
        emit w.schedule()->itemActivated(0);
        QTRY_VERIFY(w.livePane()->hasDocument());
        auto* fo = dynamic_cast<FakeOutput*>(w.projection()->output());
        QVERIFY(fo && fo->updates.last().isPresentationMode);
        QCOMPARE(fo->updates.last().presentationPage, 0);
        QCOMPARE(w.statusText(), QStringLiteral("Live Presentation: Order of Service - Slide 1"));
        w.liveNext();
        QCOMPARE(fo->updates.last().presentationPage, 1);

        // the same item from the context-menu "Go Live"
        emit w.schedule()->goLiveRequested(0);
        QTRY_COMPARE(w.livePane()->documentPage(), 0);
    }

    void missingFilesInTheScheduleAreHandled()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        PresentationService::Document doc;
        doc.title = QStringLiteral("Vanished");
        doc.filePath = env.path(QStringLiteral("vanished.pdf"));
        doc.pdfPath = doc.filePath;
        doc.pageCount = 4;
        w.addDocumentToSchedule(doc);
        QCOMPARE(w.scheduleService().count(), 1);
        w.scheduleService().userSelect(0);
        QTRY_VERIFY(w.statusText().startsWith(QStringLiteral("Presentation unavailable")));
        QCOMPARE(w.previewPane()->mode(), SlidePaneView::Mode::PresentationDoc);
        QVERIFY(!w.previewPane()->hasDocument());
        QVERIFY(w.previewPane()->pageView()->message().contains(QStringLiteral("not found")));
        // going live with nothing to show must not crash and must not start a presentation
        emit w.schedule()->itemActivated(0);
        QTest::qWait(50);
        auto* fo = dynamic_cast<FakeOutput*>(w.projection()->output());
        QVERIFY(!fo || fo->updates.isEmpty() || !fo->updates.last().isPresentationMode);
        QVERIFY(!w.projection()->state().isPresentationMode);

        // a corrupt file replaced behind the schedule's back
        const QString file = env.pdf(QStringLiteral("real.pdf"));
        QFile f(file);
        QVERIFY(f.open(QIODevice::WriteOnly | QIODevice::Truncate));
        f.write("not a pdf");
        f.close();
        doc.filePath = doc.pdfPath = file;
        w.addDocumentToSchedule(doc);
        w.scheduleService().userSelect(1);
        QTRY_VERIFY(w.previewPane()->pageView()->message().contains(QStringLiteral("not a valid PDF")));
    }
};

QTEST_MAIN(PresentationTests)
#include "test_presentation.moc"
