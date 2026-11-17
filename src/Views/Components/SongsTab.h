#pragma once

#include "Models/Song.h"
#include "Services/SongSearch.h"

#include <QTreeWidget>
#include <QWidget>

class QLineEdit;
class QPushButton;
class QTimer;
class QMenu;

namespace Lux {

class DataService;

// Song list with the drag source of the reference (a song id travels as MIME data).
class SongListWidget : public QTreeWidget
{
    Q_OBJECT
public:
    explicit SongListWidget(QWidget* parent = nullptr);

protected:
    QStringList mimeTypes() const override;
    QMimeData* mimeData(const QList<QTreeWidgetItem*>& items) const override;
};

// Songs library tab (SongsTab.xaml + SongsViewModel.cs): title search + Favorites, the song
// list (Title / Author), lyrics search, context menu and the empty-state card.
class SongsTab : public QWidget
{
    Q_OBJECT
public:
    explicit SongsTab(DataService& data, QWidget* parent = nullptr);

    // ReloadSongs: re-reads the songs from the DataService and re-applies the filter,
    // keeping `targetSongId` (or the current selection) selected when it is still listed.
    void reload(const QString& targetSongId = QString());
    void applyFilterImmediate(const QString& targetSongId = QString());

    void setTitleQuery(const QString& q);   // clears the lyrics query when non-empty (as the reference)
    void setLyricsQuery(const QString& q);  // clears the title query when non-empty
    QString titleQuery() const;
    QString lyricsQuery() const;
    bool favoritesOnly() const;
    void setFavoritesOnly(bool on);

    QList<Song> filteredSongs() const { return m_filtered; }
    QString selectedSongId() const { return m_selectedId; }
    bool hasSongs() const { return m_hasSongs; }
    // User picked a song (click / keyboard / programmatic after a filter): fires the preview signal.
    void userSelect(const QString& songId);
    void setPaneActive(bool active);
    bool paneActive() const;

    SongListWidget* list() const { return m_list; }
    QWidget* emptyCard() const { return m_emptyCard; }
    QPushButton* openFolderButton() const { return m_openFolder; }
    QPushButton* importButton() const { return m_import; }
    QPushButton* sampleButton() const { return m_sample; }
    QPushButton* createButton() const { return m_create; }
    QPushButton* favoritesButton() const { return m_favorites; }
    QLineEdit* titleSearch() const { return m_titleSearch; }
    QLineEdit* lyricsSearch() const { return m_lyricsSearch; }
    QMenu* contextMenu() const { return m_contextMenu; }
    QAction* actionAddToSchedule() const { return m_aAdd; }
    QAction* actionEdit() const { return m_aEdit; }
    QAction* actionGoLive() const { return m_aGoLive; }
    QAction* actionDelete() const { return m_aDelete; }

signals:
    void songSelectedForPreview(const QString& songId);
    void songDoubleClicked(const QString& songId);
    void goLiveRequested(const QString& songId);
    void addToScheduleRequested(const QString& songId);
    void newSongRequested();
    void editSongRequested(const QString& songId);
    void deleteSongRequested(const QString& songId);
    void importBackupRequested();
    void openSongsFolderRequested();
    void loadSamplesRequested();
    void selectionStateChanged(bool hasSelection);

protected:
    bool eventFilter(QObject* obj, QEvent* e) override;

private:
    void applyFilter(const QString& targetSongId);
    void setSelected(const QString& id, bool forcePreview);
    void fillList();
    QString currentListSongId() const;
    struct CandidateMatch {
        QString songId;
        QString songTitle;
        QString matchedPhrase;
        QString suffix;
        bool isLineStart = false;
    };
    bool findBestCandidate(const QString& prefix, bool isLyrics, CandidateMatch& out) const;
    static bool hasSelectedGhost(const QLineEdit* edit);

    DataService& m_data;
    SongSearch m_index;
    QList<Song> m_filtered;
    QString m_selectedId;
    bool m_hasSongs = false;
    bool m_filling = false;

    QLineEdit* m_titleSearch;
    QLineEdit* m_lyricsSearch;
    QPushButton* m_favorites;
    SongListWidget* m_list;
    QWidget* m_emptyCard;
    QPushButton *m_openFolder, *m_import, *m_sample, *m_create;
    QTimer* m_debounce;
    QMenu* m_contextMenu;
    QAction *m_aAdd, *m_aGoLive, *m_aEdit, *m_aDelete;
    bool m_hadLyricsQuery = false;
    bool m_isDeleting = false;
};

} // namespace Lux
