#pragma once

#include "Services/PresentationService.h"

#include <QWidget>

class QComboBox;
class QLabel;
class QLineEdit;
class QListWidget;
class QMenu;
class QPushButton;

namespace Lux {

class DataService;

// Presentation library tab: document selector, slide thumbnail grid and the Import/Delete bar (PresentationTab.xaml
// + PresentationViewModel). Documents are PDFs and PowerPoint/OpenDocument files (converted with LibreOffice).
class PresentationTab : public QWidget
{
    Q_OBJECT
public:
    PresentationTab(DataService& data, PresentationService& service, QWidget* parent = nullptr);

    void refresh();                                  // reload the document list from the library
    // Imports a document into the library and selects it. Returns false (with message()) when the file is
    // refused up front: legacy .ppt/.pps (the reference asks for .pptx), unsupported or missing files.
    bool importPath(const QString& path);
    bool removeDocument(const QString& docId);       // no confirmation (the button asks first)
    void selectDocumentByPath(const QString& filePath);
    void setSelectedPage(int page);                  // follow the Preview pane (no staging signal)
    void markLive(int page);                         // -1 clears
    const PresentationService::Document* currentDocument() const { return m_doc.isValid() ? &m_doc : nullptr; }
    QString message() const { return m_message; }

    QComboBox* documentCombo() const { return m_docs; }
    QListWidget* slideGrid() const { return m_slides; }
    QLineEdit* filterBox() const { return m_filter; }
    QPushButton* importButton() const { return m_import; }
    QPushButton* deleteButton() const { return m_delete; }
    QLabel* loadingLabel() const { return m_loading; }
    QMenu* slideMenu() const { return m_menu; }
    QAction* actionGoLive() const { return m_aGoLive; }
    QAction* actionAddToSchedule() const { return m_aAdd; }

signals:
    void slideStaged(const Lux::PresentationService::Document& doc, int page);      // click: show in Preview
    void slideActivated(const Lux::PresentationService::Document& doc, int page);   // double click: Go Live
    void addToScheduleRequested(const Lux::PresentationService::Document& doc);
    // Something the user has to be told (the window shows it): title, text, isError
    void messageRequested(const QString& title, const QString& text, bool isError);

private slots:
    void importDocument();       // file dialog
    void deleteDocument();       // confirmation
    void onDocumentChosen();
    void onOpened(const Lux::PresentationService::Document& doc);
    void onFailed(const QString& filePath, const QString& message);

private:
    void fillGrid();
    void fillCombo(const QString& selectId = {});

    DataService& m_data;
    PresentationService& m_service;
    QComboBox* m_docs;
    QLineEdit* m_filter;
    QPushButton *m_addSmall, *m_import, *m_delete;
    QListWidget* m_slides;
    QLabel* m_loading;
    QMenu* m_menu;
    QAction *m_aGoLive, *m_aAdd;
    PresentationService::Document m_doc;
    QString m_pendingPath;           // document being opened for the selection
    QString m_message;
    bool m_quiet = false;            // programmatic selection: no staging signals
    bool m_importing = false;        // the pending open is an import (adds/refreshes the library entry)
    int m_livePage = -1;
    QString m_selectedId;            // library id of the selected document (kept while a filter hides it)
};

} // namespace Lux
