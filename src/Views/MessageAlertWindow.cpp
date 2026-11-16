#include "Views/MessageAlertWindow.h"

#include "Services/AlertService.h"
#include "Services/DataService.h"
#include "Views/UiUtil.h"

#include <QComboBox>
#include <QGridLayout>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QLabel>
#include <QMessageBox>
#include <QPushButton>
#include <QSpinBox>
#include <QTableWidget>
#include <QVBoxLayout>

namespace Lux {

MessageAlertWindow::MessageAlertWindow(DataService& data, QWidget* parent)
    : QDialog(parent, Qt::WindowCloseButtonHint | Qt::WindowTitleHint), m_data(data)
{
    setWindowTitle(QStringLiteral("Message Alert"));
    setFixedSize(470, 310);
    setFont(tahoma(9));

    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 12, 12, 12);
    mainLayout->setSpacing(8);

    // --- Top Section: Inputs & Top Actions ---
    auto* topLayout = new QHBoxLayout();
    topLayout->setSpacing(8);

    auto* inputLayout = new QVBoxLayout();
    inputLayout->setSpacing(6);

    // Row 1: Message
    auto* msgRow = new QHBoxLayout();
    msgRow->setSpacing(6);
    auto* msgLabel = new QLabel(QStringLiteral("Message"), this);
    msgLabel->setFixedWidth(55);
    msgLabel->setFont(tahoma(9, true));
    m_messageCombo = new QComboBox(this);
    m_messageCombo->setEditable(true);
    m_messageCombo->setFixedHeight(24);
    m_messageCombo->setFont(tahoma(9));

    m_deletePresetBtn = new QPushButton(QStringLiteral("\u2715"), this);
    m_deletePresetBtn->setFixedSize(24, 24);
    m_deletePresetBtn->setToolTip(QStringLiteral("Delete selected message from preset list"));
    m_deletePresetBtn->setStyleSheet(
        QStringLiteral("QPushButton { background-color: #FEE2E2; color: #DC2626; border: 1px solid #FCA5A5; border-radius: 2px; font-weight: bold; }"
                       "QPushButton:hover { background-color: #FECACA; }"
                       "QPushButton:pressed { background-color: #FCA5A5; }"));

    msgRow->addWidget(msgLabel);
    msgRow->addWidget(m_messageCombo, 1);
    msgRow->addWidget(m_deletePresetBtn);
    inputLayout->addLayout(msgRow);

    // Row 2: Count
    auto* countRow = new QHBoxLayout();
    countRow->setSpacing(6);
    auto* countLabel = new QLabel(QStringLiteral("Count"), this);
    countLabel->setFixedWidth(55);
    countLabel->setFont(tahoma(9, true));
    m_countSpin = new QSpinBox(this);
    m_countSpin->setRange(1, 99);
    m_countSpin->setValue(3);
    m_countSpin->setFixedWidth(55);
    m_countSpin->setFixedHeight(22);
    m_countSpin->setAlignment(Qt::AlignCenter);

    countRow->addWidget(countLabel);
    countRow->addWidget(m_countSpin);
    countRow->addStretch(1);
    inputLayout->addLayout(countRow);

    topLayout->addLayout(inputLayout, 1);

    // Top Right Action Buttons: Add, Remove, Hide
    auto* topBtnsLayout = new QVBoxLayout();
    topBtnsLayout->setSpacing(4);

    m_addBtn = new QPushButton(QStringLiteral("Add"), this);
    m_addBtn->setFixedSize(85, 23);
    m_removeBtn = new QPushButton(QStringLiteral("Remove"), this);
    m_removeBtn->setFixedSize(85, 23);
    m_hideBtn = new QPushButton(QStringLiteral("Hide"), this);
    m_hideBtn->setFixedSize(85, 23);

    topBtnsLayout->addWidget(m_addBtn);
    topBtnsLayout->addWidget(m_removeBtn);
    topBtnsLayout->addWidget(m_hideBtn);
    topLayout->addLayout(topBtnsLayout);

