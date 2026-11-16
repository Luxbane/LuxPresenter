#include "Services/Presentation/OfficeConverter.h"

#include "Services/AppPaths.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QUrl>

#ifdef Q_OS_WIN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

namespace Lux {

OfficeConverter::OfficeConverter(QObject* parent) : QObject(parent)
{
    m_timer.setSingleShot(true);
    connect(&m_timer, &QTimer::timeout, this, [this] {
        if (m_process)
            m_process->disconnect(this);   // the kill below must not be reported as a second result
        done(false, {}, QStringLiteral("LibreOffice did not finish converting the presentation in time."), /*killed=*/true);
    });
}

OfficeConverter::~OfficeConverter()
{
    killProcessTree();
    if (m_process) {
        m_process->disconnect(this);
        m_process->kill();
        m_process->waitForFinished(2000);
    }
}

QStringList OfficeConverter::officeExtensions()
{
    return {QStringLiteral(".pptx"), QStringLiteral(".ppsx"), QStringLiteral(".odp"), QStringLiteral(".ppt"), QStringLiteral(".pps")};
}

bool OfficeConverter::isOfficeFile(const QString& path)
{
    return officeExtensions().contains(QLatin1Char('.') + QFileInfo(path).suffix().toLower());
}

QString OfficeConverter::installProblem(const QString& dir)
{
    if (dir.isEmpty() || !QDir(dir).exists())
        return QStringLiteral("folder not found");
    QStringList missing;
    // Launcher, the real program, its configuration and the core libraries a conversion needs.
    for (const char* rel : {"program/soffice.bin", "program/bootstrap.ini", "program/fundamental.ini", "program/version.ini",
                            "program/mergedlo.dll", "program/sal3.dll", "program/cppu3.dll", "program/sdlo.dll",
                            "share/registry/main.xcd"}) {
        if (!QFileInfo::exists(QDir(dir).filePath(QLatin1String(rel))))
            missing << QLatin1String(rel);
    }
    if (findSoffice(dir).isEmpty())
        missing.prepend(QStringLiteral("program/soffice.exe"));
    return missing.join(QStringLiteral(", "));
}

QString OfficeConverter::findSoffice(const QString& libreOfficeDir)
{
    const QString dir = libreOfficeDir.isEmpty() ? QDir(AppPaths::baseDirectory()).filePath(QStringLiteral("Engines/LibreOffice")) : libreOfficeDir;
    for (const char* name : {"soffice.com", "soffice.exe"}) {
        const QString p = QDir(dir).filePath(QStringLiteral("program/") + QLatin1String(name));
        if (QFileInfo::exists(p))
            return p;
    }
    return {};
}

OfficeConverter::Engine OfficeConverter::locate(const QString& bundledDir, const QStringList& systemDirs)
{
    Engine e;
    const QString bundled = bundledDir.isEmpty() ? QDir(AppPaths::baseDirectory()).filePath(QStringLiteral("Engines/LibreOffice")) : bundledDir;
    QStringList system = systemDirs;
    if (system.isEmpty()) {
        for (const char* var : {"ProgramFiles", "ProgramW6432", "ProgramFiles(x86)"}) {
            const QString pf = qEnvironmentVariable(var);
            if (!pf.isEmpty() && !system.contains(QDir(pf).filePath(QStringLiteral("LibreOffice"))))
                system << QDir(pf).filePath(QStringLiteral("LibreOffice"));
        }
    }
    QString bundledProblem;
    if (QDir(bundled).exists()) {
        bundledProblem = installProblem(bundled);
        if (bundledProblem.isEmpty()) {
            e.soffice = findSoffice(bundled);
            e.directory = bundled;
            e.bundled = true;
            return e;
        }
    }
    for (const QString& dir : std::as_const(system)) {
        if (QDir(dir).exists() && installProblem(dir).isEmpty()) {
            e.soffice = findSoffice(dir);
            e.directory = dir;
            return e;
        }
    }
    e.problem = bundledProblem.isEmpty()
                    ? QStringLiteral("LibreOffice was not found (expected in the Engines\\LibreOffice folder next to LuxPresenter). "
                                     "PDF files can still be used.")
                    : QStringLiteral("The bundled LibreOffice (Engines\\LibreOffice) is incomplete or damaged (missing: %1). "
                                     "Extract the LuxPresenter package again. PDF files can still be used.")
                          .arg(bundledProblem);
    return e;
}

void OfficeConverter::killProcessTree()
{
#ifdef Q_OS_WIN
    if (m_job) {
        TerminateJobObject(static_cast<HANDLE>(m_job), 1);   // LibreOffice launcher and soffice.bin together
        CloseHandle(static_cast<HANDLE>(m_job));
        m_job = nullptr;
    }
#endif
}

void OfficeConverter::done(bool ok, const QString& pdf, const QString& error, bool killed)
{
    m_timer.stop();
    if (killed || !ok)
        killProcessTree();
    if (m_process) {
        m_process->disconnect(this);
        if (m_process->state() != QProcess::NotRunning) {
            m_process->kill();
            m_process->waitForFinished(2000);
        }
        m_process.release()->deleteLater();
    }
    if (m_job) {          // finished normally: the job (now empty) is no longer needed
        killProcessTree();
    }
    if (!ok) {
        // Leave nothing behind: a partial result and, after a kill, the possibly inconsistent user profile.
        if (!m_expectedPdf.isEmpty())
            QFile::remove(m_expectedPdf);
        if (killed && !m_profileDir.isEmpty())
            QDir(m_profileDir).removeRecursively();
    }
    emit finished(ok, pdf, error);
}

void OfficeConverter::cancel()
{
    if (m_process) {
        m_process->disconnect(this);
        done(false, {}, QStringLiteral("The conversion was cancelled."), /*killed=*/true);
    }
}

void OfficeConverter::convert(const QString& input, const QString& outputDir, const QString& userProfileDir)
{
    QString soffice = m_override;
    QString problem;
    if (soffice.isEmpty()) {
        const Engine e = locate();
        soffice = e.soffice;
        problem = e.problem;
    }
    if (soffice.isEmpty() || !QFileInfo::exists(soffice)) {
        const QString msg = problem.isEmpty() ? QStringLiteral("LibreOffice was not found.") : problem;
        QTimer::singleShot(0, this, [this, msg] { emit finished(false, {}, msg); });
        return;
    }
    // .pptx / .ppsx / .odp are ZIP packages. LibreOffice would happily turn a damaged or mislabelled file into a
    // PDF of its raw text, so refuse anything that is not a package up front.
    if (const QString ext = QFileInfo(input).suffix().toLower(); ext == QLatin1String("pptx") || ext == QLatin1String("ppsx") || ext == QLatin1String("odp")) {
        QFile f(input);
        const QByteArray head = f.open(QIODevice::ReadOnly) ? f.read(4) : QByteArray();
        if (head != QByteArray("PK\x03\x04", 4)) {
            const QString msg = QStringLiteral("The file is not a valid presentation (it is damaged or has the wrong extension): %1").arg(QDir::toNativeSeparators(input));
            QTimer::singleShot(0, this, [this, msg] { emit finished(false, {}, msg); });
            return;
        }
    }
    QDir().mkpath(outputDir);
    QDir().mkpath(userProfileDir);
    m_profileDir = userProfileDir;
    m_expectedPdf = QDir(outputDir).filePath(QFileInfo(input).completeBaseName() + QStringLiteral(".pdf"));
    QFile::remove(m_expectedPdf);

    // Remove stale lock files that can cause headless LibreOffice to freeze waiting for user input
    QFile::remove(QDir(userProfileDir).filePath(QStringLiteral(".lock")));
    QFile::remove(QDir(userProfileDir).filePath(QStringLiteral(".~lock")));

#ifdef Q_OS_WIN
    // Clear any lingering or orphaned headless soffice processes from previous interrupted attempts
    PROCESS_INFORMATION pi{};
    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    wchar_t cmd[] = L"taskkill.exe /F /IM soffice.bin /IM soffice.exe";
    if (CreateProcessW(nullptr, cmd, nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi)) {
        WaitForSingleObject(pi.hProcess, 1000);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }
#endif

    m_process = std::make_unique<QProcess>();
    // Background only: no console window, no splash, no first-start wizard, no document lock files, never visible.
    QStringList args{QStringLiteral("--headless"), QStringLiteral("--invisible"), QStringLiteral("--norestore"), QStringLiteral("--nologo"),
                     QStringLiteral("--nodefault"), QStringLiteral("--nolockcheck"), QStringLiteral("--nofirststartwizard"),
                     QStringLiteral("-env:UserInstallation=") + QUrl::fromLocalFile(userProfileDir).toString(),
                     QStringLiteral("--convert-to"), QStringLiteral("pdf:impress_pdf_Export"),
                     QDir::toNativeSeparators(input), QStringLiteral("--outdir"), QDir::toNativeSeparators(outputDir)};
#ifdef Q_OS_WIN
    m_process->setCreateProcessArgumentsModifier([](QProcess::CreateProcessArguments* a) {
        a->flags |= CREATE_NO_WINDOW;
        a->startupInfo->dwFlags |= STARTF_USESHOWWINDOW;
        a->startupInfo->wShowWindow = SW_HIDE;
    });
    {
        // One job object per conversion: when it is terminated or closed every process LibreOffice started goes with it.
        HANDLE job = CreateJobObjectW(nullptr, nullptr);
        if (job) {
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION info{};
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            SetInformationJobObject(job, JobObjectExtendedLimitInformation, &info, sizeof(info));
            m_job = job;
        }
    }
    connect(m_process.get(), &QProcess::started, this, [this] {
        if (m_job && m_process) {
            if (HANDLE h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, FALSE, DWORD(m_process->processId()))) {
                AssignProcessToJobObject(static_cast<HANDLE>(m_job), h);
                CloseHandle(h);
            }
        }
    });
#endif
    connect(m_process.get(), &QProcess::errorOccurred, this, [this](QProcess::ProcessError e) {
        if (e == QProcess::FailedToStart)
            done(false, {}, QStringLiteral("LibreOffice could not be started."));
    });
    connect(m_process.get(), &QProcess::finished, this, [this](int code, QProcess::ExitStatus st) {
        const QString err = m_process ? QString::fromLocal8Bit(m_process->readAllStandardError()).trimmed() : QString();
        if (QFileInfo::exists(m_expectedPdf) && QFileInfo(m_expectedPdf).size() > 0) {
            done(true, m_expectedPdf, {});
            return;
        }
        QString msg = QStringLiteral("LibreOffice could not convert the presentation to PDF");
        if (st == QProcess::CrashExit)
            msg += QStringLiteral(" (it crashed)");
        else if (code != 0)
            msg += QStringLiteral(" (exit code %1)").arg(code);
        if (!err.isEmpty())
            msg += QStringLiteral(": ") + err.left(300);
        done(false, {}, msg + QLatin1Char('.'), /*killed=*/st == QProcess::CrashExit);
    });
    m_process->setProgram(soffice);
    m_process->setArguments(args);
    m_process->start();
    m_timer.start(m_timeoutMs);
}

} // namespace Lux
