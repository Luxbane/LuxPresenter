#include "Views/Components/PageView.h"

#include "Services/PageImageCache.h"
#include "Views/UiUtil.h"

#include <QPainter>

namespace Lux {

PageView::PageView(QWidget* parent) : QWidget(parent)
{
    setAttribute(Qt::WA_OpaquePaintEvent);
    setMinimumSize(80, 45);
    setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
}

void PageView::setPage(const QString& pdfPath, int page)
{
    m_pdf = pdfPath;
    m_page = page;
    m_message.clear();
    update();
}

void PageView::clear()
{
    m_pdf.clear();
    m_page = -1;
    m_message.clear();
    update();
}

void PageView::setMessage(const QString& text)
{
    m_message = text;
    update();
}

QImage PageView::currentImage()
{
    if (m_pdf.isEmpty() || m_page < 0)
        return {};
    const qreal dpr = devicePixelRatioF();
    return PageImageCache::instance().page(m_pdf, m_page, QSize(qRound(width() * dpr), qRound(height() * dpr)));
}

void PageView::paintEvent(QPaintEvent*)
{
    QPainter p(this);
    p.fillRect(rect(), Qt::black);
    if (!m_message.isEmpty()) {
        p.setPen(QColor(0xCB, 0xD5, 0xE1));
        p.setFont(tahoma(11));
        p.drawText(rect().adjusted(12, 12, -12, -12), Qt::AlignCenter | Qt::TextWordWrap, m_message);
        return;
    }
    QImage img = currentImage();
    if (img.isNull())
        return;
    const qreal dpr = devicePixelRatioF();
    const QPoint origin(qRound(width() * dpr / 2.0 - img.width() / 2.0), qRound(height() * dpr / 2.0 - img.height() / 2.0));
    img.setDevicePixelRatio(dpr);
    p.drawImage(QPointF(origin.x() / dpr, origin.y() / dpr), img);
}

} // namespace Lux
