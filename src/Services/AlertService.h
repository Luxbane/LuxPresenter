#pragma once

#include "Models/PresentationDocModels.h"

#include <QList>
#include <QObject>
#include <QString>

namespace Lux {

class AlertService : public QObject
{
    Q_OBJECT
public:
    static AlertService& instance();

    const QList<AlertMessageItem>& runningMessages() const { return m_runningMessages; }

    bool isAlertHidden() const { return m_isAlertHidden; }
    void setAlertHidden(bool hidden);
    void toggleHide();

    bool isAlertVisible() const;
    AlertMessageItem currentActiveMessage() const;

    void addMessage(const QString& text, int count = 3);
    void removeMessage(int index);
    void removeMessage(const AlertMessageItem& item);
    void setPriority(int index);
    void selectNext();
    void notifyPassCompleted();
    void clear();

signals:
    void alertStateChanged();

private:
    explicit AlertService(QObject* parent = nullptr);

    QList<AlertMessageItem> m_runningMessages;
    bool m_isAlertHidden = false;
};

} // namespace Lux
