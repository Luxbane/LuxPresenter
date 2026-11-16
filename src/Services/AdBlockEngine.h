#pragma once
#include <QHash>
#include <QString>
#include <QStringList>
#include <QVector>
namespace Lux {
// Network-request filter for the Online browser, reading the Adblock Plus filter syntax used by EasyList and
// EasyPrivacy: "||domain^", "|" anchors, "*" wildcards, "^" separators, "@@" exceptions and the options
// third-party, domain=, important and the resource types. Page-element ("cosmetic") rules (##, #@#, ...) and options
// that need browser support this engine does not have (redirect, removeparam, csp, header, replace ...) are skipped
// when a list is read: such rules cannot be honoured, and applying them without their options would block too much.
class AdBlockEngine
{
public:
    enum Type : quint32 {
        Other = 1, Script = 2, Image = 4, Stylesheet = 8, XmlHttp = 16, Subdocument = 32, Media = 64, Font = 128,
        WebSocket = 256, Ping = 512, Object = 1024, Document = 2048, Popup = 4096,
        DefaultTypes = 0x3FF | Object   // everything except Document and Popup (the ABP default)
    };
    struct ListStats { int blockRules = 0, exceptionRules = 0, skipped = 0; };
    void clear();
    ListStats addList(const QString& text);       // may be called for several lists
    int ruleCount() const { return m_block.rules.size() + m_except.rules.size(); }
    int blockRuleCount() const { return m_block.rules.size(); }
    // Should the request be cancelled? `documentUrl` is the page that makes it (empty for the top-level page).
    bool shouldBlock(const QString& url, const QString& documentUrl, quint32 type, QString* matchedRule = nullptr) const;
    // helpers (also used by the service's whitelist)
    static QString hostOf(const QString& url);
    static QString registrableDomain(const QString& host);          // approximation of "eTLD+1" without a public suffix list
    static bool hostMatches(const QString& host, const QString& domain);   // host == domain or a sub-domain of it
private:
    struct Rule
    {
        QString pattern;                 // lower case; may contain * and ^
        QString raw;
        bool anchorDomain = false, anchorStart = false, anchorEnd = false, important = false;
        quint32 types = DefaultTypes;
        int party = 0;                   // 0 any, 1 third-party only, 2 first-party only
        QStringList includeDomains, excludeDomains;
    };
    struct Set
    {
        QVector<Rule> rules;
        QHash<QString, QVector<int>> index;   // token -> rules
        QVector<int> generic;                 // rules without a usable token
    };
    static bool parseRule(const QString& line, Rule* rule, bool* exception, bool* skipped);
    static void addToSet(Set& set, const Rule& rule);
    bool matchSet(const Set& set, const QString& urlLower, int hostStart, int hostEnd, const QString& docHost, bool third, quint32 type,
                  bool onlyImportant, QString* rule) const;
    static bool ruleMatches(const Rule& r, const QString& url, int hostStart, int hostEnd);
    Set m_block, m_except;
};
} // namespace Lux
