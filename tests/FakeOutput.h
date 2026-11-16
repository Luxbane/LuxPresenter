#pragma once

#include "Services/ProjectionService.h"

namespace LuxTest {

// Stand-in for the projector windows: records what the service asks of the output.
class FakeOutput : public Lux::IProjectionOutput
{
public:
    QList<Lux::ScreenInfo> screenList;
    bool shown = false;
    Lux::ScreenInfo shownOn;
    int showCalls = 0, hideCalls = 0;
    QList<Lux::ProjectionState> updates;   // every state pushed while shown
    int mTop = -1, mBottom = -1, mLeft = -1, mRight = -1;
    double lastSeek = -1;

    FakeOutput()
    {
        Lux::ScreenInfo s;
        s.physicalWidth = 1920;
        s.physicalHeight = 1080;
        s.isPrimary = true;
        screenList << s;
    }
    QList<Lux::ScreenInfo> screens() const override { return screenList; }
    bool show(const Lux::ScreenInfo& screen) override { shown = true; shownOn = screen; ++showCalls; return true; }
    void hide() override { shown = false; ++hideCalls; }
    bool isShown() const override { return shown; }
    void setMargins(int t, int b, int l, int r) override { mTop = t; mBottom = b; mLeft = l; mRight = r; }
    void update(const Lux::ProjectionState& s) override { updates << s; }
    void seekVideo(double s) override { lastSeek = s; }
};

inline Lux::ProjectionService::OutputFactory fakeFactory(const QList<Lux::ScreenInfo>& screens = {})
{
    return [screens]() -> std::unique_ptr<Lux::IProjectionOutput> {
        auto f = std::make_unique<FakeOutput>();
        if (!screens.isEmpty())
            f->screenList = screens;
        return f;
    };
}

} // namespace LuxTest
