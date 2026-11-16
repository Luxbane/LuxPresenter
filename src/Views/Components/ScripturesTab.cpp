#include "Views/Components/ScripturesTab.h"
#include "Services/DataService.h"
#include "Services/ThemeService.h"
#include "Views/Components/TabStrip.h"
#include "Views/UiUtil.h"
#include <QComboBox>
#include <QFile>
#include <QFileDialog>
#include <QHBoxLayout>
#include <QLabel>
#include <QLineEdit>
#include <QListWidget>
#include <QMenu>
#include <QMessageBox>
#include <QPainter>
#include <QPushButton>
#include <QSaveFile>
#include <QStyledItemDelegate>
#include <QTextBlockFormat>
#include <QTextCursor>
#include <QTextDocument>
#include <QVBoxLayout>
#include <cmath>
namespace Lux {
namespace {
constexpr int kVerseRole = Qt::UserRole;
constexpr int kHitRole = Qt::UserRole + 1;

class VerseItemDelegate : public QStyledItemDelegate
{
public:
    using QStyledItemDelegate::QStyledItemDelegate;

    QSize sizeHint(const QStyleOptionViewItem& opt, const QModelIndex& idx) const override
    {
        int w = opt.rect.width();
        if (w <= 0 && opt.widget)
            w = opt.widget->width() - 20;
        if (w <= 0)
            w = 300;
        QTextDocument doc;
        setupDoc(doc, qMax(60, w - 24), makeHtml(idx, false));
        const int h = static_cast<int>(std::ceil(doc.size().height())) + 14;
        return QSize(w, h);
    }

    void paint(QPainter* p, const QStyleOptionViewItem& opt, const QModelIndex& idx) const override
    {
        p->save();
        p->setRenderHint(QPainter::Antialiasing);

        const QRect r = opt.rect.adjusted(2, 2, -2, -2);
        const bool selected = opt.state & QStyle::State_Selected;
        const bool hover = opt.state & QStyle::State_MouseOver;
        const bool dark = ThemeService::instance().theme().isDark;

        if (selected) {
            p->setPen(QPen(dark ? QColor(0x47, 0x55, 0x69) : QColor(0xCB, 0xD5, 0xE1), 1));
            p->setBrush(dark ? QColor(0x1E, 0x29, 0x3B) : QColor(0xF8, 0xFA, 0xFC));
            p->drawRoundedRect(r, 2, 2);
        } else if (hover) {
            p->setPen(QPen(dark ? QColor(0x33, 0x41, 0x55) : QColor(0xE2, 0xE8, 0xF0), 1));
            p->setBrush(dark ? QColor(0x1E, 0x29, 0x3B) : QColor(0xF1, 0xF5, 0xF9));
            p->drawRoundedRect(r, 2, 2);
        }

        QTextDocument doc;
        setupDoc(doc, r.width() - 16, makeHtml(idx, dark));

        p->translate(r.left() + 8, r.top() + 7);
        doc.drawContents(p);
        p->restore();
    }

private:
    static void setupDoc(QTextDocument& doc, int textWidth, const QString& html)
    {
        doc.setDefaultFont(tahoma(11));
        doc.setTextWidth(textWidth);
        doc.setHtml(html);
        QTextCursor cursor(&doc);
        cursor.select(QTextCursor::Document);
        QTextBlockFormat bf;
        bf.setLineHeight(130, QTextBlockFormat::ProportionalHeight);
        cursor.mergeBlockFormat(bf);
    }

