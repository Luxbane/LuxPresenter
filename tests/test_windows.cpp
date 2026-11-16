// Milestone 4 tests that need real Win32 windows (run on the "windows" platform, not offscreen):
// monitor enumeration, projection window placement/z-order/styles with small fake screens, rendering of
// the real projection window, and the dual-deck libmpv playback + crossfade.
// The windows are small and shown for a moment only; nothing here uses a full-screen window.

#include "FakeOutput.h"
#include "Services/MpvApi.h"
#include "Services/ProjectionService.h"
#include "LoWatch.h"
#include "Models/PresentationDocModels.h"
#include "Services/Presentation/OfficeConverter.h"
#include "Services/PresentationService.h"
#include "Services/AppPaths.h"
#include "Services/PageImageCache.h"
#include "Services/PdfDocument.h"
#include "Views/Components/OnlineBrowserView.h"
#include "Services/AdBlockEngine.h"
#include "Services/AdBlockService.h"
#include "Views/Components/WebView2Host.h"
#include <QTcpServer>
#include <QTcpSocket>
#include "Views/Projection/DisplayHelper.h"
#include "Views/Projection/DualDeckVideo.h"
#include "Views/Projection/ProjectionOutput.h"

#include <QElapsedTimer>
#include <QImage>
#include <memory>
#include <QDir>
#include <QPageSize>
#include <QPainter>
#include <QPdfWriter>
#include <QTemporaryDir>
#include <QUrl>
#include <QFileInfo>
#include <QVBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QLineEdit>
#include <QtTest>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

using namespace Lux;

namespace {

ScreenInfo fakeScreen(int index, const QRect& r, bool primary)
{
    ScreenInfo s;
    s.index = index;
    s.physicalLeft = r.left();
    s.physicalTop = r.top();
    s.physicalWidth = r.width();
    s.physicalHeight = r.height();
    s.isPrimary = primary;
    return s;
}

// Position of a window in the top-level z-order (0 = topmost); -1 if not found.
int zIndex(HWND h)
{
    int i = 0;
    for (HWND w = GetTopWindow(nullptr); w; w = GetWindow(w, GW_HWNDNEXT), ++i)
        if (w == h)
            return i;
    return -1;
}

// document.title of the page, JSON-encoded ("" when the browser does not answer in time)
QString pageTitle(WebView2Host* host)
{
    auto result = std::make_shared<QString>();
    auto done = std::make_shared<bool>(false);
    host->evaluate(QStringLiteral("document.title"), [result, done](const QString& r) { *result = r; *done = true; });
    QElapsedTimer t;
    t.start();
    while (!*done && t.elapsed() < 10000)
        QTest::qWait(20);
    return *result;
}

// Local web server for the ad-blocker tests: records the requested paths.
class PageServer : public QObject
{
public:
    PageServer()
    {
        m_server.listen(QHostAddress::LocalHost, 0);
        connect(&m_server, &QTcpServer::newConnection, this, [this] {
            while (QTcpSocket* s = m_server.nextPendingConnection()) {
                connect(s, &QTcpSocket::readyRead, this, [this, s] {
                    const QByteArray req = s->readAll();
                    const QString path = QString::fromLatin1(req.split(' ').value(1));
                    hits << path;
                    QByteArray body, type = "text/html";
                    const QString p = path.section(QLatin1Char('?'), 0, 0);
                    if (p == QLatin1String("/page.html")) {
                        const bool popup = !path.contains(QStringLiteral("nopopup"));
                        body = "<html><head><title>Ad test page</title></head><body><h1>content</h1>"
                               "<script src=\"/adblock/frontend_loader.js\"></script>"
                               "<script src=\"/88/tag.min.js\"></script>"
                               "<script src=\"/lib/app.js\"></script>"
                               "<script src=\"/?view=ad\"></script>"
                               + QByteArray(popup ? "<script>setTimeout(function(){ window.open('/popup.html'); }, 300);</script>" : "") +
                               "</body></html>";
                    } else if (p == QLatin1String("/adblock/frontend_loader.js") || p == QLatin1String("/88/tag.min.js") || path.contains(QStringLiteral("view=ad"))) {
                        body = "window.adLoaded = 1;"; type = "application/javascript";
                    } else if (p == QLatin1String("/lib/app.js")) {
                        body = "window.appLoaded = 1;"; type = "application/javascript";
                    } else if (p == QLatin1String("/popup.html")) {
                        body = "<html><head><title>Popup page</title></head><body>popup</body></html>";
                    } else {
                        s->write("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        s->disconnectFromHost();
                        return;
                    }
                    s->write("HTTP/1.1 200 OK\r\nContent-Type: " + type + "\r\nContent-Length: " + QByteArray::number(body.size()) + "\r\nConnection: close\r\n\r\n" + body);
                    s->disconnectFromHost();
                });
            }
        });
    }
    QString url(const QString& path) const { return QStringLiteral("http://127.0.0.1:%1%2").arg(m_server.serverPort()).arg(path); }
    int count(const QString& prefix) const
    {
        int n = 0;
        for (const QString& h : hits)
            n += h.startsWith(prefix) ? 1 : 0;
        return n;
    }
    QStringList hits;
private:
    QTcpServer m_server;
};
QString runJs(WebView2Host* host, const QString& script)
{
    auto result = std::make_shared<QString>();
    auto done = std::make_shared<bool>(false);
    host->evaluate(script, [result, done](const QString& r) { *result = r; *done = true; });
    QElapsedTimer t;
    t.start();
    while (!*done && t.elapsed() < 10000)
        QTest::qWait(20);
    return *result;
}
QByteArray adList(const QString& rule)
{
    QByteArray b = "[Adblock Plus 2.0]\n! Title: test list\n" + rule.toUtf8() + "\n";
    for (int i = 0; i < 1500; ++i)
        b += "||filler" + QByteArray::number(i) + ".example.test^$third-party\n";
    return b;
}HWND hwnd(QWidget* w) { return reinterpret_cast<HWND>(w->winId()); }

} // namespace

class WindowTests : public QObject
{
    Q_OBJECT
private slots:
    void platformIsWindows()
    {
        QCOMPARE(QGuiApplication::platformName(), QStringLiteral("windows"));
    }

