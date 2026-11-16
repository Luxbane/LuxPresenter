// Milestone 6 tests: options, backgrounds, Bible, ad blocker (offscreen; the real-window checks are in luxwindows_tests).
#include "FakeOutput.h"
#include "Services/AppPaths.h"
#include "Services/BackgroundThumbnails.h"
#include "Services/BackgroundUtil.h"
#include "Services/AdBlockEngine.h"
#include "Services/AdBlockService.h"
#include "Services/BibleLibrary.h"
#include "Views/Components/OnlineBrowserView.h"
#include <QAction>
#include <QElapsedTimer>
#include <QMenu>
#include <QTcpServer>
#include <QTcpSocket>
#include "Views/Components/ScripturesTab.h"
#include "Views/Components/TabStrip.h"
#include <QCryptographicHash>
#include <QPushButton>
#include <QSignalSpy>
#include "Services/AdBlockEngine.h"
#include "Services/AdBlockService.h"
#include "Services/BibleLibrary.h"
#include "Views/Components/OnlineBrowserView.h"
#include <QAction>
#include <QElapsedTimer>
#include <QMenu>
#include <QTcpServer>
#include <QTcpSocket>
#include "Views/Components/ScripturesTab.h"
#include "Views/Components/TabStrip.h"
#include <QCryptographicHash>
#include <QPushButton>
#include <QListWidget>
#include <QSignalSpy>
#include "Services/MpvApi.h"
#include "Views/BackgroundDialog.h"
#include "Views/Components/ScheduleView.h"
#include "Services/DataService.h"
#include "Services/ProjectionComposer.h"
#include "Services/ProjectionService.h"
#include "Services/ThemeService.h"
#include "Views/Components/SlideCanvas.h"
#include "Views/Components/SlidePaneView.h"
#include "Views/MainWindow.h"
#include "Views/OptionsDialog.h"
#include "Views/Projection/ProjectionWindow.h"
#include <QCheckBox>
#include <QComboBox>
#include <QDir>
#include <QFile>
#include <QImage>
#include <QListWidget>
#include <QPainter>
#include <QPlainTextEdit>
#include <QRadioButton>
#include <QSpinBox>
#include <QTemporaryDir>
#include <QtTest>
using namespace Lux;
namespace {
struct Env
{
    explicit Env(bool withBible = false)
    {
        QDir().mkpath(QStringLiteral(LUX_TEST_TMP));
        dir = std::make_unique<QTemporaryDir>(QStringLiteral(LUX_TEST_TMP "/m6-XXXXXX"));
        AppPaths::setBaseDirectoryOverride(dir->path());
        if (withBible) {
            QDir().mkpath(dir->filePath(QStringLiteral("Data")));
            QFile::copy(QStringLiteral(LUX_DATA_SRC "/bibles.json"), dir->filePath(QStringLiteral("Data/bibles.json")));
        }
        data = std::make_unique<DataService>();
        ThemeService::instance().apply(false);
    }
    ~Env()
    {
        window.reset();
        data.reset();
        AppPaths::setBaseDirectoryOverride(QString());
    }
    MainWindow& mainWindow()
    {
        if (!window) {
            window = std::make_unique<MainWindow>(*data);
            window->setDialogsSuppressed(true);
            window->projection()->setOutputFactory(LuxTest::fakeFactory());
        }
        return *window;
    }
    std::unique_ptr<QTemporaryDir> dir;
    std::unique_ptr<DataService> data;
    std::unique_ptr<MainWindow> window;
};
template <class T> T* child(QObject* o, const char* name)
{
    T* c = o->findChild<T*>(QString::fromLatin1(name));
    if (!c)
        qWarning("missing widget %s", name);
    return c;
}
void selectText(QComboBox* c, const QString& text)
{
    const int i = c->findText(text);
    QVERIFY2(i >= 0, qPrintable(text));
    c->setCurrentIndex(i);
}
// Bounding box of everything that is not the plain background colour.
QRect inkBox(const QImage& img, QRgb bg)
{
    int x0 = img.width(), y0 = img.height(), x1 = -1, y1 = -1;
    for (int y = 0; y < img.height(); ++y)
        for (int x = 0; x < img.width(); ++x)
            if (img.pixel(x, y) != bg) {
                x0 = qMin(x0, x); x1 = qMax(x1, x); y0 = qMin(y0, y); y1 = qMax(y1, y);
            }
    return x1 < 0 ? QRect() : QRect(QPoint(x0, y0), QPoint(x1, y1));
}
QImage renderScene(const ProjectionState& st, QRect* ink = nullptr, const QString& background = QStringLiteral("#FF0000"))
{
    ProjectionComposer c;
    c.setClock([] { return qint64(100000); });
    ProjectionWindow win(&c);
    ProjectionState s = st;
    s.activeBackground = background;
    c.applyState(s);
    QImage img(1920, 1080, QImage::Format_ARGB32_Premultiplied);
    img.fill(Qt::transparent);
    QPainter p(&img);
    win.paintScene(&p, QRectF(0, 0, 1920, 1080), 100000);
    p.end();
    if (ink)
        *ink = inkBox(img, qRgb(255, 0, 0));
    return img;
}
// Minimal HTTP server for the filter-list update tests: path -> (status, body).
class TinyServer : public QObject
{
public:
    TinyServer()
    {
        m_server.listen(QHostAddress::LocalHost, 0);
        connect(&m_server, &QTcpServer::newConnection, this, [this] {
            while (QTcpSocket* s = m_server.nextPendingConnection()) {
                connect(s, &QTcpSocket::readyRead, this, [this, s] {
                    const QByteArray req = s->readAll();
                    const QString path = QString::fromLatin1(req.split(' ').value(1));
                    ++requests[path];
                    const auto r = routes.value(path, qMakePair(404, QByteArray("not found")));
                    s->write("HTTP/1.1 " + QByteArray::number(r.first) + " X\r\nContent-Length: " + QByteArray::number(r.second.size()) +
                             "\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\n" + r.second);
                    s->disconnectFromHost();
                });
            }
        });
    }
    QString url(const QString& path) const { return QStringLiteral("http://127.0.0.1:%1%2").arg(m_server.serverPort()).arg(path); }
    QHash<QString, QPair<int, QByteArray>> routes;
    QHash<QString, int> requests;
private:
    QTcpServer m_server;
};
QByteArray syntheticList(const QString& title, int rules, const QString& domainPrefix = QStringLiteral("ad"))
{
    QByteArray b = "[Adblock Plus 2.0]\n! Title: " + title.toUtf8() + "\n! Last modified: 01 Jan 2030 00:00 UTC\n";
    for (int i = 0; i < rules; ++i)
        b += "||" + domainPrefix.toUtf8() + QByteArray::number(i) + ".example.test^$third-party\n";
    return b;
}Slide mkSlide(const QString& label, const QString& text)
{
    Slide s;
    s.label = label;
    s.content = text;
    s.type = SlideType::Verse;
    return s;
}
} // namespace
class M6Tests : public QObject
{
    Q_OBJECT
private slots:
    // ---------------------------------------------------------------- Options
    void optionsDialogHasNoAdvancedAndEditsEveryField()
    {
        AppSettings in;
        QList<OptionsDialog::Monitor> mons{{QStringLiteral("\\\\.\\DISPLAY1"), QStringLiteral("1: Primary")},
                                           {QStringLiteral("\\\\.\\DISPLAY2"), QStringLiteral("2: Secondary")}};
        OptionsDialog d(in, mons);
        QCOMPARE(d.categories(), (QStringList{"General", "Songs", "Online"}));   // no Advanced page
        child<QRadioButton>(&d, "optThemeDark")->setChecked(true);
        selectText(child<QComboBox>(&d, "optOutputDisplay"), QStringLiteral("2: Secondary"));
        child<QSpinBox>(&d, "optMarginTop")->setValue(3);
        child<QSpinBox>(&d, "optMarginBottom")->setValue(4);
        child<QSpinBox>(&d, "optMarginLeft")->setValue(5);
        child<QSpinBox>(&d, "optMarginRight")->setValue(-6);
        selectText(child<QComboBox>(&d, "optTextAlign"), QStringLiteral("Left"));
        selectText(child<QComboBox>(&d, "optVertAlign"), QStringLiteral("Bottom"));
        selectText(child<QComboBox>(&d, "optFontFamily"), QStringLiteral("Verdana"));
        selectText(child<QComboBox>(&d, "optFontSize"), QStringLiteral("20"));
        child<QCheckBox>(&d, "optBold")->setChecked(false);
        child<QCheckBox>(&d, "optShowLabels")->setChecked(true);
        selectText(child<QComboBox>(&d, "optLabelLocation"), QStringLiteral("Bottom Left"));
        selectText(child<QComboBox>(&d, "optLabelSize"), QStringLiteral("Large"));
        child<QCheckBox>(&d, "optIndentLabel")->setChecked(true);
        child<QCheckBox>(&d, "optAutoWrap")->setChecked(false);
        child<QCheckBox>(&d, "optCapAll")->setChecked(false);
        child<QCheckBox>(&d, "optCapFirst")->setChecked(true);
        child<QCheckBox>(&d, "optCapKeywords")->setChecked(true);
        child<QPlainTextEdit>(&d, "optKeywords")->setPlainText(QStringLiteral("LORD\nJESUS"));
        selectText(child<QComboBox>(&d, "optSongTransition"), QStringLiteral("Cut"));
        selectText(child<QComboBox>(&d, "optSlideTransition"), QStringLiteral("Cut"));
        selectText(child<QComboBox>(&d, "optScreenTransition"), QStringLiteral("Cut"));
        selectText(child<QComboBox>(&d, "optDuration"), QStringLiteral("Slow (1 s)"));
        child<QCheckBox>(&d, "optAdBlock")->setChecked(false);
        child<QPlainTextEdit>(&d, "optWhitelist")->setPlainText(QStringLiteral("Example.com\nsub.test.org"));
        const AppSettings s = d.settings();
        QVERIFY(s.isDarkMode);
        QCOMPARE(s.outputDisplay, QStringLiteral("\\\\.\\DISPLAY2"));
        QCOMPARE(s.marginTop, 3);
        QCOMPARE(s.marginBottom, 4);
        QCOMPARE(s.marginLeft, 5);
        QCOMPARE(s.marginRight, -6);
        QCOMPARE(s.songTextAlignment, QStringLiteral("Left"));
        QCOMPARE(s.songVerticalAlignment, QStringLiteral("Bottom"));
        QCOMPARE(s.songFontFamily, QStringLiteral("Verdana"));
        QCOMPARE(s.songFontSize, 20);
        QVERIFY(!s.songFontBold);
        QVERIFY(s.showVerseLabels);
        QCOMPARE(s.labelLocation, QStringLiteral("Bottom Left"));
        QCOMPARE(s.labelSize, QStringLiteral("Large"));
        QVERIFY(s.indentLabel);
        QVERIFY(!s.autoWrapWords);
        QVERIFY(!s.capitalizeAllWords);
        QVERIFY(s.capitalizeFirstCharEachLine);
        QVERIFY(s.autoCapitalizeKeywords);
        QCOMPARE(s.capitalizeKeywords.split(QRegularExpression("[\\r\\n]+"), Qt::SkipEmptyParts), (QStringList{"LORD", "JESUS"}));
        QCOMPARE(s.songTransition, QStringLiteral("Cut"));
        QCOMPARE(s.slideTransition, QStringLiteral("Cut"));
        QCOMPARE(s.screenTransition, QStringLiteral("Cut"));
        QCOMPARE(s.transitionDurationMs, 1000);
        QVERIFY(!s.adBlockEnabled);
        QCOMPARE(s.adBlockWhitelist, (QStringList{"example.com", "sub.test.org"}));
    }
    void optionsDialogShowsCurrentValuesAndDisablesIrrelevantControls()
    {
        AppSettings in;
        in.isDarkMode = true;
        in.labelLocation = QStringLiteral("Top Center");
        in.showVerseLabels = true;
        in.outputDisplay = QStringLiteral("\\\\.\\DISPLAY9");   // monitor that is not connected right now
        OptionsDialog d(in, {{QStringLiteral("\\\\.\\DISPLAY1"), QStringLiteral("1: Primary")}});
        QVERIFY(child<QRadioButton>(&d, "optThemeDark")->isChecked());
        QVERIFY(child<QComboBox>(&d, "optLabelLocation")->isEnabled());
        QVERIFY(!child<QCheckBox>(&d, "optIndentLabel")->isEnabled());   // a centred label has nothing to indent from
        selectText(child<QComboBox>(&d, "optLabelLocation"), QStringLiteral("Top Right"));
        QVERIFY(child<QCheckBox>(&d, "optIndentLabel")->isEnabled());
        child<QCheckBox>(&d, "optShowLabels")->setChecked(false);
        QVERIFY(!child<QComboBox>(&d, "optLabelLocation")->isEnabled());
        QVERIFY(!child<QCheckBox>(&d, "optIndentLabel")->isEnabled());
        // the saved but unplugged monitor stays selected, so cancelling/accepting does not forget it
        QCOMPARE(d.settings().outputDisplay, QStringLiteral("\\\\.\\DISPLAY9"));
    }
    void settingsAreSanitisedAndCorruptFilesTolerated()
    {
        AppSettings s;
        s.songFontSize = 5000;
        s.marginTop = 999;
        s.songTextAlignment = QStringLiteral("Diagonal");
        s.labelLocation = QStringLiteral("Nowhere");
        s.transitionDurationMs = 123;
        s.adBlockWhitelist = {QStringLiteral(" HTTPS://Example.com/path "), QStringLiteral("example.com"), QString()};
        s.sanitize();
        QVERIFY(s.songFontSize <= 96 && s.songFontSize >= 6);
        QVERIFY(s.marginTop <= 40);
        QCOMPARE(s.songTextAlignment, QStringLiteral("Center"));
        QCOMPARE(s.labelLocation, QStringLiteral("Top Center"));
        QCOMPARE(s.transitionDurationMs, 250);   // nearest offered value
        QCOMPARE(s.adBlockWhitelist, (QStringList{"example.com"}));
        // a damaged settings.json falls back to defaults
        QTemporaryDir tmp(QStringLiteral(LUX_TEST_TMP "/m6c-XXXXXX"));
        AppPaths::setBaseDirectoryOverride(tmp.path());
        QDir().mkpath(AppPaths::dataDirectory());
        QFile f(QDir(AppPaths::dataDirectory()).filePath(QStringLiteral("settings.json")));
        QVERIFY(f.open(QIODevice::WriteOnly));
        f.write("{ not json at all");
        f.close();
        DataService ds;
        QCOMPARE(ds.settings().songTextAlignment, QStringLiteral("Center"));
        QVERIFY(ds.settings().adBlockEnabled);
        AppPaths::setBaseDirectoryOverride(QString());
    }
    void applySettingsSavesAppliesAndSurvivesRestart()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        AppSettings s = env.data->settings();
        s.isDarkMode = true;
        s.songTextAlignment = QStringLiteral("Right");
        s.songVerticalAlignment = QStringLiteral("Bottom");
        s.songFontFamily = QStringLiteral("Verdana");
        s.songFontSize = 24;
        s.showVerseLabels = true;
        s.labelLocation = QStringLiteral("Bottom Left");
        s.labelSize = QStringLiteral("Small");
        s.indentLabel = true;
        s.autoWrapWords = false;
        s.capitalizeAllWords = false;
        s.marginTop = 7;
        s.marginLeft = 9;
        s.transitionDurationMs = 750;
        s.songTransition = QStringLiteral("Cut");
        s.slideTransition = QStringLiteral("Cut");
        s.screenTransition = QStringLiteral("Cut");
        s.adBlockEnabled = false;
        s.adBlockWhitelist = {QStringLiteral("example.com")};
        w.applySettings(s);
        // applied
        QVERIFY(ThemeService::instance().isDark());
        const ProjectionState& st = w.projection()->state();
        QCOMPARE(st.songTextAlignment, QStringLiteral("Right"));
        QCOMPARE(st.songVerticalAlignment, QStringLiteral("Bottom"));
        QCOMPARE(st.songFontFamily, QStringLiteral("Verdana"));
        QCOMPARE(st.songFontSize, 24);
        QCOMPARE(st.labelLocation, QStringLiteral("Bottom Left"));
        QVERIFY(!st.autoWrapWords);
        QCOMPARE(st.transitionDurationMs, 750);
        QCOMPARE(w.previewPane()->canvas()->hasSlide(), false);
        // saved: a second DataService on the same folder (= next start) sees everything
        DataService again;
        QCOMPARE(again.settings().toJson(), env.data->settings().toJson());
        QCOMPARE(again.settings().songFontSize, 24);
        QVERIFY(again.settings().isDarkMode);
        QVERIFY(!again.settings().adBlockEnabled);
        QCOMPARE(again.settings().adBlockWhitelist, (QStringList{"example.com"}));
        QCOMPARE(again.settings().labelLocation, QStringLiteral("Bottom Left"));
        ThemeService::instance().apply(false);
    }
    void outputDisplayChoiceIsRemembered()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        ScreenInfo a, b;
        a.index = 0; a.physicalWidth = 1600; a.physicalHeight = 1000; a.isPrimary = true; a.deviceName = QStringLiteral("\\\\.\\DISPLAY1");
        b.index = 1; b.physicalLeft = 1600; b.physicalWidth = 1920; b.physicalHeight = 1080; b.deviceName = QStringLiteral("\\\\.\\DISPLAY2");
        w.projection()->setOutputFactory(LuxTest::fakeFactory({a, b}));
        w.selectOutputDisplay(1);
        QCOMPARE(env.data->settings().outputDisplay, QStringLiteral("\\\\.\\DISPLAY2"));
        w.selectOutputDisplay(-1);
        QCOMPARE(env.data->settings().outputDisplay, QString());
        // Options picks the monitor by device name
        AppSettings s = env.data->settings();
        s.outputDisplay = QStringLiteral("\\\\.\\DISPLAY2");
        w.applySettings(s);
        QCOMPARE(w.projection()->selectedMonitorIndex(), 1);
        s.outputDisplay = QStringLiteral("\\\\.\\DISPLAY7");   // not connected: automatic, choice kept
        w.applySettings(s);
        QCOMPARE(w.projection()->selectedMonitorIndex(), -1);
        QCOMPARE(env.data->settings().outputDisplay, QStringLiteral("\\\\.\\DISPLAY7"));
    }
    void marginOptionReachesTheProjector()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        w.projection()->showWindow();
        auto* fo = dynamic_cast<LuxTest::FakeOutput*>(w.projection()->output());
        QVERIFY(fo);
        AppSettings s = env.data->settings();
        s.marginTop = 6; s.marginBottom = 7; s.marginLeft = 8; s.marginRight = 9;
        w.applySettings(s);
        QCOMPARE(fo->mTop, 6);
        QCOMPARE(fo->mBottom, 7);
        QCOMPARE(fo->mLeft, 8);
        QCOMPARE(fo->mRight, 9);
    }
    void labelLocationAndSizeAreRendered()
    {
        ProjectionState st;
        AppSettings s;
        s.showVerseLabels = true;
        s.songVerticalAlignment = QStringLiteral("Center");
        s.labelLocation = QStringLiteral("Top Left");
        st.applySettings(s);
        st.activeSlide = mkSlide(QStringLiteral("Verse 1"), QStringLiteral("Hi"));
        QRect topLeft;
        renderScene(st, &topLeft);
        // ink spans from the label (top left) down to the text (middle)
        QVERIFY2(topLeft.top() < 120, qPrintable(QString::number(topLeft.top())));
        QVERIFY(topLeft.left() < 400);
        s.labelLocation = QStringLiteral("Bottom Right");
        st.applySettings(s);
        QRect bottomRight;
        renderScene(st, &bottomRight);
        QVERIFY2(bottomRight.bottom() > 960, qPrintable(QString::number(bottomRight.bottom())));
        QVERIFY(bottomRight.right() > 1500);
        QVERIFY(bottomRight.top() > 120);   // nothing at the top now
        // size: the label of "Large" covers more pixels than "Small"
        auto inkAmount = [&](const QString& size) {
            AppSettings t = s;
            t.labelSize = size;
            t.labelLocation = QStringLiteral("Bottom Center");
            ProjectionState x = st;
            x.applySettings(t);
            x.activeSlide = mkSlide(QStringLiteral("Verse 1"), QString());
            const QImage img = renderScene(x);
            qint64 n = 0;
            for (int y = 900; y < 1080; ++y)
                for (int xx = 0; xx < 1920; ++xx)
                    n += img.pixel(xx, y) != qRgb(255, 0, 0);
            return n;
        };
        QVERIFY(inkAmount(QStringLiteral("Large")) > inkAmount(QStringLiteral("Small")));
        // labels off: nothing but the text
        s.showVerseLabels = false;
        s.labelLocation = QStringLiteral("Bottom Right");
        st.applySettings(s);
        QRect none;
        renderScene(st, &none);
        QVERIFY(none.bottom() < 960);
    }
    void indentMovesSideLabelsInwards()
    {
        ProjectionState st;
        AppSettings s;
        s.showVerseLabels = true;
        s.labelLocation = QStringLiteral("Top Left");
        st.applySettings(s);
        st.activeSlide = mkSlide(QStringLiteral("Chorus"), QString());
        QRect plain, indented;
        renderScene(st, &plain);
        s.indentLabel = true;
        st.applySettings(s);
        renderScene(st, &indented);
        QVERIFY(indented.left() >= plain.left() + 50);
    }
    void autoWrapOffKeepsLongLinesOnOneLine()
    {
        const QString longLine = QStringLiteral("Amazing grace how sweet the sound that saved a wretch like me I once was lost but now am found was blind but now I see");
        ProjectionState st;
        AppSettings s;
        s.songVerticalAlignment = QStringLiteral("Center");
        s.songFontSize = 30;
        st.applySettings(s);
        st.activeSlide = mkSlide(QStringLiteral("Verse 1"), longLine);
        QRect wrapped, single;
        renderScene(st, &wrapped);
        s.autoWrapWords = false;
        st.applySettings(s);
        renderScene(st, &single);
        QVERIFY2(single.height() < wrapped.height() / 2, qPrintable(QStringLiteral("%1 vs %2").arg(single.height()).arg(wrapped.height())));
        QVERIFY(single.width() <= 1720 + 60);
    }
    void slideTransitionOptionControlsTextFade()
    {
        ProjectionComposer c;
        qint64 now = 0;
        c.setClock([&] { return now; });
        ProjectionState st;
        AppSettings s;
        s.slideTransition = QStringLiteral("Blend");
        s.songTransition = QStringLiteral("Cut");   // background transition only
        s.transitionDurationMs = 500;
        st.applySettings(s);
        st.activeSlide = mkSlide(QStringLiteral("A"), QStringLiteral("first"));
        c.applyState(st);
        now = 1000;
        st.activeSlide = mkSlide(QStringLiteral("B"), QStringLiteral("second"));
        c.applyState(st);
        QVERIFY(c.isAnimating(now));                      // text crossfades although backgrounds are "Cut"
        s.slideTransition = QStringLiteral("Cut");
        st.applySettings(s);
        now = 5000;
        st.activeSlide = mkSlide(QStringLiteral("C"), QStringLiteral("third"));
        c.applyState(st);
        QVERIFY(!c.isAnimating(now));
    }
    void previewCanvasHonoursLabelAndWrapOptions()
    {
        SlideCanvas cv;
        cv.resize(640, 360);
        AppSettings s;
        s.showVerseLabels = false;
        cv.setSettings(s);
        cv.setSlide(mkSlide(QStringLiteral("Verse 1"), QStringLiteral("Hi")));
        QImage off = cv.grab().toImage();
        s.showVerseLabels = true;
        s.labelLocation = QStringLiteral("Bottom Right");
        cv.setSettings(s);
        QImage on = cv.grab().toImage();
        QVERIFY(off != on);
        s.labelLocation = QStringLiteral("Top Left");
        cv.setSettings(s);
        QVERIFY(cv.grab().toImage() != on);
    }
    // ------------------------------------------------------------ Backgrounds
    void backgroundUtilClassifiesFiles()
    {
        QVERIFY(Backgrounds::isImagePath(QStringLiteral("C:/x/a.JPG")));
        QVERIFY(Backgrounds::isVideoPath(QStringLiteral("C:/x/a.MP4")));
        QVERIFY(!Backgrounds::isImagePath(QStringLiteral("a.txt")));
        QVERIFY(Backgrounds::isColor(QStringLiteral("#0B132B")));
        QVERIFY(!Backgrounds::isColor(QStringLiteral("#12")));
        QVERIFY(!Backgrounds::isColor(QStringLiteral("red")));
        QCOMPARE(Backgrounds::usable(QStringLiteral("C:/definitely/missing.png")), QStringLiteral("#001833"));
        QCOMPARE(Backgrounds::usable(QStringLiteral("#223344")), QStringLiteral("#223344"));
        QVERIFY(Backgrounds::builtinColors().size() >= 6);
        BackgroundItem it;
        QString err;
        QVERIFY(!Backgrounds::itemFromFile(QStringLiteral("C:/definitely/missing.png"), &it, &err));
        QVERIFY(err.contains(QStringLiteral("does not exist")));
    }
    void backgroundDialogManagesAndPersists()
    {
        Env env;
        const QString png1 = env.dir->filePath(QStringLiteral("one.png")), png2 = env.dir->filePath(QStringLiteral("two.jpg"));
        QImage a(64, 36, QImage::Format_RGB32); a.fill(QColor(0, 200, 0)); QVERIFY(a.save(png1));
        QImage b(64, 36, QImage::Format_RGB32); b.fill(QColor(0, 0, 200)); QVERIFY(b.save(png2));
        const QString txt = env.dir->filePath(QStringLiteral("notes.txt"));
        { QFile f(txt); QVERIFY(f.open(QIODevice::WriteOnly)); f.write("hello"); }
        const QString bad = env.dir->filePath(QStringLiteral("broken.png"));
        { QFile f(bad); QVERIFY(f.open(QIODevice::WriteOnly)); f.write("not a png"); }
        BackgroundDialog d(*env.data, QStringLiteral("#001833"));
        const int builtin = d.itemCount();
        QVERIFY(builtin >= 6);
        const QStringList problems = d.addFiles({png1, png2, txt, bad, env.dir->filePath(QStringLiteral("gone.png")), png1});
        QCOMPARE(problems.size(), 4);   // txt, broken, missing, duplicate
        QVERIFY(problems.join(QLatin1Char(' ')).contains(QStringLiteral("unsupported")));
        QVERIFY(problems.join(QLatin1Char(' ')).contains(QStringLiteral("cannot be read")));
        QVERIFY(problems.join(QLatin1Char(' ')).contains(QStringLiteral("already")));
        QCOMPARE(d.itemCount(), builtin + 2);
        QVERIFY(d.addColor(QStringLiteral("#123456")));
        QCOMPARE(d.selectedValue(), QStringLiteral("#123456"));
        QVERIFY(!d.addColor(QStringLiteral("not a colour")));
        QCOMPARE(d.itemCount(), builtin + 3);
        // "next start": the lists are read back from backgrounds.json
        DataService again;
        QCOMPARE(again.backgrounds().size(), 3);
        d.selectValue(QDir::toNativeSeparators(png1));
        QVERIFY(d.removeSelected());
        QCOMPARE(d.itemCount(), builtin + 2);
        d.selectValue(QStringLiteral("#001833"));
        QVERIFY(d.removeSelected());   // built-in colours can now be removed
        QCOMPARE(d.itemCount(), builtin + 1);
        // a file that disappears is marked and falls back to the default
        QVERIFY(QFile::remove(png2));
        BackgroundDialog d2(*env.data, QStringLiteral("#001833"));
        d2.selectValue(QDir::toNativeSeparators(png2));
        QVERIFY(d2.isMissing(d2.selectedValue()));
    }
    void activeBackgroundReachesPreviewLiveAndProjectorOnlyWhenAsked()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        const QString png = env.dir->filePath(QStringLiteral("g.png"));
        QImage g(64, 36, QImage::Format_RGB32); g.fill(QColor(0, 200, 0)); QVERIFY(g.save(png));
        PresentationItem pres;
        pres.title = QStringLiteral("T");
        pres.slides << mkSlide(QStringLiteral("Verse 1"), QStringLiteral("hello"));
        w.previewPane()->loadItem(pres, w.activeGlobalBackground());
        w.goLive(0);
        QCOMPARE(w.projection()->state().activeBackground, QStringLiteral("#001833"));
        const auto liveSlide = w.projection()->state().activeSlide;
        QVERIFY(liveSlide.has_value());
        // Preview only
        w.setActiveBackground(QDir::toNativeSeparators(png), false);
        QCOMPARE(w.previewPane()->canvas()->background(), QDir::toNativeSeparators(png));
        QCOMPARE(w.livePane()->canvas()->background(), QStringLiteral("#001833"));
        QCOMPARE(w.projection()->state().activeBackground, QStringLiteral("#001833"));
        QCOMPARE(w.projection()->state().activeSlide->id, liveSlide->id);   // the live slide itself is untouched
        // Preview and Live
        w.setActiveBackground(QDir::toNativeSeparators(png), true);
        QCOMPARE(w.livePane()->canvas()->background(), QDir::toNativeSeparators(png));
        QCOMPARE(w.projection()->state().activeBackground, QDir::toNativeSeparators(png));
        QCOMPARE(w.projection()->state().activeSlide->id, liveSlide->id);
        // the projector really draws it
        QRect ink;
        const QImage frame = renderScene(w.projection()->state(), &ink, w.projection()->state().activeBackground);
        QCOMPARE(frame.pixel(5, 5), qRgb(0, 200, 0));
        // persisted and restored
        DataService again;
        QCOMPARE(again.settings().lastUsedBackground, QDir::toNativeSeparators(png));
        // a missing file is refused with the default as the fallback
        w.setActiveBackground(env.dir->filePath(QStringLiteral("nope.png")), true);
        QCOMPARE(w.projection()->state().activeBackground, QStringLiteral("#001833"));
        QVERIFY(w.statusText().contains(QStringLiteral("not available")));
        // colours
        w.setActiveBackground(QStringLiteral("#336699"), true);
        QCOMPARE(renderScene(w.projection()->state(), nullptr, QStringLiteral("#336699")).pixel(5, 5), qRgb(0x33, 0x66, 0x99));

        // Go Live without slides in Preview updates Live and Projector background
        w.previewPane()->setSlides({});
        w.setActiveBackground(QStringLiteral("#445566"), false);
        QCOMPARE(w.previewPane()->canvas()->background(), QStringLiteral("#445566"));
        QCOMPARE(w.livePane()->canvas()->background(), QStringLiteral("#336699"));
        w.goLive();
        QCOMPARE(w.livePane()->canvas()->background(), QStringLiteral("#445566"));
        QCOMPARE(w.projection()->state().activeBackground, QStringLiteral("#445566"));
    }
    void backgroundIsKeptWithScheduleItems()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        PresentationItem pres;
        pres.title = QStringLiteral("T");
        pres.slides << mkSlide(QStringLiteral("Verse 1"), QStringLiteral("hello"));
        w.setActiveBackground(QStringLiteral("#336699"), false);
        w.previewPane()->loadItem(pres, w.activeGlobalBackground());
        w.addPreviewToSchedule();
        QCOMPARE(w.scheduleService().count(), 1);
        QCOMPARE(w.scheduleService().items().at(0).presentation.backgroundPath, QStringLiteral("#336699"));
        // saved and re-opened schedule still points at it
        const QString file = env.dir->filePath(QStringLiteral("s.lps"));
        QString err;
        QVERIFY2(w.saveScheduleFile(file, &err), qPrintable(err));
        w.setActiveBackground(QStringLiteral("#000000"), false);
        QVERIFY(w.newScheduleNoPrompt());
        QVERIFY2(w.openScheduleFile(file, &err), qPrintable(err));
        QCOMPARE(w.scheduleService().items().at(0).presentation.backgroundPath, QStringLiteral("#336699"));
        w.scheduleService().userSelect(0);
        QCOMPARE(w.previewPane()->canvas()->background(), QStringLiteral("#336699"));
    }
    void videoBackgroundGetsAPosterFrame()
    {
        const QString dir = QStringLiteral(LUX_TEST_TMP "/m6video");
        QDir().mkpath(dir);
        const QString video = dir + QStringLiteral("/clip.avi");
        QFile::remove(video);
        MpvApi& api = MpvApi::instance();
        if (!api.load(QStringLiteral(LUX_MPV_DLL)))
            QSKIP("libmpv is not available");
        // make a tiny test clip with libmpv's encoder
        void* h = api.create();
        QVERIFY(h);
        const QByteArray out = video.toUtf8();
        api.setOptionString(h, "o", out.constData());
        api.setOptionString(h, "ovc", "mpeg4");
        api.setOptionString(h, "of", "avi");
        api.setOptionString(h, "aid", "no");
        api.setOptionString(h, "terminal", "no");
        api.setOptionString(h, "idle", "yes");
        api.setOptionString(h, "config", "no");
        QVERIFY(api.initialize(h) >= 0);
        const char* args[] = {"loadfile", "av://lavfi:testsrc=size=320x180:rate=25:duration=2", nullptr};
        api.command(h, args);
        for (int i = 0; i < 300; ++i) {
            QTest::qWait(50);
            char* idle = api.getPropertyString(h, "idle-active");
            const bool done = idle && QByteArray(idle) == "yes" && i > 4;
            if (idle) api.freeMem(idle);
            if (done) break;
        }
        api.terminateDestroy(h);
        if (QFileInfo(video).size() < 1000)
            QSKIP("this libmpv build cannot encode a test clip");
        auto& th = BackgroundThumbnails::instance();
        QSignalSpy spy(&th, &BackgroundThumbnails::ready);
        QImage first = th.image(video, 320);
        if (first.isNull()) {
            QVERIFY(spy.wait(30000));
            first = th.image(video, 320);
        }
        QVERIFY2(!first.isNull(), "no frame was extracted from the video");
        QVERIFY(first.width() <= 320 && first.width() > 100);
        // the Preview canvas shows the frame (not an empty colour)
        SlideCanvas cv;
        cv.resize(640, 360);
        cv.setBackground(video);
        const QImage shot = cv.grab().toImage();
        QVERIFY(shot.pixel(320, 20) != qRgb(0, 0x18, 0x33));
    }    // ------------------------------------------------------------------- Bible
    void bundledKjvIsCompleteAndVerified()
    {
        Env env(true);
        const BibleTranslation* kjv = env.data->bibles().find(QStringLiteral("KJV"));
        QVERIFY(kjv);
        QVERIFY(kjv->builtIn);
        QCOMPARE(kjv->books.size(), 66);
        QCOMPARE(kjv->totalVerses(), 31102);
        int chapters = 0;
        for (const auto& b : kjv->books) {
            QCOMPARE(b.chapters.size(), BibleLibrary::bookInfo(b.id)->chapters);   // same chapter count as the usual versification
            chapters += int(b.chapters.size());
        }
        QCOMPARE(chapters, 1189);
        QCOMPARE(kjv->verse(1, 1, 1), QStringLiteral("In the beginning God created the heaven and the earth."));
        QCOMPARE(kjv->verse(43, 3, 16), QStringLiteral("For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life."));
        QCOMPARE(kjv->verseCount(19, 119), 176);
        QCOMPARE(kjv->verse(66, 22, 21), QStringLiteral("The grace of our Lord Jesus Christ be with you all. Amen."));
        // the shipped data file is exactly the one described in licenses/KJV-NOTICE.txt
        QFile f(QStringLiteral(LUX_DATA_SRC "/bibles.json"));
        QVERIFY(f.open(QIODevice::ReadOnly));
        QCOMPARE(QString::fromLatin1(QCryptographicHash::hash(f.readAll(), QCryptographicHash::Sha256).toHex()).toUpper(),
                 QStringLiteral("5FB87869E76A6D49A2B795D5FD688DDF3707D13197AECB4EE37F49943A0E5A26"));
    }
    void referenceAndTextSearch()
    {
        Env env(true);
        const BibleTranslation& k = *env.data->bibles().find(QStringLiteral("KJV"));
        auto ref = [&](const QString& q) { return BibleLibrary::search(k, q); };
        BibleSearchResult r = ref(QStringLiteral("John 3:16"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::Reference);
        QCOMPARE(r.book, 43); QCOMPARE(r.chapter, 3); QCOMPARE(r.verse, 16);
        r = ref(QStringLiteral("1 cor 13"));
        QCOMPARE(r.book, 46); QCOMPARE(r.chapter, 13); QCOMPARE(r.verse, 0);
        r = ref(QStringLiteral("Ps 23:1-3"));
        QCOMPARE(r.book, 19); QCOMPARE(r.verse, 1); QCOMPARE(r.verseEnd, 3);
        r = ref(QStringLiteral("song of solomon 2:4"));
        QCOMPARE(r.book, 22);
        r = ref(QStringLiteral("Genesis"));
        QCOMPARE(r.book, 1); QCOMPARE(r.chapter, 1);
        r = ref(QStringLiteral("jud"));   // Jude (as in the reference program)
        QCOMPARE(r.book, 65);
        r = ref(QStringLiteral("rev 22:21"));
        QCOMPARE(r.book, 66);
        // problems are reported, not guessed
        r = ref(QStringLiteral("John 99:1"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::None);
        QVERIFY2(r.message.contains(QStringLiteral("chapters 1-21")), qPrintable(r.message));
        r = ref(QStringLiteral("Genesis 1:99"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::None);
        QVERIFY(r.message.contains(QStringLiteral("31 verses")));
        r = ref(QStringLiteral("John 3:18-16"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::None);
        // full text: every word must occur
        r = ref(QStringLiteral("only begotten son"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::Text);
        QVERIFY(!r.hits.isEmpty());
        bool john316 = false;
        for (const BibleHit& h : r.hits)
            john316 = john316 || (h.book == 43 && h.chapter == 3 && h.verse == 16);
        QVERIFY(john316);
        r = ref(QStringLiteral("zzzzqqq"));
        QCOMPARE(r.kind, BibleSearchResult::Kind::Text);
        QVERIFY(r.hits.isEmpty());
        QVERIFY(r.message.contains(QStringLiteral("No verse")));
        r = ref(QStringLiteral("the"));
        QVERIFY(r.truncated);
        QCOMPARE(r.hits.size(), 200);
        QCOMPARE(BibleLibrary::formatReference(QStringLiteral("John"), 3, {16, 17, 18, 20}), QStringLiteral("John 3:16-18,20"));
    }
    void importValidationGivesUsefulErrors()
    {
        auto check = [](const QByteArray& json, const QString& expected) {
            const BibleImportResult r = BibleLibrary::parse(json, nullptr);
            QVERIFY2(!r.ok, qPrintable(expected));
            QVERIFY2(r.errors.join(QLatin1Char('\n')).contains(expected), qPrintable(expected + QStringLiteral(" in: ") + r.errors.join(QLatin1Char('|'))));
        };
        check("not json", "Not valid JSON");
        check("[1,2]", "[0]");
        check("{\"schema\":2,\"id\":\"AB\",\"name\":\"x\",\"books\":[]}", "schema");
        check("{\"id\":\"A\",\"name\":\"x\",\"books\":[]}", "id:");
        check("{\"id\":\"has space\",\"name\":\"x\",\"books\":[]}", "id:");
        check("{\"id\":\"AB\",\"books\":[]}", "name:");
        check("{\"id\":\"AB\",\"name\":\"x\"}", "books:");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":67,\"name\":\"X\",\"chapters\":[[\"a\"]]}]}", "books[0].id");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":1,\"name\":\"X\",\"chapters\":[[\"a\"]]},{\"id\":1,\"name\":\"Y\",\"chapters\":[[\"b\"]]}]}", "appears twice");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":1,\"name\":\"X\",\"chapters\":[[\"a\",5]]}]}", "books[0].chapters[0][1]: expected a string or null");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":1,\"name\":\"X\",\"chapters\":[\"a\"]}]}", "books[0].chapters[0]");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":1,\"name\":\"X\",\"chapters\":[]}]}", "books[0].chapters");
        check("{\"id\":\"AB\",\"name\":\"x\",\"books\":[{\"id\":1,\"name\":\"X\",\"chapters\":[[null,\"\"]]}]}", "no verse text");
        check("[{\"Book\":\"Genesis\",\"Chapter\":1,\"Verse\":1,\"Text\":\"a\",\"Version\":\"AA\"},{\"Book\":\"Genesis\",\"Chapter\":1,\"Verse\":2,\"Text\":\"b\",\"Version\":\"BB\"}]", "several versions");
        check("[{\"Book\":\"Nowhere\",\"Chapter\":1,\"Verse\":1,\"Text\":\"a\",\"Version\":\"AA\"}]", "unknown book");
        // the documented sample is valid
        BibleTranslation t;
        const BibleImportResult ok = BibleLibrary::parse(BibleLibrary::sampleJson(), &t);
        QVERIFY2(ok.ok, qPrintable(ok.summary()));
        QCOMPARE(t.id, QStringLiteral("SAMPLE"));
        QCOMPARE(t.books.size(), 2);
        QVERIFY(t.verse(1, 2, 2).isEmpty());   // null = verse left out
        QCOMPARE(t.verse(1, 2, 3), QStringLiteral("Sample text for Genesis chapter 2, verse 3 (verse 2 is left out on purpose)."));
        QFile docSample(QStringLiteral(LUX_DATA_SRC "/../docs/sample-bible.json"));
        QVERIFY(docSample.open(QIODevice::ReadOnly));
        QVERIFY(BibleLibrary::parse(docSample.readAll(), nullptr).ok);   // the shipped sample file is valid
        // the reference program's flat list is accepted
        const BibleImportResult flat = BibleLibrary::parse("[{\"BookId\":43,\"Book\":\"Yohanes\",\"Chapter\":3,\"Verse\":16,\"Text\":\"x\",\"Version\":\"TB\"}]", &t);
        QVERIFY2(flat.ok, qPrintable(flat.summary()));
        QCOMPARE(t.id, QStringLiteral("TB"));
        QCOMPARE(t.bookName(43), QStringLiteral("Yohanes"));
    }
    void importsInstallSeparatelyPersistAndNeverOverwrite()
    {
        Env env(true);
        BibleLibrary& lib = env.data->bibles();
        auto write = [](const QString& path, const QByteArray& bytes) { QFile f(path); QVERIFY(f.open(QIODevice::WriteOnly)); f.write(bytes); };
        const QString sample = env.dir->filePath(QStringLiteral("sample.json"));
        write(sample, BibleLibrary::sampleJson());
        BibleImportResult r = lib.importFile(sample);
        QVERIFY2(r.ok, qPrintable(r.summary()));
        QCOMPARE(r.verses, 7);
        // stored under the data folder, not the program folder
        QVERIFY(QFile::exists(env.dir->filePath(QStringLiteral("Data/Bibles/SAMPLE.json"))));
        // the same id / name again: refused, nothing changes
        r = lib.importFile(sample);
        QVERIFY(!r.ok);
        QVERIFY(r.summary().contains(QStringLiteral("already installed")));
        const QString p2 = env.dir->filePath(QStringLiteral("p2.json"));
        auto variant = [&](const QString& id, const QString& name) {
            QString s = QString::fromUtf8(BibleLibrary::sampleJson());
            s.replace(QStringLiteral("\"SAMPLE\""), QStringLiteral("\"%1\"").arg(id));
            s.replace(QStringLiteral("Sample Translation (demo text)"), name);
            write(p2, s.toUtf8());
            return lib.importFile(p2);
        };
        QVERIFY(!variant(QStringLiteral("sample"), QStringLiteral("Another name")).ok);                 // same id, other case
        QVERIFY(!variant(QStringLiteral("KJV"), QStringLiteral("Mine")).ok);                            // cannot replace the built-in
        QVERIFY(variant(QStringLiteral("OTHER"), QStringLiteral("Sample Translation (demo text)")).summary().contains(QStringLiteral("named")));
        QCOMPARE(lib.translations().size(), 2);
        QVERIFY(variant(QStringLiteral("TB"), QStringLiteral("Terjemahan Uji")).ok);
        QCOMPARE(lib.translations().size(), 3);
        // a damaged file in the folder does not stop the others from loading
        write(env.dir->filePath(QStringLiteral("Data/Bibles/BROKEN.json")), "{ nope");
        DataService again;   // "next start"
        BibleLibrary& lib2 = again.bibles();
        QCOMPARE(lib2.translations().size(), 3);
        QCOMPARE(lib2.translations().first()->id, QStringLiteral("KJV"));
        QVERIFY(lib2.find(QStringLiteral("tb")));
        QVERIFY(!lib2.loadWarnings().isEmpty());
        // removal: user translations only
        QString err;
        QVERIFY(!lib2.remove(QStringLiteral("KJV"), &err));
        QVERIFY(lib2.remove(QStringLiteral("TB"), &err));
        QVERIFY(!QFile::exists(env.dir->filePath(QStringLiteral("Data/Bibles/TB.json"))));
        QCOMPARE(lib2.translations().size(), 2);
    }
    void scripturesTabWorksEndToEnd()
    {
        Env env(true);
        MainWindow& w = env.mainWindow();
        ScripturesTab* tab = w.scripturesTab();
        tab->setDialogsSuppressed(true);
        QCOMPARE(tab->selectedVersion(), QStringLiteral("KJV"));
        QCOMPARE(tab->verseList()->count(), 31);   // Genesis 1
        // navigation: book / chapter / next / previous across a book boundary
        tab->showChapter(1, 50);
        QCOMPARE(tab->verseList()->count(), 26);
        QVERIFY(tab->nextButton()->isEnabled());
        tab->nextButton()->click();
        QCOMPARE(tab->bookCombo()->currentText(), QStringLiteral("Exodus"));
        QCOMPARE(tab->chapterCombo()->currentText(), QStringLiteral("1"));
        tab->previousButton()->click();
        QCOMPARE(tab->bookCombo()->currentText(), QStringLiteral("Genesis"));
        QCOMPARE(tab->chapterCombo()->currentText(), QStringLiteral("50"));
        tab->showChapter(1, 1);
        QVERIFY(!tab->previousButton()->isEnabled());
        // search by reference selects the verse and stages it in Preview (not Live)
        QSignalSpy sel(tab, &ScripturesTab::passageSelected);
        tab->runSearch(QStringLiteral("John 3:16"));
        QCOMPARE(sel.size(), 1);
        QCOMPARE(w.previewSource(), MainWindow::PreviewSource::Scripture);
        QCOMPARE(w.previewPane()->itemTitle(), QStringLiteral("John 3:16"));
        QVERIFY(w.previewPane()->slides().first().content.contains(QStringLiteral("only begotten Son"), Qt::CaseInsensitive));
        QCOMPARE(w.previewPane()->slides().first().type, SlideType::Scripture);
        QVERIFY(!w.isLiveOutputActive());
        // several verses make one passage
        tab->showChapter(19, 23);
        tab->verseList()->item(0)->setSelected(true);
        tab->verseList()->item(1)->setSelected(true);
        tab->verseList()->item(2)->setSelected(true);
        QCOMPARE(w.previewPane()->itemTitle(), QStringLiteral("Psalms 23:1-3"));
        QCOMPARE(w.previewPane()->slides().size(), 1);
        // Add to schedule keeps it as a Scripture item
        emit tab->addToScheduleRequested(tab->currentPassage());
        QCOMPARE(w.scheduleService().count(), 1);
        QCOMPARE(w.scheduleService().items().at(0).category, ItemCategory::Scripture);
        // double click / Enter goes live on the projector
        QSignalSpy act(tab, &ScripturesTab::passageActivated);
        emit tab->verseList()->itemActivated(tab->verseList()->item(0));
        QCOMPARE(act.size(), 1);
        QVERIFY(w.isLiveOutputActive());
        QVERIFY(w.projection()->state().activeSlide.has_value());
        QCOMPARE(w.projection()->state().activeSlide->type, SlideType::Scripture);
        QCOMPARE(w.projection()->state().activeSlide->title, QStringLiteral("Psalms 23:1-3"));
        // the schedule item survives save / open
        const QString file = env.dir->filePath(QStringLiteral("bible.lps"));
        QString err;
        QVERIFY2(w.saveScheduleFile(file, &err), qPrintable(err));
        QVERIFY(w.newScheduleNoPrompt());
        QVERIFY2(w.openScheduleFile(file, &err), qPrintable(err));
        QCOMPARE(w.scheduleService().items().at(0).category, ItemCategory::Scripture);
        QCOMPARE(w.scheduleService().items().at(0).presentation.slides.first().title, QStringLiteral("Psalms 23:1-3"));
        // invalid references report a problem and leave the list alone
        const int before = tab->verseList()->count();
        tab->runSearch(QStringLiteral("John 99"));
        QVERIFY(tab->message().contains(QStringLiteral("chapters 1-21")));
        QCOMPARE(tab->verseList()->count(), before);
        // text search shows results; clearing the box returns to the chapter
        tab->runSearch(QStringLiteral("only begotten"));
        QVERIFY(tab->inResultsMode());
        QVERIFY(tab->verseList()->count() >= 1);
        tab->runSearch(QString());
        QVERIFY(!tab->inResultsMode());
    }
    void importedTranslationWorksInTheTabAndSurvivesRestart()
    {
        Env env(true);
        MainWindow& w = env.mainWindow();
        ScripturesTab* tab = w.scripturesTab();
        tab->setDialogsSuppressed(true);
        auto write = [](const QString& path, const QByteArray& bytes) { QFile f(path); QVERIFY(f.open(QIODevice::WriteOnly)); f.write(bytes); };
        const QString sample = env.dir->filePath(QStringLiteral("sample.json"));
        write(sample, BibleLibrary::sampleJson());
        const QString bad = env.dir->filePath(QStringLiteral("bad.json"));
        write(bad, "{\"id\":\"ZZ\"}");
        QVERIFY(!tab->importTranslation(bad).ok);   // bad file: nothing installed
        QCOMPARE(tab->versionTabs()->count(), 1);
        const BibleImportResult r = tab->importTranslation(sample);
        QVERIFY2(r.ok, qPrintable(r.summary()));
        QCOMPARE(tab->versionTabs()->count(), 2);
        QCOMPARE(tab->selectedVersion(), QStringLiteral("SAMPLE"));
        // navigation / search / Preview work with the imported text
        tab->runSearch(QStringLiteral("Genesis 1:2"));
        QCOMPARE(w.previewPane()->itemTitle(), QStringLiteral("Genesis 1:2"));
        QVERIFY(w.previewPane()->slides().first().content.contains(QStringLiteral("Sample text for Genesis chapter 1, verse 2"), Qt::CaseInsensitive));
        tab->runSearch(QStringLiteral("Genesis 2:2"));   // left out in the sample
        QVERIFY(tab->message().contains(QStringLiteral("not in")));
        tab->runSearch(QStringLiteral("Exodus 1"));      // the sample has no Exodus
        QVERIFY(tab->message().contains(QStringLiteral("not in")));
        // switching translations keeps the chosen verses and updates Preview
        tab->setTranslation(QStringLiteral("KJV"));
        tab->runSearch(QStringLiteral("Genesis 1:2"));
        QVERIFY(w.previewPane()->slides().first().content.contains(QStringLiteral("without form"), Qt::CaseInsensitive));
        tab->setTranslation(QStringLiteral("SAMPLE"));
        QCOMPARE(tab->currentPassage().reference, QStringLiteral("Genesis 1:2"));
        QVERIFY(w.previewPane()->slides().first().content.contains(QStringLiteral("Sample text"), Qt::CaseInsensitive));
        // next start: translation list and the active one come back
        DataService again;
        QCOMPARE(again.settings().selectedBibleVersion, QStringLiteral("SAMPLE"));
        ScripturesTab tab2(again);
        QCOMPARE(tab2.versionTabs()->count(), 2);
        QCOMPARE(tab2.selectedVersion(), QStringLiteral("SAMPLE"));
        // remove it again
        QString err;
        QVERIFY(tab->removeTranslation(QStringLiteral("SAMPLE"), &err));
        QCOMPARE(tab->versionTabs()->count(), 1);
        QCOMPARE(tab->selectedVersion(), QStringLiteral("KJV"));
    }
    void noBibleDataDoesNotCrash()
    {
        Env env(false);
        MainWindow& w = env.mainWindow();
        ScripturesTab* tab = w.scripturesTab();
        QCOMPARE(tab->versionTabs()->count(), 0);
        QVERIFY(tab->translation() == nullptr);
        tab->runSearch(QStringLiteral("John 3:16"));
    }    // ------------------------------------------------------------- Ad blocker
    void adBlockEngineUnderstandsTheListSyntax()
    {
        AdBlockEngine e;
        const auto st = e.addList(QStringLiteral(
            "[Adblock Plus 2.0]\n! comment\n"
            "||ads.example.com^\n"
            "/adserver/*\n"
            "||tracker.net^$third-party\n"
            "||cdn.net/lib.js$script\n"
            "||only-on-a.net^$domain=a.com|~sub.a.com\n"
            "|http://exact.test/|\n"
            "||blocked-but-excepted.com^\n"
            "@@||blocked-but-excepted.com/ok^\n"
            "||important.com^$important\n"
            "@@||important.com^\n"
            "||img.example^$image,~third-party\n"
            "||whole-page.com^\n"
            "@@||friendly.com^$document\n"
            "||blocked-on-friendly.com^\n"
            "example.com##.banner\n"
            "/regex.*rule/\n"
            "||redirected.com^$redirect=noop.js\n"
            "||removeparam.com^$removeparam=x\n"));
        QCOMPARE(st.skipped, 3);                    // regex, redirect, removeparam
        auto blocked = [&](const QString& url, const QString& page = QStringLiteral("https://page.org/"), quint32 type = AdBlockEngine::Script) {
            return e.shouldBlock(url, page, type);
        };
        QVERIFY(blocked(QStringLiteral("http://ads.example.com/x.js")));
        QVERIFY(blocked(QStringLiteral("https://sub.ads.example.com:8443/x")));
        QVERIFY(!blocked(QStringLiteral("https://notads.example.com/x")));
        QVERIFY(!blocked(QStringLiteral("https://example.com/?u=ads.example.com")));   // "||" anchors at the host only
        QVERIFY(blocked(QStringLiteral("https://site.org/adserver/banner.gif")));
        QVERIFY(!blocked(QStringLiteral("https://site.org/adserve/banner.gif")));
        QVERIFY(blocked(QStringLiteral("https://tracker.net/p.gif"), QStringLiteral("https://page.org/")));
        QVERIFY(!blocked(QStringLiteral("https://tracker.net/p.gif"), QStringLiteral("https://www.tracker.net/")));   // first party
        QVERIFY(blocked(QStringLiteral("https://cdn.net/lib.js"), QStringLiteral("https://page.org/"), AdBlockEngine::Script));
        QVERIFY(!blocked(QStringLiteral("https://cdn.net/lib.js"), QStringLiteral("https://page.org/"), AdBlockEngine::Image));
        QVERIFY(blocked(QStringLiteral("https://only-on-a.net/x"), QStringLiteral("https://a.com/")));
        QVERIFY(blocked(QStringLiteral("https://only-on-a.net/x"), QStringLiteral("https://www.a.com/")));
        QVERIFY(!blocked(QStringLiteral("https://only-on-a.net/x"), QStringLiteral("https://sub.a.com/")));
        QVERIFY(!blocked(QStringLiteral("https://only-on-a.net/x"), QStringLiteral("https://b.com/")));
        QVERIFY(blocked(QStringLiteral("http://exact.test/")));
        QVERIFY(!blocked(QStringLiteral("http://exact.test/more")));
        QVERIFY(blocked(QStringLiteral("https://blocked-but-excepted.com/banner")));
        QVERIFY(!blocked(QStringLiteral("https://blocked-but-excepted.com/ok")));      // @@ exception
        QVERIFY(blocked(QStringLiteral("https://important.com/x")));                   // $important beats @@
        QVERIFY(!blocked(QStringLiteral("https://site.org/ads.example.com"), QStringLiteral("https://page.org/")));
        QVERIFY(!blocked(QStringLiteral("data:text/plain,hello")));
        QVERIFY(!blocked(QStringLiteral("blob:https://page.org/abc")));
        QVERIFY(!blocked(QStringLiteral("https://regex.org/regexXrule")));
        QVERIFY(!blocked(QStringLiteral("https://redirected.com/x")));
        // "@@...$document" switches the filter off for that page
        QVERIFY(blocked(QStringLiteral("https://blocked-on-friendly.com/x"), QStringLiteral("https://other.org/")));
        QVERIFY(!blocked(QStringLiteral("https://blocked-on-friendly.com/x"), QStringLiteral("https://friendly.com/page")));
        QCOMPARE(AdBlockEngine::registrableDomain(QStringLiteral("a.b.example.co.uk")), QStringLiteral("example.co.uk"));
        QCOMPARE(AdBlockEngine::registrableDomain(QStringLiteral("www.example.com")), QStringLiteral("example.com"));
    }
    void shippedFilterListsAreValidAndSelective()
    {
        QElapsedTimer timer;
        timer.start();
        AdBlockService svc(QStringLiteral(LUX_TEST_TMP "/m6-no-user-lists"), QStringLiteral(LUX_DATA_SRC "/adblock"));
        svc.reload();
        const qint64 loadMs = timer.elapsed();
        qInfo() << "filter lists loaded in" << loadMs << "ms:" << svc.statusText().replace(QLatin1Char('\n'), QStringLiteral(" | "));
        QCOMPARE(svc.lists().size(), 2);
        for (const auto& i : svc.lists()) {
            QCOMPARE(i.source, QStringLiteral("bundled"));
            QVERIFY2(i.blockRules > 10000, qPrintable(i.title));
        }
        const QString page = QStringLiteral("https://example.org/news");
        QVERIFY(svc.shouldBlock(QStringLiteral("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js"), page, AdBlockEngine::Script));
        QVERIFY(svc.shouldBlock(QStringLiteral("https://securepubads.g.doubleclick.net/tag/js/gpt.js"), page, AdBlockEngine::Script));
        QVERIFY(svc.shouldBlock(QStringLiteral("https://www.google-analytics.com/analytics.js"), page, AdBlockEngine::Script));
        // ordinary content and the media of YouTube are left alone
        for (const char* u : {"https://en.wikipedia.org/wiki/Main_Page", "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://i.ytimg.com/vi/x/hqdefault.jpg",
                              "https://rr1---sn-4g5e6nzl.googlevideo.com/videoplayback?expire=1&itag=18", "https://fonts.gstatic.com/s/a.woff2",
                              "https://www.youtube.com/s/player/abc/player_ias.vflset/en_US/base.js", "https://example.org/style.css"})
            QVERIFY2(!svc.shouldBlock(QString::fromLatin1(u), QStringLiteral("https://www.youtube.com/watch?v=x"), AdBlockEngine::Script), u);
        timer.restart();
        int n = 0;
        for (int i = 0; i < 2000; ++i)
            n += svc.shouldBlock(QStringLiteral("https://cdn%1.example.net/assets/app.js?v=%1").arg(i), page, AdBlockEngine::Script);
        qInfo() << "2000 lookups:" << timer.elapsed() << "ms";
        Q_UNUSED(n);
        QVERIFY(timer.elapsed() < 5000);
    }
    void adBlockSwitchWhitelistAndMissingLists()
    {
        AdBlockService svc(QStringLiteral(LUX_TEST_TMP "/m6-none-a"), QStringLiteral(LUX_TEST_TMP "/m6-none-b"));   // no list files anywhere
        svc.reload();
        QVERIFY(svc.statusText().contains(QStringLiteral("not available")));
        QVERIFY(!svc.shouldBlock(QStringLiteral("https://pagead2.googlesyndication.com/x.js"), QStringLiteral("https://a.org/"), AdBlockEngine::Script));   // no crash, nothing blocked
        AdBlockService ok(QStringLiteral(LUX_TEST_TMP "/m6-none-a"), QStringLiteral(LUX_DATA_SRC "/adblock"));
        ok.reload();
        const QString ad = QStringLiteral("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js");
        QVERIFY(ok.shouldBlock(ad, QStringLiteral("https://news.example.org/a"), AdBlockEngine::Script));
        ok.setEnabled(false);
        QVERIFY(!ok.shouldBlock(ad, QStringLiteral("https://news.example.org/a"), AdBlockEngine::Script));
        ok.setEnabled(true);
        ok.setWhitelist({QStringLiteral("HTTPS://News.Example.org/path"), QStringLiteral("news.example.org")});
        QCOMPARE(ok.whitelist(), (QStringList{"news.example.org"}));
        QVERIFY(!ok.shouldBlock(ad, QStringLiteral("https://news.example.org/a"), AdBlockEngine::Script));
        QVERIFY(!ok.shouldBlock(ad, QStringLiteral("https://m.news.example.org/a"), AdBlockEngine::Script));   // sub-domains too
        QVERIFY(ok.shouldBlock(ad, QStringLiteral("https://other.org/a"), AdBlockEngine::Script));
        QVERIFY(!ok.blocksPopupsFor(QStringLiteral("https://news.example.org/")));
        QVERIFY(ok.blocksPopupsFor(QStringLiteral("https://other.org/")));
        // a damaged downloaded copy falls back to the bundled one
        const QString userDir = QStringLiteral(LUX_TEST_TMP "/m6-damaged");
        QDir(userDir).removeRecursively();
        QDir().mkpath(userDir);
        { QFile f(userDir + QStringLiteral("/easylist.txt")); QVERIFY(f.open(QIODevice::WriteOnly)); f.write("this is not a filter list"); }
        AdBlockService fb(userDir, QStringLiteral(LUX_DATA_SRC "/adblock"));
        fb.reload();
        QCOMPARE(fb.lists().first().source, QStringLiteral("bundled"));
        QVERIFY(fb.shouldBlock(ad, QStringLiteral("https://a.org/"), AdBlockEngine::Script));
    }
    void filterListUpdateIsValidatedAndNeverBreaksTheOldList()
    {
        const QString userDir = QStringLiteral(LUX_TEST_TMP "/m6-upd");
        QDir(userDir).removeRecursively();
        TinyServer srv;
        srv.routes[QStringLiteral("/easylist.txt")] = {200, syntheticList(QStringLiteral("Fake EasyList"), 1500)};
        srv.routes[QStringLiteral("/easyprivacy.txt")] = {200, QByteArray("<html>an error page, not a list</html>")};
        AdBlockService svc(userDir, QStringLiteral(LUX_DATA_SRC "/adblock"));
        svc.setLists({{QStringLiteral("easylist"), QStringLiteral("EasyList"), QStringLiteral("easylist.txt"), QUrl(srv.url(QStringLiteral("/easylist.txt")))},
                      {QStringLiteral("easyprivacy"), QStringLiteral("EasyPrivacy"), QStringLiteral("easyprivacy.txt"), QUrl(srv.url(QStringLiteral("/easyprivacy.txt")))}});
        svc.reload();
        const int before = svc.lists().at(1).blockRules;
        QVERIFY(before > 10000);
        QSignalSpy done(&svc, &AdBlockService::updateFinished);
        svc.updateLists();
        QVERIFY(svc.updating());
        QVERIFY(done.wait(20000));
        QVERIFY(!svc.updating());
        QVERIFY(!done.first().at(0).toBool());   // one of the two failed
        const QString msg = done.first().at(1).toString();
        QVERIFY2(msg.contains(QStringLiteral("1 list(s) updated")) && msg.contains(QStringLiteral("EasyPrivacy was not updated")), qPrintable(msg));
        // EasyList now comes from the downloaded (fake) file, EasyPrivacy still from the bundled snapshot
        QCOMPARE(svc.lists().at(0).source, QStringLiteral("downloaded"));
        QCOMPARE(svc.lists().at(0).blockRules, 1500);
        QCOMPARE(svc.lists().at(1).source, QStringLiteral("bundled"));
        QCOMPARE(svc.lists().at(1).blockRules, before);
        QVERIFY(!QFile::exists(userDir + QStringLiteral("/easyprivacy.txt")));
        QVERIFY(svc.shouldBlock(QStringLiteral("https://ad7.example.test/x.js"), QStringLiteral("https://page.org/"), AdBlockEngine::Script));
        // a server error and an unreachable server keep what is installed
        srv.routes[QStringLiteral("/easylist.txt")] = {500, QByteArray("boom")};
        done.clear();
        svc.updateLists();
        QVERIFY(done.wait(20000));
        QVERIFY(!done.first().at(0).toBool());
        QCOMPARE(svc.lists().at(0).blockRules, 1500);   // the earlier download is still used
        QUrl dead(QStringLiteral("http://127.0.0.1:9/easylist.txt"));
        svc.setLists({{QStringLiteral("easylist"), QStringLiteral("EasyList"), QStringLiteral("easylist.txt"), dead}});
        done.clear();
        svc.updateLists();
        QVERIFY(done.wait(20000));
        QVERIFY(!done.first().at(0).toBool());
        QVERIFY(done.first().at(1).toString().contains(QStringLiteral("not updated")));
        QCOMPARE(svc.lists().at(0).blockRules, 1500);
        // a valid second download replaces the file
        srv.routes[QStringLiteral("/easylist.txt")] = {200, syntheticList(QStringLiteral("Fake EasyList 2"), 2500, QStringLiteral("zz"))};
        svc.setLists({{QStringLiteral("easylist"), QStringLiteral("EasyList"), QStringLiteral("easylist.txt"), QUrl(srv.url(QStringLiteral("/easylist.txt")))}});
        done.clear();
        svc.updateLists();
        QVERIFY(done.wait(20000));
        QVERIFY(done.first().at(0).toBool());
        QCOMPARE(svc.lists().at(0).blockRules, 2500);
    }
    void onlineToolbarShieldFollowsTheService()
    {
        Env env;
        MainWindow& w = env.mainWindow();
        QVERIFY(w.adBlock());
        QVERIFY(w.online()->adBlockButton());
        QVERIFY(w.adBlock()->enabled());
        QVERIFY(w.online()->adBlockButton()->text().contains(QStringLiteral("On")));
        // switching it off from the shield menu is saved
        QAction* toggle = w.online()->adBlockMenu()->actions().first();
        QVERIFY(toggle->isChecked());
        toggle->trigger();
        QVERIFY(!w.adBlock()->enabled());
        QVERIFY(w.online()->adBlockButton()->text().contains(QStringLiteral("Off")));
        QVERIFY(!env.data->settings().adBlockEnabled);
        // ... and from Options
        AppSettings s = env.data->settings();
        s.adBlockEnabled = true;
        s.adBlockWhitelist = {QStringLiteral("example.org")};
        w.applySettings(s);
        QVERIFY(w.adBlock()->enabled());
        QCOMPARE(w.adBlock()->whitelist(), (QStringList{"example.org"}));
        QVERIFY(w.online()->adBlockButton()->text().contains(QStringLiteral("On")));
        // restart: the service starts with the saved values
        DataService again;
        QVERIFY(again.settings().adBlockEnabled);
        QCOMPARE(again.settings().adBlockWhitelist, (QStringList{"example.org"}));
    }};
QTEST_MAIN(M6Tests)
#include "test_m6.moc"
