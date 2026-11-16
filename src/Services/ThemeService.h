#pragma once

#include <QColor>
#include <QObject>
#include <QString>

namespace Lux {

// Colour palette: values copied from ThemeService.cs / App.xaml of the reference.
struct Theme
{
    bool isDark = false;

    QColor windowBg, panelBg, surfaceBg, statusBarBg, menuBg;
    QColor border, splitterBg;
    QColor textPrimary, textSecondary, textMuted;
    QColor listBg, listItemText;
    QColor inputBg, inputBorder, inputFg;
    QColor buttonBg, buttonBorder, buttonHoverBg, buttonHoverBorder;
    QColor inactiveSelBg, inactiveSelBorder, inactiveSelText;
    QColor tabControlBg, tabItemBg, tabItemSelectedBg, tabItemFg;
    QColor scrollTrack, scrollThumb;
    QColor donateText;
    QColor headerTop, headerBottom;   // HeaderBgBrush gradient
    QColor toolbarTop, toolbarBottom; // ToolbarBgBrush gradient
    QColor pageTitle;

    // Fixed colours used by the reference outside the theme dictionary.
    static QColor selectionBlue() { return QColor(0x00, 0x78, 0xD7); }       // active selection
    static QColor selectionBlueBorder() { return QColor(0x00, 0x5A, 0x9E); }
    static QColor accentBlue() { return QColor(0x25, 0x63, 0xEB); }          // Live / toggled buttons

    static Theme makeLight();
    static Theme makeDark();
};

// Applies the Light/Dark theme application-wide (palette + style sheet).
class ThemeService : public QObject
{
    Q_OBJECT
public:
    static ThemeService& instance();

    bool isDark() const { return m_theme.isDark; }
    const Theme& theme() const { return m_theme; }

    void apply(bool dark); // requires a QApplication
    static QString buildStyleSheet(const Theme& t);

signals:
    void themeChanged(bool isDark);

private:
    ThemeService();
    Theme m_theme;
};

} // namespace Lux