    void enumeratesMonitors()
    {
        const QList<ScreenInfo> screens = DisplayHelper::enumerateScreens();
        QVERIFY(!screens.isEmpty());
        int primaries = 0;
        for (int i = 0; i < screens.size(); ++i) {
            QCOMPARE(screens[i].index, i);
            QVERIFY(screens[i].physicalWidth > 0 && screens[i].physicalHeight > 0);
            QVERIFY(screens[i].dpiScaleX >= 1.0);
            primaries += screens[i].isPrimary ? 1 : 0;
        }
        QCOMPARE(primaries, 1);
        qInfo() << "monitors:" << screens.size();
        for (const ScreenInfo& s : screens)
            qInfo() << " " << s.displayName() << s.physicalLeft << s.physicalTop << "dpi" << s.dpiScaleX;
    }

    void secondaryPlacementIsTopmostNonActivating()
    {
        const QRect rect(60, 60, 320, 180);
        ProjectionOutput out([&] { return QList<ScreenInfo>{fakeScreen(0, QRect(0, 0, 800, 600), true), fakeScreen(1, rect, false)}; });
        QWidget probe;                       // a normal window that must not lose the foreground
        probe.setGeometry(500, 300, 200, 120);
        probe.show();
        probe.activateWindow();
        QVERIFY(QTest::qWaitForWindowActive(&probe));

        const HWND foregroundBefore = GetForegroundWindow();
        QVERIFY(out.show(out.screens().at(1)));
        QVERIFY(out.isShown());
        HWND fg = hwnd(out.foreground());
        HWND bg = hwnd(out.background());
        QVERIFY(IsWindowVisible(fg) && IsWindowVisible(bg));
        QCOMPARE(DisplayHelper::windowRect(quintptr(fg)), rect);
        QCOMPARE(DisplayHelper::windowRect(quintptr(bg)), rect);
        const LONG_PTR ex = GetWindowLongPtrW(fg, GWL_EXSTYLE);
        QVERIFY(ex & WS_EX_NOACTIVATE);
        QVERIFY(ex & WS_EX_TOOLWINDOW);            // no taskbar button
        QVERIFY(GetWindowLongPtrW(bg, GWL_EXSTYLE) & WS_EX_NOACTIVATE);
        QVERIFY(GetForegroundWindow() != fg && GetForegroundWindow() != bg);
        QVERIFY(!out.foreground()->isActiveWindow() && !out.background()->isActiveWindow());
        // Showing the output must not change which window has the focus (another application may own it).
        QCOMPARE(GetForegroundWindow(), foregroundBefore);
        if (foregroundBefore == hwnd(&probe))
            QVERIFY(probe.isActiveWindow());
        QVERIFY2(zIndex(fg) >= 0 && zIndex(fg) < zIndex(bg), "foreground window above the video window");
        QVERIFY2(zIndex(fg) < zIndex(hwnd(&probe)), "secondary output above ordinary windows");

        out.hide();
        QVERIFY(!out.isShown());
        QVERIFY(!IsWindowVisible(fg) && !IsWindowVisible(bg));
    }

    // Regression: the first show must leave both windows exactly on the target rectangle once the event loop
    // has run (Qt applies its own pending geometry/DPI handling after the native SetWindowPos).
    void firstShowGeometrySticksAfterEventLoop()
    {
        const QRect rect(120, 90, 480, 270);   // deliberately different from Qt's default 640x480
        QList<ScreenInfo> monitors{fakeScreen(0, QRect(0, 0, 800, 600), true), fakeScreen(1, rect, false)};
        ProjectionOutput out([&] { return monitors; });
        for (int round = 0; round < 3; ++round) {      // first show, repeated show while visible, hide + show
            if (round == 2) out.hide();
            QVERIFY(out.show(out.screens().at(1)));
            for (int phase = 0; phase < 2; ++phase) {
                if (phase == 1)
                    QTest::qWait(300);                 // after the event loop has digested everything
                HWND fg = hwnd(out.foreground());
                HWND bg = hwnd(out.background());
                QCOMPARE(DisplayHelper::windowRect(quintptr(fg)), rect);
                QCOMPARE(DisplayHelper::windowRect(quintptr(bg)), rect);
                const qreal dpr = out.foreground()->devicePixelRatio();
                QCOMPARE(qRound(out.foreground()->width() * dpr), rect.width());
                QCOMPARE(qRound(out.foreground()->height() * dpr), rect.height());
                QCOMPARE(qRound(out.background()->width() * dpr), rect.width());
                QCOMPARE(qRound(out.decks()->deckWidget(0)->width() * dpr), rect.width());
            }
        }
        // Moving to another target rectangle while visible
        const QRect other(300, 200, 400, 225);
        monitors[1] = fakeScreen(1, other, false);
        QVERIFY(out.show(monitors[1]));
        QTest::qWait(200);
        QCOMPARE(DisplayHelper::windowRect(quintptr(hwnd(out.foreground()))), other);
        QCOMPARE(DisplayHelper::windowRect(quintptr(hwnd(out.background()))), other);
        out.hide();
    }

