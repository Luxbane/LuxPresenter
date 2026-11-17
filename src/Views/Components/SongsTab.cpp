#include "Views/Components/SongsTab.h"

#include "Services/DataService.h"
#include "Services/TextRepair.h"
#include "Services/ThemeService.h"
#include "Views/Components/ScheduleView.h"
#include "Views/UiUtil.h"

#include <QAction>
#include <QGridLayout>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QKeyEvent>
#include <QLabel>
#include <QLineEdit>
#include <QMenu>
#include <QMimeData>
#include <QPushButton>
#include <QRegularExpression>
#include <QTimer>
#include <QVBoxLayout>

namespace Lux {

namespace {

QPushButton* cardButton(const QString& text, const QString& style, QWidget* parent)
{
    auto* b = new QPushButton(text, parent);
    b->setCursor(Qt::PointingHandCursor);
    b->setStyleSheet(QStringLiteral("QPushButton{font-size:11px; padding:5px 10px; %1}"
                                    "QPushButton:disabled{color:#94A3B8; background:#F1F5F9; border:1px solid #E2E8F0;}")
                         .arg(style));
    return b;
}

} // namespace

// ---------------------------------------------------------------------------------------
// SongListWidget
// ---------------------------------------------------------------------------------------

SongListWidget::SongListWidget(QWidget* parent) : QTreeWidget(parent)
{
    setDragEnabled(true);
    setAcceptDrops(false);
    setDragDropMode(QAbstractItemView::DragOnly);
    setDefaultDropAction(Qt::CopyAction);
}

QStringList SongListWidget::mimeTypes() const
{
    return {QString::fromLatin1(ScheduleListWidget::kSongMime)};
}

QMimeData* SongListWidget::mimeData(const QList<QTreeWidgetItem*>& items) const
{
    if (items.isEmpty())
        return nullptr;
    auto* m = new QMimeData;
    m->setData(QString::fromLatin1(ScheduleListWidget::kSongMime), items.first()->data(0, Qt::UserRole).toString().toUtf8());
    return m;
}

// ---------------------------------------------------------------------------------------
// SongsTab
// ---------------------------------------------------------------------------------------

SongsTab::SongsTab(DataService& data, QWidget* parent) : QWidget(parent), m_data(data)
{
    setObjectName(QStringLiteral("PanelBg"));
    setAttribute(Qt::WA_StyledBackground, true);
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // 1. Top bar: "Title" [search] [Favorites]
    auto* top = new QFrame(this);
    top->setObjectName(QStringLiteral("SurfaceBarBottom"));
    auto* tl = new QHBoxLayout(top);
    tl->setContentsMargins(6, 4, 6, 4);
    tl->setSpacing(0);
    auto* titleLabel = new QLabel(QStringLiteral("Title"), top);
    titleLabel->setFont(tahoma(11));
    titleLabel->setContentsMargins(2, 0, 8, 0);
    m_titleSearch = new QLineEdit(top);
    m_titleSearch->setObjectName(QStringLiteral("TitleSearch"));
    m_favorites = new QPushButton(QStringLiteral("Favorites"), top);
    m_favorites->setCheckable(true);
    m_favorites->setStyleSheet(QStringLiteral("QPushButton{font-size:11px; padding:2px 8px; margin-left:6px;}"));
    tl->addWidget(titleLabel);
    tl->addWidget(m_titleSearch, 1);
    tl->addWidget(m_favorites);
    root->addWidget(top);

    // 2. Song list with an empty-state card laid over it
    auto* middle = new QWidget(this);
    auto* grid = new QGridLayout(middle);
    grid->setContentsMargins(0, 0, 0, 0);

    m_list = new SongListWidget(middle);
    m_list->setObjectName(QStringLiteral("SongList"));
    m_list->setColumnCount(2);
    m_list->setHeaderLabels({QStringLiteral("Title"), QStringLiteral("Author")});
    m_list->setRootIsDecorated(false);
    m_list->setUniformRowHeights(true);
    m_list->setFrameShape(QFrame::NoFrame);
    m_list->setSelectionMode(QAbstractItemView::SingleSelection);
    m_list->setVerticalScrollMode(QAbstractItemView::ScrollPerPixel);
    m_list->setMouseTracking(true);
    m_list->header()->setSectionsMovable(false);
    m_list->header()->setStretchLastSection(true);
    m_list->setColumnWidth(0, 200);
    m_list->setColumnWidth(1, 120);
    m_list->setProperty("paneActive", false);
    m_list->installEventFilter(this);
    grid->addWidget(m_list, 0, 0);

    auto* card = new QFrame(middle);
    m_emptyCard = card;
    card->setObjectName(QStringLiteral("EmptyCard"));
    card->setMaximumWidth(300);
    card->setStyleSheet(QStringLiteral(
        "QFrame#EmptyCard{background:palette(alternate-base); border:1px solid palette(mid); border-radius:4px;}"));
    auto* cl = new QVBoxLayout(card);
    cl->setContentsMargins(12, 14, 12, 14);
    cl->setSpacing(2);
    auto* icon = new QLabel(QStringLiteral("\U0001F4D6"), card);
    icon->setFont(segoeEmoji(26));
    icon->setAlignment(Qt::AlignHCenter);
    auto* head = new QLabel(QStringLiteral("Song Library is Empty"), card);
    head->setFont(tahoma(12, true));
    head->setAlignment(Qt::AlignHCenter);
    auto* hint = new QLabel(QStringLiteral("Import your church's songs or start fresh:"), card);
    hint->setFont(tahoma(10));
    hint->setStyleSheet(QStringLiteral("color:#64748B;"));
    hint->setAlignment(Qt::AlignHCenter);
    hint->setWordWrap(true);
    hint->setContentsMargins(0, 3, 0, 8);
    cl->addWidget(icon);
    cl->addWidget(head);
    cl->addWidget(hint);

    m_import = cardButton(QStringLiteral("\U0001F4E5 Import Song Library Backup (.json)"),
                          QStringLiteral("background:#0284C7; color:white; font-weight:600; border:1px solid #0284C7;"), card);
    m_openFolder = cardButton(QStringLiteral("\U0001F4C2 Open Songs Database Folder"),
                              QStringLiteral("background:palette(button); color:palette(button-text); border:1px solid palette(mid);"), card);
    m_sample = cardButton(QStringLiteral("\U0001F3B5 Load Sample Songs"),
                          QStringLiteral("background:palette(button); color:palette(button-text); border:1px solid palette(mid);"), card);
    m_create = cardButton(QStringLiteral("\u2795 Create New Song"),
                          QStringLiteral("background:palette(button); color:#16A34A; font-weight:600; border:1px solid #86EFAC;"), card);
    m_import->setToolTip(QStringLiteral("Import a songs backup (.json)"));
    m_openFolder->setToolTip(QStringLiteral("Open the folder that contains songs.json"));
    m_sample->setToolTip(QStringLiteral("Add a few public-domain hymns to the library"));
    cl->addWidget(m_import);
    cl->addWidget(m_openFolder);
    cl->addWidget(m_sample);
    cl->addWidget(m_create);
    grid->addWidget(card, 0, 0, Qt::AlignCenter);
    root->addWidget(middle, 1);

    // 3. Bottom bar: "Search" [lyrics search]
    auto* bottom = new QFrame(this);
    bottom->setObjectName(QStringLiteral("SurfaceBarTop"));
    auto* bl = new QHBoxLayout(bottom);
    bl->setContentsMargins(6, 4, 6, 4);
    bl->setSpacing(0);
    auto* searchLabel = new QLabel(QStringLiteral("Search"), bottom);
    searchLabel->setFont(tahoma(11));
    searchLabel->setContentsMargins(2, 0, 8, 0);
    m_lyricsSearch = new QLineEdit(bottom);
    m_lyricsSearch->setObjectName(QStringLiteral("LyricsSearch"));
    bl->addWidget(searchLabel);
    bl->addWidget(m_lyricsSearch, 1);
    root->addWidget(bottom);

    // Context menu (Add to Schedule / Go Live / Edit / Delete)
    m_contextMenu = new QMenu(this);
    m_aAdd = m_contextMenu->addAction(QString::fromUtf8("\u2795 Add to Schedule"));
    m_aGoLive = m_contextMenu->addAction(QString::fromUtf8("\u25B6 Go Live"));
    m_contextMenu->addSeparator();
    m_aEdit = m_contextMenu->addAction(QString::fromUtf8("\u270F Edit Song..."));
    m_aDelete = m_contextMenu->addAction(QString::fromUtf8("\U0001F5D1 Delete Song"));
    m_list->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(m_list, &QWidget::customContextMenuRequested, this, [this](const QPoint& pos) {
        if (QTreeWidgetItem* it = m_list->itemAt(pos))
            userSelect(it->data(0, Qt::UserRole).toString()); // right-click selects, like a click
        m_contextMenu->popup(m_list->viewport()->mapToGlobal(pos));
    });
    connect(m_aAdd, &QAction::triggered, this, [this] { if (!m_selectedId.isEmpty()) emit addToScheduleRequested(m_selectedId); });
    connect(m_aGoLive, &QAction::triggered, this, [this] { if (!m_selectedId.isEmpty()) emit goLiveRequested(m_selectedId); });
    connect(m_aEdit, &QAction::triggered, this, [this] { if (!m_selectedId.isEmpty()) emit editSongRequested(m_selectedId); });
    connect(m_aDelete, &QAction::triggered, this, [this] { if (!m_selectedId.isEmpty()) emit deleteSongRequested(m_selectedId); });

    // Debounced search (180 ms) with mutually exclusive queries.
    m_debounce = new QTimer(this);
    m_debounce->setSingleShot(true);
    m_debounce->setInterval(180);
    connect(m_debounce, &QTimer::timeout, this, [this] { applyFilter(QString()); });
    connect(m_titleSearch, &QLineEdit::textEdited, this, [this](const QString& t) {
        if (!t.isEmpty() && !m_lyricsSearch->text().isEmpty()) {
            QSignalBlocker b(m_lyricsSearch);
            m_lyricsSearch->clear();
        }
        m_debounce->start();
    });
    connect(m_lyricsSearch, &QLineEdit::textEdited, this, [this](const QString& t) {
        if (!t.isEmpty() && !m_titleSearch->text().isEmpty()) {
            QSignalBlocker b(m_titleSearch);
            m_titleSearch->clear();
        }
        m_debounce->start();
    });
    connect(m_favorites, &QPushButton::toggled, this, [this] { applyFilterImmediate(); });
    m_titleSearch->installEventFilter(this);
    m_lyricsSearch->installEventFilter(this);

    // List interaction
    connect(m_list, &QTreeWidget::itemPressed, this, [this](QTreeWidgetItem* it) {
        if (!m_filling)
            userSelect(it->data(0, Qt::UserRole).toString());
    });
    connect(m_list, &QTreeWidget::itemDoubleClicked, this, [this](QTreeWidgetItem* it) {
        const QString id = it->data(0, Qt::UserRole).toString();
        userSelect(id);
        emit songDoubleClicked(id);
    });

    // Empty-state card
    connect(m_import, &QPushButton::clicked, this, &SongsTab::importBackupRequested);
    connect(m_openFolder, &QPushButton::clicked, this, &SongsTab::openSongsFolderRequested);
    connect(m_sample, &QPushButton::clicked, this, &SongsTab::loadSamplesRequested);
    connect(m_create, &QPushButton::clicked, this, &SongsTab::newSongRequested);

    reload();
}

bool SongsTab::findBestCandidate(const QString& prefix, bool isLyrics, CandidateMatch& out) const
{
    if (prefix.isEmpty())
        return false;

    const QList<Song> allSongs = m_data.songs();
    if (allSongs.isEmpty())
        return false;

    const bool favOnly = m_favorites->isChecked();

    if (!isLyrics) {
        QList<CandidateMatch> candidates;
        for (const Song& s : allSongs) {
            if (favOnly && !s.isFavorite)
                continue;
            const QString title = s.title.trimmed();
            if (title.startsWith(prefix, Qt::CaseInsensitive)) {
                CandidateMatch m;
                m.songId = s.id;
                m.songTitle = title;
                m.matchedPhrase = title;
                m.suffix = title.mid(prefix.length());
                m.isLineStart = true;
                candidates.append(m);
            }
        }
        if (candidates.isEmpty())
            return false;

        std::stable_sort(candidates.begin(), candidates.end(), [](const CandidateMatch& a, const CandidateMatch& b) {
            return QString::localeAwareCompare(a.songTitle.toLower(), b.songTitle.toLower()) < 0;
        });

        out = candidates.first();
        return true;
    }

    // Lyrics search
    QList<CandidateMatch> candidates;
    static const QRegularExpression lineSplitRx(QStringLiteral(R"([\r\n]+)"));
    static const QRegularExpression leadingNumRx(QStringLiteral(R"(^\d+[\.\)\s\-]+)"));

    for (const Song& s : allSongs) {
        if (favOnly && !s.isFavorite)
            continue;
        const QStringList lines = s.lyricsRaw.split(lineSplitRx, Qt::SkipEmptyParts);
        for (const QString& rawLine : lines) {
            QString line = rawLine.trimmed();
            if (line.isEmpty())
                continue;
            if ((line.startsWith(QLatin1Char('[')) && line.endsWith(QLatin1Char(']'))) ||
                (line.endsWith(QLatin1Char(':')) && line.length() < 30))
                continue;

            QString cleanLine = line;
            cleanLine.remove(leadingNumRx);
            cleanLine = cleanLine.trimmed();
            if (cleanLine.isEmpty())
                continue;

            if (cleanLine.startsWith(prefix, Qt::CaseInsensitive)) {
                CandidateMatch m;
                m.songId = s.id;
                m.songTitle = s.title;
                m.matchedPhrase = cleanLine;
                m.suffix = cleanLine.mid(prefix.length());
                m.isLineStart = true;
                candidates.append(m);
            } else {
                int idx = 0;
                while ((idx = cleanLine.indexOf(prefix, idx, Qt::CaseInsensitive)) != -1) {
                    if (idx > 0 && (cleanLine.at(idx - 1).isSpace() || cleanLine.at(idx - 1) == QLatin1Char(',') || cleanLine.at(idx - 1) == QLatin1Char('.'))) {
                        QString phrase = cleanLine.mid(idx).trimmed();
                        CandidateMatch m;
                        m.songId = s.id;
                        m.songTitle = s.title;
                        m.matchedPhrase = phrase;
                        m.suffix = phrase.mid(prefix.length());
                        m.isLineStart = false;
                        candidates.append(m);
                        break;
                    }
                    idx += prefix.length();
                }
            }
        }
    }

    if (candidates.isEmpty()) {
        for (const Song& s : allSongs) {
            if (favOnly && !s.isFavorite)
                continue;
            const QString title = s.title.trimmed();
            if (title.startsWith(prefix, Qt::CaseInsensitive)) {
                CandidateMatch m;
                m.songId = s.id;
                m.songTitle = s.title;
                m.matchedPhrase = title;
                m.suffix = title.mid(prefix.length());
                m.isLineStart = false;
                candidates.append(m);
            }
        }
    }

    if (candidates.isEmpty())
        return false;

    std::stable_sort(candidates.begin(), candidates.end(), [](const CandidateMatch& a, const CandidateMatch& b) {
        if (a.isLineStart != b.isLineStart)
            return a.isLineStart > b.isLineStart;
        const int cmp = QString::localeAwareCompare(a.matchedPhrase.toLower(), b.matchedPhrase.toLower());
        if (cmp != 0)
            return cmp < 0;
        return QString::localeAwareCompare(a.songTitle.toLower(), b.songTitle.toLower()) < 0;
    });

    out = candidates.first();
    return true;
}

bool SongsTab::hasSelectedGhost(const QLineEdit* edit)
{
    if (!edit || !edit->hasSelectedText())
        return false;
    const int start = edit->selectionStart();
    const int len = edit->selectedText().length();
    return (start == edit->cursorPosition() && start + len == edit->text().length());
}

QString SongsTab::titleQuery() const
{
    return hasSelectedGhost(m_titleSearch) ? m_titleSearch->text().left(m_titleSearch->cursorPosition()) : m_titleSearch->text();
}

QString SongsTab::lyricsQuery() const
{
    return hasSelectedGhost(m_lyricsSearch) ? m_lyricsSearch->text().left(m_lyricsSearch->cursorPosition()) : m_lyricsSearch->text();
}
bool SongsTab::favoritesOnly() const { return m_favorites->isChecked(); }

void SongsTab::setFavoritesOnly(bool on) { m_favorites->setChecked(on); }

void SongsTab::setTitleQuery(const QString& q)
{
    if (!q.isEmpty() && !m_lyricsSearch->text().isEmpty())
        m_lyricsSearch->clear();
    m_titleSearch->setText(q);
    m_debounce->start();
}

void SongsTab::setLyricsQuery(const QString& q)
{
    if (!q.isEmpty() && !m_titleSearch->text().isEmpty())
        m_titleSearch->clear();
    m_lyricsSearch->setText(q);
    m_debounce->start();
}

void SongsTab::reload(const QString& targetSongId)
{
    const QString target = targetSongId.isEmpty() ? m_selectedId : targetSongId;
    const QList<Song> all = m_data.songs();
    m_index = SongSearch(all);
    m_hasSongs = !all.isEmpty();
    m_emptyCard->setVisible(!m_hasSongs);
    m_debounce->stop();
    applyFilter(target);
}

void SongsTab::applyFilterImmediate(const QString& targetSongId)
{
    m_debounce->stop();
    applyFilter(targetSongId);
}

void SongsTab::fillList()
{
    m_filling = true;
    m_list->clear();
    for (const Song& s : std::as_const(m_filtered)) {
        auto* item = new QTreeWidgetItem(m_list, {displayText(s.title), displayText(s.author)});
        item->setData(0, Qt::UserRole, s.id);
    }
    m_filling = false;
}

QString SongsTab::currentListSongId() const
{
    const QTreeWidgetItem* it = m_list->currentItem();
    return it ? it->data(0, Qt::UserRole).toString() : QString();
}

void SongsTab::setSelected(const QString& id, bool forcePreview)
{
    const bool changed = (id != m_selectedId);
    m_selectedId = id;
    m_filling = true;
    QTreeWidgetItem* match = nullptr;
    for (int i = 0; i < m_list->topLevelItemCount(); ++i)
        if (m_list->topLevelItem(i)->data(0, Qt::UserRole).toString() == id) {
            match = m_list->topLevelItem(i);
            break;
        }
    m_list->setCurrentItem(match);
    if (match)
        m_list->scrollToItem(match);
    m_filling = false;
    if (changed)
        emit selectionStateChanged(!id.isEmpty());
    if (!id.isEmpty() && (changed || forcePreview))
        emit songSelectedForPreview(id);
}

void SongsTab::applyFilter(const QString& targetSongId)
{
    const QString target = targetSongId.isEmpty() ? m_selectedId : targetSongId;
    const QString tQuery = hasSelectedGhost(m_titleSearch) ? m_titleSearch->text().left(m_titleSearch->cursorPosition()) : m_titleSearch->text();
    const QString lQuery = hasSelectedGhost(m_lyricsSearch) ? m_lyricsSearch->text().left(m_lyricsSearch->cursorPosition()) : m_lyricsSearch->text();
    const bool hasQuery = !tQuery.trimmed().isEmpty() || !lQuery.trimmed().isEmpty();
    // A cleared lyrics search re-previews the selected song so that the matching stanza it had highlighted does not linger.
    const bool lyricsQueryCleared = m_hadLyricsQuery && lQuery.trimmed().isEmpty();
    m_hadLyricsQuery = !lQuery.trimmed().isEmpty();
    m_filtered = m_index.filter(tQuery, lQuery, m_favorites->isChecked());
    fillList();

    if (!target.isEmpty()) {
        for (const Song& s : std::as_const(m_filtered)) {
            if (s.id == target) {
                if (m_selectedId != target)
                    setSelected(target, false);
                else {
                    setSelected(target, hasQuery || lyricsQueryCleared); // same song: refresh the list highlight, re-preview on searches
                }
                return;
            }
        }
    }
    if (!m_filtered.isEmpty()) {
        const QString first = m_filtered.first().id;
        setSelected(first, /*forcePreview=*/true); // first result is selected and previewed
    } else {
        setSelected(QString(), false);
    }
}

void SongsTab::userSelect(const QString& songId)
{
    // UserSelectSong: change selection, or re-request the preview when it is already selected.
    setSelected(songId, true);
}

void SongsTab::setPaneActive(bool active)
{
    m_list->setProperty("paneActive", active);
    repolish(m_list);
}

bool SongsTab::paneActive() const { return m_list->property("paneActive").toBool(); }

bool SongsTab::eventFilter(QObject* obj, QEvent* e)
{
    if (e->type() == QEvent::FocusOut) {
        if (obj == m_titleSearch || obj == m_lyricsSearch) {
            auto* edit = static_cast<QLineEdit*>(obj);
            if (hasSelectedGhost(edit)) {
                const int cur = edit->cursorPosition();
                const QString base = edit->text().left(cur);
                QSignalBlocker b(edit);
                edit->setText(base);
                edit->setCursorPosition(base.length());
            }
        }
    } else if (e->type() == QEvent::KeyPress) {
        auto* ke = static_cast<QKeyEvent*>(e);
        const int key = ke->key();
        if (obj == m_list) {
            if (key == Qt::Key_Delete) {
                if (!m_selectedId.isEmpty())
                    emit deleteSongRequested(m_selectedId);
                return true;
            }
            if (key == Qt::Key_Return || key == Qt::Key_Enter) {
                // Enter on the list = Go Live for the selected song (the same action as the Go Live command). The event is
                // consumed so the view's own "activated" handling cannot run it a second time; key repeat is ignored.
                const QString id = currentListSongId();
                if (!ke->isAutoRepeat() && ke->modifiers().testFlag(Qt::ControlModifier) == false && !id.isEmpty()) {
                    if (id != m_selectedId)
                        setSelected(id, true);   // keyboard selection that has not been previewed yet
                    emit goLiveRequested(id);
                }
                return true;
            }
            if (key == Qt::Key_Up || key == Qt::Key_Down || key == Qt::Key_PageUp || key == Qt::Key_PageDown ||
                key == Qt::Key_Home || key == Qt::Key_End) {
                QTimer::singleShot(0, this, [this] {
                    const QString id = currentListSongId();
                    if (!id.isEmpty())
                        userSelect(id);
                });
            }
        } else if (obj == m_titleSearch || obj == m_lyricsSearch) {
            auto* edit = static_cast<QLineEdit*>(obj);
            const bool isLyrics = (obj == m_lyricsSearch);
            if (key == Qt::Key_Backspace) {
                if (hasSelectedGhost(edit)) {
                    m_isDeleting = true;
                    const int cur = edit->cursorPosition();
                    const QString base = edit->text().left(cur);
                    const QString newText = base.left(std::max(0, cur - 1));
                    {
                        QSignalBlocker b(edit);
                        edit->setText(newText);
                        edit->setCursorPosition(newText.length());
                    }
                    m_debounce->start();
                    return true;
                }
                m_isDeleting = true;
            } else if (key == Qt::Key_Delete) {
                if (hasSelectedGhost(edit)) {
                    m_isDeleting = true;
                    const int cur = edit->cursorPosition();
                    const QString base = edit->text().left(cur);
                    {
                        QSignalBlocker b(edit);
                        edit->setText(base);
                        edit->setCursorPosition(cur);
                    }
                    m_debounce->start();
                    return true;
                }
                m_isDeleting = true;
            } else if (key == Qt::Key_Tab || (key == Qt::Key_Right && edit->cursorPosition() == edit->selectionStart())) {
                if (hasSelectedGhost(edit)) {
                    edit->deselect();
                    edit->setCursorPosition(edit->text().length());
                    applyFilterImmediate();
                    return true;
                }
            } else if (key == Qt::Key_Return || key == Qt::Key_Enter) {
                if (hasSelectedGhost(edit)) {
                    edit->deselect();
                    edit->setCursorPosition(edit->text().length());
                    applyFilterImmediate();
                    if (!m_selectedId.isEmpty()) {
                        emit goLiveRequested(m_selectedId);
                    }
                    return true;
                }
                applyFilterImmediate();
                m_list->setFocus();
                return true;
            } else if (key == Qt::Key_Down || key == Qt::Key_Up) {
                if (hasSelectedGhost(edit)) {
                    const int cur = edit->cursorPosition();
                    const QString base = edit->text().left(cur);
                    QSignalBlocker b(edit);
                    edit->setText(base);
                    edit->setCursorPosition(base.length());
                }
                m_list->setFocus();
                const int row = m_list->indexOfTopLevelItem(m_list->currentItem());
                const int n = m_list->topLevelItemCount();
                const int next = key == Qt::Key_Down ? std::min(n - 1, row + 1) : std::max(0, row - 1);
                if (n > 0 && next != row) {
                    m_list->setCurrentItem(m_list->topLevelItem(next));
                    userSelect(currentListSongId());
                }
                return true;
            } else if (key == Qt::Key_Escape) {
                { QSignalBlocker b1(m_titleSearch), b2(m_lyricsSearch); m_titleSearch->clear(); m_lyricsSearch->clear(); }
                applyFilterImmediate();
                return true;
            } else if (!ke->text().isEmpty() && !ke->modifiers().testFlag(Qt::ControlModifier) && !ke->modifiers().testFlag(Qt::AltModifier)) {
                // Input Guard & Constrained Autocomplete:
                const QString input = ke->text();
                // Determine candidate typed prefix
                QString candidateTyped;
                if (hasSelectedGhost(edit)) {
                    candidateTyped = edit->text().left(edit->cursorPosition()) + input;
                } else if (edit->hasSelectedText()) {
                    candidateTyped = edit->text().left(edit->selectionStart()) + input + edit->text().mid(edit->selectionStart() + edit->selectedText().length());
                } else {
                    candidateTyped = edit->text().left(edit->cursorPosition()) + input + edit->text().mid(edit->cursorPosition());
                }

                CandidateMatch best;
                if (findBestCandidate(candidateTyped, isLyrics, best)) {
                    // Accepted! Mutually exclusive clear of the other search box
                    QLineEdit* other = isLyrics ? m_titleSearch : m_lyricsSearch;
                    if (!other->text().isEmpty()) {
                        QSignalBlocker b(other);
                        other->clear();
                    }
                    m_isDeleting = false;
                    const int typedLen = candidateTyped.length();
                    const int suffixLen = best.suffix.length();
                    const int fullLen = typedLen + suffixLen;
                    {
                        QSignalBlocker b(edit);
                        edit->setText(candidateTyped + best.suffix);
                        if (suffixLen > 0)
                            edit->setSelection(fullLen, -suffixLen);
                        else
                            edit->setCursorPosition(fullLen);
                    }
                    m_debounce->stop();
                    applyFilter(best.songId);
                    setSelected(best.songId, true);
                    return true;
                } else {
                    // Rejected! Input Guard blocks typos & invalid continuation
                    return true;
                }
            } else {
                m_isDeleting = false;
            }
        }
    }
    return QWidget::eventFilter(obj, e);
}

} // namespace Lux
