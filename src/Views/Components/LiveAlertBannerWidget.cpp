#include "Views/Components/LiveAlertBannerWidget.h"

#include "Services/AlertService.h"
#include "Views/UiUtil.h"

#include <QFontMetricsF>
#include <QPainter>

namespace Lux {

LiveAlertBannerWidget::LiveAlertBannerWidget(QWidget* parent)
    : QWidget(parent)
{
    setFixedHeight(24);
    setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);

    connect(&m_animTimer, &QTimer::timeout, this, QOverload<>::of(&QWidget::update));
    connect(&AlertService::instance(), &AlertService::alertStateChanged, this, &LiveAlertBannerWidget::onAlertStateChanged);

    onAlertStateChanged();
}

void LiveAlertBannerWidget::onAlertStateChanged()
{
    if (AlertService::instance().isAlertVisible()) {
        if (!isVisible())
            show();
        if (!m_animTimer.isActive()) {
            m_startTime = QDateTime::currentMSecsSinceEpoch();
            m_animTimer.start(16);
        }
    } else {
        m_animTimer.stop();
        m_startTime = 0;
        if (isVisible())
            hide();
    }
    update();
}

void LiveAlertBannerWidget::paintEvent(QPaintEvent*)
{
    if (!AlertService::instance().isAlertVisible())
        return;

    QPainter p(this);
    p.setRenderHint(QPainter::Antialiasing);
    p.setRenderHint(QPainter::TextAntialiasing);

    // Red alert banner background
    p.fillRect(rect(), QColor(0xFF, 0x00, 0x00));

    const QString text = AlertService::instance().currentActiveMessage().text;
    if (text.isEmpty())
        return;

    QFont f = tahoma(10, true);
    QFontMetricsF fm(f);
    const qreal textW = fm.horizontalAdvance(text);
    const qreal totalDist = width() + textW;
    const qreal durationSec = qMax(3.0, totalDist / 90.0); // 90 px/sec

    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    if (m_startTime <= 0)
        m_startTime = now;

    qreal elapsed = (now - m_startTime) / 1000.0;
    if (elapsed >= durationSec) {
        m_startTime = now;
        elapsed = 0;
    }

    const qreal x = width() - (elapsed / durationSec) * totalDist;
    const qreal y = (height() - fm.height()) / 2.0 + fm.ascent();

    p.save();
    p.setClipRect(rect());
    p.setFont(f);

    // Drop shadow
    p.setPen(QColor(0, 0, 0, 200));
    p.drawText(QPointF(x + 1.5, y + 1.5), text);

    // Foreground white text
    p.setPen(Qt::white);
    p.drawText(QPointF(x, y), text);
    p.restore();
}

} // namespace Lux
