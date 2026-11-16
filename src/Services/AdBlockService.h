#pragma once
#include "Services/AdBlockEngine.h"
#include <QList>
#include <QObject>
#include <QPointer>
#include <QStringList>
#include <QUrl>
class QNetworkAccessManager;
class QNetworkReply;
namespace Lux {
// Ad blocker of the Online tab: the filter engine plus the on/off switch, the per-site exceptions and the filter
// lists (EasyList + EasyPrivacy). Lists come from <Data>/AdBlock (downloaded updates) or, when there is no valid
// downloaded copy, from the snapshot shipped with the program (<app>/Data/AdBlock). An update is downloaded to memory,
// validated and only then written (atomically) over the previous copy, so a failed update never damages a list.
class AdBlockService : public QObject
{
    Q_OBJECT
public:
    struct ListSpec { QString id, title, file; QUrl url; };
    struct ListInfo
    {
        QString id, title;
        QString source;            // "downloaded", "bundled", "missing", "invalid"
        QString version;           // from the list's own header ("Last modified" / "Version")
        int blockRules = 0, exceptionRules = 0;
        QString error;
    };
    AdBlockService(const QString& userDirectory, const QString& bundledDirectory, QObject* parent = nullptr);
    static QList<ListSpec> defaultLists();
    void setLists(const QList<ListSpec>& lists) { m_specs = lists; }   // tests
    bool enabled() const { return m_enabled; }
    void setEnabled(bool on);
    QStringList whitelist() const { return m_whitelist; }
    void setWhitelist(const QStringList& hosts);
    bool isWhitelisted(const QString& pageUrl) const;
    void reload();                                       // (re)reads the lists from disk
    QList<ListInfo> lists() const { return m_infos; }
    int ruleCount() const { return m_engine.blockRuleCount(); }
    QString statusText() const;
    // The decision for one request of a page. Off, or the page's site is on the exception list: never blocked.
    bool shouldBlock(const QString& url, const QString& pageUrl, quint32 type) const;
    int blockedCount() const { return m_blocked; }
    void resetCounter() { m_blocked = 0; }
    // Unrequested pop-ups: blocked while the blocker is on (and the page is not whitelisted).
    bool blocksPopupsFor(const QString& pageUrl) const { return m_enabled && !isWhitelisted(pageUrl); }
    void noteBlockedPopup() { ++m_blocked; }
    void updateLists();                                  // asynchronous; emits updateFinished
    bool updating() const { return m_updating; }
    static QString validateList(const QByteArray& data);  // "" = valid, else why not
signals:
    void changed();
    void updateFinished(bool allOk, const QString& message);
private:
    void fetch(int index);
    void finishUpdate();
    QString m_userDir, m_bundledDir;
    QList<ListSpec> m_specs;
    QList<ListInfo> m_infos;
    AdBlockEngine m_engine;
    bool m_enabled = true;
    QStringList m_whitelist;
    mutable int m_blocked = 0;
    bool m_updating = false;
    QNetworkAccessManager* m_net = nullptr;
    QStringList m_updateMessages;
    bool m_updateOk = true;
    int m_updated = 0;
};
} // namespace Lux
