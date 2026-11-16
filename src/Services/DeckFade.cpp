#include "Services/DeckFade.h"

#include <algorithm>

namespace Lux {

int DeckFade::begin(int crossfadeMs, qint64 now)
{
    const int incoming = m_active < 0 ? 0 : 1 - m_active;
    const int outgoing = m_active;
    // A fade still in progress is cut short: its outgoing deck is the one being reused as incoming.
    if (m_fading && m_outgoing == incoming)
        m_outgoing = -1;
    m_outgoing = outgoing;
    m_active = incoming;
    if (outgoing >= 0 && crossfadeMs > 0) {
        m_alpha[incoming] = 0;
        m_alpha[outgoing] = 255;
        m_fading = true;
        m_start = now;
        m_duration = crossfadeMs;
    } else {
        m_alpha[incoming] = 255;
        if (outgoing >= 0)
            m_alpha[outgoing] = 0;
        m_fading = outgoing >= 0; // outgoing still needs stopping; reported by the next tick
        m_start = now;
        m_duration = 0;
    }
    return incoming;
}

int DeckFade::tick(qint64 now)
{
    if (!m_fading)
        return -1;
    if (m_duration > 0) {
        const double p = std::clamp(double(now - m_start) / m_duration, 0.0, 1.0);
        m_alpha[m_active] = int(p * 255);
        if (p < 1.0)
            return -1;
        m_alpha[m_active] = 255;
    }
    m_fading = false;
    const int out = m_outgoing;
    m_outgoing = -1;
    if (out >= 0)
        m_alpha[out] = 0;
    return out;
}

void DeckFade::stopAll()
{
    m_active = m_outgoing = -1;
    m_alpha[0] = m_alpha[1] = 0;
    m_fading = false;
}

} // namespace Lux
