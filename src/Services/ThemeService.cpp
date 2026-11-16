#include "Services/ThemeService.h"

#include <QApplication>
#include <QHash>
#include <QPalette>
#include <QStyleFactory>

namespace Lux {

namespace {
QColor rgb(unsigned v) { return QColor((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF); }
} // namespace

Theme Theme::makeLight()
{
    Theme t;
    t.isDark = false;
    t.windowBg = rgb(0xECE9D8);
    t.panelBg = rgb(0xFFFFFF);
    t.surfaceBg = rgb(0xF1F4F8);
    t.statusBarBg = rgb(0xF1F4F8);
    t.menuBg = rgb(0xF0F3F7);
    t.border = rgb(0xB2BAC2);
    t.splitterBg = rgb(0xB2BAC2);
    t.textPrimary = rgb(0x1E293B);
    t.textSecondary = rgb(0x475569);
    t.textMuted = rgb(0x64748B);
    t.listBg = rgb(0xFFFFFF);
    t.listItemText = rgb(0x0F172A);
    t.inputBg = rgb(0xFFFFFF);
    t.inputBorder = rgb(0x0078D7);
    t.inputFg = rgb(0x0F172A);
    t.buttonBg = rgb(0xF1F5F9);
    t.buttonBorder = rgb(0xCBD5E1);
    t.buttonHoverBg = rgb(0xE2E8F0);
    t.buttonHoverBorder = rgb(0x94A3B8);
    t.inactiveSelBg = rgb(0xCBD5E1);
    t.inactiveSelBorder = rgb(0x94A3B8);
    t.inactiveSelText = rgb(0x1E293B);
    t.tabControlBg = rgb(0xFFFFFF);
    t.tabItemBg = rgb(0xF1F4F8);
    t.tabItemSelectedBg = rgb(0xFFFFFF);
    t.tabItemFg = rgb(0x1E293B);
    t.scrollTrack = rgb(0xF1F4F8);
    t.scrollThumb = rgb(0xCBD5E1);
    t.donateText = rgb(0x854D0E);
    t.headerTop = rgb(0xF9FAFB);
    t.headerBottom = rgb(0xDFE4EA);
    t.toolbarTop = rgb(0xFFFFFF);
    t.toolbarBottom = rgb(0xE6EBF0);
    t.pageTitle = rgb(0x2563EB);
    return t;
}

Theme Theme::makeDark()
{
    Theme t;
    t.isDark = true;
    t.windowBg = rgb(0x24262C);
    t.panelBg = rgb(0x1F2025);
    t.surfaceBg = rgb(0x2B2D34);
    t.statusBarBg = rgb(0x222429);
    t.menuBg = rgb(0x26282E);
    t.border = rgb(0x3D4049);
    t.splitterBg = rgb(0x383A42);
    t.textPrimary = rgb(0xEBEDF0);
    t.textSecondary = rgb(0x9EA4B0);
    t.textMuted = rgb(0x6D7382);
    t.listBg = rgb(0x1E1F24);
    t.listItemText = rgb(0xEBEDF0);
    t.inputBg = rgb(0x18191D);
    t.inputBorder = rgb(0x444752);
    t.inputFg = rgb(0xF3F4F6);
    t.buttonBg = rgb(0x2E3038);
    t.buttonBorder = rgb(0x424550);
    t.buttonHoverBg = rgb(0x3A3D47);
    t.buttonHoverBorder = rgb(0x565A67);
    t.inactiveSelBg = rgb(0x353842);
    t.inactiveSelBorder = rgb(0x4B4E5B);
    t.inactiveSelText = rgb(0xD1D5DB);
    t.tabControlBg = rgb(0x1F2025);
    t.tabItemBg = rgb(0x272930);
    t.tabItemSelectedBg = rgb(0x1F2025);
    t.tabItemFg = rgb(0xEBEDF0);
    t.scrollTrack = rgb(0x1F2025);
    t.scrollThumb = rgb(0x464954);
    t.donateText = rgb(0xFBBF24);
    t.headerTop = rgb(0x32353E);
    t.headerBottom = rgb(0x272930);
    t.toolbarTop = rgb(0x31333B);
    t.toolbarBottom = rgb(0x25272E);
    t.pageTitle = rgb(0x60A5FA);
    return t;
}

ThemeService::ThemeService() : m_theme(Theme::makeLight()) {}

ThemeService& ThemeService::instance()
{
    static ThemeService s;
    return s;
}

void ThemeService::apply(bool dark)
{
    m_theme = dark ? Theme::makeDark() : Theme::makeLight();

    QPalette p;
    p.setColor(QPalette::Window, m_theme.windowBg);
    p.setColor(QPalette::WindowText, m_theme.textPrimary);
    p.setColor(QPalette::Base, m_theme.inputBg);
    p.setColor(QPalette::AlternateBase, m_theme.surfaceBg);
    p.setColor(QPalette::Mid, m_theme.border);
    p.setColor(QPalette::Text, m_theme.inputFg);
    p.setColor(QPalette::Button, m_theme.buttonBg);
    p.setColor(QPalette::ButtonText, m_theme.textPrimary);
    p.setColor(QPalette::ToolTipBase, m_theme.menuBg);
    p.setColor(QPalette::ToolTipText, m_theme.textPrimary);
    p.setColor(QPalette::Highlight, Theme::selectionBlue());
    p.setColor(QPalette::HighlightedText, Qt::white);
    p.setColor(QPalette::PlaceholderText, QColor(0x94, 0xA3, 0xB8));
    p.setColor(QPalette::Disabled, QPalette::WindowText, m_theme.textMuted);
    p.setColor(QPalette::Disabled, QPalette::Text, m_theme.textMuted);
    p.setColor(QPalette::Disabled, QPalette::ButtonText, m_theme.textMuted);

    if (auto* app = qobject_cast<QApplication*>(QCoreApplication::instance())) {
        app->setPalette(p);
        app->setStyleSheet(buildStyleSheet(m_theme));
    }
    emit themeChanged(dark);
}

QString ThemeService::buildStyleSheet(const Theme& t)
{
    const QHash<QString, QString> v{
        {"windowBg", t.windowBg.name()},       {"panelBg", t.panelBg.name()},
        {"surfaceBg", t.surfaceBg.name()},     {"statusBarBg", t.statusBarBg.name()},
        {"menuBg", t.menuBg.name()},           {"border", t.border.name()},
        {"splitterBg", t.splitterBg.name()},   {"textPrimary", t.textPrimary.name()},
        {"textSecondary", t.textSecondary.name()}, {"textMuted", t.textMuted.name()},
        {"listBg", t.listBg.name()},           {"listItemText", t.listItemText.name()},
        {"inputBg", t.inputBg.name()},         {"inputBorder", t.inputBorder.name()},
        {"inputFg", t.inputFg.name()},         {"buttonBg", t.buttonBg.name()},
        {"buttonBorder", t.buttonBorder.name()}, {"buttonHoverBg", t.buttonHoverBg.name()},
        {"buttonHoverBorder", t.buttonHoverBorder.name()},
        {"inactiveSelBg", t.inactiveSelBg.name()}, {"inactiveSelBorder", t.inactiveSelBorder.name()},
        {"inactiveSelText", t.inactiveSelText.name()},
        {"tabItemBg", t.tabItemBg.name()},     {"tabItemSelectedBg", t.tabItemSelectedBg.name()},
        {"tabItemFg", t.tabItemFg.name()},     {"scrollTrack", t.scrollTrack.name()},
        {"scrollThumb", t.scrollThumb.name()}, {"headerTop", t.headerTop.name()},
        {"headerBottom", t.headerBottom.name()}, {"toolbarTop", t.toolbarTop.name()},
        {"toolbarBottom", t.toolbarBottom.name()},
        {"selBlue", Theme::selectionBlue().name()}, {"selBlueBorder", Theme::selectionBlueBorder().name()},
        {"pageTitle", t.pageTitle.name()},
    };

    QString qss = QStringLiteral(R"(
QMainWindow, QDialog, QWidget#MainRoot { background: @windowBg@; }
QLabel { color: @textPrimary@; background: transparent; }
QToolTip { background: @menuBg@; color: @textPrimary@; border: 1px solid @border@; padding: 2px 4px; }

QMenuBar { background: @menuBg@; color: @textPrimary@; border-bottom: 1px solid @border@; font: 11px "Tahoma"; }
QMenuBar::item { background: transparent; padding: 4px 8px; }
QMenuBar::item:selected, QMenuBar::item:pressed { background: @buttonHoverBg@; }
QMenu { background: @menuBg@; color: @textPrimary@; border: 1px solid @border@; padding: 2px; font: 11px "Tahoma"; }
QMenu::item { padding: 4px 28px 4px 24px; background: transparent; }
QMenu::item:selected { background: @buttonHoverBg@; }
QMenu::item:disabled { color: @textMuted@; }
QMenu::separator { height: 1px; background: @border@; margin: 3px 4px; }
QMenu::indicator { width: 13px; height: 13px; left: 6px; }

QFrame#Toolbar { background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 @toolbarTop@, stop:1 @toolbarBottom@); border: none; border-bottom: 1px solid @border@; }
QFrame#PaneHeader { background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 @headerTop@, stop:1 @headerBottom@); border: none; border-bottom: 1px solid @border@; }
QFrame#SurfaceBarBottom { background: @surfaceBg@; border: none; border-bottom: 1px solid @border@; }
QFrame#SurfaceBarTop { background: @surfaceBg@; border: none; border-top: 1px solid @border@; }
QFrame#TabStrip { background: @surfaceBg@; border: none; }
QFrame#TabStripTop { background: @surfaceBg@; border: none; border-top: 1px solid @border@; }
QFrame#SchedToolbar { background: @surfaceBg@; border: none; border-left: 1px solid @border@; }
QWidget#PanelBg { background: @panelBg@; }
QStatusBar { background: @statusBarBg@; color: @textPrimary@; border-top: 1px solid @border@; font: 11px "Tahoma"; }
QStatusBar::item { border: none; }

QSplitter::handle { background: @splitterBg@; }

QLineEdit { background: @inputBg@; color: @inputFg@; border: 1px solid @inputBorder@; padding: 2px 4px; font-size: 11px; selection-background-color: @selBlue@; selection-color: white; }
QLineEdit:disabled { color: @textMuted@; background: @surfaceBg@; border-color: @buttonBorder@; }
QComboBox { background: @inputBg@; color: @inputFg@; border: 1px solid @buttonBorder@; padding: 1px 6px; font-size: 11px; min-height: 20px; }
QComboBox:disabled { color: @textMuted@; background: @surfaceBg@; }
QComboBox::drop-down { width: 16px; border: none; }
QComboBox QAbstractItemView { background: @listBg@; color: @listItemText@; border: 1px solid @border@; selection-background-color: @selBlue@; selection-color: white; outline: 0; }

QSpinBox { background: @inputBg@; color: @inputFg@; border: 1px solid @buttonBorder@; padding: 1px 4px; font-size: 11px; min-height: 20px; }
QSpinBox:disabled { color: @textMuted@; background: @surfaceBg@; }

QPlainTextEdit, QTextEdit { background: @inputBg@; color: @inputFg@; border: 1px solid @buttonBorder@; font-size: 11px; selection-background-color: @selBlue@; selection-color: white; }
QPlainTextEdit:disabled, QTextEdit:disabled { color: @textMuted@; background: @surfaceBg@; }

QCheckBox, QRadioButton { color: @textPrimary@; background: transparent; font-family: "Tahoma", "Segoe UI"; font-size: 11px; }
QCheckBox:disabled, QRadioButton:disabled { color: @textMuted@; }

QGroupBox { color: @textPrimary@; border: 1px solid @border@; border-radius: 3px; margin-top: 10px; padding-top: 10px; font-family: "Tahoma", "Segoe UI"; font-weight: bold; font-size: 11px; }
QGroupBox::title { subcontrol-origin: margin; subcontrol-position: top left; padding: 0 4px; color: @textPrimary@; }

QPushButton { background: @buttonBg@; color: @textPrimary@; border: 1px solid @buttonBorder@; padding: 2px 8px; font-size: 10px; }
QPushButton:hover { background: @buttonHoverBg@; border-color: @buttonHoverBorder@; }
QPushButton:checked { background: @buttonHoverBg@; border-color: @buttonHoverBorder@; }
QPushButton:disabled { color: @textMuted@; background: @surfaceBg@; border-color: @buttonBorder@; }
QToolButton { background: transparent; border: none; color: @textSecondary@; }
QToolButton:hover:enabled { background: @buttonHoverBg@; }

QAbstractItemView { background: @listBg@; color: @listItemText@; border: none; outline: 0; font: 11px "Tahoma"; }
QTreeView::item { padding: 2px 4px; border: 1px solid transparent; }
QTreeView::item:hover { background: @buttonHoverBg@; border-color: @buttonHoverBorder@; }
QTreeView::item:selected { background: @inactiveSelBg@; border-color: @inactiveSelBorder@; color: @inactiveSelText@; }
QTreeView[paneActive="true"]::item:selected { background: @selBlue@; border-color: @selBlueBorder@; color: white; }
QHeaderView::section { background: qlineargradient(x1:0,y1:0,x2:0,y2:1, stop:0 @headerTop@, stop:1 @headerBottom@); color: @textPrimary@; border: none; border-right: 1px solid @border@; border-bottom: 1px solid @border@; padding: 4px 6px; font: bold 11px "Tahoma"; }

QScrollBar:vertical { background: @scrollTrack@; width: 10px; margin: 0; border: none; }
QScrollBar:horizontal { background: @scrollTrack@; height: 10px; margin: 0; border: none; }
QScrollBar::handle:vertical { background: @scrollThumb@; border-radius: 3px; min-height: 24px; margin: 2px; }
QScrollBar::handle:horizontal { background: @scrollThumb@; border-radius: 3px; min-width: 24px; margin: 2px; }
QScrollBar::add-line, QScrollBar::sub-line { width: 0; height: 0; border: none; background: none; }
QScrollBar::add-page, QScrollBar::sub-page { background: none; }

QTabBar { background: transparent; border: none; }
QTabBar::tab { background: @tabItemBg@; color: @tabItemFg@; border: 1px solid @border@; border-bottom: none; border-top-left-radius: 3px; border-top-right-radius: 3px; padding: 4px 6px; margin: 1px 1px 0 1px; font: bold 11px "Tahoma"; }
QTabBar::tab:hover { background: @buttonHoverBg@; }
QTabBar::tab:selected { background: @tabItemSelectedBg@; margin-top: 0; padding-bottom: 5px; }
QTabBar[density="small"]::tab { padding: 1px 6px; font-size: 10px; }
QTabBar[density="small"]::tab:selected { padding-bottom: 2px; }
QTabBar[density="wide"]::tab { padding: 2px 12px; }
QTabBar[density="wide"]::tab:selected { padding-bottom: 3px; }

QSlider::groove:horizontal { height: 4px; background: #94A3B8; border-radius: 2px; }
QSlider::handle:horizontal { background: #E2E8F0; border: 1px solid #64748B; width: 10px; margin: -5px 0; border-radius: 2px; }

/* Options Dialog */
QFrame#CategoryBox { background: @panelBg@; border: 1px solid @border@; border-radius: 2px; }
QListWidget#OptionsCategories { background: transparent; border: none; outline: none; }
QListWidget#OptionsCategories::item { height: 38px; padding-left: 14px; font-family: "Tahoma", "Segoe UI"; font-weight: bold; font-size: 11px; color: @textPrimary@; border-bottom: 1px solid @border@; }
QListWidget#OptionsCategories::item:last { border-bottom: none; }
QListWidget#OptionsCategories::item:hover { background: @buttonHoverBg@; }
QListWidget#OptionsCategories::item:selected { background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #3B82F6, stop:1 #1D4ED8); color: #FFFFFF; font-weight: bold; }
QFrame#SettingsCard { background: @panelBg@; border: 1px solid @border@; border-radius: 2px; }
QLabel#OptionsPageTitle { color: @pageTitle@; font-family: "Tahoma", "Segoe UI"; font-size: 18pt; font-weight: bold; margin-bottom: 4px; }
QLabel#OptionsDescText { color: @textMuted@; font-size: 11px; padding: 4px 0; }

/* Background Options Dialog */
QFrame#BgThumbnailFrame { background: @panelBg@; border: 1px solid @border@; }
QListWidget#bgList { background: @listBg@; border: none; outline: none; }
QListWidget#bgList::item { border: 2px solid @border@; margin: 3px; background: #000000; }
QListWidget#bgList::item:hover { border: 2px solid @buttonHoverBorder@; }
QListWidget#bgList::item:selected { border: 2px solid @selBlue@; background: #000000; }
QFrame#BgActionBar { background: @surfaceBg@; border: none; border-top: 1px solid @border@; border-bottom: 1px solid @border@; }
QFrame#BgFooterBar { background: @windowBg@; border: none; border-top: 1px solid @border@; }
QFrame#BgActionBar QPushButton, QFrame#BgFooterBar QPushButton { padding: 3px 14px; font-size: 11px; border-radius: 2px; }
QLabel#BgEmptyLabel { color: @textMuted@; font-family: "Tahoma", "Segoe UI"; font-size: 11px; }
)");
    for (auto it = v.cbegin(); it != v.cend(); ++it)
        qss.replace(QLatin1Char('@') + it.key() + QLatin1Char('@'), it.value());
    return qss;
}

} // namespace Lux