    // Output follows the *target monitor's* pixel size and aspect ratio (not the main window or panes): monitors
    // of different resolution/aspect ratio, switching between them, and a monitor changing resolution.
    void outputFollowsTargetMonitorResolution()
    {
        QList<ScreenInfo> monitors{fakeScreen(0, QRect(20, 20, 1600, 1000), true), fakeScreen(1, QRect(40, 40, 1280, 720), false),
                                   fakeScreen(2, QRect(60, 60, 1024, 768), false), fakeScreen(3, QRect(80, 80, 1920, 800), false)};
        ProjectionService svc;
        svc.setOutputFactory([&]() -> std::unique_ptr<IProjectionOutput> {
            return std::make_unique<ProjectionOutput>([&] { return monitors; });
        });
        Slide s;
        s.label = QStringLiteral("Verse 1");
        s.content = QStringLiteral("Amazing grace how sweet the sound");
        svc.setLiveSlide(s, QStringLiteral("#FF0000"));
        svc.showWindow();
        auto* out = dynamic_cast<ProjectionOutput*>(svc.output());
        QVERIFY(out);

        auto textWidthDesign = [&](const QRect& target) {
            QTest::qWait(60);
            const QImage img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
            // output size in device pixels == the monitor's pixel size, whatever the Qt scale factor is
            [&] { QCOMPARE(img.size(), target.size()); }();
            for (QPoint pt : {QPoint(0, 0), QPoint(img.width() - 1, 0), QPoint(0, img.height() - 1), QPoint(img.width() - 1, img.height() - 1)})
                if (img.pixel(pt) != qRgb(255, 0, 0))
                    return -1.0;
            int x0 = img.width(), x1 = -1;
            for (int y = 0; y < img.height(); y += 2)
                for (int x = 0; x < img.width(); ++x) {
                    const QRgb px = img.pixel(x, y);
                    if (qRed(px) < 200 || qGreen(px) > 60) { x0 = qMin(x0, x); x1 = qMax(x1, x); }
                }
            const double sc = qMin(target.width() / 1920.0, target.height() / 1080.0);
            return (x1 - x0 + 1) / sc;
        };

        QList<double> widths;
        for (int idx : {1, 2, 3, 0, 1}) {
            svc.setSelectedMonitorIndex(idx);
            const QRect target(monitors[idx].physicalLeft, monitors[idx].physicalTop, monitors[idx].physicalWidth, monitors[idx].physicalHeight);
            QCOMPARE(DisplayHelper::windowRect(quintptr(hwnd(out->foreground()))), target);
            const double w = textWidthDesign(target);
            QVERIFY2(w > 0, qPrintable(QStringLiteral("monitor %1: corners not filled / size mismatch").arg(idx)));
            widths << w;
        }
        for (double w : widths)
            QVERIFY2(qAbs(w / widths[0] - 1) < 0.03, qPrintable(QString::number(w)));

        // The selected monitor changes resolution while the output is up.
        monitors[1] = fakeScreen(1, QRect(40, 40, 1920, 1080), false);
        svc.setSelectedMonitorIndex(1);
        monitors[1] = fakeScreen(1, QRect(40, 40, 1366, 768), false);
        out->refreshPlacement();
        QTest::qWait(100);
        const QRect changed(40, 40, 1366, 768);
        QCOMPARE(DisplayHelper::windowRect(quintptr(hwnd(out->foreground()))), changed);
        QCOMPARE(DisplayHelper::windowRect(quintptr(hwnd(out->background()))), changed);
        const double w = textWidthDesign(changed);
        QVERIFY(w > 0 && qAbs(w / widths[0] - 1) < 0.03);
        svc.hideWindow();
    }
    void primaryPlacementStaysBehindNormalWindows()
    {
        const QRect rect(80, 80, 320, 180);
        ProjectionOutput out([&] { return QList<ScreenInfo>{fakeScreen(0, rect, true)}; });
        QWidget probe;
        probe.setGeometry(500, 300, 200, 120);
        probe.show();
        probe.activateWindow();
        QVERIFY(QTest::qWaitForWindowActive(&probe));

        QVERIFY(out.show(out.screens().first()));
        HWND fg = hwnd(out.foreground());
        HWND bg = hwnd(out.background());
        QCOMPARE(DisplayHelper::windowRect(quintptr(fg)), rect);
        QCOMPARE(DisplayHelper::windowRect(quintptr(bg)), rect);
        QVERIFY2(zIndex(hwnd(&probe)) < zIndex(fg), "single-screen test mode keeps the output behind the main window");
        QVERIFY(zIndex(fg) < zIndex(bg));
        QVERIFY(GetForegroundWindow() == hwnd(&probe) || GetForegroundWindow() != fg);
        out.hide();
    }

    void serviceDrivesRealOutputWindows()
    {
        const QRect rect(100, 100, 320, 180);
        ProjectionService svc;
        svc.setOutputFactory([&]() -> std::unique_ptr<IProjectionOutput> {
            return std::make_unique<ProjectionOutput>([&] { return QList<ScreenInfo>{fakeScreen(0, rect, false)}; });
        });
        Slide s;
        s.label = QStringLiteral("Verse 1");
        s.content = QStringLiteral("Amazing grace");
        svc.setLiveSlide(s, QStringLiteral("#FF0000"));
        svc.showWindow();
        auto* out = dynamic_cast<ProjectionOutput*>(svc.output());
        QVERIFY(out && out->isShown());
        QCOMPARE(svc.activeScreenNumber(), 1);

        // The first state after showing is applied without a transition: red background, text visible.
        QTest::qWait(100); // let Qt process the native resize before grabbing
        QImage img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QVERIFY(!img.isNull());
        auto px = [&](int x, int y) { return img.pixel(x * img.width() / 320, y * img.height() / 180); };
        QVERIFY2(qRed(px(4, 4)) > 240 && qGreen(px(4, 4)) < 15, qPrintable(QString::number(qRed(px(4, 4)))));

        // Black: the scene fades to black (transition 500 ms), then stays black.
        svc.toggleBlack(true);
        QTRY_VERIFY_WITH_TIMEOUT(out->composer().blackOpacity(out->composer().now()) > 0.99, 3000);
        img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QCOMPARE(img.pixel(4, 4), qRgb(0, 0, 0));
        svc.toggleBlack(false);
        QTRY_VERIFY_WITH_TIMEOUT(!out->composer().blackVisible(), 3000);
        svc.toggleLogo(true);
        QTRY_VERIFY_WITH_TIMEOUT(out->composer().logoOpacity(out->composer().now()) > 0.99, 3000);
        img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QCOMPARE(img.pixel(4, 4), qRgb(0x00, 0x18, 0x33));

        // Hiding and showing again re-applies the state with no transition.
        svc.hideWindow();
        QVERIFY(!out->isShown());
        svc.showWindow();
        QVERIFY(out->isShown());
        QVERIFY(out->composer().logoOpacity(out->composer().now()) > 0.99);
        svc.hideWindow();
    }

