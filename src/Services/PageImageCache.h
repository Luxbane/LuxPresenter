#pragma once

#include "Services/PdfDocument.h"

#include <QHash>
#include <QImage>
#include <QList>
#include <QSize>
#include <QString>

#include <memory>

namespace Lux {

// Opened PDF documents and rendered page images, shared by the presentation library thumbnails, the Preview/Live
// panes and the projector. Pages are rendered at the size they are shown (never upscaled from a small preview), so
// the projector output is rendered for the target monitor's own pixel size. GUI thread only (PDFium restriction).
class PageImageCache
{
public:
    static PageImageCache& instance();

    // Page count of a PDF, opening it if needed; 0 and `error` filled on failure.
    int pageCount(const QString& pdfPath, QString* error = nullptr);
    // The page scaled to fit inside `box` (aspect ratio preserved); null image on failure.
    QImage page(const QString& pdfPath, int page, const QSize& box);
    QSizeF pageSizePoints(const QString& pdfPath, int page);
    // Forget everything about a PDF (it was rewritten).
    void invalidate(const QString& pdfPath);
    void clear();

    // Test/diagnostic counters
    int renderCount() const { return m_renders; }
    int openDocuments() const { return int(m_docs.size()); }

private:
    struct DocEntry
    {
        std::shared_ptr<PdfDocument> doc;
        qint64 stamp = 0;
    };
    struct ImgEntry
    {
        QImage image;
        qint64 stamp = 0;
    };
    PdfDocument* document(const QString& pdfPath, QString* error);
    void trimImages();

    QHash<QString, DocEntry> m_docs;
    QHash<QString, ImgEntry> m_images;
    qint64 m_tick = 0;
    qint64 m_imageBytes = 0;
    int m_renders = 0;
};

} // namespace Lux
