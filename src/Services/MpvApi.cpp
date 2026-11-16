#include "Services/MpvApi.h"

#include <QCoreApplication>
#include <QDir>
#include <QFileInfo>
#include <QLibrary>

namespace Lux {

MpvApi& MpvApi::instance()
{
    static MpvApi api;
    return api;
}

bool MpvApi::load(const QString& dllPath)
{
    if (m_loaded)
        return true;
    QString path = dllPath;
    if (path.isEmpty())
        path = QDir(QCoreApplication::applicationDirPath()).filePath(QStringLiteral("libmpv-2.dll"));
    if (!QFileInfo::exists(path)) {
        m_error = QStringLiteral("libmpv-2.dll not found: %1").arg(QDir::toNativeSeparators(path));
        return false;
    }
    if (!m_lib)
        m_lib = new QLibrary(path); // intentionally kept for the life of the process
    else
        m_lib->setFileName(path);
    if (!m_lib->load()) {
        m_error = m_lib->errorString();
        return false;
    }
    auto sym = [this](const char* n) { return m_lib->resolve(n); };
    create = reinterpret_cast<decltype(create)>(sym("mpv_create"));
    initialize = reinterpret_cast<decltype(initialize)>(sym("mpv_initialize"));
    terminateDestroy = reinterpret_cast<decltype(terminateDestroy)>(sym("mpv_terminate_destroy"));
    setOptionString = reinterpret_cast<decltype(setOptionString)>(sym("mpv_set_option_string"));
    setPropertyString = reinterpret_cast<decltype(setPropertyString)>(sym("mpv_set_property_string"));
    getPropertyString = reinterpret_cast<decltype(getPropertyString)>(sym("mpv_get_property_string"));
    getProperty = reinterpret_cast<decltype(getProperty)>(sym("mpv_get_property"));
    freeMem = reinterpret_cast<decltype(freeMem)>(sym("mpv_free"));
    command = reinterpret_cast<decltype(command)>(sym("mpv_command"));
    errorString = reinterpret_cast<decltype(errorString)>(sym("mpv_error_string"));
    clientApiVersion = reinterpret_cast<decltype(clientApiVersion)>(sym("mpv_client_api_version"));
    if (!create || !initialize || !terminateDestroy || !setOptionString || !setPropertyString || !getPropertyString ||
        !getProperty || !freeMem || !command) {
        m_error = QStringLiteral("libmpv-2.dll is missing required exports");
        m_lib->unload();
        return false;
    }
    m_path = path;
    m_loaded = true;
    m_error.clear();
    return true;
}

} // namespace Lux