    void dualDeckPlaybackAndCrossfade()
    {
        MpvApi& api = MpvApi::instance();
        if (!api.load(QStringLiteral(LUX_MPV_DLL)))
            QSKIP(qPrintable(QStringLiteral("libmpv not available: ") + api.lastError()));
        qInfo() << "libmpv client API version" << Qt::hex << (api.clientApiVersion ? api.clientApiVersion() : 0);

        QWidget host;
        host.setGeometry(50, 50, 320, 180);
        host.show();
        QVERIFY(QTest::qWaitForWindowExposed(&host));
        DualDeckVideo decks(&host);

        VideoCommand a;
        a.kind = VideoCommand::Kind::PlayBackground;
        a.path = QStringLiteral("av://lavfi:testsrc=size=320x180:rate=25");
        QVERIFY2(decks.execute(a), qPrintable(decks.lastError()));
        QCOMPARE(decks.activeDeck(), 0);
        QCOMPARE(decks.appliedAlpha(0), 255);
        QCOMPARE(decks.appliedAlpha(1), 0);
        QTRY_VERIFY_WITH_TIMEOUT(decks.player(0)->hasVideo(), 20000);
        QCOMPARE(decks.player(0)->property("mute"), QStringLiteral("yes"));       // backgrounds are silent
        QCOMPARE(decks.player(0)->property("loop-file"), QStringLiteral("inf"));  // and loop
        QCOMPARE(decks.player(0)->property("keepaspect"), QStringLiteral("no"));  // background video stretches to match monitor
        QCOMPARE(decks.player(0)->property("panscan").toDouble(), 0.0);
        const double p0 = decks.player(0)->position();
        QTest::qWait(600);
        QVERIFY2(decks.player(0)->position() > p0, "deck 0 is playing");

        // Crossfade onto the second deck.
        VideoCommand b = a;
        b.path = QStringLiteral("av://lavfi:testsrc2=size=320x180:rate=25");
        b.crossfadeMs = 800;
        QVERIFY2(decks.execute(b), qPrintable(decks.lastError()));
        QCOMPARE(decks.activeDeck(), 1);
        QVERIFY(decks.isFading());
        QList<int> samples;
        QElapsedTimer t;
        t.start();
        while (decks.isFading() && t.elapsed() < 5000) {
            samples << decks.appliedAlpha(1);
            QCOMPARE(decks.appliedAlpha(0), 255);              // outgoing deck stays opaque underneath
            QTest::qWait(25);
        }
        QVERIFY(!decks.isFading());
        QVERIFY2(samples.size() >= 8, qPrintable(QString::number(samples.size())));
        bool intermediate = false;
        for (int i = 0; i < samples.size(); ++i) {
            if (i > 0)
                QVERIFY2(samples[i] >= samples[i - 1], "alpha ramps up monotonically");
            intermediate |= samples[i] > 0 && samples[i] < 255;
        }
        QVERIFY2(intermediate, "intermediate alpha values were applied to the window");
        QVERIFY(samples.first() < 128);
        QCOMPARE(decks.appliedAlpha(1), 255);
        QCOMPARE(decks.appliedAlpha(0), 0);
        QTRY_VERIFY_WITH_TIMEOUT(decks.player(1)->hasVideo(), 20000);
        QTRY_COMPARE_WITH_TIMEOUT(decks.player(0)->property("idle-active"), QStringLiteral("yes"), 5000); // outgoing deck stopped

        // Media role: sound on, aspect kept; reuses the idle deck (0) for a cut.
        VideoCommand m;
        m.kind = VideoCommand::Kind::PlayMedia;
        m.path = QStringLiteral("av://lavfi:testsrc=size=320x180:rate=25");
        m.looping = false;
        QVERIFY2(decks.execute(m), qPrintable(decks.lastError()));
        QCOMPARE(decks.activeDeck(), 0);
        QCOMPARE(decks.player(0)->property("keepaspect"), QStringLiteral("yes"));
        QCOMPARE(decks.player(0)->property("panscan").toDouble(), 0.0);           // media: whole picture visible
        QCOMPARE(decks.player(0)->property("mute"), QStringLiteral("no"));
        QCOMPARE(decks.player(0)->property("loop-file"), QStringLiteral("no"));
        decks.tickNow();
        QCOMPARE(decks.appliedAlpha(0), 255);
        QCOMPARE(decks.appliedAlpha(1), 0);

        VideoCommand pause;
        pause.kind = VideoCommand::Kind::Pause;
        QVERIFY(decks.execute(pause));
        QTRY_COMPARE_WITH_TIMEOUT(decks.player(0)->property("pause"), QStringLiteral("yes"), 3000);
        pause.kind = VideoCommand::Kind::Resume;
        QVERIFY(decks.execute(pause));
        QTRY_COMPARE_WITH_TIMEOUT(decks.player(0)->property("pause"), QStringLiteral("no"), 3000);

        VideoCommand stop;
        stop.kind = VideoCommand::Kind::Stop;
        QVERIFY(decks.execute(stop));
        QCOMPARE(decks.activeDeck(), -1);
        QCOMPARE(decks.appliedAlpha(0) + decks.appliedAlpha(1), 0);
        QTRY_COMPARE_WITH_TIMEOUT(decks.player(0)->property("idle-active"), QStringLiteral("yes"), 5000);
    }

