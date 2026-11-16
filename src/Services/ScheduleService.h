#pragma once

#include "Models/ScheduleItem.h"

#include <QList>
#include <QObject>
#include <QString>

namespace Lux {

// Port of ViewModels/ScheduleViewModel.cs: the ordered list of schedule items, the
// selected item, the "unsaved changes" flag and the current schedule file.
class ScheduleService : public QObject
{
    Q_OBJECT
public:
    explicit ScheduleService(QObject* parent = nullptr) : QObject(parent) {}

    const QList<ScheduleItem>& items() const { return m_items; }
    int count() const { return static_cast<int>(m_items.size()); }
    int selectedIndex() const { return m_selected; }
    const ScheduleItem* selectedItem() const { return m_selected >= 0 ? &m_items[m_selected] : nullptr; }
    bool hasUnsavedChanges() const { return m_dirty; }
    QString currentFilePath() const { return m_filePath; } // null when never saved/opened

    // Appends a copy of `presentation`, selects it and requests a preview (AddToSchedule).
    void addToSchedule(const PresentationItem& presentation);
    // Inserts at `index` (clamped), selects the new item (drag & drop of a song/presentation).
    void insertItem(int index, const ScheduleItem& item);

    // SelectedItem setter semantics: preview is requested only when the selection changes.
    void select(int index);
    // UserSelectItem semantics: preview is requested even if the item is already selected.
    void userSelect(int index);

    bool removeSelected();            // selects the neighbour afterwards
    bool moveUp();
    bool moveDown();
    bool move(int from, int to);      // keeps the moved item selected
    void clear();                     // empties the list and forgets the file path

    // Renames/replaces items whose title matches (used after a song was edited). Does not
    // mark the schedule as modified, like the reference.
    void replaceByTitle(const QString& oldTitle, const QString& newTitle, const PresentationItem& presentation);

    bool saveToFile(const QString& path, QString* error = nullptr);
    bool loadFromFile(const QString& path, QString* error = nullptr);

signals:
    void itemsChanged();
    void selectionChanged(int index);
    void itemSelectedForPreview(int index);
    void unsavedChangesChanged(bool dirty);
    void filePathChanged(const QString& path);

private:
    void setDirty(bool d);
    void setSelectedInternal(int index, bool forcePreview);

    QList<ScheduleItem> m_items;
    int m_selected = -1;
    bool m_dirty = false;
    QString m_filePath;
};

} // namespace Lux
