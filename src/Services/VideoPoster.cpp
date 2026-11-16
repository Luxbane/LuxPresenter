#include "Services/VideoPoster.h"
#include "Services/MpvApi.h"
#include <QDir>
#include <QElapsedTimer>
#include <QFile>
#include <QFileInfo>
#include <QThread>
#include <clocale>
namespace Lux {
bool VideoPoster::generate(const QString& videoPath, const QString& outPng, QString* error, int timeoutMs)
{
    auto fail = [&](const QString& m) {
        if (error)
            *error = m;
        return false;
    };
    MpvApi& api = MpvApi::instance();
    if (!api.isAvailable() && !api.load())
        return fail(api.lastError());
    QDir().mkpath(QFileInfo(outPng).absolutePath());
    const QString tmpDir = outPng + QStringLiteral(".tmp");
    QDir(tmpDir).removeRecursively();
    QDir().mkpath(tmpDir);
    std::setlocale(LC_NUMERIC, "C");
    void* h = api.create();
    if (!h)
        return fail(QStringLiteral("mpv_create failed"));
    const QByteArray dir = QDir::toNativeSeparators(tmpDir).toUtf8();
    const char* opts[][2] = {{"vo", "image"}, {"vo-image-format", "png"}, {"ao", "null"}, {"aid", "no"}, {"sid", "no"},
                             {"frames", "1"}, {"start", "10%"}, {"hr-seek", "yes"}, {"keep-open", "no"}, {"idle", "yes"},
                             {"config", "no"}, {"load-scripts", "no"}, {"ytdl", "no"}, {"terminal", "no"}, {"osc", "no"}};
    for (auto& o : opts)
        api.setOptionString(h, o[0], o[1]);
    api.setOptionString(h, "vo-image-outdir", dir.constData());
    if (api.initialize(h) < 0) {
        api.terminateDestroy(h);
        QDir(tmpDir).removeRecursively();
        return fail(QStringLiteral("mpv_initialize failed"));
    }
    const QByteArray p = videoPath.toUtf8();
    const char* args[] = {"loadfile", p.constData(), nullptr};
    bool ok = api.command(h, args) >= 0;
    QString produced;
    if (ok) {
        QElapsedTimer t;
        t.start();
        bool started = false;
        while (t.elapsed() < timeoutMs) {
            QThread::msleep(40);
            const QStringList files = QDir(tmpDir).entryList({QStringLiteral("*.png")}, QDir::Files);
            char* idle = api.getPropertyString(h, "idle-active");
            const bool isIdle = idle && QByteArray(idle) == "yes";
            if (idle)
                api.freeMem(idle);
            if (!isIdle)
                started = true;
            if (!files.isEmpty() && (isIdle || t.elapsed() > 1500)) {
                produced = QDir(tmpDir).filePath(files.first());
                break;
            }
            if (started && isIdle)
                break;   // finished without a frame (undecodable file)
        }
    }
    api.terminateDestroy(h);   // closes the output files
    bool result = false;
    if (!produced.isEmpty() && QFileInfo(produced).size() > 0) {
        QFile::remove(outPng);
        result = QFile::copy(produced, outPng);
    }
    QDir(tmpDir).removeRecursively();
    return result ? true : fail(QStringLiteral("No frame could be read from %1.").arg(QFileInfo(videoPath).fileName()));
}
} // namespace Lux
