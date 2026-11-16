#pragma once
#include <QString>
namespace Lux {
// Extracts one still frame from a video file with libmpv (vo=image), used for background thumbnails and the
// Preview pane. Runs synchronously (about a second); call it from a worker thread.
class VideoPoster
{
public:
    // Writes a PNG of a frame at ~10% of the video to `outPng`. Returns false (and sets *error) when libmpv is
    // missing, the file cannot be decoded or nothing was produced within `timeoutMs`.
    static bool generate(const QString& videoPath, const QString& outPng, QString* error = nullptr, int timeoutMs = 20000);
};
} // namespace Lux
