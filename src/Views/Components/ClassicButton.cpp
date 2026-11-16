#include "Views/Components/ClassicButton.h"

#include "Services/ThemeService.h"

#include <QFontMetrics>
#include <QPainter>

namespace Lux {

ClassicButton::ClassicButton(Kind kind, QWidget* parent) : QAbstractButton(parent), m_kind(kind)
{
    setCursor(Qt::PointingHandCursor);
    setFocusPolicy(Qt::TabFocus);
    setCheckable(kind == Kind::Toggle || kind == Kind::Live);
    connect(&ThemeService::instance(), &ThemeService::themeChanged, this, qOverload<>(&QWidget::update));
}

ClassicButton& ClassicButton::addIcon(Icon icon, int w, int h, int gapAfter)
{
    Part p;
    p.isIcon = true;
    p.icon = icon;
    p.iconSize = QSize(w, h);
    p.gapAfter = gapAfter;
    m_parts.append(p);
    updateGeometry();
    return *this;
}

ClassicButton& ClassicButton::addText(const QString& text, const QFont& font, ColorRole role, int gapAfter, QColor fixed)
{
    Part p;
    p.text = text;
    p.font = font;
    p.role = role;
    p.fixed = fixed;
    p.gapAfter = gapAfter;
    m_parts.append(p);
    updateGeometry();
    return *this;
}

void ClassicButton::clearParts()
{
    m_parts.clear();
    updateGeometry();
    update();
}

void ClassicButton::setTextPart(int index, const QString& text, QColor fixed)
{
    int n = 0;
    for (Part& p : m_parts) {
        if (p.isIcon)
            continue;
        if (n++ == index) {
            p.text = text;
            if (fixed.isValid()) {
                p.fixed = fixed;
                p.role = ColorRole::Fixed;
            }
            break;
        }
    }
    updateGeometry();
    update();
}

QMargins ClassicButton::outerMargins() const
{
    switch (m_kind) {
    case Kind::Icon: return {1, 1, 1, 1};
    case Kind::Toggle: return {2, 0, 2, 0};
    case Kind::Framed: return {0, 0, 8, 0};
    case Kind::Live: return {6, 1, 2, 1};
    }
    return {};
}

QMargins ClassicButton::padding() const
{
    switch (m_kind) {
    case Kind::Icon: return {4, 2, 4, 2};
    case Kind::Toggle: return {6, 2, 6, 2};
    case Kind::Framed: return {8, 2, 8, 2};
    case Kind::Live: return {16, 3, 16, 3};
    }
    return {};
}

int ClassicButton::fixedHeight() const
{
    return m_kind == Kind::Live ? 26 : 28; // Live: 28 incl. 1px top/bottom margin
}

int ClassicButton::contentWidth() const
{
    int w = 0;
    for (const Part& p : m_parts) {
        w += p.isIcon ? p.iconSize.width() : QFontMetrics(p.font).horizontalAdvance(p.text);
        w += p.gapAfter;
    }
    return w;
}

QSize ClassicButton::sizeHint() const
{
    const QMargins o = outerMargins();
    const QMargins pad = padding();
    const int w = o.left() + o.right() + 2 + pad.left() + pad.right() + contentWidth();
    return {w, o.top() + o.bottom() + fixedHeight()};
}

QColor ClassicButton::partColor(const Part& p, bool checked) const
{
    const Theme& t = ThemeService::instance().theme();
    if (m_kind == Kind::Live)
        return checked ? QColor(Qt::white) : t.textSecondary;
    if (checked && m_kind == Kind::Toggle && p.role == ColorRole::Primary)
        return QColor(Qt::white);
    switch (p.role) {
    case ColorRole::Primary: return t.textPrimary;
    case ColorRole::Secondary: return t.textSecondary;
    case ColorRole::Donate: return t.donateText;
    case ColorRole::Fixed: return p.fixed;
    }
    return t.textPrimary;
}

void ClassicButton::paintEvent(QPaintEvent*)
{
    const Theme& t = ThemeService::instance().theme();
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing, false);
    if (!isEnabled())
        painter.setOpacity(0.45);

    const QRect frame = rect().marginsRemoved(outerMargins());
    const bool checked = isChecked();
    const bool pressed = isDown();

    QColor bg(Qt::transparent);
    QColor borderColor(Qt::transparent);

    switch (m_kind) {
    case Kind::Icon:
        if (pressed) bg = t.buttonBg;
        if (m_hover || pressed) borderColor = t.border;
        break;
    case Kind::Toggle:
        if (checked) {
            bg = Theme::accentBlue();
            borderColor = QColor(0x3B, 0x82, 0xF6);
        } else if (m_hover) {
            borderColor = t.border;
        }
        break;
    case Kind::Framed:
        bg = pressed ? t.buttonHoverBg : t.buttonBg;
        borderColor = m_hover ? t.buttonHoverBorder : t.buttonBorder;
        break;
    case Kind::Live:
        bg = checked ? Theme::accentBlue() : t.buttonBg;
        borderColor = checked ? Theme::accentBlue() : t.border;
        break;
    }

    if (bg.alpha() > 0)
        painter.fillRect(frame, bg);
    if (borderColor.alpha() > 0) {
        painter.setPen(borderColor);
        painter.setBrush(Qt::NoBrush);
        painter.drawRect(frame.adjusted(0, 0, -1, -1));
    }

    // Content, centred in the padded interior.
    const QMargins pad = padding();
    const QRect inner = frame.adjusted(1 + pad.left(), 1 + pad.top(), -(1 + pad.right()), -(1 + pad.bottom()));
    int x = inner.left() + (inner.width() - contentWidth()) / 2;
    const qreal dpr = devicePixelRatioF();
    for (const Part& p : std::as_const(m_parts)) {
        if (p.isIcon) {
            const QPixmap pm = IconFactory::pixmap(p.icon, p.iconSize.width(), p.iconSize.height(), dpr, t);
            painter.drawPixmap(QPoint(x, inner.top() + (inner.height() - p.iconSize.height()) / 2), pm);
            x += p.iconSize.width();
        } else {
            painter.setFont(p.font);
            painter.setPen(partColor(p, checked));
            const int w = QFontMetrics(p.font).horizontalAdvance(p.text);
            painter.drawText(QRect(x, inner.top(), w + 1, inner.height()), Qt::AlignVCenter | Qt::AlignLeft, p.text);
            x += w;
        }
        x += p.gapAfter;
    }

    if (hasFocus()) {
        painter.setPen(QPen(t.textMuted, 1, Qt::DotLine));
        painter.drawRect(frame.adjusted(2, 2, -3, -3));
    }
}

void ClassicButton::enterEvent(QEnterEvent* e)
{
    m_hover = true;
    update();
    QAbstractButton::enterEvent(e);
}

void ClassicButton::leaveEvent(QEvent* e)
{
    m_hover = false;
    update();
    QAbstractButton::leaveEvent(e);
}

} // namespace Lux
