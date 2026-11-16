#include "Services/ScheduleService.h"

#include "Models/ScheduleFileData.h"

#include <QFile>

#include <algorithm>

namespace Lux {

void ScheduleService::setDirty(bool d)
{
    if (m_dirty == d)
        return;
    m_dirty = d;
    emit unsavedChangesChanged(d);
}

void ScheduleService::setSelectedInternal(int index, bool forcePreview)
{
    if (index < -1 || index >= m_items.size())
        index = -1;
    const bool changed = (index != m_selected);
    m_selected = index;
    if (changed)
        emit selectionChanged(index);
    if (index >= 0 && (changed || forcePreview))
        emit itemSelectedForPreview(index);
}

void ScheduleService::select(int index) { setSelectedInternal(index, false); }
void ScheduleService::userSelect(int index) { setSelectedInternal(index, true); }

void ScheduleService::addToSchedule(const PresentationItem& presentation)
{
    m_items.append(ScheduleItem::fromPresentation(presentation));
    setDirty(true);
    emit itemsChanged();
    setSelectedInternal(static_cast<int>(m_items.size()) - 1, true);
}

void ScheduleService::insertItem(int index, const ScheduleItem& item)
{
    index = std::clamp(index, 0, static_cast<int>(m_items.size()));
    m_items.insert(index, item);
    if (m_selected >= index)
        ++m_selected; // keep the previously selected item selected
    setDirty(true);
    emit itemsChanged();
    setSelectedInternal(index, false);
}

bool ScheduleService::removeSelected()
{
    if (m_selected < 0)
        return false;
    const int idx = m_selected;
    m_items.removeAt(idx);
    m_selected = -1; // the removed item was the selection
    setDirty(true);
    emit itemsChanged();
    emit selectionChanged(-1);
    if (!m_items.isEmpty())
        setSelectedInternal(std::clamp(idx, 0, static_cast<int>(m_items.size()) - 1), false);
    return true;
}

bool ScheduleService::move(int from, int to)
{
    const int n = static_cast<int>(m_items.size());
    if (from < 0 || from >= n || to < 0 || to >= n || from == to)
        return false;
    const QString movedId = m_items[from].id;
    m_items.move(from, to);
    setDirty(true);
    emit itemsChanged();
    // The reference re-assigns SelectedItem to the moved item (same object => no preview).
    if (m_selected == from)
        m_selected = to;
    else if (from < m_selected && to >= m_selected)
        --m_selected;
    else if (from > m_selected && to <= m_selected)
        ++m_selected;
    Q_UNUSED(movedId);
    emit selectionChanged(m_selected);
    return true;
}

bool ScheduleService::moveUp()
{
    return m_selected > 0 && move(m_selected, m_selected - 1);
}

bool ScheduleService::moveDown()
{
    return m_selected >= 0 && m_selected < m_items.size() - 1 && move(m_selected, m_selected + 1);
}

void ScheduleService::clear()
{
    m_items.clear();
    m_selected = -1;
    m_filePath = QString();
    setDirty(false);
    emit itemsChanged();
    emit selectionChanged(-1);
    emit filePathChanged(m_filePath);
}

void ScheduleService::replaceByTitle(const QString& oldTitle, const QString& newTitle, const PresentationItem& presentation)
{
    bool any = false;
    for (ScheduleItem& it : m_items) {
        if (it.title.compare(newTitle, Qt::CaseInsensitive) == 0 ||
            (!oldTitle.isNull() && it.title.compare(oldTitle, Qt::CaseInsensitive) == 0)) {
            it.title = newTitle;
            it.presentation = presentation.clone();
            any = true;
        }
    }
    if (any)
        emit itemsChanged();
}

bool ScheduleService::saveToFile(const QString& path, QString* error)
{
    if (!ScheduleFile::save(path, m_items, error))
        return false;
    m_filePath = path;
    setDirty(false);
    emit filePathChanged(m_filePath);
    return true;
}

bool ScheduleService::loadFromFile(const QString& path, QString* error)
{
    if (!QFile::exists(path)) {
        if (error)
            *error = QStringLiteral("File not found: %1").arg(path);
        return false;
    }
    QList<ScheduleItem> loaded;
    if (!ScheduleFile::load(path, loaded, error))
        return false;
    m_items = loaded;
    m_selected = -1;
    m_filePath = path;
    setDirty(false);
    emit itemsChanged();
    emit selectionChanged(-1);
    emit filePathChanged(m_filePath);
    if (!m_items.isEmpty())
        setSelectedInternal(0, true);
    return true;
}

} // namespace Lux
