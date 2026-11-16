#include "Services/PageImageCache.h"

#include <QDir>
#include <QFileInfo>

#include <algorithm>

namespace Lux {

namespace {
constexpr int kMaxDocs = 6;
constexpr qint64 kMaxImageBytes = 256ll * 1024 * 1024;

QString normalised(const QString& path) { return QDir::cleanPath(path).toLower(); }
} // namespace

PageImageCache& PageImageCache::instance()
{
    static PageImageCache cache;
    return cache;
}

PdfDocument* PageImageCache::document(const QString& pdfPath, QString* error)
{
    const QString key = normalised(pdfPath);
    auto it = m_docs.find(key);
    if (it != m_docs.end()) {
        it->stamp = ++m_tick;
        return it->doc.get();
    }
    auto doc = std::make_shared<PdfDocument>();
    if (!doc->open(pdfPath)) {
        if (error)
            *error = doc->errorMessage();
        return nullptr;
    }
    if (m_docs.size() >= kMaxDocs) {   // drop the least recently used document
        auto oldest = std::min_element(m_docs.begin(), m_docs.end(), [](const DocEntry& a, const DocEntry& b) { return a.stamp < b.stamp; });
        const QString drop = oldest.key();
        m_docs.erase(oldest);
        for (auto i = m_images.begin(); i != m_images.end();) {
            if (i.key().startsWith(drop + QLatin1Char('|'))) {
                m_imageBytes -= i->image.sizeInBytes();
                i = m_images.erase(i);
            } else {
                ++i;
            }
        }
    }
    DocEntry e;
    e.doc = std::move(doc);
    e.stamp = ++m_tick;
    PdfDocument* raw = e.doc.get();
    m_docs.insert(key, std::move(e));
    return raw;
}

int PageImageCache::pageCount(const QString& pdfPath, QString* error)
{
    PdfDocument* d = document(pdfPath, error);
    return d ? d->pageCount() : 0;
}

QSizeF PageImageCache::pageSizePoints(const QString& pdfPath, int page)
{
    PdfDocument* d = document(pdfPath, nullptr);
    return d ? d->pageSize(page) : QSizeF();
}

QImage PageImageCache::page(const QString& pdfPath, int page, const QSize& box)
{
    if (box.isEmpty())
        return {};
    PdfDocument* d = document(pdfPath, nullptr);
    if (!d || page < 0 || page >= d->pageCount())
        return {};
    // The cache key uses the size the page will really have, so equal-looking requests share one image.
    const QSizeF ps = d->pageSize(page);
    if (ps.isEmpty())
        return {};
    const double s = std::min(box.width() / ps.width(), box.height() / ps.height());
    const QSize real(std::max(1, int(std::lround(ps.width() * s))), std::max(1, int(std::lround(ps.height() * s))));
    const QString key = normalised(pdfPath) + QLatin1Char('|') + QString::number(page) + QLatin1Char('|') +
                        QString::number(real.width()) + QLatin1Char('x') + QString::number(real.height());
    auto it = m_images.find(key);
    if (it != m_images.end()) {
        it->stamp = ++m_tick;
        return it->image;
    }
    const PdfDocument::Bitmap bmp = d->render(page, box.width(), box.height());
    if (bmp.isNull())
        return {};
    ++m_renders;
    // BGRx rows -> RGB32 image (deep copy: the bitmap buffer is released with `bmp`)
    QImage img(bmp.width, bmp.height, QImage::Format_RGB32);
    for (int y = 0; y < bmp.height; ++y)
        memcpy(img.scanLine(y), bmp.data.constData() + qsizetype(y) * bmp.stride, size_t(bmp.width) * 4);
    ImgEntry e;
    e.image = img;
    e.stamp = ++m_tick;
    m_imageBytes += img.sizeInBytes();
    m_images.insert(key, e);
    trimImages();
    return img;
}

void PageImageCache::trimImages()
{
    while (m_imageBytes > kMaxImageBytes && m_images.size() > 1) {
        auto oldest = std::min_element(m_images.begin(), m_images.end(), [](const ImgEntry& a, const ImgEntry& b) { return a.stamp < b.stamp; });
        m_imageBytes -= oldest->image.sizeInBytes();
        m_images.erase(oldest);
    }
}

void PageImageCache::invalidate(const QString& pdfPath)
{
    const QString key = normalised(pdfPath);
    m_docs.remove(key);
    for (auto i = m_images.begin(); i != m_images.end();) {
        if (i.key().startsWith(key + QLatin1Char('|'))) {
            m_imageBytes -= i->image.sizeInBytes();
            i = m_images.erase(i);
        } else {
            ++i;
        }
    }
}

void PageImageCache::clear()
{
    m_docs.clear();
    m_images.clear();
    m_imageBytes = 0;
}

} // namespace Lux
