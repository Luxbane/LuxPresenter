#pragma once

// Test helpers: which processes run from a folder, and which windows they show. Used to prove that the bundled
// LibreOffice stays invisible and leaves no process behind.
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <tlhelp32.h>

#include <QDir>
#include <QList>
#include <QObject>
#include <QStringList>
#include <QTimer>

namespace LuxTest {

inline QString exePathOf(DWORD pid)
{
    QString result;
    if (HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid)) {
        wchar_t buf[2048];
        DWORD n = 2048;
        if (QueryFullProcessImageNameW(h, 0, buf, &n))
            result = QString::fromWCharArray(buf, int(n));
        CloseHandle(h);
    }
    return result;
}

inline bool isUnder(const QString& exe, const QString& dir)
{
    const QString d = QDir::toNativeSeparators(QDir(dir).absolutePath()).toLower() + QLatin1Char('\\');
    return QDir::toNativeSeparators(exe).toLower().startsWith(d);
}

// Processes (pid + exe) running from `dir`.
inline QStringList processesUnder(const QString& dir)
{
    QStringList out;
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE)
        return out;
    PROCESSENTRY32W pe{};
    pe.dwSize = sizeof(pe);
    for (BOOL ok = Process32FirstW(snap, &pe); ok; ok = Process32NextW(snap, &pe)) {
        const QString exe = exePathOf(pe.th32ProcessID);
        if (!exe.isEmpty() && isUnder(exe, dir))
            out << QStringLiteral("%1:%2").arg(pe.th32ProcessID).arg(exe);
    }
    CloseHandle(snap);
    return out;
}

// Visible top-level windows owned by processes running from `dir` (title, class).
inline QStringList visibleWindowsOf(const QString& dir)
{
    struct Ctx { QString dir; QStringList found; } ctx{dir, {}};
    EnumWindows(
        [](HWND h, LPARAM lp) -> BOOL {
            auto* c = reinterpret_cast<Ctx*>(lp);
            if (!IsWindowVisible(h))
                return TRUE;
            DWORD pid = 0;
            GetWindowThreadProcessId(h, &pid);
            const QString exe = exePathOf(pid);
            if (exe.isEmpty() || !isUnder(exe, c->dir))
                return TRUE;
            wchar_t title[256] = {}, cls[256] = {};
            GetWindowTextW(h, title, 255);
            GetClassNameW(h, cls, 255);
            c->found << QStringLiteral("%1 [%2] (%3)").arg(QString::fromWCharArray(title), QString::fromWCharArray(cls), exe);
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&ctx));
    return ctx.found;
}

// Polls for visible windows of `dir` while it is alive and remembers everything it saw.
class WindowWatcher : public QObject
{
public:
    explicit WindowWatcher(const QString& dir) : m_dir(dir)
    {
        m_timer.setInterval(15);
        QObject::connect(&m_timer, &QTimer::timeout, &m_timer, [this] {
            ++m_polls;
            for (const QString& w : visibleWindowsOf(m_dir))
                if (!m_seen.contains(w))
                    m_seen << w;
            if (!processesUnder(m_dir).isEmpty())
                ++m_pollsWithProcess;
        });
        m_timer.start();
    }
    QStringList seen() const { return m_seen; }
    int polls() const { return m_polls; }
    int pollsWithProcess() const { return m_pollsWithProcess; }   // proves the watcher overlapped a running LibreOffice

private:
    QString m_dir;
    QTimer m_timer;
    QStringList m_seen;
    int m_polls = 0, m_pollsWithProcess = 0;
};

} // namespace LuxTest
