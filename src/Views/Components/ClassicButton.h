#pragma once

#include "Views/IconFactory.h"

#include <QAbstractButton>
#include <QList>
#include <QMargins>

namespace Lux {

// Custom-painted button reproducing the toolbar button styles of MainWindow.xaml
// (ClassicIconBtn, EWToggleBtn, the Go Live button and the Live master toggle).
// Content is a sequence of icons and text runs, each with its own font/colour.
class ClassicButton : public QAbstractButton
{
    Q_OBJECT
public:
    enum class Kind { Icon, Toggle, Framed, Live };
    enum class ColorRole { Primary, Secondary, Donate, Fixed };

    struct Part
    {
        bool isIcon = false;
        Icon icon = Icon::Add;
        QSize iconSize;
        QString text;
        QFont font;
        ColorRole role = ColorRole::Primary;
        QColor fixed;
        int gapAfter = 0;
    };

    explicit ClassicButton(Kind kind, QWidget* parent = nullptr);

    ClassicButton& addIcon(Icon icon, int w, int h, int gapAfter = 0);
    ClassicButton& addText(const QString& text, const QFont& font, ColorRole role = ColorRole::Primary,
                           int gapAfter = 0, QColor fixed = QColor());
    void clearParts();
    // Replace the text of the n-th text part (counting text parts only).
    void setTextPart(int index, const QString& text, QColor fixed = QColor());

    Kind kind() const { return m_kind; }
    QSize sizeHint() const override;
    QSize minimumSizeHint() const override { return sizeHint(); }

protected:
    void paintEvent(QPaintEvent*) override;
    void enterEvent(QEnterEvent*) override;
    void leaveEvent(QEvent*) override;

private:
    QMargins outerMargins() const;
    QMargins padding() const;
    int fixedHeight() const;
    int contentWidth() const;
    QColor partColor(const Part& p, bool checked) const;

    Kind m_kind;
    QList<Part> m_parts;
    bool m_hover = false;
};

} // namespace Lux
