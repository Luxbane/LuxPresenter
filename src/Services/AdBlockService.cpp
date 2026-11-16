#include "Services/AdBlockService.h"
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QRegularExpression>
#include <QSaveFile>
namespace Lux {
namespace {
constexpr int kMinBytes = 20 * 1024;          // a real list is hundreds of kilobytes
constexpr qint64 kMaxBytes = 40LL * 1024 * 1024;
QString headerValue(const QString& text, const QString& key)
{
    const QRegularExpression re(QStringLiteral("^! *%1: *(.+)$").arg(QRegularExpression::escape(key)), QRegularExpression::MultilineOption);
    const auto m = re.match(text.left(4000));
    return m.hasMatch() ? m.captured(1).trimmed() : QString();
}
} // namespace
QList<AdBlockService::ListSpec> AdBlockService::defaultLists()
{
    return {{QStringLiteral("easylist"), QStringLiteral("EasyList"), QStringLiteral("easylist.txt"), QUrl(QStringLiteral("https://easylist.to/easylist/easylist.txt"))},
            {QStringLiteral("easyprivacy"), QStringLiteral("EasyPrivacy"), QStringLiteral("easyprivacy.txt"), QUrl(QStringLiteral("https://easylist.to/easylist/easyprivacy.txt"))}};
}
AdBlockService::AdBlockService(const QString& userDirectory, const QString& bundledDirectory, QObject* parent)
    : QObject(parent), m_userDir(userDirectory), m_bundledDir(bundledDirectory), m_specs(defaultLists())
{
}
void AdBlockService::setEnabled(bool on)
{
    if (m_enabled == on)
        return;
    m_enabled = on;
    emit changed();
}
void AdBlockService::setWhitelist(const QStringList& hosts)
{
    QStringList clean;
    for (QString h : hosts) {
        h = AdBlockEngine::hostOf(h.contains(QStringLiteral("://")) ? h : QStringLiteral("http://") + h.trimmed());
        if (!h.isEmpty() && !clean.contains(h))
            clean << h;
    }
    if (clean == m_whitelist)
        return;
    m_whitelist = clean;
    emit changed();
}
bool AdBlockService::isWhitelisted(const QString& pageUrl) const
{
    const QString host = AdBlockEngine::hostOf(pageUrl);
    for (const QString& w : m_whitelist)
        if (AdBlockEngine::hostMatches(host, w))
            return true;
    return false;
}
QString AdBlockService::validateList(const QByteArray& data)
{
    if (data.size() < kMinBytes)
        return QStringLiteral("the file is too small to be a filter list (%1 bytes)").arg(data.size());
    if (data.size() > kMaxBytes)
        return QStringLiteral("the file is unreasonably large");
    if (!data.left(64).trimmed().startsWith("[Adblock") && !data.left(64).trimmed().startsWith("[adblock"))
        return QStringLiteral("it does not start with an \"[Adblock Plus ...]\" header (probably not a filter list)");
    AdBlockEngine probe;
    const auto st = probe.addList(QString::fromUtf8(data));
    if (st.blockRules < 500)
        return QStringLiteral("it contains only %1 blocking rules").arg(st.blockRules);
    return {};
}
void AdBlockService::reload()
{
    m_engine.clear();
    m_infos.clear();
    for (const ListSpec& spec : m_specs) {
        ListInfo info;
        info.id = spec.id;
        info.title = spec.title;
        info.source = QStringLiteral("missing");
        const QList<QPair<QString, QString>> candidates{{QStringLiteral("downloaded"), QDir(m_userDir).filePath(spec.file)},
                                                         {QStringLiteral("bundled"), QDir(m_bundledDir).filePath(spec.file)}};
        for (const auto& c : candidates) {
            QFile f(c.second);
            if (!f.exists())
                continue;
            if (!f.open(QIODevice::ReadOnly)) {
                info.source = QStringLiteral("invalid");
                info.error = QStringLiteral("cannot read %1").arg(QDir::toNativeSeparators(c.second));
                continue;
            }
            const QByteArray data = f.readAll();
            f.close();
            const QString why = validateList(data);
            if (!why.isEmpty()) {
                info.source = QStringLiteral("invalid");
                info.error = QStringLiteral("%1: %2").arg(QFileInfo(c.second).fileName(), why);
                continue;   // a damaged downloaded copy falls back to the bundled one
            }
            const QString text = QString::fromUtf8(data);
            const auto st = m_engine.addList(text);
            info.source = c.first;
            info.error.clear();
            info.blockRules = st.blockRules;
            info.exceptionRules = st.exceptionRules;
            info.version = headerValue(text, QStringLiteral("Last modified"));
            if (info.version.isEmpty())
                info.version = headerValue(text, QStringLiteral("Version"));
            break;
        }
        m_infos << info;
    }
    emit changed();
}
QString AdBlockService::statusText() const
{
    if (m_infos.isEmpty())
        return QStringLiteral("No filter lists loaded.");
    QStringList parts;
    for (const ListInfo& i : m_infos) {
        if (i.source == QLatin1String("missing") || i.source == QLatin1String("invalid"))
            parts << QStringLiteral("%1: not available%2").arg(i.title, i.error.isEmpty() ? QString() : QStringLiteral(" (%1)").arg(i.error));
        else
            parts << QStringLiteral("%1: %2 rules, %3%4").arg(i.title).arg(i.blockRules + i.exceptionRules)
                         .arg(i.source == QLatin1String("downloaded") ? QStringLiteral("updated copy") : QStringLiteral("bundled copy"),
                              i.version.isEmpty() ? QString() : QStringLiteral(" (%1)").arg(i.version));
    }
    return parts.join(QStringLiteral("\n"));
}
bool AdBlockService::shouldBlock(const QString& url, const QString& pageUrl, quint32 type) const
{
    if (!m_enabled || isWhitelisted(pageUrl))
        return false;
    if (!m_engine.shouldBlock(url, pageUrl, type))
        return false;
    ++m_blocked;
    return true;
}
// ---- updating ----------------------------------------------------------------------------
void AdBlockService::updateLists()
{
    if (m_updating)
        return;
    m_updating = true;
    m_updateOk = true;
    m_updated = 0;
    m_updateMessages.clear();
    if (!m_net)
        m_net = new QNetworkAccessManager(this);
    fetch(0);
}
void AdBlockService::fetch(int index)
{
    if (index >= m_specs.size()) {
        finishUpdate();
        return;
    }
    const ListSpec spec = m_specs.at(index);
    QNetworkRequest req(spec.url);
    req.setTransferTimeout(30000);
    req.setAttribute(QNetworkRequest::RedirectPolicyAttribute, QNetworkRequest::NoLessSafeRedirectPolicy);
    req.setHeader(QNetworkRequest::UserAgentHeader, QStringLiteral("LuxPresenter filter-list update"));
    QNetworkReply* reply = m_net->get(req);
    connect(reply, &QNetworkReply::finished, this, [this, reply, spec, index] {
        reply->deleteLater();
        QString problem;
        if (reply->error() != QNetworkReply::NoError) {
            problem = reply->errorString();
        } else {
            const QByteArray data = reply->readAll();
            problem = validateList(data);
            if (problem.isEmpty()) {
                QDir().mkpath(m_userDir);
                QSaveFile out(QDir(m_userDir).filePath(spec.file));
                if (out.open(QIODevice::WriteOnly) && out.write(data) == data.size() && out.commit())
                    ++m_updated;
                else
                    problem = QStringLiteral("could not save the file (%1)").arg(out.errorString());
            }
        }
        if (!problem.isEmpty()) {
            m_updateOk = false;
            m_updateMessages << QStringLiteral("%1 was not updated: %2.").arg(spec.title, problem);
        }
        fetch(index + 1);
    });
}
void AdBlockService::finishUpdate()
{
    m_updating = false;
    reload();
    QString msg = m_updated > 0 ? QStringLiteral("%1 list(s) updated.").arg(m_updated) : QStringLiteral("No list was updated.");
    if (!m_updateMessages.isEmpty())
        msg += QLatin1Char(' ') + m_updateMessages.join(QLatin1Char(' ')) + QStringLiteral(" The previous lists stay in use.");
    emit updateFinished(m_updateOk, msg);
}
} // namespace Lux
