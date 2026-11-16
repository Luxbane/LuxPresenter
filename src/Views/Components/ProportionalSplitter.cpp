#include "Views/Components/ProportionalSplitter.h"

#include <numeric>

namespace Lux {

ProportionalSplitter::ProportionalSplitter(Qt::Orientation orientation, QWidget* parent)
    : QSplitter(orientation, parent)
{
    connect(this, &QSplitter::splitterMoved, this, [this] {
        if (!m_applying)
            m_userAdjusted = true;
    });
}

void ProportionalSplitter::setDefaultRatios(const QList<int>& ratios)
{
    m_ratios = ratios;
    if (!m_userAdjusted)
        applyRatios();
}

void ProportionalSplitter::resetToDefault()
{
    m_userAdjusted = false;
    applyRatios();
}

void ProportionalSplitter::resizeEvent(QResizeEvent* e)
{
    QSplitter::resizeEvent(e);
    if (!m_userAdjusted)
        applyRatios();
}

void ProportionalSplitter::applyRatios()
{
    const int n = count();
    if (n < 2 || m_ratios.size() != n)
        return;
    const int total = (orientation() == Qt::Vertical ? height() : width()) - handleWidth() * (n - 1);
    const int sum = std::accumulate(m_ratios.cbegin(), m_ratios.cend(), 0);
    if (total <= 0 || sum <= 0)
        return;
    QList<int> sizes;
    int used = 0;
    for (int i = 0; i < n; ++i) {
        const int s = (i == n - 1) ? total - used : total * m_ratios[i] / sum;
        sizes << s;
        used += s;
    }
    m_applying = true;
    setSizes(sizes);
    m_applying = false;
}

} // namespace Lux
