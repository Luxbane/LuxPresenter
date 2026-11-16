#pragma once

#include <QDateTime>
#include <QTimer>
#include <QWidget>

namespace Lux {

class LiveAlertBannerWidget : public QWidget
{
    Q_OBJECT
public:
    explicit LiveAlertBannerWidget(QWidget* parent = nullptr);

protected:
    void paintEvent(QPaintEvent* e) override;

private slots:
    void onAlertStateChanged();

private:
    QTimer m_animTimer;
    qint64 m_startTime = 0;
};

} // namespace Lux
