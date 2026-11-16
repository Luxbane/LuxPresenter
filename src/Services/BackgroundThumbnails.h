#pragma once
#include <QHash>
#include <QImage>
#include <QObject>
#include <QSet>
namespace Lux {
// Pictures of image / video backgrounds for the Background window and the Preview pane. Image files are read scaled
// down; for a video one frame is extracted with libmpv on a worker thread and cached as PNG in the thumbnail
// cache folder (keyed by path, size and modification time).
class BackgroundThumbnails : public QObject
{
    Q_OBJECT
public:
    enum class State { Ready, Pending, Failed, NotApplicable };
    static BackgroundThumbnails& instance();
    // Null image while a video frame is still being extracted, or when the file cannot be read.
    QImage image(const QString& path, int maxWidth);
    State state(const QString& path, int maxWidth) const;
    void clearMemoryCache();
    QString posterFileFor(const QString& videoPath) const;   // cache file of a video's frame
signals:
    void ready(const QString& path);                          // a video frame became available (or failed)
private:
    BackgroundThumbnails() = default;
    QString key(const QString& path, int w) const;
    QHash<QString, QImage> m_cache;
    QSet<QString> m_pending, m_failed;
};
} // namespace Lux
