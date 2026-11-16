#pragma once
#include "Services/BibleLibrary.h"
#include <QTimer>
#include <QWidget>
class QComboBox;
class QLabel;
class QLineEdit;
class QListWidget;
class QMenu;
class QPushButton;
namespace Lux {
class DataService;
class TabStrip;
// Scriptures library tab: reference / text search, Books index, book and chapter navigation, verse list and one tab
// per installed translation (the bundled KJV plus user-imported translations). The selected verses of a chapter are
// offered as one passage (a single Scripture slide). Selecting verses stages them in Preview; double click / Enter
// goes live; "Add to Schedule" is in the context menu.
class ScripturesTab : public QWidget
{
    Q_OBJECT
public:
    explicit ScripturesTab(DataService& data, QWidget* parent = nullptr);
    QString selectedVersion() const;                  // id of the active translation
    const BibleTranslation* translation() const { return m_current; }
    BiblePassage currentPassage() const;              // the selected verses ("" / invalid when nothing is selected)
    // --- navigation (also used by the search box) ---
    bool setTranslation(const QString& id);
    void showChapter(int book, int chapter, const QList<int>& selectVerses = {});
    void runSearch(const QString& text);              // reference or text; same as typing it in the search box
    QString message() const;                          // what the search/navigation reported
    bool inResultsMode() const { return m_results; }
    // --- translations ---
    BibleImportResult importTranslation(const QString& path);   // validates + installs, refreshes the tabs
    bool removeTranslation(const QString& id, QString* error = nullptr);
    void promptImport();                              // file dialog + result message
    void promptSaveSample();
    void promptRemove();
    // --- parts (tests) ---
    TabStrip* versionTabs() const { return m_versions; }
    QListWidget* verseList() const { return m_verses; }
    QLineEdit* searchBox() const { return m_search; }
    QPushButton* booksButton() const { return m_books; }
    QPushButton* importButton() const { return m_import; }
    QComboBox* bookCombo() const { return m_bookCombo; }
    QComboBox* chapterCombo() const { return m_chapterCombo; }
    QPushButton* previousButton() const { return m_prev; }
    QPushButton* nextButton() const { return m_next; }
    QMenu* booksMenu() const { return m_booksMenu; }
    QMenu* importMenu() const { return m_importMenu; }
    void setDialogsSuppressed(bool on) { m_quiet = on; }
signals:
    void passageSelected(const Lux::BiblePassage& passage);      // user picked verses: stage in Preview
    void passageActivated(const Lux::BiblePassage& passage);     // double click / Enter: go live
    void addToScheduleRequested(const Lux::BiblePassage& passage);
    void translationsChanged();
private:
    void rebuildVersionTabs();
    void fillBooks();
    void fillChapters();
    void fillVerses(const QList<int>& select = {});
    void step(int delta);
    void onVerseSelection();
    void setMessage(const QString& text, bool problem = false);
    void saveSelectedVersion();
    BiblePassage passageFromSelection() const;
    void buildBooksMenu();
    DataService& m_data;
    BibleLibrary& m_lib;
    const BibleTranslation* m_current = nullptr;
    int m_book = 0, m_chapter = 1;
    bool m_results = false, m_loading = false, m_quiet = false;
    QList<BibleHit> m_hits;
    QLineEdit* m_search;
    QPushButton *m_books, *m_import, *m_prev, *m_next;
    QComboBox *m_bookCombo, *m_chapterCombo;
    QListWidget* m_verses;
    TabStrip* m_versions;
    QLabel* m_message;
    QMenu *m_booksMenu, *m_importMenu;
    QTimer m_debounce;
};
} // namespace Lux
