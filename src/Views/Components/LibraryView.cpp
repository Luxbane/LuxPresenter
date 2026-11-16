#include "Views/Components/LibraryView.h"

#include "Views/Components/MediaTab.h"
#include "Services/PresentationService.h"
#include "Views/Components/PresentationTab.h"
#include "Views/Components/ScripturesTab.h"
#include "Views/Components/SongsTab.h"
#include "Views/Components/TabStrip.h"

#include <QStackedWidget>
#include <QVBoxLayout>

namespace Lux {

LibraryView::LibraryView(DataService& data, PresentationService& presentations, QWidget* parent) : QWidget(parent)
{
    setObjectName(QStringLiteral("PanelBg"));
    setAttribute(Qt::WA_StyledBackground, true);
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    m_tabs = new TabStrip(TabStrip::Density::Normal, /*topBorder=*/true, this);
    m_pages = new QStackedWidget(this);

    m_songs = new SongsTab(data, m_pages);
    m_scriptures = new ScripturesTab(data, m_pages);
    m_media = new MediaTab(data, m_pages);
    m_presentation = new PresentationTab(data, presentations, m_pages);
    auto* online = new QWidget(m_pages); // empty page, as in the reference
    online->setObjectName(QStringLiteral("PanelBg"));
    online->setAttribute(Qt::WA_StyledBackground, true);

    const struct { const char* title; QWidget* page; } tabs[] = {
        {"Songs", m_songs}, {"Scriptures", m_scriptures}, {"Media", m_media},
        {"Presentation", m_presentation}, {"Online", online},
    };
    for (const auto& t : tabs) {
        m_tabs->addTab(QString::fromLatin1(t.title));
        m_pages->addWidget(t.page);
    }

    root->addWidget(m_tabs);
    root->addWidget(m_pages, 1);

    connect(m_tabs, &TabStrip::currentChanged, this, [this](int i) {
        m_pages->setCurrentIndex(i);
        emit currentTabChanged(i);
    });
}

int LibraryView::currentTab() const { return m_tabs->currentIndex(); }

void LibraryView::selectTab(int index)
{
    if (index >= 0 && index < m_tabs->count())
        m_tabs->setCurrentIndex(index);
}

int LibraryView::tabCount() const { return m_tabs->count(); }

QString LibraryView::tabText(int index) const { return m_tabs->tabText(index); }

} // namespace Lux
