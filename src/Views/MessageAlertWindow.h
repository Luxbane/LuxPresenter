#pragma once

#include "Services/AlertService.h"

#include <QDialog>

class QComboBox;
class QLabel;
class QPushButton;
class QSpinBox;
class QTableWidget;

namespace Lux {

class DataService;

class MessageAlertWindow : public QDialog
{
    Q_OBJECT
public:
    explicit MessageAlertWindow(DataService& data, QWidget* parent = nullptr);

protected:
    void showEvent(QShowEvent* e) override;

private slots:
    void onAddClicked();
    void onRemoveClicked();
    void onHideClicked();
    void onDeletePresetClicked();
    void onPriorityClicked();
    void onNextClicked();
    void onTableSelectionChanged();
    void updateUIState();

private:
    void loadPresets();
    void savePresets();

    DataService& m_data;
    QComboBox* m_messageCombo = nullptr;
    QPushButton* m_deletePresetBtn = nullptr;
    QSpinBox* m_countSpin = nullptr;
    QPushButton* m_addBtn = nullptr;
    QPushButton* m_removeBtn = nullptr;
    QPushButton* m_hideBtn = nullptr;

    QTableWidget* m_table = nullptr;
    QPushButton* m_priorityBtn = nullptr;
    QPushButton* m_nextBtn = nullptr;
    QPushButton* m_closeBtn = nullptr;
};

} // namespace Lux
