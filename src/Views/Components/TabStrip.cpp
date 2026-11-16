#include "Views/Components/TabStrip.h"

#include <QHBoxLayout>

namespace Lux {

TabStrip::TabStrip(Density density, bool topBorder, QWidget* parent) : QFrame(parent), m_bar(new QTabBar(this))
{
    setObjectName(topBorder ? QStringLiteral("TabStripTop") : QStringLiteral("TabStrip"));
    m_bar->setDrawBase(false);
    m_bar->setExpanding(false);
    m_bar->setUsesScrollButtons(false);
    m_bar->setElideMode(Qt::ElideNone);
    m_bar->setFocusPolicy(Qt::NoFocus);
    m_bar->setCursor(Qt::PointingHandCursor);
    m_bar->setProperty("density", density == Density::Small  ? "small"
                                  : density == Density::Wide ? "wide"
                                                             : "normal");
    auto* lay = new QHBoxLayout(this);
    lay->setContentsMargins(2, topBorder ? 3 : 0, 2, 0);
    lay->setSpacing(0);
    lay->addWidget(m_bar, 0, Qt::AlignBottom);
    lay->addStretch(1);
    connect(m_bar, &QTabBar::currentChanged, this, &TabStrip::currentChanged);
}

int TabStrip::addTab(const QString& title, const QString& tag)
{
    const int i = m_bar->addTab(title);
    m_bar->setTabData(i, tag.isEmpty() ? title : tag);
    return i;
}

} // namespace Lux
