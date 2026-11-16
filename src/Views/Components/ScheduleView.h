#pragma once

#include <QListWidget>
#include <QWidget>

class QMenu;
class QToolButton;

namespace Lux {

class ScheduleService;

// List with the drag & drop rules of ScheduleView.xaml.cs: songs dragged in from the song
// list are inserted at the drop position, rows dragged inside the list are reordered.
class ScheduleListWidget : public QListWidget
{
    Q_OBJECT
public:
    static constexpr const char* kSongMime = "application/x-luxpresenter-song";
    static constexpr const char* kRowMime = "application/x-luxpresenter-schedule-row";

    explicit ScheduleListWidget(QWidget* parent = nullptr);
    int dropIndexAt(const QPoint& viewportPos) const;

signals:
    void songDropped(const QString& songId, int index);
    void rowMoved(int from, int to);

protected:
    QStringList mimeTypes() const override;
    QMimeData* mimeData(const QList<QListWidgetItem*>& items) const override;
    void dragEnterEvent(QDragEnterEvent* e) override;
    void dragMoveEvent(QDragMoveEvent* e) override;
    void dropEvent(QDropEvent* e) override;
};

// Schedule pane (top of the left column): header with options menu, item list and the
// +, -, up, down, options tool column. It mirrors a ScheduleService.
class ScheduleView : public QWidget
{
    Q_OBJECT
public:
    explicit ScheduleView(ScheduleService& service, QWidget* parent = nullptr);

    int itemCount() const;
    void setPaneActive(bool active);
    bool paneActive() const;

    ScheduleListWidget* listWidget() const { return m_list; }
    QMenu* headerMenu() const { return m_headerMenu; }
    QMenu* contextMenu() const { return m_contextMenu; }
    QToolButton* addButton() const { return m_add; }
    QToolButton* removeButton() const { return m_remove; }
    QToolButton* moveUpButton() const { return m_up; }
    QToolButton* moveDownButton() const { return m_down; }
    QToolButton* optionsButton() const { return m_options; }
    QAction* actionNew() const { return m_aNew; }
    QAction* actionOpen() const { return m_aOpen; }
    QAction* actionSave() const { return m_aSave; }
    QAction* actionSaveAs() const { return m_aSaveAs; }
    QAction* actionClear() const { return m_aClear; }

signals:
    void addRequested();
    void newRequested();
    void openRequested();
    void saveRequested();
    void saveAsRequested();
    void clearRequested();
    void songDropped(const QString& songId, int index);
    void itemActivated(int index); // double click / Enter
    void goLiveRequested(int index);

protected:
    bool eventFilter(QObject* obj, QEvent* e) override;

private:
    QToolButton* makeToolButton(const QString& name, const QString& text, int px, bool bold, const QString& color,
                                const QString& tip);
    void rebuildList();
    void syncSelection();
    void updateButtons();

    ScheduleService& m_service;
    ScheduleListWidget* m_list;
    QMenu* m_headerMenu;
    QMenu* m_contextMenu;
    QAction *m_aNew, *m_aOpen, *m_aSave, *m_aSaveAs, *m_aClear;
    QToolButton *m_add, *m_remove, *m_up, *m_down, *m_options;
    bool m_syncing = false;
};

} // namespace Lux
