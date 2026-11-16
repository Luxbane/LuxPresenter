#include "Views/Components/ScheduleView.h"

#include "Services/ScheduleService.h"
#include "Services/TextRepair.h"
#include "Services/ThemeService.h"
#include "Views/UiUtil.h"

#include <QAction>
#include <QDragEnterEvent>
#include <QDragMoveEvent>
#include <QDropEvent>
#include <QEvent>
#include <QHBoxLayout>
#include <QKeyEvent>
#include <QLabel>
#include <QMenu>
#include <QMimeData>
#include <QPainter>
#include <QStyledItemDelegate>
#include <QTimer>
#include <QToolButton>
#include <QVBoxLayout>

#include <algorithm>

namespace Lux {

namespace {

// Paints a schedule row like the reference DataTemplate: 32x22 "[auto]" thumbnail,
// bold title and a "<notes>" subtitle.
class ScheduleDelegate : public QStyledItemDelegate
{
public:
    using QStyledItemDelegate::QStyledItemDelegate;

    QSize sizeHint(const QStyleOptionViewItem& opt, const QModelIndex&) const override
    {
        return {opt.rect.width(), 34};
    }

    void paint(QPainter* p, const QStyleOptionViewItem& opt, const QModelIndex& idx) const override
    {
        const Theme& t = ThemeService::instance().theme();
        const bool selected = opt.state & QStyle::State_Selected;
        const bool hover = opt.state & QStyle::State_MouseOver;
        const bool active = opt.widget && opt.widget->property("paneActive").toBool();

        QColor bg(Qt::transparent), border(Qt::transparent), title = t.listItemText, notes(0x94, 0xA3, 0xB8);
        if (hover) { bg = t.buttonHoverBg; border = t.buttonHoverBorder; }
        if (selected && !active) {
            bg = t.inactiveSelBg; border = t.inactiveSelBorder; title = t.inactiveSelText; notes = QColor(0x64, 0x74, 0x8B);
        } else if (selected && active) {
            bg = Theme::selectionBlue(); border = Theme::selectionBlueBorder(); title = Qt::white; notes = QColor(0xE0, 0xE7, 0xFF);
        }

        p->save();
        const QRect r = opt.rect.adjusted(1, 1, -1, -1);
        if (bg.alpha() > 0) p->fillRect(r, bg);
        if (border.alpha() > 0) { p->setPen(border); p->drawRect(r.adjusted(0, 0, -1, -1)); }

        const QRect inner = r.adjusted(5, 3, -5, -3); // border 1 + padding 4,2
        const QRect thumb(inner.left() + 1, inner.center().y() - 11, 32, 22);
        p->fillRect(thumb, QColor(0x0A, 0x18, 0x26));
        p->setPen(QColor(0x64, 0x74, 0x8B));
        p->drawRect(thumb.adjusted(0, 0, -1, -1));
        p->setFont(tahoma(7));
        const QRect tag(thumb.center().x() - 11, thumb.center().y() - 5, 22, 10);
        p->fillRect(tag, QColor(0, 0, 0, 160));
        p->setPen(QColor(0xF8, 0xFA, 0xFC));
        p->drawText(tag, Qt::AlignCenter, QStringLiteral("[auto]"));

        const int tx = inner.left() + 36 + 6;
        const QRect titleRect(tx, inner.top() + 1, inner.right() - tx, 14);
        p->setFont(tahoma(11, true));
        p->setPen(title);
        p->drawText(titleRect, Qt::AlignVCenter | Qt::AlignLeft,
                    p->fontMetrics().elidedText(idx.data(Qt::DisplayRole).toString(), Qt::ElideRight, titleRect.width()));
        p->setFont(tahoma(9));
        p->setPen(notes);
        p->drawText(QRect(tx, titleRect.bottom() - 1, inner.right() - tx, 12), Qt::AlignVCenter | Qt::AlignLeft,
                    QStringLiteral("<notes>"));
        p->restore();
    }
};

} // namespace

// ---------------------------------------------------------------------------------------
// ScheduleListWidget
// ---------------------------------------------------------------------------------------

ScheduleListWidget::ScheduleListWidget(QWidget* parent) : QListWidget(parent)
{
    setAcceptDrops(true);
    setDragEnabled(true);
    setDragDropMode(QAbstractItemView::DragDrop);
    setDefaultDropAction(Qt::MoveAction);
    setDropIndicatorShown(true);
}

QStringList ScheduleListWidget::mimeTypes() const
{
    return {QString::fromLatin1(kRowMime)};
}

QMimeData* ScheduleListWidget::mimeData(const QList<QListWidgetItem*>& items) const
{
    if (items.isEmpty())
        return nullptr;
    auto* m = new QMimeData;
    m->setData(QString::fromLatin1(kRowMime), QByteArray::number(row(items.first())));
    return m;
}

int ScheduleListWidget::dropIndexAt(const QPoint& pos) const
{
    if (count() == 0)
        return 0;
    if (QListWidgetItem* it = itemAt(pos)) {
        int idx = row(it);
        if (pos.y() > visualItemRect(it).center().y())
            ++idx; // lower half of a row = drop after it
        return idx;
    }
    return count();
}

void ScheduleListWidget::dragEnterEvent(QDragEnterEvent* e)
{
    if (e->mimeData()->hasFormat(QString::fromLatin1(kSongMime)) || e->mimeData()->hasFormat(QString::fromLatin1(kRowMime)))
        e->acceptProposedAction();
    else
        e->ignore();
}

void ScheduleListWidget::dragMoveEvent(QDragMoveEvent* e)
{
    if (e->mimeData()->hasFormat(QString::fromLatin1(kSongMime)) || e->mimeData()->hasFormat(QString::fromLatin1(kRowMime)))
        e->acceptProposedAction();
    else
        e->ignore();
}

void ScheduleListWidget::dropEvent(QDropEvent* e)
{
    const int target = dropIndexAt(e->position().toPoint());
    if (e->mimeData()->hasFormat(QString::fromLatin1(kSongMime))) {
        emit songDropped(QString::fromUtf8(e->mimeData()->data(QString::fromLatin1(kSongMime))), target);
        e->setDropAction(Qt::CopyAction);
        e->accept();
    } else if (e->mimeData()->hasFormat(QString::fromLatin1(kRowMime))) {
        const int from = e->mimeData()->data(QString::fromLatin1(kRowMime)).toInt();
        int to = target;
        if (from < to)
            --to;
        to = std::clamp(to, 0, std::max(0, count() - 1));
        if (from != to)
            emit rowMoved(from, to);
        e->setDropAction(Qt::IgnoreAction); // the model is rebuilt from the service, never moved here
        e->accept();
    } else {
        e->ignore();
    }
}

// ---------------------------------------------------------------------------------------
// ScheduleView
// ---------------------------------------------------------------------------------------

ScheduleView::ScheduleView(ScheduleService& service, QWidget* parent) : QWidget(parent), m_service(service)
{
    setObjectName(QStringLiteral("PanelBg"));
    setAttribute(Qt::WA_StyledBackground, true);
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // Header (26px)
    auto* header = new QFrame(this);
    header->setObjectName(QStringLiteral("PaneHeader"));
    header->setFixedHeight(26);
    auto* hl = new QHBoxLayout(header);
    hl->setContentsMargins(8, 0, 8, 0);
    auto* title = new QLabel(QStringLiteral("Schedule"), header);
    title->setFont(tahoma(11, true));
    hl->addWidget(title);
    hl->addStretch(1);

    m_headerMenu = new QMenu(this);
    m_aNew = m_headerMenu->addAction(QString::fromUtf8("\U0001F4C4 New Schedule"));
    m_aOpen = m_headerMenu->addAction(QString::fromUtf8("\U0001F4C1 Open Schedule..."));
    m_aSave = m_headerMenu->addAction(QString::fromUtf8("\U0001F4BE Save Schedule"));
    m_aSaveAs = m_headerMenu->addAction(QString::fromUtf8("\U0001F4BE Save Schedule As..."));
    m_headerMenu->addSeparator();
    m_aClear = m_headerMenu->addAction(QString::fromUtf8("\U0001F5D1 Clear Schedule"));
    connect(m_aNew, &QAction::triggered, this, &ScheduleView::newRequested);
    connect(m_aOpen, &QAction::triggered, this, &ScheduleView::openRequested);
    connect(m_aSave, &QAction::triggered, this, &ScheduleView::saveRequested);
    connect(m_aSaveAs, &QAction::triggered, this, &ScheduleView::saveAsRequested);
    connect(m_aClear, &QAction::triggered, this, &ScheduleView::clearRequested);

    auto* gear = new QToolButton(header);
    gear->setText(QStringLiteral("\u2699 \u25BE"));
    gear->setFont(segoeSymbol(10));
    gear->setToolTip(QStringLiteral("Schedule menu"));
    gear->setCursor(Qt::PointingHandCursor);
    gear->setPopupMode(QToolButton::InstantPopup);
    gear->setMenu(m_headerMenu);
    gear->setStyleSheet(QStringLiteral("QToolButton::menu-indicator{image:none;} QToolButton{padding:0;}"));
    hl->addWidget(gear);
    root->addWidget(header);

    // Body: list + tool column
    auto* body = new QWidget(this);
    auto* bl = new QHBoxLayout(body);
    bl->setContentsMargins(0, 0, 0, 0);
    bl->setSpacing(0);

    m_list = new ScheduleListWidget(body);
    m_list->setObjectName(QStringLiteral("ScheduleList"));
    m_list->setItemDelegate(new ScheduleDelegate(m_list));
    m_list->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    m_list->setVerticalScrollMode(QAbstractItemView::ScrollPerPixel);
    m_list->setFrameShape(QFrame::NoFrame);
    m_list->setMouseTracking(true);
    m_list->setProperty("paneActive", false);
    bl->addWidget(m_list, 1);

    auto* tools = new QFrame(body);
    tools->setObjectName(QStringLiteral("SchedToolbar"));
    tools->setFixedWidth(28);
    auto* tl = new QVBoxLayout(tools);
    tl->setContentsMargins(2, 4, 2, 0);
    tl->setSpacing(0);
    m_add = makeToolButton(QStringLiteral("SchedAdd"), QStringLiteral("+"), 16, true, QStringLiteral("22,163,74"),
                           QStringLiteral("Add selected item to schedule"));
    m_remove = makeToolButton(QStringLiteral("SchedRemove"), QStringLiteral("\u2212"), 16, true, QStringLiteral("220,38,38"),
                              QStringLiteral("Remove selected item from schedule"));
    m_up = makeToolButton(QStringLiteral("SchedUp"), QStringLiteral("\u25B2"), 10, false, QStringLiteral("71,85,105"),
                          QStringLiteral("Move item up"));
    m_down = makeToolButton(QStringLiteral("SchedDown"), QStringLiteral("\u25BC"), 10, false, QStringLiteral("71,85,105"),
                            QStringLiteral("Move item down"));
    m_options = makeToolButton(QStringLiteral("SchedOptions"), QStringLiteral("\U0001F4D1"), 12, false,
                               QStringLiteral("245,158,11"), QStringLiteral("Schedule Options"));
    for (QToolButton* b : {m_add, m_remove, m_up, m_down}) {
        tl->addWidget(b, 0, Qt::AlignHCenter);
        tl->addSpacing(4);
    }
    tl->addSpacing(2);
    tl->addWidget(m_options, 0, Qt::AlignHCenter);
    tl->addStretch(1);
    bl->addWidget(tools);
    root->addWidget(body, 1);

    // Context menu of the list
    m_contextMenu = new QMenu(this);
    QAction* goLive = m_contextMenu->addAction(QString::fromUtf8("\u25B6 Go Live"));
    connect(goLive, &QAction::triggered, this, [this] {
        const int row = m_list->currentRow();
        if (row >= 0)
            emit goLiveRequested(row);
    });
    m_contextMenu->addSeparator();
    QAction* up = m_contextMenu->addAction(QString::fromUtf8("\u25B2 Move Up"));
    QAction* down = m_contextMenu->addAction(QString::fromUtf8("\u25BC Move Down"));
    m_contextMenu->addSeparator();
    QAction* remove = m_contextMenu->addAction(QString::fromUtf8("\U0001F5D1 Remove from Schedule"));
    connect(up, &QAction::triggered, m_up, &QToolButton::click);
    connect(down, &QAction::triggered, m_down, &QToolButton::click);
    connect(remove, &QAction::triggered, m_remove, &QToolButton::click);
    m_list->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(m_list, &QWidget::customContextMenuRequested, this, [this](const QPoint& pos) {
        m_contextMenu->popup(m_list->viewport()->mapToGlobal(pos));
    });

    // Buttons -> service
    connect(m_add, &QToolButton::clicked, this, &ScheduleView::addRequested);
    connect(m_remove, &QToolButton::clicked, this, [this] { m_service.removeSelected(); });
    connect(m_up, &QToolButton::clicked, this, [this] { m_service.moveUp(); });
    connect(m_down, &QToolButton::clicked, this, [this] { m_service.moveDown(); });
    connect(m_options, &QToolButton::clicked, this, [this] {
        m_headerMenu->popup(m_options->mapToGlobal(QPoint(m_options->width(), 0)));
    });

    // List interaction -> service ("UserSelectItem": a click always requests the preview)
    connect(m_list, &QListWidget::itemPressed, this, [this](QListWidgetItem* it) {
        if (!m_syncing)
            m_service.userSelect(m_list->row(it));
    });
    connect(m_list, &QListWidget::itemDoubleClicked, this, [this](QListWidgetItem* it) {
        emit itemActivated(m_list->row(it));
    });
    connect(m_list, &ScheduleListWidget::songDropped, this, &ScheduleView::songDropped);
    connect(m_list, &ScheduleListWidget::rowMoved, this, [this](int from, int to) {
        m_service.move(from, to);
        m_service.select(to);
    });
    m_list->installEventFilter(this);

    // Service -> list
    connect(&m_service, &ScheduleService::itemsChanged, this, [this] { rebuildList(); });
    connect(&m_service, &ScheduleService::selectionChanged, this, [this] { syncSelection(); updateButtons(); });

    rebuildList();
    updateButtons();
}

QToolButton* ScheduleView::makeToolButton(const QString& name, const QString& text, int px, bool bold, const QString& rgb,
                                          const QString& tip)
{
    auto* b = new QToolButton(this);
    b->setObjectName(name);
    b->setText(text);
    b->setFixedSize(22, 22);
    QFont f = (px == 12) ? segoeEmoji(px) : segoeSymbol(px);
    f.setBold(bold);
    b->setFont(f);
    b->setCursor(Qt::PointingHandCursor);
    b->setStyleSheet(QStringLiteral("QToolButton#%1{color:rgb(%2);} QToolButton#%1:disabled{color:rgba(%2,110);}")
                         .arg(name, rgb));
    b->setToolTip(tip);
    return b;
}

void ScheduleView::rebuildList()
{
    m_syncing = true;
    m_list->clear();
    for (const ScheduleItem& it : m_service.items())
        m_list->addItem(displayText(it.title));
    m_syncing = false;
    syncSelection();
    updateButtons();
}

void ScheduleView::syncSelection()
{
    m_syncing = true;
    const QSignalBlocker block(m_list);
    m_list->setCurrentRow(m_service.selectedIndex());
    if (m_service.selectedIndex() < 0)
        m_list->clearSelection();
    m_syncing = false;
}

void ScheduleView::updateButtons()
{
    m_remove->setEnabled(m_service.selectedIndex() >= 0); // RemoveItemCommand.CanExecute
    const bool any = m_service.count() > 0;
    m_aSave->setEnabled(true);
    m_aClear->setEnabled(any);
}

int ScheduleView::itemCount() const { return m_list->count(); }

void ScheduleView::setPaneActive(bool active)
{
    m_list->setProperty("paneActive", active);
    m_list->viewport()->update();
}

bool ScheduleView::paneActive() const { return m_list->property("paneActive").toBool(); }

bool ScheduleView::eventFilter(QObject* obj, QEvent* e)
{
    if (obj == m_list && e->type() == QEvent::KeyPress) {
        const int key = static_cast<QKeyEvent*>(e)->key();
        if (key == Qt::Key_Delete) {
            if (m_remove->isEnabled())
                m_remove->click();
            return true;
        }
        if (key == Qt::Key_Up || key == Qt::Key_Down || key == Qt::Key_Return || key == Qt::Key_Enter) {
            // Let the list move its current row first, then request the preview ("UserSelectItem").
            QTimer::singleShot(0, this, [this] {
                const int row = m_list->currentRow();
                if (row >= 0)
                    m_service.userSelect(row);
            });
        }
    }
    return QWidget::eventFilter(obj, e);
}

} // namespace Lux