    mainLayout->addLayout(topLayout);

    // --- Section Header: Current Running Messages ---
    auto* headerLabel = new QLabel(QStringLiteral("Current Running Messages"), this);
    headerLabel->setFont(tahoma(9, true));
    mainLayout->addWidget(headerLabel);

    // --- Bottom Section: Table & Side Actions ---
    auto* bottomLayout = new QHBoxLayout();
    bottomLayout->setSpacing(8);

    m_table = new QTableWidget(this);
    m_table->setColumnCount(2);
    m_table->setHorizontalHeaderLabels({QStringLiteral("Count"), QStringLiteral("Message")});
    m_table->horizontalHeader()->setStretchLastSection(true);
    m_table->horizontalHeader()->resizeSection(0, 60);
    m_table->verticalHeader()->setVisible(false);
    m_table->setSelectionBehavior(QAbstractItemView::SelectRows);
    m_table->setSelectionMode(QAbstractItemView::SingleSelection);
    m_table->setEditTriggers(QAbstractItemView::NoEditTriggers);
    m_table->setShowGrid(true);

    bottomLayout->addWidget(m_table, 1);

    auto* bottomBtnsLayout = new QVBoxLayout();
    bottomBtnsLayout->setSpacing(4);

    m_priorityBtn = new QPushButton(QStringLiteral("Select Priority"), this);
    m_priorityBtn->setFixedSize(85, 23);
    m_nextBtn = new QPushButton(QStringLiteral("Select Next"), this);
    m_nextBtn->setFixedSize(85, 23);
    m_closeBtn = new QPushButton(QStringLiteral("Close"), this);
    m_closeBtn->setFixedSize(85, 24);
    m_closeBtn->setFont(tahoma(9, true));

    bottomBtnsLayout->addWidget(m_priorityBtn);
    bottomBtnsLayout->addWidget(m_nextBtn);
    bottomBtnsLayout->addStretch(1);
    bottomBtnsLayout->addWidget(m_closeBtn);

    bottomLayout->addLayout(bottomBtnsLayout);
    mainLayout->addLayout(bottomLayout, 1);

    // Connections
    connect(m_addBtn, &QPushButton::clicked, this, &MessageAlertWindow::onAddClicked);
    connect(m_removeBtn, &QPushButton::clicked, this, &MessageAlertWindow::onRemoveClicked);
    connect(m_hideBtn, &QPushButton::clicked, this, &MessageAlertWindow::onHideClicked);
    connect(m_deletePresetBtn, &QPushButton::clicked, this, &MessageAlertWindow::onDeletePresetClicked);
    connect(m_priorityBtn, &QPushButton::clicked, this, &MessageAlertWindow::onPriorityClicked);
    connect(m_nextBtn, &QPushButton::clicked, this, &MessageAlertWindow::onNextClicked);
    connect(m_closeBtn, &QPushButton::clicked, this, &QDialog::close);
    connect(m_table, &QTableWidget::itemSelectionChanged, this, &MessageAlertWindow::onTableSelectionChanged);

    connect(&AlertService::instance(), &AlertService::alertStateChanged, this, &MessageAlertWindow::updateUIState);

    loadPresets();
    updateUIState();
}

void MessageAlertWindow::showEvent(QShowEvent* e)
{
    QDialog::showEvent(e);
    loadPresets();
    updateUIState();
}

void MessageAlertWindow::loadPresets()
{
    const QString current = m_messageCombo->currentText();
    m_messageCombo->clear();
    const auto& settings = m_data.settings();
    for (const QString& msg : settings.presetAlertMessages) {
        if (!msg.trimmed().isEmpty())
            m_messageCombo->addItem(msg.trimmed());
    }
    if (!current.isEmpty())
        m_messageCombo->setCurrentText(current);
    else if (m_messageCombo->count() > 0)
        m_messageCombo->setCurrentIndex(0);
}

