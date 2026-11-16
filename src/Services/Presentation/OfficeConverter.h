#pragma once

#include <QObject>
#include <QProcess>
#include <QStringList>
#include <QTimer>

#include <memory>

namespace Lux {

// Converts PowerPoint/OpenDocument presentations to PDF with LibreOffice running headless in the background
// (soffice --headless --convert-to pdf), like the reference PresentationEngineService. LuxPresenter ships its own
// copy in <app>\Engines\LibreOffice (an unmodified official LibreOffice, see README); a system installation is only a
// fallback. LibreOffice never shows a window: the process is created hidden and in a job object, so the whole process
// tree is ended on timeout/cancel/exit and nothing is left running.
class OfficeConverter : public QObject
{
    Q_OBJECT
public:
    explicit OfficeConverter(QObject* parent = nullptr);
    ~OfficeConverter() override;

    // ".pptx", ".ppsx", ".odp", ".ppt", ".pps"
    static QStringList officeExtensions();
    static bool isOfficeFile(const QString& path);

    // Files an install folder must contain to be usable ("" = complete, otherwise what is missing, comma separated).
    static QString installProblem(const QString& libreOfficeDir);
    // <dir>/program/soffice.com (console launcher, preferred) or soffice.exe; empty when the folder has neither.
    // `libreOfficeDir` empty = the bundled folder.
    static QString findSoffice(const QString& libreOfficeDir = {});

    struct Engine
    {
        QString soffice;     // executable to run ("" = none usable)
        QString directory;   // its LibreOffice folder
        bool bundled = false;
        QString problem;     // why no engine is usable (missing / damaged), for the user
    };
    // Bundled copy first (<app>\Engines\LibreOffice), then %ProgramFiles%\LibreOffice. `bundledDir` empty = default.
    static Engine locate(const QString& bundledDir = {}, const QStringList& systemDirs = {});

    void setSofficeOverride(const QString& exe) { m_override = exe; }   // tests
    void setTimeoutMs(int ms) { m_timeoutMs = ms; }
    bool isRunning() const { return m_process != nullptr; }

    // Starts the conversion of `input` into `outputDir/<base>.pdf`; finished() is always emitted exactly once.
    void convert(const QString& input, const QString& outputDir, const QString& userProfileDir);
    void cancel();

signals:
    void finished(bool ok, const QString& pdfPath, const QString& error);

private:
    void done(bool ok, const QString& pdf, const QString& error, bool killed = false);
    void killProcessTree();

    QString m_override;
    int m_timeoutMs = 45000;
    std::unique_ptr<QProcess> m_process;
    QTimer m_timer;
    QString m_expectedPdf;
    QString m_profileDir;
    void* m_job = nullptr;   // Windows job object holding the LibreOffice process tree
};

} // namespace Lux
