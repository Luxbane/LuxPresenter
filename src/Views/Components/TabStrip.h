#pragma once

#include <QFrame>
#include <QSignalBlocker>
#include <QTabBar>

namespace Lux {

// Tab header strip of the reference's global TabControl template: a surface-coloured
// bar (padding 2,3,2,0) holding bottom-aligned tabs. The selected page is shown by
// the owner (LibraryView) or is purely informational (sub-tab bars).
class TabStrip : public QFrame
{
    Q_OBJECT
public:
    enum class Density { Normal, Small, Wide };

    explicit TabStrip(Density density = Density::Normal, bool topBorder = true, QWidget* parent = nullptr);

    int addTab(const QString& title, const QString& tag = QString());
    void clear() { QSignalBlocker b(m_bar); while (m_bar->count() > 0) m_bar->removeTab(0); }
    void setTabToolTip(int i, const QString& tip) { m_bar->setTabToolTip(i, tip); }
    QTabBar* tabBar() const { return m_bar; }
    int currentIndex() const { return m_bar->currentIndex(); }
    void setCurrentIndex(int i) { m_bar->setCurrentIndex(i); }
    int count() const { return m_bar->count(); }
    QString tabText(int i) const { return m_bar->tabText(i); }
    QString currentTag() const { return m_bar->tabData(m_bar->currentIndex()).toString(); }

signals:
    void currentChanged(int index);

private:
    QTabBar* m_bar;
};

} // namespace Lux