    // ---- Milestone 5: presentation page on the real projector windows ---------------------------
    void pdfPageIsShownFittedOnTheRealProjectorWindow()
    {
        QTemporaryDir tmp;
        QVERIFY(PdfiumApi::instance().load(QStringLiteral(LUX_PDFIUM_DLL)));
        const QString pdf = tmp.filePath(QStringLiteral("deck.pdf"));
        {
            QPdfWriter w(pdf);
            w.setResolution(72);
            QPainter p;
            const QList<QPair<QSizeF, QColor>> pages{{QSizeF(960, 540), QColor(0, 0, 255)}, {QSizeF(595, 842), QColor(0, 128, 0)}};
            for (int i = 0; i < pages.size(); ++i) {
                w.setPageSize(QPageSize(pages[i].first, QPageSize::Point));
                w.setPageMargins(QMarginsF(0, 0, 0, 0));
                if (i == 0) p.begin(&w); else w.newPage();
                p.fillRect(QRectF(-20, -20, pages[i].first.width() + 40, pages[i].first.height() + 40), pages[i].second);
            }
            p.end();
        }
        const QRect rect(100, 100, 800, 500);                          // 16:10 "monitor"
        ProjectionService svc;
        svc.setOutputFactory([&]() -> std::unique_ptr<IProjectionOutput> {
            return std::make_unique<ProjectionOutput>([&] { return QList<ScreenInfo>{fakeScreen(0, rect, false)}; });
        });
        svc.setLivePresentation(pdf, 0);
        svc.showWindow();
        auto* out = dynamic_cast<ProjectionOutput*>(svc.output());
        QVERIFY(out && out->isShown());
        QTest::qWait(150);
        QImage img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QCOMPARE(img.size(), rect.size());
        // 16:9 page in a 16:10 window: 800x450, black bars of 25 px
        QCOMPARE(img.pixel(400, 250), qRgb(0, 0, 255));
        QCOMPARE(img.pixel(2, 30), qRgb(0, 0, 255));
        QCOMPARE(img.pixel(400, 10), qRgb(0, 0, 0));
        QCOMPARE(img.pixel(400, 490), qRgb(0, 0, 0));
        svc.navigatePresentation(1);                                    // A4 portrait: pillarboxed
        QTest::qWait(150);
        img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QCOMPARE(img.pixel(400, 250), qRgb(0, 128, 0));
        QCOMPARE(img.pixel(100, 250), qRgb(0, 0, 0));
        QCOMPARE(img.pixel(700, 250), qRgb(0, 0, 0));
        svc.toggleBlack(true);
        QTRY_VERIFY_WITH_TIMEOUT(out->composer().blackOpacity(out->composer().now()) > 0.99, 3000);
        img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
        QCOMPARE(img.pixel(400, 250), qRgb(0, 0, 0));
        svc.hideWindow();
        PageImageCache::instance().clear();
    }

    // ---- Milestone 5: WebView2 online browser --------------------------------------------------
    void webView2BrowserRunsPrivatelyAndNavigates()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        QTemporaryDir base;
        AppPaths::setBaseDirectoryOverride(base.path());
        const QString stale = QDir(AppPaths::dataDirectory()).filePath(QStringLiteral("WebView2Data/stale.txt"));
        QDir().mkpath(QFileInfo(stale).absolutePath());
        QFile(stale).open(QIODevice::WriteOnly);

        QWidget top;
        top.setGeometry(80, 80, 900, 600);
        auto* lay = new QVBoxLayout(&top);
        lay->setContentsMargins(0, 0, 0, 0);
        auto* host = new WebView2Host(&top);
        lay->addWidget(host);
        QSignalSpy unavailable(host, &WebView2Host::unavailable);
        top.show();
        QVERIFY(QTest::qWaitForWindowExposed(&top));
        host->initialize();
        QTRY_VERIFY_WITH_TIMEOUT(host->isReady() || host->state() == WebView2Host::State::Unavailable, 60000);
        if (!host->isReady())
            QSKIP(qPrintable(QStringLiteral("WebView2 Runtime not available here: ") + host->unavailableReason()));

        QVERIFY2(!host->isPrivate(), "the session uses a persistent profile for extensions");
        QVERIFY(host->userDataFolder().startsWith(QDir::cleanPath(AppPaths::dataDirectory())));

