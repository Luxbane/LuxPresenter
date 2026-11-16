#pragma once

#include <QWidget>

class QStackedWidget;

namespace Lux {

class DataService;
class MediaTab;
class PresentationService;
class PresentationTab;
class ScripturesTab;
class SongsTab;
class TabStrip;

// Library pane (bottom of the left column): Songs, Scriptures, Media, Presentation, Online.
// Mirrors LibraryView.xaml; the Online tab has an empty page and switches the main
// window into full-width online mode.
class LibraryView : public QWidget
{
    Q_OBJECT
public:
    static constexpr int kOnlineTab = 4;

    LibraryView(DataService& data, PresentationService& presentations, QWidget* parent = nullptr);

    int currentTab() const;
    void selectTab(int index);
    int tabCount() const;
    QString tabText(int index) const;

    SongsTab* songsTab() const { return m_songs; }
    ScripturesTab* scripturesTab() const { return m_scriptures; }
    MediaTab* mediaTab() const { return m_media; }
    PresentationTab* presentationTab() const { return m_presentation; }
    QStackedWidget* pages() const { return m_pages; }

signals:
    void currentTabChanged(int index);

private:
    TabStrip* m_tabs;
    QStackedWidget* m_pages;
    SongsTab* m_songs;
    ScripturesTab* m_scriptures;
    MediaTab* m_media;
    PresentationTab* m_presentation;
};

} // namespace Lux
