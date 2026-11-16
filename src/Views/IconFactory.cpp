#include "Views/IconFactory.h"

#include <QPainter>
#include <QSvgRenderer>

namespace Lux {

QString IconFactory::svg(Icon icon, const Theme& theme)
{
    const QString text = theme.textPrimary.name();
    const QString text2 = theme.textSecondary.name();
    QString body;
    switch (icon) {
    case Icon::Add:
        body = QStringLiteral(R"(<path d="M8 2v12 M2 8h12" stroke="#22C55E" stroke-width="2.6" stroke-linecap="round" fill="none"/>)");
        break;
    case Icon::Edit:
        body = QStringLiteral(R"(<path d="M11.5 2.5a1.414 1.414 0 0 1 2 2L5 13H2.5v-2.5L11.5 2.5z" fill="#38BDF8" stroke="#0284C7" stroke-width="0.6"/>
<line x1="9.5" y1="4.5" x2="11.5" y2="6.5" stroke="#0284C7" stroke-width="0.6"/>)");
        break;
    case Icon::Display:
        body = QStringLiteral(R"(<rect x="1.5" y="2" width="13" height="9" rx="1" ry="1" fill="#60A5FA" stroke="#2563EB" stroke-width="0.8"/>
<rect x="3" y="3.5" width="10" height="6" fill="#1E293B"/>
<path d="M8 11v3 M5 14h6" stroke="%1" stroke-width="1.2" stroke-linecap="round" fill="none"/>)").arg(text);
        break;
    case Icon::Globe:
        body = QStringLiteral(R"(<circle cx="8" cy="8" r="6.5" stroke="#10B981" stroke-width="1.4" fill="none"/>
<path d="M1.5 8h13 M8 1.5c-2.5 3-2.5 10 0 13 M8 1.5c2.5 3 2.5 10 0 13" stroke="#10B981" stroke-width="1.1" fill="none"/>)");
        break;
    case Icon::Donate:
        body = QStringLiteral(R"(<path d="M3 5h8v5a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V5z M11 6h1.5a1.5 1.5 0 0 1 0 3H11" stroke="#F59E0B" fill="#FBBF24" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M5 2c0 1 1 1.5 1 2.5 M8 1.5c0 1 1 1.5 1 2.5" stroke="#F59E0B" stroke-width="1" stroke-linecap="round" fill="none"/>)");
        break;
    case Icon::Background:
        body = QStringLiteral(R"(<rect x="1.5" y="2" width="13" height="12" rx="1.5" ry="1.5" stroke="#818CF8" stroke-width="1.2" fill="#6366F1" fill-opacity="0.2"/>
<rect x="1.5" y="2" width="13" height="12" rx="1.5" ry="1.5" stroke="#818CF8" stroke-width="1.2" fill="none"/>
<circle cx="11.25" cy="5.75" r="1.25" fill="#F59E0B"/>
<path d="M3 12.5l3.5-4 2.5 3 2-2 3.5 3" stroke="#818CF8" stroke-width="1.2" stroke-linejoin="round" fill="none"/>)");
        break;
    case Icon::Alert:
        body = QStringLiteral(R"(<path d="M8 1.5l7 12H1z" fill="#FEF08A" stroke="#EAB308" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M8 5.5v4 M8 11.5v0.01" stroke="#854D0E" stroke-width="1.4" stroke-linecap="round" fill="none"/>)");
        break;
    case Icon::Logo:
        body = QStringLiteral(R"(<rect x="1" y="2" width="14" height="10" rx="1" ry="1" stroke="%1" stroke-width="1.2" fill="none"/>
<path d="M8 4v6 M6 6h4" stroke="#38BDF8" stroke-width="1.4" stroke-linecap="round" fill="none"/>
<path d="M8 12v2 M5 14h6" stroke="%1" stroke-width="1.2" fill="none"/>)").arg(text);
        break;
    case Icon::BlackSquare:
        body = QStringLiteral(R"(<rect x="0.5" y="0.5" width="15" height="15" rx="1.5" ry="1.5" fill="#000000" stroke="%1" stroke-width="1.2"/>)").arg(text2);
        break;
    case Icon::Clear:
        body = QStringLiteral(R"(<circle cx="8" cy="8" r="6.5" stroke="#EF4444" stroke-width="1.5" fill="none"/>
<line x1="3.5" y1="12.5" x2="12.5" y2="3.5" stroke="#EF4444" stroke-width="1.5"/>)");
        break;
    }
    return QStringLiteral(R"(<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" width="16" height="16">)")
           + body + QStringLiteral("</svg>");
}

QPixmap IconFactory::pixmap(Icon icon, int w, int h, qreal dpr, const Theme& theme)
{
    QSvgRenderer renderer(svg(icon, theme).toUtf8());
    QPixmap pm(qRound(w * dpr), qRound(h * dpr));
    pm.fill(Qt::transparent);
    QPainter p(&pm);
    p.setRenderHint(QPainter::Antialiasing);
    renderer.render(&p, QRectF(0, 0, pm.width(), pm.height()));
    p.end();
    pm.setDevicePixelRatio(dpr);
    return pm;
}

} // namespace Lux
