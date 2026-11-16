#pragma once

#include <QAbstractItemView>
#include <QEvent>
#include <QFont>
#include <QFrame>
#include <QObject>
#include <QStyle>
#include <QWidget>

namespace Lux {

// Fonts: the reference sets sizes in WPF device-independent pixels (1/96 inch).
inline QFont tahoma(int px, bool bold = false)
{
    QFont f(QStringLiteral("Tahoma"));
    f.setPixelSize(px);
    f.setBold(bold);
    return f;
}

inline QFont segoeEmoji(int px)
{
    QFont f(QStringLiteral("Segoe UI Emoji"));
    f.setPixelSize(px);
    return f;
}

inline QFont segoeSymbol(int px)
{
    QFont f(QStringLiteral("Segoe UI Symbol"));
    f.setPixelSize(px);
    return f;
}

// Re-evaluates the style sheet after a dynamic property changed.
inline void repolish(QWidget* w)
{
    w->style()->unpolish(w);
    w->style()->polish(w);
    w->update();
}

// Marks a list as the "active pane" (solid blue selection) while it has keyboard
// focus; otherwise the grey inactive selection is shown (as IsActive in the reference).
class ActiveSelectionTracker : public QObject
{
public:
    explicit ActiveSelectionTracker(QAbstractItemView* view) : QObject(view), m_view(view)
    {
        view->setProperty("paneActive", false);
        view->installEventFilter(this);
    }

protected:
    bool eventFilter(QObject* obj, QEvent* e) override
    {
        if (obj == m_view && (e->type() == QEvent::FocusIn || e->type() == QEvent::FocusOut)) {
            m_view->setProperty("paneActive", e->type() == QEvent::FocusIn);
            repolish(m_view);
            m_view->viewport()->update();
        }
        return false;
    }

private:
    QAbstractItemView* m_view;
};

// Tooltip suffix for controls whose behaviour arrives in a later milestone.
inline QString plannedNote(int milestone)
{
    return QStringLiteral("Not available yet (planned for Milestone %1)").arg(milestone);
}

} // namespace Lux
