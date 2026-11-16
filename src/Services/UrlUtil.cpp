#include "Services/UrlUtil.h"

#include <QUrl>

namespace Lux {

bool isHomeUrl(const QString& url)
{
    return url.isEmpty() || url == QLatin1String("about:blank") || url.startsWith(QLatin1String("data:text/html"));
}

QString normalizeBrowserInput(const QString& input)
{
    QString url = input.trimmed();
    if (url.isEmpty() || url.compare(QLatin1String("about:blank"), Qt::CaseInsensitive) == 0 ||
        url.compare(QLatin1String("home"), Qt::CaseInsensitive) == 0)
        return {};
    if (!url.startsWith(QLatin1String("http://"), Qt::CaseInsensitive) && !url.startsWith(QLatin1String("https://"), Qt::CaseInsensitive)) {
        if (url.contains(QLatin1Char('.')) && !url.contains(QLatin1Char(' ')))
            url = QStringLiteral("https://") + url;
        else
            url = QStringLiteral("https://www.google.com/search?q=") + QString::fromLatin1(QUrl::toPercentEncoding(url));
    }
    return url;
}

} // namespace Lux
