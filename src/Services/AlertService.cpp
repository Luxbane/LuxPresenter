#include "Services/AlertService.h"

namespace Lux {

AlertService& AlertService::instance()
{
    static AlertService s_instance;
    return s_instance;
}

AlertService::AlertService(QObject* parent)
    : QObject(parent)
{
}

bool AlertService::isAlertVisible() const
{
    return !m_isAlertHidden && !m_runningMessages.isEmpty() && m_runningMessages.first().count > 0;
}

AlertMessageItem AlertService::currentActiveMessage() const
{
    return m_runningMessages.isEmpty() ? AlertMessageItem{} : m_runningMessages.first();
}

void AlertService::setAlertHidden(bool hidden)
{
    if (m_isAlertHidden != hidden) {
        m_isAlertHidden = hidden;
        emit alertStateChanged();
    }
}

void AlertService::toggleHide()
{
    setAlertHidden(!m_isAlertHidden);
}

void AlertService::addMessage(const QString& text, int count)
{
    const QString trimmed = text.trimmed();
    if (trimmed.isEmpty())
        return;
    if (count <= 0)
        count = 1;

    m_runningMessages.append(AlertMessageItem{count, trimmed});
    m_isAlertHidden = false;
    emit alertStateChanged();
}

void AlertService::removeMessage(int index)
{
    if (index >= 0 && index < m_runningMessages.size()) {
        m_runningMessages.removeAt(index);
        emit alertStateChanged();
    }
}

void AlertService::removeMessage(const AlertMessageItem& item)
{
    const int idx = m_runningMessages.indexOf(item);
    if (idx >= 0)
        removeMessage(idx);
}

void AlertService::setPriority(int index)
{
    if (index > 0 && index < m_runningMessages.size()) {
        const AlertMessageItem item = m_runningMessages.takeAt(index);
        m_runningMessages.prepend(item);
        emit alertStateChanged();
    }
}

void AlertService::selectNext()
{
    if (m_runningMessages.size() > 1) {
        const AlertMessageItem item = m_runningMessages.takeFirst();
        m_runningMessages.append(item);
        emit alertStateChanged();
    }
}

void AlertService::notifyPassCompleted()
{
    if (!m_runningMessages.isEmpty()) {
        m_runningMessages[0].count--;
        if (m_runningMessages[0].count <= 0) {
            m_runningMessages.removeFirst();
        }
        emit alertStateChanged();
    }
}

void AlertService::clear()
{
    if (!m_runningMessages.isEmpty()) {
        m_runningMessages.clear();
        emit alertStateChanged();
    }
}

} // namespace Lux
