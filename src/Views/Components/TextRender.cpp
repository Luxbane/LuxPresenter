#include "Views/Components/TextRender.h"

#include <QFontMetricsF>
#include <QPainterPath>
#include <QTextLayout>

#include <algorithm>
#include <cmath>

namespace Lux {

namespace {

struct Line
{
    QString text;
    qreal width = 0;
    qreal ascent = 0;
    qreal height = 0;
};

QList<Line> layoutLines(const QString& text, const QFont& font, qreal maxWidth, bool wrap)
{
    QList<Line> out;
    const QFontMetricsF fm(font);
    const QStringList paragraphs = text.split(QLatin1Char('\n'));
    for (const QString& para : paragraphs) {
        if (para.trimmed().isEmpty()) {
            out.append({QString(), 0, fm.ascent(), fm.height()});
            continue;
        }
        QTextLayout layout(para, font);
        QTextOption opt;
        opt.setWrapMode(wrap ? QTextOption::WordWrap : QTextOption::NoWrap);
        layout.setTextOption(opt);
        layout.beginLayout();
        for (;;) {
            QTextLine line = layout.createLine();
            if (!line.isValid())
                break;
            line.setLineWidth(wrap ? maxWidth : 1.0e6);
            out.append({para.mid(line.textStart(), line.textLength()).trimmed(), line.naturalTextWidth(),
                        line.ascent(), line.height()});
        }
        layout.endLayout();
    }
    return out;
}

} // namespace

TextBlockMetrics measureTextBlock(const QString& text, const TextBlockStyle& style)
{
    TextBlockMetrics m;
    const qreal w = std::max<qreal>(50, style.width - style.strokeThickness * 2);
    for (const Line& l : layoutLines(text, style.font, w, style.wrap)) {
        m.width = std::max(m.width, l.width);
        m.height += l.height;
    }
    return m;
}

void drawTextBlock(QPainter* p, const QString& text, const TextBlockStyle& style, const QPointF& origin)
{
    const qreal w = std::max<qreal>(50, style.width - style.strokeThickness * 2);
    const QList<Line> lines = layoutLines(text, style.font, w, style.wrap);

    QPainterPath all;
    qreal y = style.strokeThickness;
    for (const Line& l : lines) {
        if (!l.text.isEmpty()) {
            qreal x = style.strokeThickness;
            if (style.hAlign & Qt::AlignHCenter)
                x = (style.width - l.width) / 2;
            else if (style.hAlign & Qt::AlignRight)
                x = style.width - style.strokeThickness - l.width;
            all.addText(QPointF(x, y + l.ascent), style.font, l.text);
        }
        y += l.height;
    }
    all.translate(origin);

    p->save();
    p->setRenderHint(QPainter::Antialiasing);
    if (style.shadow) {
        // Soft drop shadow: ~3.5 units toward the lower right (WPF Direction 315, depth 3.5, blur 8).
        const qreal d = style.shadowDepth * 0.70710678;
        for (int i = 0; i < 4; ++i) {
            const qreal grow = i * 0.9 * (style.shadowDepth / 3.5);
            QPainterPath shifted = all.translated(d, d);
            p->setPen(QPen(QColor(0, 0, 0, 52), style.strokeThickness + grow * 2, Qt::SolidLine, Qt::RoundCap, Qt::RoundJoin));
            p->setBrush(QColor(0, 0, 0, 52));
            p->drawPath(shifted);
        }
    }
    if (style.outline && style.strokeThickness > 0) {
        p->setPen(QPen(Qt::black, style.strokeThickness, Qt::SolidLine, Qt::RoundCap, Qt::RoundJoin));
        p->setBrush(Qt::NoBrush);
        p->drawPath(all);
    }
    p->setPen(Qt::NoPen);
    p->setBrush(Qt::white);
    p->drawPath(all);
    p->restore();
}

qreal optimalFontSize(const QString& text, QFont font, qreal maxW, qreal maxH, qreal targetMaxFs, qreal minFs, bool wrap)
{
    if (text.trimmed().isEmpty())
        return targetMaxFs;
    qreal low = minFs, high = targetMaxFs, best = minFs;
    for (int i = 0; i < 7; ++i) {
        const qreal mid = (low + high) / 2.0;
        font.setPixelSize(std::max(1, static_cast<int>(std::lround(mid))));
        TextBlockStyle st;
        st.font = font;
        st.width = std::max<qreal>(100, maxW);
        st.strokeThickness = 0;
        st.wrap = wrap;
        const TextBlockMetrics m = measureTextBlock(text, st);
        if (m.height <= maxH && m.width <= maxW) {
            best = mid;
            low = mid + 0.5;
        } else {
            high = mid - 0.5;
        }
    }
    return std::round(best * 10.0) / 10.0;
}

LabelPlacement labelPlacement(const QString& location)
{
    LabelPlacement p;
    p.top = location.startsWith(QStringLiteral("Top"), Qt::CaseInsensitive);
    p.h = location.endsWith(QStringLiteral("Left"), Qt::CaseInsensitive)    ? Qt::AlignLeft
          : location.endsWith(QStringLiteral("Right"), Qt::CaseInsensitive) ? Qt::AlignRight
                                                                             : Qt::AlignHCenter;
    return p;
}
qreal labelPixelSize(const QString& size)
{
    if (size.compare(QStringLiteral("Small"), Qt::CaseInsensitive) == 0)
        return 28;
    if (size.compare(QStringLiteral("Large"), Qt::CaseInsensitive) == 0)
        return 48;
    return 36;
}
} // namespace Lux

