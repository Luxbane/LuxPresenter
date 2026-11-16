#pragma once

#include <QList>
#include <QSplitter>

namespace Lux {

// A splitter whose panes keep a default proportion (e.g. 45:55) while the window is resized,
// until the user drags a handle; after that the normal splitter behaviour applies and the
// user's layout is respected. Minimum pane sizes of the children are still honoured.
class ProportionalSplitter : public QSplitter
{
    Q_OBJECT
public:
    explicit ProportionalSplitter(Qt::Orientation orientation, QWidget* parent = nullptr);

    void setDefaultRatios(const QList<int>& ratios);
    QList<int> defaultRatios() const { return m_ratios; }
    bool userAdjusted() const { return m_userAdjusted; }
    void resetToDefault(); // forget the user's layout and re-apply the default proportions

protected:
    void resizeEvent(QResizeEvent* e) override;

private:
    void applyRatios();

    QList<int> m_ratios;
    bool m_userAdjusted = false;
    bool m_applying = false;
};

} // namespace Lux
