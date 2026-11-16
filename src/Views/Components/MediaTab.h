#pragma once

#include "Models/MediaItem.h"
#include <QWidget>
#include <optional>

class QComboBox;
class QListWidget;
class QPushButton;

namespace Lux {

class DataService;
class TabStrip;

// Media library tab: folder selector, filmstrip thumbnail grid, Add/Delete bar and the
// Videos/Images/Audio sub-tabs.
class MediaTab : public QWidget
{
    Q_OBJECT
public:
    explicit MediaTab(DataService& data, QWidget* parent = nullptr);

    void refresh();
    QListWidget* grid() const { return m_grid; }
    QComboBox* folderCombo() const { return m_folder; }
    TabStrip* subTabs() const { return m_subTabs; }
    QPushButton* addButton() const { return m_add; }
    QPushButton* deleteButton() const { return m_delete; }
    QPushButton* browseButton() const { return m_browse; }
    QPushButton* optionsButton() const { return m_options; }

    bool isGridView() const { return m_isGridView; }
    void setGridView(bool grid);

    std::optional<MediaItem> selectedMedia() const;

signals:
    void mediaSelected(const Lux::MediaItem& item);
    void mediaActivated(const Lux::MediaItem& item);
    void mediaCleared();

private:
    void onAddClicked();
    void onDeleteClicked();
    void onBrowseClicked();
    void onSelectionChanged();
    void onOptionsClicked();
    void applyViewMode();

    DataService& m_data;
    bool m_isGridView = true;
    QComboBox* m_folder = nullptr;
    QPushButton *m_browse = nullptr, *m_add = nullptr, *m_delete = nullptr, *m_options = nullptr;
    QListWidget* m_grid = nullptr;
    TabStrip* m_subTabs = nullptr;
};

} // namespace Lux