void MessageAlertWindow::savePresets()
{
    AppSettings s = m_data.settings();
    QStringList list;
    for (int i = 0; i < m_messageCombo->count(); ++i) {
        const QString item = m_messageCombo->itemText(i).trimmed();
        if (!item.isEmpty() && !list.contains(item, Qt::CaseInsensitive))
            list.append(item);
    }
    s.presetAlertMessages = list;
    m_data.saveSettings(s);
}

void MessageAlertWindow::onAddClicked()
{
    const QString text = m_messageCombo->currentText().trimmed();
    if (text.isEmpty())
        return;
    const int count = m_countSpin->value();

    AlertService::instance().addMessage(text, count);

    // Add to presets if not already present
    if (m_messageCombo->findText(text, Qt::MatchFixedString) < 0) {
        m_messageCombo->addItem(text);
        savePresets();
    }

    updateUIState();
}

void MessageAlertWindow::onDeletePresetClicked()
{
    const QString targetText = m_messageCombo->currentText().trimmed();
    if (targetText.isEmpty()) {
        QMessageBox::information(this, QStringLiteral("Delete Preset Message"),
                                 QStringLiteral("Please select a preset message from the dropdown list to delete."));
        return;
    }

    const int idx = m_messageCombo->findText(targetText, Qt::MatchFixedString);
    if (idx < 0) {
        QMessageBox::information(this, QStringLiteral("Delete Preset Message"),
                                 QStringLiteral("\"%1\" is not in the preset list.").arg(targetText));
        return;
    }

    const auto res = QMessageBox::question(
        this, QStringLiteral("Delete Preset Message"),
        QStringLiteral("Are you sure you want to delete this preset message from the list?\n\n\"%1\"").arg(targetText),
        QMessageBox::Yes | QMessageBox::No);

    if (res == QMessageBox::Yes) {
        m_messageCombo->removeItem(idx);
        savePresets();
    }
}

void MessageAlertWindow::onRemoveClicked()
{
    const int row = m_table->currentRow();
    if (row >= 0 && row < m_table->rowCount()) {
        AlertService::instance().removeMessage(row);
    } else if (!AlertService::instance().runningMessages().isEmpty()) {
        AlertService::instance().removeMessage(0);
    }
    updateUIState();
}

void MessageAlertWindow::onHideClicked()
{
    AlertService::instance().toggleHide();
    updateUIState();
}

void MessageAlertWindow::onPriorityClicked()
{
    const int row = m_table->currentRow();
    if (row > 0 && row < m_table->rowCount()) {
        AlertService::instance().setPriority(row);
        m_table->selectRow(0);
    }
    updateUIState();
}

void MessageAlertWindow::onNextClicked()
{
    AlertService::instance().selectNext();
    updateUIState();
}

void MessageAlertWindow::onTableSelectionChanged()
{
    const int sel = m_table->currentRow();
    const int total = m_table->rowCount();
    m_priorityBtn->setEnabled(sel > 0 && sel < total);
    m_removeBtn->setEnabled(total > 0);
    m_nextBtn->setEnabled(total > 1);
}

void MessageAlertWindow::updateUIState()
{
    m_hideBtn->setText(AlertService::instance().isAlertHidden() ? QStringLiteral("Show") : QStringLiteral("Hide"));

    const auto& list = AlertService::instance().runningMessages();
    const int prevRow = m_table->currentRow();

    m_table->setRowCount(list.size());
    for (int i = 0; i < list.size(); ++i) {
        auto* countItem = new QTableWidgetItem(QString::number(list[i].count));
        countItem->setTextAlignment(Qt::AlignCenter);
        auto* textItem = new QTableWidgetItem(list[i].text);

        m_table->setItem(i, 0, countItem);
        m_table->setItem(i, 1, textItem);
    }

    if (prevRow >= 0 && prevRow < list.size()) {
        m_table->selectRow(prevRow);
    }

    onTableSelectionChanged();
}

} // namespace Lux
