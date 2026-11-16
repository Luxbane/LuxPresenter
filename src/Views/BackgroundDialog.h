#pragma once
#include "Models/BackgroundItem.h"
#include <QDialog>
class QCheckBox;
class QListWidget;
class QListWidgetItem;
namespace Lux {
class DataService;
class SlideCanvas;
// Background window: thumbnails of the built-in colours and of the images / videos / colours the user added, a
// preview of lyrics on the selected background, and "also show on the live output".
class BackgroundDialog : public QDialog
{
    Q_OBJECT
public:
    BackgroundDialog(DataService& data, const QString& current, QWidget* parent = nullptr);
    QString selectedValue() const;                        // colour ("#RRGGBB") or file path; empty = nothing selected
    bool applyToLive() const;
    void setApplyToLive(bool on);
    // Adds image / video files; returns one message per file that was refused (missing, unsupported, unreadable, duplicate).
    QStringList addFiles(const QStringList& paths);
    bool addColor(const QString& hex);
    bool removeSelected();                                // user-added items only
    void selectValue(const QString& value);
    int itemCount() const;
    bool isMissing(const QString& value) const;
    bool overrideGlobal() const;
    void setOverrideGlobal(bool on);
    QListWidget* list() const { return m_list; }
    SlideCanvas* previewCanvas() const { return m_canvas; }
private:
    void reload(const QString& select = {});
    QIcon iconFor(const BackgroundItem& item) const;
    void refreshIcons();
    void onSelectionChanged();
    DataService& m_data;
    QList<BackgroundItem> m_items;
    QListWidget* m_list = nullptr;
    SlideCanvas* m_canvas = nullptr;
    class QLabel* m_videoBadge = nullptr;
    class QLabel* m_emptyLabel = nullptr;
    QCheckBox* m_override = nullptr;
    QCheckBox* m_live = nullptr;
    class QPushButton* m_addBtn = nullptr;
    class QPushButton* m_addMultipleBtn = nullptr;
    class QPushButton* m_remove = nullptr;
    class QPushButton* m_okBtn = nullptr;
    class QPushButton* m_cancelBtn = nullptr;
    class QLabel* m_note = nullptr;
};
} // namespace Lux
