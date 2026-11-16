#include "Services/PdfDocument.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QLibrary>

#include <algorithm>
#include <climits>
#include <cmath>

namespace Lux {

PdfiumApi& PdfiumApi::instance()
{
    static PdfiumApi api;
    return api;
}

bool PdfiumApi::load(const QString& dllPath)
{
    if (m_loaded)
        return true;
    QString path = dllPath;
    if (path.isEmpty())
        path = QDir(QCoreApplication::applicationDirPath()).filePath(QStringLiteral("pdfium.dll"));
    if (!QFileInfo::exists(path)) {
        m_error = QStringLiteral("pdfium.dll not found: %1").arg(QDir::toNativeSeparators(path));
        return false;
    }
    if (!m_lib)
        m_lib = new QLibrary(path); // kept for the life of the process
    else
        m_lib->setFileName(path);
    if (!m_lib->load()) {
        m_error = m_lib->errorString();
        return false;
    }
    auto sym = [this](const char* n) { return m_lib->resolve(n); };
#define LUX_PDFIUM_SYM(member, name) member = reinterpret_cast<decltype(member)>(sym(name))
    LUX_PDFIUM_SYM(initLibrary, "FPDF_InitLibrary");
    LUX_PDFIUM_SYM(loadMemDocument, "FPDF_LoadMemDocument");
    LUX_PDFIUM_SYM(getLastError, "FPDF_GetLastError");
    LUX_PDFIUM_SYM(getPageCount, "FPDF_GetPageCount");
    LUX_PDFIUM_SYM(loadPage, "FPDF_LoadPage");
    LUX_PDFIUM_SYM(getPageWidthF, "FPDF_GetPageWidthF");
    LUX_PDFIUM_SYM(getPageHeightF, "FPDF_GetPageHeightF");
    LUX_PDFIUM_SYM(closePage, "FPDF_ClosePage");
    LUX_PDFIUM_SYM(closeDocument, "FPDF_CloseDocument");
    LUX_PDFIUM_SYM(bitmapCreate, "FPDFBitmap_Create");
    LUX_PDFIUM_SYM(bitmapFillRect, "FPDFBitmap_FillRect");
    LUX_PDFIUM_SYM(bitmapGetBuffer, "FPDFBitmap_GetBuffer");
    LUX_PDFIUM_SYM(bitmapGetStride, "FPDFBitmap_GetStride");
    LUX_PDFIUM_SYM(bitmapDestroy, "FPDFBitmap_Destroy");
    LUX_PDFIUM_SYM(renderPageBitmap, "FPDF_RenderPageBitmap");
#undef LUX_PDFIUM_SYM
    if (!initLibrary || !loadMemDocument || !getLastError || !getPageCount || !loadPage || !getPageWidthF ||
        !getPageHeightF || !closePage || !closeDocument || !bitmapCreate || !bitmapFillRect || !bitmapGetBuffer ||
        !bitmapGetStride || !bitmapDestroy || !renderPageBitmap) {
        m_error = QStringLiteral("pdfium.dll is missing required exports");
        m_lib->unload();
        return false;
    }
    initLibrary();
    m_path = path;
    m_loaded = true;
    m_error.clear();
    return true;
}

bool PdfDocument::fail(Error e, const QString& msg)
{
    close();
    m_error = e;
    m_message = msg;
    return false;
}

void PdfDocument::close()
{
    PdfiumApi& api = PdfiumApi::instance();
    if (m_doc && api.isAvailable())
        api.closeDocument(m_doc);
    m_doc = nullptr;
    m_bytes.clear();
    m_pageCount = 0;
    m_sizes.clear();
    m_error = Error::None;
    m_message.clear();
}

bool PdfDocument::open(const QString& path)
{
    close();
    PdfiumApi& api = PdfiumApi::instance();
    if (!api.isAvailable() && !api.load())
        return fail(Error::EngineUnavailable, QStringLiteral("The PDF engine (pdfium.dll) is not available: %1").arg(api.lastError()));

    QFile f(path);
    if (!QFileInfo::exists(path))
        return fail(Error::NotFound, QStringLiteral("The file was not found: %1").arg(QDir::toNativeSeparators(path)));
    if (!f.open(QIODevice::ReadOnly))
        return fail(Error::Unreadable, QStringLiteral("The file cannot be read (%1): %2").arg(f.errorString(), QDir::toNativeSeparators(path)));
    m_bytes = f.readAll();
    f.close();
    if (m_bytes.isEmpty())
        return fail(Error::Empty, QStringLiteral("The file is empty: %1").arg(QDir::toNativeSeparators(path)));
    if (m_bytes.size() > INT_MAX - 1)
        return fail(Error::Unsupported, QStringLiteral("The PDF file is too large."));

    m_doc = api.loadMemDocument(m_bytes.constData(), int(m_bytes.size()), nullptr);
    if (!m_doc) {
        switch (api.getLastError()) {
        case 4: return fail(Error::PasswordProtected, QStringLiteral("The PDF is password protected and cannot be shown."));
        case 5: return fail(Error::Unsupported, QStringLiteral("The PDF uses an unsupported security handler."));
        case 2: return fail(Error::Unreadable, QStringLiteral("The PDF file could not be read."));
        default: return fail(Error::Invalid, QStringLiteral("The file is not a valid PDF document or is corrupted."));
        }
    }
    m_pageCount = api.getPageCount(m_doc);
    if (m_pageCount <= 0)
        return fail(Error::NoPages, QStringLiteral("The PDF document contains no pages."));
    m_sizes.assign(size_t(m_pageCount), QSizeF());
    for (int i = 0; i < m_pageCount; ++i) {
        if (PdfiumApi::Page pg = api.loadPage(m_doc, i)) {
            m_sizes[size_t(i)] = QSizeF(api.getPageWidthF(pg), api.getPageHeightF(pg));
            api.closePage(pg);
        }
    }
    return true;
}

QSizeF PdfDocument::pageSize(int page) const
{
    if (page < 0 || page >= m_pageCount)
        return {};
    return m_sizes[size_t(page)];
}

PdfDocument::Bitmap PdfDocument::render(int page, int boxW, int boxH) const
{
    Bitmap out;
    const QSizeF ps = pageSize(page);
    if (!m_doc || ps.isEmpty() || boxW <= 0 || boxH <= 0)
        return out;
    PdfiumApi& api = PdfiumApi::instance();
    const double s = std::min(boxW / ps.width(), boxH / ps.height());
    int w = std::max(1, int(std::lround(ps.width() * s)));
    int h = std::max(1, int(std::lround(ps.height() * s)));
    constexpr int kMax = 8192;   // sanity limit for absurd targets
    if (w > kMax || h > kMax) {
        const double k = double(kMax) / std::max(w, h);
        w = std::max(1, int(w * k));
        h = std::max(1, int(h * k));
    }
    PdfiumApi::Page pg = api.loadPage(m_doc, page);
    if (!pg)
        return out;
    if (PdfiumApi::Bitmap bmp = api.bitmapCreate(w, h, 0)) {
        api.bitmapFillRect(bmp, 0, 0, w, h, 0xFFFFFFFFul);   // opaque white paper
        api.renderPageBitmap(bmp, pg, 0, 0, w, h, 0, 0x01 /* FPDF_ANNOT */);
        const int stride = api.bitmapGetStride(bmp);
        const char* buf = static_cast<const char*>(api.bitmapGetBuffer(bmp));
        if (buf && stride >= w * 4) {
            out.width = w;
            out.height = h;
            out.stride = stride;
            out.data = QByteArray(buf, qsizetype(stride) * h);
        }
        api.bitmapDestroy(bmp);
    }
    api.closePage(pg);
    return out;
}

} // namespace Lux