        auto title = [&] { return pageTitle(host); };
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Online Browser\""), 15000);      // the home page
        QVERIFY(host->currentUrl().isEmpty());

        QFile a(base.filePath(QStringLiteral("a.html"))), b(base.filePath(QStringLiteral("b.html")));
        QVERIFY(a.open(QIODevice::WriteOnly) && b.open(QIODevice::WriteOnly));
        a.write("<html><head><title>Page A</title></head><body>A</body></html>");
        b.write("<html><head><title>Page B</title></head><body>B</body></html>");
        a.close();
        b.close();
        QSignalSpy loading(host, &WebView2Host::loadingChanged);
        host->loadUrl(QUrl::fromLocalFile(a.fileName()).toString());
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Page A\""), 15000);
        QVERIFY(loading.size() >= 2 && loading.first().first().toBool());
        QVERIFY(host->currentUrl().endsWith(QStringLiteral("a.html")));
        host->loadUrl(QUrl::fromLocalFile(b.fileName()).toString());
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Page B\""), 15000);
        QTRY_VERIFY_WITH_TIMEOUT(host->canGoBack(), 5000);
        QVERIFY(!host->canGoForward());
        host->goBack();
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Page A\""), 15000);
        QTRY_VERIFY_WITH_TIMEOUT(host->canGoForward(), 5000);
        host->goForward();
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Page B\""), 15000);
        host->reload();
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Page B\""), 15000);
        host->goHome();
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Online Browser\""), 15000);

        // the control follows its parent: resizing keeps working and nothing throws
        top.resize(700, 420);
        QTest::qWait(100);
        QCOMPARE(title(), QStringLiteral("\"Online Browser\""));
        AppPaths::setBaseDirectoryOverride(QString());
    }

    // ---- Milestone 6: uBlock Origin browser extension on the real bundled WebView2 runtime ---------
    void adBlockerFiltersRealRequestsAndKeepsBrowsingWorking()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        PageServer srv;
        QWidget top;
        top.setGeometry(80, 80, 900, 600);
        auto* lay = new QVBoxLayout(&top);
        lay->setContentsMargins(0, 0, 0, 0);
        auto* host = new WebView2Host(&top);
        lay->addWidget(host);
        top.show();
        QVERIFY(QTest::qWaitForWindowExposed(&top));
        host->initialize();
        QTRY_VERIFY_WITH_TIMEOUT(host->isReady() || host->state() == WebView2Host::State::Unavailable, 60000);
        if (!host->isReady())
            QSKIP(qPrintable(QStringLiteral("WebView2 Runtime not available here: ") + host->unavailableReason()));
        auto title = [&] { return pageTitle(host); };
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Online Browser\""), 15000);
        QVERIFY(host->isExtensionEnabled());

        // 1. blocker on: the page loads, its ordinary script runs, the ad requests never reach the server, no pop-up
        QTest::qWait(2500); // Allow uBO background page to compile filter assets
        host->loadUrl(srv.url(QStringLiteral("/page.html")));
        QTRY_VERIFY_WITH_TIMEOUT(srv.count(QStringLiteral("/lib/app.js")) >= 1, 15000);
        QTest::qWait(1500);   // the page tries to open a pop-up after 300 ms
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Ad test page\""), 15000);
        QTRY_COMPARE_WITH_TIMEOUT(runJs(host, QStringLiteral("window.appLoaded === 1")), QStringLiteral("true"), 10000);
        QCOMPARE(srv.count(QStringLiteral("/adblock/frontend_loader.js")), 0);
        QCOMPARE(srv.count(QStringLiteral("/88/tag.min.js")), 0);
        QCOMPARE(srv.count(QStringLiteral("/?view=ad")), 0);
        QCOMPARE(srv.count(QStringLiteral("/popup.html")), 0);
        QCOMPARE(runJs(host, QStringLiteral("window.adLoaded === 1")), QStringLiteral("false"));
        QCOMPARE(title(), QStringLiteral("\"Ad test page\""));   // still on the page: the pop-up did not take over the view

        // 2. blocker off: toggle extension off, the same page loads everything
        host->setExtensionEnabled(false);
        QVERIFY(!host->isExtensionEnabled());
        QTest::qWait(1500);
        srv.hits.clear();
        host->loadUrl(srv.url(QStringLiteral("/page.html?nopopup=1")));
        QTRY_COMPARE_WITH_TIMEOUT(runJs(host, QStringLiteral("window.adLoaded === 1")), QStringLiteral("true"), 15000);
        QVERIFY(srv.count(QStringLiteral("/adblock/frontend_loader.js")) >= 1);

        // 3. blocker re-enabled: blocked again; navigation (back/forward/reload) keeps working
        host->setExtensionEnabled(true);
        QVERIFY(host->isExtensionEnabled());
        QTest::qWait(1500);
        srv.hits.clear();
        host->loadUrl(srv.url(QStringLiteral("/page.html?nopopup=3")));
        QTRY_COMPARE_WITH_TIMEOUT(runJs(host, QStringLiteral("window.appLoaded === 1 && window.adLoaded !== 1")), QStringLiteral("true"), 15000);
        QCOMPARE(srv.count(QStringLiteral("/adblock/frontend_loader.js")), 0);
        host->goBack();
        QTRY_COMPARE_WITH_TIMEOUT(runJs(host, QStringLiteral("location.search")), QStringLiteral("\"?nopopup=1\""), 15000);
        host->reload();
        QTRY_COMPARE_WITH_TIMEOUT(title(), QStringLiteral("\"Ad test page\""), 15000);
    }
    void youTubeStillLoadsWithTheRealFilterLists()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        QTemporaryDir base;
        AppPaths::setBaseDirectoryOverride(base.path());
        const QString adDir = QFile::exists(QStringLiteral(LUX_DATA_SRC "/adblock/easylist.txt"))
                                  ? QStringLiteral(LUX_DATA_SRC "/adblock")
                                  : QStringLiteral(LUX_FIXTURES "/adblock");
        AdBlockService ads(base.filePath(QStringLiteral("user")), adDir);
        ads.reload();
        QVERIFY(ads.ruleCount() >= 0);
        QWidget top;
        top.setGeometry(80, 80, 1000, 700);
        auto* lay = new QVBoxLayout(&top);
        lay->setContentsMargins(0, 0, 0, 0);
        auto* host = new WebView2Host(&top);
        host->setAdBlock(&ads);
        lay->addWidget(host);
        top.show();
        QVERIFY(QTest::qWaitForWindowExposed(&top));
        host->initialize();
        QTRY_VERIFY_WITH_TIMEOUT(host->isReady() || host->state() == WebView2Host::State::Unavailable, 60000);
        if (!host->isReady())
            QSKIP("WebView2 Runtime not available here");
        host->loadUrl(QStringLiteral("https://www.youtube.com/watch?v=jNQXAC9IVRw"));
        // needs the internet; skip (not fail) when it is not reachable
        QElapsedTimer t;
        t.start();
        bool loaded = false;
        while (t.elapsed() < 40000 && !loaded) {
            QTest::qWait(500);
            loaded = runJs(host, QStringLiteral("!!document.querySelector('video')")) == QLatin1String("true");
        }
        if (!loaded)
            QSKIP("YouTube could not be reached from this machine (or showed a consent page instead of the player)");
        // start it muted and see time advance
        runJs(host, QStringLiteral("(function(){var v=document.querySelector('video'); v.muted=true; v.play(); return 1;})()"));
        bool playing = false;
        t.restart();
        while (t.elapsed() < 25000 && !playing) {
            QTest::qWait(500);
            playing = runJs(host, QStringLiteral("(function(){var v=document.querySelector('video'); return v.currentTime>0.5 && v.readyState>=3;})()")) == QLatin1String("true");
        }
        qInfo() << "YouTube video playing with the ad blocker on:" << playing << " blocked requests:" << ads.blockedCount();
        QVERIFY2(playing, "the video did not start playing with the filter lists active");
        AppPaths::setBaseDirectoryOverride(QString());
    }    void webView2UnavailableRuntimeIsReportedNotFatal()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        QTemporaryDir base;
        AppPaths::setBaseDirectoryOverride(base.path());
        qputenv("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", QDir::toNativeSeparators(base.filePath(QStringLiteral("no-such-runtime"))).toLocal8Bit());
        OnlineBrowserView view;
        view.resize(800, 500);
        view.show();
        QVERIFY(QTest::qWaitForWindowExposed(&view));
        QTRY_VERIFY_WITH_TIMEOUT(view.isUnavailable(), 30000);
        qunsetenv("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
        QVERIFY(view.unavailableLabel()->isVisible());
        QVERIFY(view.unavailableLabel()->text().contains(QStringLiteral("not available")));
        for (QPushButton* b : view.navigationButtons())
            QVERIFY(!b->isEnabled());
        QVERIFY(view.closeButton()->isEnabled());                       // the Close button still returns to the library
        AppPaths::setBaseDirectoryOverride(QString());
    }

    void onlineBrowserViewStartsWhenShownAndDrivesTheHost()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        QTemporaryDir base;
        AppPaths::setBaseDirectoryOverride(base.path());
        OnlineBrowserView view;
        view.resize(900, 600);
        QCOMPARE(view.host()->state(), WebView2Host::State::NotStarted);   // nothing starts before the page is shown
        view.show();
        QVERIFY(QTest::qWaitForWindowExposed(&view));
        QTRY_VERIFY_WITH_TIMEOUT(view.host()->isReady() || view.isUnavailable(), 60000);
        if (view.isUnavailable())
            QSKIP("WebView2 Runtime not available here");
        QVERIFY(view.navigationButtons()[0]->isEnabled());                 // home
        QVERIFY(view.urlBox()->isEnabled());
        QVERIFY(!view.navigationButtons()[1]->isEnabled());                // back: no history yet
        QVERIFY(view.urlBox()->text().isEmpty());
        QSignalSpy closeSpy(&view, &OnlineBrowserView::closeRequested);
        view.closeButton()->click();
        QCOMPARE(closeSpy.size(), 1);
        AppPaths::setBaseDirectoryOverride(QString());
    }
    // ---- the bundled LibreOffice feeding the real projector (single and dual monitor) ----------------------------
    void convertedPresentationShowsOnlyOnTheProjectorNeverInLibreOffice()
    {
        const QString lo = QStringLiteral(LUX_BUNDLED_LO);
        const OfficeConverter::Engine engine = OfficeConverter::locate(lo, {});
        QVERIFY2(!engine.soffice.isEmpty(), qPrintable(engine.problem));
        QVERIFY(PdfiumApi::instance().load(QStringLiteral(LUX_PDFIUM_DLL)));
        QTemporaryDir base;
        AppPaths::setBaseDirectoryOverride(base.path());
        PresentationService presentations;
        presentations.setSofficeOverride(engine.soffice);
        presentations.setConversionTimeoutMs(240000);
        const QString odp = base.filePath(QStringLiteral("Sunday.odp"));
        QVERIFY(QFile::copy(QStringLiteral(LUX_FIXTURES "/source_deck.odp"), odp));
        LuxTest::WindowWatcher watcher(lo);
        QSignalSpy opened(&presentations, &PresentationService::opened);
        presentations.open(odp);
        QTRY_COMPARE_WITH_TIMEOUT(opened.size(), 1, 250000);
        const auto doc = opened[0][0].value<PresentationService::Document>();
        QCOMPARE(doc.pageCount, 3);

        for (int monitors : {1, 2}) {                               // single-monitor test mode, then a projector monitor
            QList<ScreenInfo> screens{fakeScreen(0, QRect(40, 40, 640, 400), true)};
            if (monitors == 2)
                screens << fakeScreen(1, QRect(720, 40, 800, 450), false);
            ProjectionService svc;
            svc.setOutputFactory([&]() -> std::unique_ptr<IProjectionOutput> {
                return std::make_unique<ProjectionOutput>([&] { return screens; });
            });
            svc.setLivePresentation(doc.pdfPath, 0);
            svc.showWindow();
            auto* out = dynamic_cast<ProjectionOutput*>(svc.output());
            QVERIFY(out && out->isShown());
            QCOMPARE(svc.activeScreenNumber(), monitors);           // the secondary monitor when there is one
            for (int page = 0; page < 3; ++page) {
                svc.navigatePresentation(page);
                QTest::qWait(150);
                const QImage img = out->foreground()->grab().toImage().convertToFormat(QImage::Format_ARGB32);
                const QColor expect[3] = {QColor(0, 0, 255), QColor(0, 128, 0), QColor(200, 0, 0)};
                const QRect r = out->currentScreen().physicalWidth == 640 ? QRect(0, 0, 640, 400) : QRect(0, 0, 800, 450);
                QCOMPARE(img.size(), r.size());
                const QRgb c = img.pixel(img.width() / 2, img.height() - 30);   // inside the page, below the caption (16:10 monitors letterbox the 16:9 page)
                QVERIFY2(qAbs(qRed(c) - expect[page].red()) < 16 && qAbs(qGreen(c) - expect[page].green()) < 16 && qAbs(qBlue(c) - expect[page].blue()) < 16,
                         qPrintable(QStringLiteral("monitors=%1 page %2").arg(monitors).arg(page + 1)));
            }
            svc.hideWindow();
        }
        QTRY_VERIFY_WITH_TIMEOUT(LuxTest::processesUnder(lo).isEmpty(), 15000);
        QVERIFY2(watcher.seen().isEmpty(), qPrintable(QStringLiteral("LibreOffice showed a window: ") + watcher.seen().join(QStringLiteral("; "))));
        QVERIFY(watcher.pollsWithProcess() > 0);
        PageImageCache::instance().clear();
        AppPaths::setBaseDirectoryOverride(QString());
    }

    // ---- WebView2 runtime deployment ---------------------------------------------------------------------------------
    void runtimeCandidatesFollowTheDeploymentOrder()
    {
        QTemporaryDir t;
        auto make = [&](const QString& rel) {
            const QString p = t.filePath(rel);
            QDir().mkpath(QFileInfo(p).absolutePath());
            QFile f(p);
            f.open(QIODevice::WriteOnly);
            f.write("x");
        };
        // bundled fixed runtime (two versions: the newest wins), a registered runtime, and an unregistered one in Program Files
        make(QStringLiteral("app/Engines/WebView2/150.0.1/msedgewebview2.exe"));
        make(QStringLiteral("app/Engines/WebView2/157.0.4341.60/msedgewebview2.exe"));
        make(QStringLiteral("app/WebView2/msedgewebview2.exe"));                      // reference layout
        make(QStringLiteral("pf86/Microsoft/EdgeWebView/Application/99.0.1/msedgewebview2.exe"));
        make(QStringLiteral("pf86/Microsoft/EdgeWebView/Application/120.0.2/msedgewebview2.exe"));
        const QByteArray oldPf86 = qgetenv("ProgramFiles(x86)"), oldPf = qgetenv("ProgramFiles"), oldPf64 = qgetenv("ProgramW6432");
        qputenv("ProgramFiles(x86)", t.filePath(QStringLiteral("pf86")).toLocal8Bit());
        qputenv("ProgramFiles", t.filePath(QStringLiteral("nothing")).toLocal8Bit());
        qputenv("ProgramW6432", t.filePath(QStringLiteral("nothing")).toLocal8Bit());
        const QStringList c = WebView2Host::runtimeCandidates(t.filePath(QStringLiteral("app")));
        qputenv("ProgramFiles(x86)", oldPf86);
        qputenv("ProgramFiles", oldPf);
        qputenv("ProgramW6432", oldPf64);
        QCOMPARE(c.size(), 4);
        QCOMPARE(c[0], QDir::cleanPath(t.filePath(QStringLiteral("app/Engines/WebView2/157.0.4341.60"))));
        QCOMPARE(c[1], QDir::cleanPath(t.filePath(QStringLiteral("app/WebView2"))));
        QVERIFY(c[2].isEmpty());                                                       // Evergreen via the loader
        QCOMPARE(c[3], QDir::cleanPath(t.filePath(QStringLiteral("pf86/Microsoft/EdgeWebView/Application/120.0.2"))));   // reference's Program Files probe
        // an explicit override is the only candidate
        qputenv("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", "C:/x");
        QCOMPARE(WebView2Host::runtimeCandidates(t.filePath(QStringLiteral("app"))), QStringList{QString()});
        qunsetenv("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
        // nothing bundled: Evergreen first
        QVERIFY(WebView2Host::runtimeCandidates(t.filePath(QStringLiteral("empty"))).first().isEmpty());
    }

    void bundledFixedRuntimeRunsTheBrowserAndBadCandidatesFallBack()
    {
        if (!WebView2Host::builtWithSdk())
            QSKIP("built without the WebView2 SDK");
        const QString portableApp = QDir::cleanPath(QDir(QStringLiteral(LUX_BUNDLED_WV2)).absoluteFilePath(QStringLiteral("..")) + QStringLiteral("/.."));
        QVERIFY2(!WebView2Host::newestRuntimeIn(QStringLiteral(LUX_BUNDLED_WV2)).isEmpty(), "the Portable build bundles the Fixed Version runtime");
        for (int scenario = 0; scenario < 2; ++scenario) {
            QTemporaryDir base;
            AppPaths::setBaseDirectoryOverride(base.path());
            QString appDir = portableApp;
            if (scenario == 1) {
                // a damaged "fixed runtime" (executable present, nothing else) in front of the real one: skipped, not fatal
                appDir = base.filePath(QStringLiteral("app"));
                QDir().mkpath(appDir + QStringLiteral("/Engines/WebView2/1.0.0"));
                QFile bad(appDir + QStringLiteral("/Engines/WebView2/1.0.0/msedgewebview2.exe"));
                bad.open(QIODevice::WriteOnly);
                bad.write("not an executable");
                bad.close();
            }
            WebView2Host::setApplicationDirectoryOverride(appDir);
            QWidget top;
            top.setGeometry(80, 80, 700, 450);
            auto* lay = new QVBoxLayout(&top);
            auto* host = new WebView2Host(&top);
            lay->addWidget(host);
            top.show();
            QVERIFY(QTest::qWaitForWindowExposed(&top));
            host->initialize();
            QTRY_VERIFY_WITH_TIMEOUT(host->isReady() || host->state() == WebView2Host::State::Unavailable, 90000);
            QVERIFY2(host->isReady(), qPrintable(host->unavailableReason()));
            QVERIFY(!host->isPrivate());
            if (scenario == 0) {
                // the self-contained package: the browser engine is the bundled one
                QVERIFY2(QDir::cleanPath(host->runtimeSource()).startsWith(QDir::cleanPath(QStringLiteral(LUX_BUNDLED_WV2))), qPrintable(host->runtimeSource()));
                QCOMPARE(host->browserVersion(), QStringLiteral("157.0.4341.60"));
            } else {
                QVERIFY2(!host->runtimeSource().contains(QStringLiteral("1.0.0")), qPrintable(host->runtimeSource()));
            }
            QTRY_COMPARE_WITH_TIMEOUT(pageTitle(host), QStringLiteral("\"Online Browser\""), 20000);
            WebView2Host::setApplicationDirectoryOverride(QString());
            AppPaths::setBaseDirectoryOverride(QString());
        }
    }
    void missingEngineIsHandled()
    {
        // A bogus DLL path must fail cleanly (the app then runs without video).
        MpvApi bogus;
        QVERIFY(!bogus.load(QStringLiteral("C:/no/such/libmpv-2.dll")));
        QVERIFY(!bogus.lastError().isEmpty());
    }
};

QTEST_MAIN(WindowTests)
#include "test_windows.moc"
