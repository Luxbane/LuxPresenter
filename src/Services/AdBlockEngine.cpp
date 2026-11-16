#include "Services/AdBlockEngine.h"
#include <QSet>
#include <QUrl>
#include <algorithm>
namespace Lux {
namespace {
bool isSeparator(QChar c)
{
    return !(c.isLetterOrNumber() || c == QLatin1Char('_') || c == QLatin1Char('-') || c == QLatin1Char('.') || c == QLatin1Char('%'));
}
bool isTokenChar(QChar c) { return c.isLetterOrNumber() || c == QLatin1Char('%'); }
// Matches pattern p[pi..] against u starting exactly at upos. '*' = any run, '^' = separator or end of URL.
bool glob(const QString& u, int upos, const QString& p, int pi, bool endAnchor)
{
    const int ul = u.size(), pl = p.size();
    while (pi < pl) {
        const QChar pc = p.at(pi);
        if (pc == QLatin1Char('*')) {
            while (pi < pl && p.at(pi) == QLatin1Char('*'))
                ++pi;
            if (pi == pl)
                return true;   // a trailing * matches the rest
            for (int k = upos; k <= ul; ++k)
                if (glob(u, k, p, pi, endAnchor))
                    return true;
            return false;
        }
        if (pc == QLatin1Char('^')) {
            if (upos == ul)
                return pi + 1 == pl;          // the end of the URL counts as a separator, once
            if (!isSeparator(u.at(upos)))
                return false;
            ++upos;
            ++pi;
            continue;
        }
        if (upos >= ul || u.at(upos) != pc)
            return false;
        ++upos;
        ++pi;
    }
    return !endAnchor || upos == ul;
}
QStringList splitDomains(const QString& s)
{
    return s.split(QLatin1Char('|'), Qt::SkipEmptyParts);
}
quint32 typeFromName(const QString& n)
{
    using T = AdBlockEngine;
    static const QHash<QString, quint32> map{
        {QStringLiteral("script"), T::Script}, {QStringLiteral("image"), T::Image}, {QStringLiteral("stylesheet"), T::Stylesheet},
        {QStringLiteral("css"), T::Stylesheet}, {QStringLiteral("xmlhttprequest"), T::XmlHttp}, {QStringLiteral("xhr"), T::XmlHttp},
        {QStringLiteral("subdocument"), T::Subdocument}, {QStringLiteral("frame"), T::Subdocument}, {QStringLiteral("media"), T::Media},
        {QStringLiteral("font"), T::Font}, {QStringLiteral("websocket"), T::WebSocket}, {QStringLiteral("ping"), T::Ping},
        {QStringLiteral("object"), T::Object}, {QStringLiteral("other"), T::Other}, {QStringLiteral("document"), T::Document},
        {QStringLiteral("doc"), T::Document}, {QStringLiteral("popup"), T::Popup}};
    return map.value(n, 0);
}
} // namespace
void AdBlockEngine::clear()
{
    m_block = Set();
    m_except = Set();
}
QString AdBlockEngine::hostOf(const QString& url)
{
    int s = url.indexOf(QStringLiteral("://"));
    s = s < 0 ? 0 : s + 3;
    int e = url.size();
    for (int i = s; i < url.size(); ++i) {
        const QChar c = url.at(i);
        if (c == QLatin1Char('/') || c == QLatin1Char('?') || c == QLatin1Char('#')) { e = i; break; }
    }
    QString host = url.mid(s, e - s);
    const int at = host.lastIndexOf(QLatin1Char('@'));
    if (at >= 0)
        host.remove(0, at + 1);
    const int colon = host.lastIndexOf(QLatin1Char(':'));
    if (colon >= 0 && !host.endsWith(QLatin1Char(']')))
        host.truncate(colon);
    return host.toLower();
}
bool AdBlockEngine::hostMatches(const QString& host, const QString& domain)
{
    return !domain.isEmpty() && (host == domain || host.endsWith(QLatin1Char('.') + domain));
}
QString AdBlockEngine::registrableDomain(const QString& host)
{
    const QStringList l = host.split(QLatin1Char('.'), Qt::SkipEmptyParts);
    if (l.size() <= 2)
        return host;
    static const QSet<QString> second{QStringLiteral("co"), QStringLiteral("com"), QStringLiteral("org"), QStringLiteral("net"),
                                      QStringLiteral("gov"), QStringLiteral("edu"), QStringLiteral("ac"), QStringLiteral("or"), QStringLiteral("ne")};
    const bool cc = l.last().size() == 2 && second.contains(l.at(l.size() - 2));
    const int keep = cc ? 3 : 2;
    return l.mid(l.size() - keep).join(QLatin1Char('.'));
}
bool AdBlockEngine::parseRule(const QString& lineIn, Rule* r, bool* exception, bool* skipped)
{
    *skipped = false;
    QString line = lineIn.trimmed();
    if (line.isEmpty() || line.startsWith(QLatin1Char('!')) || line.startsWith(QLatin1Char('[')))
        return false;
    // page-element rules
    if (line.contains(QStringLiteral("##")) || line.contains(QStringLiteral("#@#")) || line.contains(QStringLiteral("#?#")) ||
        line.contains(QStringLiteral("#$#")) || line.contains(QStringLiteral("#%#")) || line.contains(QStringLiteral("#@$#"))) {
        return false;
    }
    r->raw = line;
    *exception = false;
    if (line.startsWith(QStringLiteral("@@"))) {
        *exception = true;
        line.remove(0, 2);
    }
    // options
    QString opts;
    const int dollar = line.lastIndexOf(QLatin1Char('$'));
    if (dollar >= 0 && !(line.startsWith(QLatin1Char('/')) && line.endsWith(QLatin1Char('/')))) {
        opts = line.mid(dollar + 1);
        line.truncate(dollar);
    }
    if (line.size() >= 2 && line.startsWith(QLatin1Char('/')) && line.endsWith(QLatin1Char('/'))) {
        *skipped = true;   // regular-expression rules are not supported
        return false;
    }
    quint32 include = 0, exclude = 0;
    bool anyType = false;
    for (const QString& oRaw : opts.split(QLatin1Char(','), Qt::SkipEmptyParts)) {
        QString o = oRaw.trimmed().toLower();
        bool neg = false;
        if (o.startsWith(QLatin1Char('~'))) {
            neg = true;
            o.remove(0, 1);
        }
        if (o == QLatin1String("third-party") || o == QLatin1String("3p"))
            r->party = neg ? 2 : 1;
        else if (o == QLatin1String("first-party") || o == QLatin1String("1p"))
            r->party = neg ? 1 : 2;
        else if (o == QLatin1String("important"))
            r->important = true;
        else if (o == QLatin1String("match-case"))
            continue;
        else if (o.startsWith(QStringLiteral("domain="))) {
            for (const QString& d : splitDomains(o.mid(7))) {
                if (d.startsWith(QLatin1Char('~')))
                    r->excludeDomains << d.mid(1);
                else
                    r->includeDomains << d;
            }
        } else if (o == QLatin1String("all")) {
            include |= 0x1FFF;
            anyType = true;
        } else if (const quint32 t = typeFromName(o)) {
            (neg ? exclude : include) |= t;
            anyType = anyType || !neg;
        } else {
            *skipped = true;   // redirect, removeparam, csp, header, replace, denyallow ... cannot be honoured
            return false;
        }
    }
    r->types = anyType ? include : (DefaultTypes & ~exclude);
    if (anyType)
        r->types &= ~exclude;
    // pattern
    if (line.startsWith(QStringLiteral("||"))) {
        r->anchorDomain = true;
        line.remove(0, 2);
    } else if (line.startsWith(QLatin1Char('|'))) {
        r->anchorStart = true;
        line.remove(0, 1);
    }
    if (line.endsWith(QLatin1Char('|'))) {
        r->anchorEnd = true;
        line.chop(1);
    }
    r->pattern = line.toLower();
    if (r->pattern.isEmpty() && !r->anchorDomain && r->includeDomains.isEmpty()) {
        *skipped = true;   // would match everything
        return false;
    }
    return true;
}
void AdBlockEngine::addToSet(Set& set, const Rule& rule)
{
    const int idx = set.rules.size();
    set.rules.append(rule);
    // index by the longest token that is delimited inside the pattern (so it equals a whole token of a matching URL)
    const QString& p = rule.pattern;
    QString best;
    for (int i = 0; i < p.size();) {
        if (!isTokenChar(p.at(i))) { ++i; continue; }
        int j = i;
        while (j < p.size() && isTokenChar(p.at(j)))
            ++j;
        const bool leftOk = (i > 0 && p.at(i - 1) != QLatin1Char('*')) || (i == 0 && (rule.anchorDomain || rule.anchorStart));
        const bool rightOk = (j < p.size() && p.at(j) != QLatin1Char('*')) || (j == p.size() && rule.anchorEnd);
        if (leftOk && rightOk && j - i > best.size())
            best = p.mid(i, j - i);
        i = j;
    }
    if (best.size() >= 2)
        set.index[best].append(idx);
    else
        set.generic.append(idx);
}
AdBlockEngine::ListStats AdBlockEngine::addList(const QString& text)
{
    ListStats st;
    for (const QStringView line : QStringView(text).split(QLatin1Char('\n'))) {
        Rule r;
        bool exception = false, skipped = false;
        if (!parseRule(line.toString(), &r, &exception, &skipped)) {
            st.skipped += skipped ? 1 : 0;
            continue;
        }
        addToSet(exception ? m_except : m_block, r);
        (exception ? st.exceptionRules : st.blockRules)++;
    }
    return st;
}
bool AdBlockEngine::ruleMatches(const Rule& r, const QString& url, int hostStart, int hostEnd)
{
    if (r.anchorDomain) {
        for (int pos = hostStart; pos < hostEnd; ++pos) {
            if (pos != hostStart && url.at(pos - 1) != QLatin1Char('.'))
                continue;
            if (glob(url, pos, r.pattern, 0, r.anchorEnd))
                return true;
        }
        return false;
    }
    if (r.anchorStart)
        return glob(url, 0, r.pattern, 0, r.anchorEnd);
    for (int pos = 0; pos <= url.size(); ++pos)
        if (glob(url, pos, r.pattern, 0, r.anchorEnd))
            return true;
    return false;
}
bool AdBlockEngine::matchSet(const Set& set, const QString& url, int hostStart, int hostEnd, const QString& docHost, bool third,
                             quint32 type, bool onlyImportant, QString* matched) const
{
    auto test = [&](int idx) {
        const Rule& r = set.rules.at(idx);
        if (!(r.types & type) || (onlyImportant && !r.important))
            return false;
        if ((r.party == 1 && !third) || (r.party == 2 && third))
            return false;
        if (!r.includeDomains.isEmpty()) {
            bool any = false;
            for (const QString& d : r.includeDomains)
                any = any || hostMatches(docHost, d);
            if (!any)
                return false;
        }
        for (const QString& d : r.excludeDomains)
            if (hostMatches(docHost, d))
                return false;
        if (!ruleMatches(r, url, hostStart, hostEnd))
            return false;
        if (matched)
            *matched = r.raw;
        return true;
    };
    // tokens of the URL
    QSet<QString> seen;
    for (int i = 0; i < url.size();) {
        if (!isTokenChar(url.at(i))) { ++i; continue; }
        int j = i;
        while (j < url.size() && isTokenChar(url.at(j)))
            ++j;
        const QString tok = url.mid(i, j - i);
        i = j;
        if (tok.size() < 2 || seen.contains(tok))
            continue;
        seen.insert(tok);
        const auto it = set.index.constFind(tok);
        if (it != set.index.constEnd())
            for (int idx : it.value())
                if (test(idx))
                    return true;
    }
    for (int idx : set.generic)
        if (test(idx))
            return true;
    return false;
}
bool AdBlockEngine::shouldBlock(const QString& urlIn, const QString& documentUrl, quint32 type, QString* matchedRule) const
{
    if (m_block.rules.isEmpty() || urlIn.isEmpty())
        return false;
    const QString url = urlIn.toLower();
    if (!(url.startsWith(QStringLiteral("http")) || url.startsWith(QStringLiteral("ws"))))
        return false;   // data:, blob:, file:, about: are never filtered
    const int s = url.indexOf(QStringLiteral("://")) + 3;
    int e = url.size();
    for (int i = s; i < url.size(); ++i)
        if (url.at(i) == QLatin1Char('/') || url.at(i) == QLatin1Char('?') || url.at(i) == QLatin1Char('#') || url.at(i) == QLatin1Char(':')) { e = i; break; }
    const QString host = url.mid(s, e - s);
    const QString docHost = hostOf(documentUrl.toLower());
    const bool third = !docHost.isEmpty() && registrableDomain(host) != registrableDomain(docHost);
    // a "$document" exception ( @@||site^$document ) switches blocking off for the whole page
    if (!docHost.isEmpty()) {
        const QString doc = documentUrl.toLower();
        const int ds = doc.indexOf(QStringLiteral("://")) + 3;
        int de = doc.size();
        for (int i = ds; i < doc.size(); ++i)
            if (doc.at(i) == QLatin1Char('/') || doc.at(i) == QLatin1Char('?') || doc.at(i) == QLatin1Char('#') || doc.at(i) == QLatin1Char(':')) { de = i; break; }
        if (matchSet(m_except, doc, ds, de, docHost, false, Document, false, nullptr))
            return false;
    }
    QString rule;
    if (!matchSet(m_block, url, s, e, docHost, third, type, false, &rule))
        return false;
    // exceptions win, except over "$important" block rules
    if (!matchSet(m_block, url, s, e, docHost, third, type, true, nullptr) && matchSet(m_except, url, s, e, docHost, third, type, false, nullptr))
        return false;
    if (matchedRule)
        *matchedRule = rule;
    return true;
}
} // namespace Lux