    static QString makeHtml(const QModelIndex& idx, bool dark)
    {
        const QString full = idx.data(Qt::DisplayRole).toString();
        const QString textColor = dark ? QStringLiteral("#F1F5F9") : QStringLiteral("#0F172A");
        const QString prefixColor = dark ? QStringLiteral("#FFFFFF") : QStringLiteral("#000000");
        const int sep = full.indexOf(QStringLiteral("   "));
        if (sep > 0) {
            const QString prefix = full.left(sep).trimmed().toHtmlEscaped();
            const QString text = full.mid(sep + 3).trimmed().toHtmlEscaped();
            return QStringLiteral("<span style='color:%1;'><b style='color:%2;'>%3</b>&nbsp;&nbsp;&nbsp;%4</span>")
                .arg(textColor, prefixColor, prefix, text);
        }
        return QStringLiteral("<span style='color:%1;'>%2</span>").arg(textColor, full.toHtmlEscaped());
    }
};
QPushButton* smallButton(const QString& emoji, const QString& label, const QString& tip, QWidget* parent)
{
    auto* b = new QPushButton(parent);
    b->setFixedHeight(22);
    b->setCursor(Qt::PointingHandCursor);
    b->setToolTip(tip);
    auto* lay = new QHBoxLayout(b);
    lay->setContentsMargins(6, 0, 6, 0);
    lay->setSpacing(3);
    auto* e = new QLabel(emoji, b);
    e->setFont(segoeEmoji(11));
    e->setAttribute(Qt::WA_TransparentForMouseEvents);
    auto* l = new QLabel(label, b);
    l->setFont(tahoma(10, true));
    l->setAttribute(Qt::WA_TransparentForMouseEvents);
    lay->addWidget(e);
    lay->addWidget(l);
    // A QPushButton without its own text ignores its child layout when sizing, so reserve
    // the width of the icon + label explicitly (this is what clipped "Books"/"Import").
    b->setMinimumWidth(lay->sizeHint().width());
    return b;
}
QPushButton* arrowButton(const QString& text, const QString& tip, QWidget* parent)
{
    auto* b = new QPushButton(text, parent);
    b->setFixedSize(22, 22);
    b->setFont(segoeSymbol(9));
    b->setStyleSheet(QStringLiteral("QPushButton{padding:0;}"));
    b->setCursor(Qt::PointingHandCursor);
    b->setToolTip(tip);
    return b;
}
} // namespace
ScripturesTab::ScripturesTab(DataService& data, QWidget* parent) : QWidget(parent), m_data(data), m_lib(data.bibles())
{
    setObjectName(QStringLiteral("PanelBg"));
    setAttribute(Qt::WA_StyledBackground, true);
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);
    // 1. "Scripture" [search] [Books] [Import]
    auto* top = new QFrame(this);
    top->setObjectName(QStringLiteral("SurfaceBarBottom"));
    auto* tl = new QHBoxLayout(top);
    tl->setContentsMargins(6, 4, 6, 4);
    tl->setSpacing(0);
    auto* label = new QLabel(QStringLiteral("Scripture"), top);
    label->setFont(tahoma(11, true));
    label->setContentsMargins(2, 0, 8, 0);
    m_search = new QLineEdit(top);
    m_search->setObjectName(QStringLiteral("scriptureSearch"));
    m_search->setClearButtonEnabled(true);
    m_search->setPlaceholderText(QStringLiteral("John 3:16  or  words to find"));
    m_search->setToolTip(QStringLiteral("Type a reference (John 3:16, 1 Cor 13, Ps 23:1-3) or words to search for"));
    m_books = smallButton(QStringLiteral("\U0001F4D6"), QStringLiteral("Books"), QStringLiteral("Quick Book Index"), top);
    m_import = smallButton(QStringLiteral("\U0001F4E5"), QStringLiteral("Import"),
                           QStringLiteral("Import a Bible translation (.json), save a sample file, remove a translation"), top);
    m_booksMenu = new QMenu(this);
    m_importMenu = new QMenu(this);
    m_books->setMenu(m_booksMenu);
    m_import->setMenu(m_importMenu);
    tl->addWidget(label);
    tl->addWidget(m_search, 1);
    tl->addSpacing(4);
    tl->addWidget(m_books);
    tl->addSpacing(4);
    tl->addWidget(m_import);
    root->addWidget(top);
    // 2. Chapter navigation bar
    auto* nav = new QFrame(this);
    nav->setObjectName(QStringLiteral("SurfaceBarBottom"));
    auto* nl = new QHBoxLayout(nav);
    nl->setContentsMargins(6, 3, 6, 3);
    nl->setSpacing(4);
    m_prev = arrowButton(QStringLiteral("\u25C0"), QStringLiteral("Previous Chapter"), nav);
    m_bookCombo = new QComboBox(nav);
    m_bookCombo->setFixedHeight(22);
    m_chapterCombo = new QComboBox(nav);
    m_chapterCombo->setFixedSize(54, 22);
    m_chapterCombo->setToolTip(QStringLiteral("Chapter"));
    m_next = arrowButton(QStringLiteral("\u25B6"), QStringLiteral("Next Chapter"), nav);
    nl->addWidget(m_prev);
    nl->addWidget(m_bookCombo, 1);
    nl->addWidget(m_chapterCombo);
    nl->addWidget(m_next);
    root->addWidget(nav);
    // message line (search outcome)
    m_message = new QLabel(this);
    m_message->setObjectName(QStringLiteral("scriptureMessage"));
    m_message->setWordWrap(true);
    m_message->setContentsMargins(8, 2, 8, 2);
    m_message->hide();
    root->addWidget(m_message);
    // 3. Verse list
    m_verses = new QListWidget(this);
    m_verses->setObjectName(QStringLiteral("VerseList"));
    m_verses->setFrameShape(QFrame::NoFrame);
    m_verses->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    m_verses->setWordWrap(true);
    m_verses->setSpacing(4);
    m_verses->setResizeMode(QListView::Adjust);
    m_verses->setItemDelegate(new VerseItemDelegate(m_verses));
    m_verses->setStyleSheet(QStringLiteral("QListWidget#VerseList { background: %1; border: none; outline: none; }")
        .arg(ThemeService::instance().theme().listBg.name()));
    m_verses->setSelectionMode(QAbstractItemView::ExtendedSelection);
    m_verses->setContextMenuPolicy(Qt::CustomContextMenu);
    new ActiveSelectionTracker(m_verses);
    root->addWidget(m_verses, 1);
    // 4. Translation tabs
    auto* bottom = new QFrame(this);
    bottom->setObjectName(QStringLiteral("SurfaceBarTop"));
    auto* bl = new QHBoxLayout(bottom);
    bl->setContentsMargins(2, 0, 2, 0);
    bl->setSpacing(0);
    m_versions = new TabStrip(TabStrip::Density::Wide, /*topBorder=*/false, bottom);
    m_versions->setStyleSheet(QStringLiteral("QFrame{background:transparent;}"));
    bl->addWidget(m_versions, 1);
    root->addWidget(bottom);
    // ---- behaviour ----
    m_debounce.setSingleShot(true);
    m_debounce.setInterval(350);
    connect(&m_debounce, &QTimer::timeout, this, [this] { runSearch(m_search->text()); });
    connect(m_search, &QLineEdit::textChanged, this, [this] { m_debounce.start(); });
    connect(m_search, &QLineEdit::returnPressed, this, [this] { m_debounce.stop(); runSearch(m_search->text()); });
    connect(m_bookCombo, &QComboBox::activated, this, [this](int i) {
        if (!m_loading && i >= 0)
            showChapter(m_bookCombo->itemData(i).toInt(), 1);
    });
    connect(m_chapterCombo, &QComboBox::activated, this, [this](int i) {
        if (!m_loading && i >= 0)
            showChapter(m_book, m_chapterCombo->itemData(i).toInt());
    });
    connect(m_prev, &QPushButton::clicked, this, [this] { step(-1); });
    connect(m_next, &QPushButton::clicked, this, [this] { step(+1); });
    connect(m_verses, &QListWidget::itemSelectionChanged, this, &ScripturesTab::onVerseSelection);
    connect(m_verses, &QListWidget::itemActivated, this, [this] {
        const BiblePassage p = passageFromSelection();
        if (p.isValid())
            emit passageActivated(p);
    });
    connect(&ThemeService::instance(), &ThemeService::themeChanged, this, [this](bool) {
        m_verses->setStyleSheet(QStringLiteral("QListWidget#VerseList { background: %1; border: none; outline: none; }")
            .arg(ThemeService::instance().theme().listBg.name()));
        m_verses->viewport()->update();
    });
    connect(m_verses, &QListWidget::customContextMenuRequested, this, [this](const QPoint& pos) {
        QListWidgetItem* it = m_verses->itemAt(pos);
        if (it && !it->isSelected()) {
            m_verses->clearSelection();
            it->setSelected(true);
        }
        const BiblePassage p = passageFromSelection();
        if (!p.isValid())
            return;
        QMenu menu(this);
        connect(menu.addAction(QStringLiteral("Go Live")), &QAction::triggered, this, [this, p] { emit passageActivated(p); });
        connect(menu.addAction(QStringLiteral("Add to Schedule")), &QAction::triggered, this, [this, p] { emit addToScheduleRequested(p); });
        if (m_results)
            connect(menu.addAction(QStringLiteral("Open Chapter")), &QAction::triggered, this, [this, p] { m_search->clear(); showChapter(p.book, p.chapter, p.verses); });
        menu.exec(m_verses->viewport()->mapToGlobal(pos));
    });
    connect(m_versions, &TabStrip::currentChanged, this, [this](int i) {
        if (!m_loading && i >= 0)
            setTranslation(m_versions->tabBar()->tabData(i).toString());
    });
    connect(m_importMenu->addAction(QStringLiteral("Import Translation (.json)...")), &QAction::triggered, this, &ScripturesTab::promptImport);
    connect(m_importMenu->addAction(QStringLiteral("Save Sample File...")), &QAction::triggered, this, &ScripturesTab::promptSaveSample);
    connect(m_importMenu->addAction(QStringLiteral("Remove Current Translation...")), &QAction::triggered, this, &ScripturesTab::promptRemove);
    // initial translation: the saved choice, else the KJV
    m_current = m_lib.find(m_data.settings().selectedBibleVersion);
    if (!m_current)
        m_current = m_lib.defaultTranslation();
    rebuildVersionTabs();
    if (m_current) {
        const BibleTranslation::Book* first = m_current->books.isEmpty() ? nullptr : &m_current->books.first();
        m_book = first ? first->id : 0;
        m_chapter = 1;
        fillBooks();
        buildBooksMenu();
        fillVerses();
    } else {
        setMessage(QStringLiteral("No Bible text is installed. Use Import to add a translation (Data/bibles.json is missing)."), true);
    }
    for (const QString& w : m_lib.loadWarnings())
        qWarning("Bible: %s", qPrintable(w));
}
QString ScripturesTab::selectedVersion() const { return m_current ? m_current->id : QString(); }
QString ScripturesTab::message() const { return m_message->isHidden() ? QString() : m_message->text(); }
void ScripturesTab::setMessage(const QString& text, bool problem)
{
    m_message->setText(text);
    m_message->setStyleSheet(problem ? QStringLiteral("color:#B91C1C;") : QStringLiteral("color:#64748B;"));
    m_message->setVisible(!text.isEmpty());
}
void ScripturesTab::rebuildVersionTabs()
{
    const bool was = m_loading;
    m_loading = true;
    m_versions->clear();
    int select = 0;
    for (const BibleTranslation* t : m_lib.translations()) {
        const int i = m_versions->addTab(t->id, t->id);
        m_versions->setTabToolTip(i, t->name + (t->copyright.isEmpty() ? QString() : QStringLiteral("\n") + t->copyright));
        if (t == m_current)
            select = i;
    }
    m_versions->setCurrentIndex(select);
    m_loading = was;
}
void ScripturesTab::fillBooks()
{
    const bool was = m_loading;
    m_loading = true;
    m_bookCombo->clear();
    if (m_current)
        for (const auto& b : m_current->books)
            m_bookCombo->addItem(b.name, b.id);
    m_bookCombo->setCurrentIndex(std::max(0, m_bookCombo->findData(m_book)));
    fillChapters();
    m_loading = was;
}
void ScripturesTab::fillChapters()
{
    const bool was = m_loading;
    m_loading = true;
    m_chapterCombo->clear();
    const int n = m_current ? m_current->chapterCount(m_book) : 0;
    for (int c = 1; c <= n; ++c)
        m_chapterCombo->addItem(QString::number(c), c);
    m_chapterCombo->setCurrentIndex(std::max(0, m_chapterCombo->findData(m_chapter)));
    const bool hasData = m_current && n > 0;
    m_bookCombo->setEnabled(hasData);
    m_chapterCombo->setEnabled(hasData);
    // previous / next chapter, across book boundaries
    bool atStart = true, atEnd = true;
    if (hasData) {
        atStart = m_chapter <= 1 && m_current->books.first().id == m_book;
        atEnd = m_chapter >= n && m_current->books.last().id == m_book;
    }
    m_prev->setEnabled(!atStart);
    m_next->setEnabled(!atEnd);
    m_loading = was;
}
void ScripturesTab::buildBooksMenu()
{
    m_booksMenu->clear();
    if (!m_current)
        return;
    QMenu* ot = m_booksMenu->addMenu(QStringLiteral("Old Testament"));
    QMenu* nt = m_booksMenu->addMenu(QStringLiteral("New Testament"));
    for (const auto& b : m_current->books) {
        const BookInfo* info = BibleLibrary::bookInfo(b.id);
        QAction* a = (info && !info->oldTestament ? nt : ot)->addAction(b.name);
        const int id = b.id;
        connect(a, &QAction::triggered, this, [this, id] { m_search->clear(); showChapter(id, 1); });
    }
}
void ScripturesTab::fillVerses(const QList<int>& select)
{
    const bool was = m_loading;
    m_loading = true;
    m_results = false;
    m_hits.clear();
    m_verses->clear();
    QList<QListWidgetItem*> toSelect;
    if (m_current) {
        for (int v : m_current->versesOf(m_book, m_chapter)) {
            auto* it = new QListWidgetItem(QStringLiteral("%1 : %2   %3").arg(m_chapter).arg(v).arg(m_current->verse(m_book, m_chapter, v)));
            it->setData(kVerseRole, v);
            m_verses->addItem(it);
            if (select.contains(v))
                toSelect << it;
        }
    }
    for (QListWidgetItem* it : toSelect)
        it->setSelected(true);
    if (!toSelect.isEmpty())
        m_verses->scrollToItem(toSelect.first(), QAbstractItemView::PositionAtTop);
    m_loading = was;
    if (!toSelect.isEmpty())
        onVerseSelection();
}
bool ScripturesTab::setTranslation(const QString& id)
{
    const BibleTranslation* t = m_lib.find(id);
    if (!t)
        return false;
    const QList<int> keep = passageFromSelection().verses;
    const bool hadResults = m_results;
    m_current = t;
    if (!m_current->book(m_book))
        m_book = m_current->books.isEmpty() ? 0 : m_current->books.first().id;
    if (m_chapter > m_current->chapterCount(m_book))
        m_chapter = 1;
    rebuildVersionTabs();
    fillBooks();
    buildBooksMenu();
    fillVerses(hadResults ? QList<int>() : keep);
    saveSelectedVersion();
    if (hadResults || !m_search->text().trimmed().isEmpty())
        runSearch(m_search->text());
    else
        setMessage({});
    return true;
}
void ScripturesTab::saveSelectedVersion()
{
    if (!m_current || m_data.settings().selectedBibleVersion == m_current->id)
        return;
    AppSettings s = m_data.settings();
    s.selectedBibleVersion = m_current->id;
    m_data.saveSettings(s);
}
void ScripturesTab::showChapter(int book, int chapter, const QList<int>& selectVerses)
{
    if (!m_current || !m_current->book(book))
        return;
    m_book = book;
    m_chapter = std::clamp(chapter, 1, std::max(1, m_current->chapterCount(book)));
    fillBooks();
    fillVerses(selectVerses);
}
void ScripturesTab::step(int delta)
{
    if (!m_current || m_current->books.isEmpty())
        return;
    int book = m_book, chapter = m_chapter + delta;
    int idx = 0;
    for (int i = 0; i < m_current->books.size(); ++i)
        if (m_current->books[i].id == book)
            idx = i;
    if (chapter < 1) {
        if (idx == 0)
            return;
        book = m_current->books[idx - 1].id;
        chapter = m_current->chapterCount(book);
    } else if (chapter > m_current->chapterCount(book)) {
        if (idx + 1 >= m_current->books.size())
            return;
        book = m_current->books[idx + 1].id;
        chapter = 1;
    }
    m_search->clear();
    showChapter(book, chapter);
}
void ScripturesTab::runSearch(const QString& text)
{
    if (!m_current)
        return;
    if (text.trimmed().isEmpty()) {
        setMessage({});
        if (m_results)
            fillVerses();
        return;
    }
    const BibleSearchResult r = BibleLibrary::search(*m_current, text);
    switch (r.kind) {
    case BibleSearchResult::Kind::Reference: {
        QList<int> verses;
        if (r.verse > 0)
            for (int v = r.verse; v <= std::max(r.verse, r.verseEnd); ++v)
                verses << v;
        showChapter(r.book, r.chapter, verses);
        setMessage(r.verse > 0 ? BibleLibrary::formatReference(m_current->bookName(r.book), r.chapter, verses) : QStringLiteral("%1 %2").arg(m_current->bookName(r.book)).arg(r.chapter));
        break;
    }
    case BibleSearchResult::Kind::Text: {
        const bool was = m_loading;
        m_loading = true;
        m_results = true;
        m_hits = r.hits;
        m_verses->clear();
        for (int i = 0; i < r.hits.size(); ++i) {
            const BibleHit& h = r.hits[i];
            auto* it = new QListWidgetItem(QStringLiteral("%1 %2 : %3   %4").arg(m_current->bookName(h.book)).arg(h.chapter).arg(h.verse).arg(h.text));
            it->setData(kHitRole, i);
            m_verses->addItem(it);
        }
        m_loading = was;
        setMessage(r.message, r.hits.isEmpty());
        break;
    }
    case BibleSearchResult::Kind::None:
        setMessage(r.message.isEmpty() ? QStringLiteral("Nothing found.") : r.message, true);
        break;
    }
}
BiblePassage ScripturesTab::passageFromSelection() const
{
    if (!m_current)
        return {};
    const auto items = m_verses->selectedItems();
    if (items.isEmpty())
        return {};
    if (!m_results) {
        QList<int> verses;
        for (const QListWidgetItem* it : items)
            verses << it->data(kVerseRole).toInt();
        return BibleLibrary::passage(*m_current, m_book, m_chapter, verses);
    }
    // search results: the selected hits that lie in the chapter of the first one
    QList<int> rows;
    for (const QListWidgetItem* it : items)
        rows << it->data(kHitRole).toInt();
    std::sort(rows.begin(), rows.end());
    const BibleHit first = m_hits.value(rows.first());
    QList<int> verses;
    for (int r : rows) {
        const BibleHit h = m_hits.value(r);
        if (h.book == first.book && h.chapter == first.chapter)
            verses << h.verse;
    }
    return BibleLibrary::passage(*m_current, first.book, first.chapter, verses);
}
BiblePassage ScripturesTab::currentPassage() const { return passageFromSelection(); }
void ScripturesTab::onVerseSelection()
{
    if (m_loading)
        return;
    const BiblePassage p = passageFromSelection();
    if (p.isValid())
        emit passageSelected(p);
}
// ---- translations -----------------------------------------------------------------------
BibleImportResult ScripturesTab::importTranslation(const QString& path)
{
    BibleImportResult r = m_lib.importFile(path);
    if (r.ok) {
        emit translationsChanged();
        setTranslation(r.id);
        setMessage(QStringLiteral("Imported %1 (%2 books, %3 verses).").arg(r.name).arg(r.books).arg(r.verses));
    }
    return r;
}
bool ScripturesTab::removeTranslation(const QString& id, QString* error)
{
    const bool wasCurrent = m_current && m_current->id.compare(id, Qt::CaseInsensitive) == 0;
    const BibleTranslation* fallback = m_lib.defaultTranslation();
    if (wasCurrent && fallback && fallback->id.compare(id, Qt::CaseInsensitive) == 0)
        return m_lib.remove(id, error);   // the built-in one: refused by the library
    if (wasCurrent)
        m_current = fallback;
    if (!m_lib.remove(id, error)) {
        if (wasCurrent)
            m_current = m_lib.find(id);
        return false;
    }
    emit translationsChanged();
    if (wasCurrent) {
        m_book = 0;
        setTranslation(fallback->id);
    } else {
        rebuildVersionTabs();
    }
    return true;
}
void ScripturesTab::promptImport()
{
    const QString path = QFileDialog::getOpenFileName(this, QStringLiteral("Import Bible Translation"), QString(),
                                                      QStringLiteral("Bible translation (*.json);;All Files (*.*)"));
    if (path.isEmpty())
        return;
    const BibleImportResult r = importTranslation(path);
    if (m_quiet)
        return;
    if (r.ok)
        QMessageBox::information(this, QStringLiteral("Import Bible Translation"),
                                 QStringLiteral("Imported \"%1\": %2 books, %3 verses.%4").arg(r.name).arg(r.books).arg(r.verses)
                                     .arg(r.warnings.isEmpty() ? QString() : QStringLiteral("\n\n") + r.warnings.join(QLatin1Char('\n'))));
    else
        QMessageBox::warning(this, QStringLiteral("Import Bible Translation"),
                             QStringLiteral("The file was not imported:\n\n%1").arg(r.summary()));
}
void ScripturesTab::promptSaveSample()
{
    const QString path = QFileDialog::getSaveFileName(this, QStringLiteral("Save Sample Bible File"),
                                                      QStringLiteral("LuxPresenter-Bible-sample.json"), QStringLiteral("JSON (*.json)"));
    if (path.isEmpty())
        return;
    QSaveFile f(path);
    const bool ok = f.open(QIODevice::WriteOnly) && f.write(BibleLibrary::sampleJson()) >= 0 && f.commit();
    if (!m_quiet)
        ok ? QMessageBox::information(this, QStringLiteral("Sample File"), QStringLiteral("Saved %1.\nEdit it with your own translation's text, then use Import.").arg(path))
           : QMessageBox::warning(this, QStringLiteral("Sample File"), QStringLiteral("The file could not be saved: %1").arg(f.errorString()));
}
void ScripturesTab::promptRemove()
{
    if (!m_current)
        return;
    if (m_current->builtIn) {
        if (!m_quiet)
            QMessageBox::information(this, QStringLiteral("Remove Translation"), QStringLiteral("The built-in %1 cannot be removed.").arg(m_current->name));
        return;
    }
    if (!m_quiet && QMessageBox::question(this, QStringLiteral("Remove Translation"),
                                          QStringLiteral("Remove the translation \"%1\"? The imported copy is deleted; your original file is not touched.").arg(m_current->name)) != QMessageBox::Yes)
        return;
    QString err;
    if (!removeTranslation(m_current->id, &err) && !m_quiet)
        QMessageBox::warning(this, QStringLiteral("Remove Translation"), err);
}
} // namespace Lux