#pragma once

#include <QByteArray>
#include <QSizeF>
#include <QString>

#include <vector>

class QLibrary;

namespace Lux {

// Minimal dynamic binding to pdfium.dll (bblanchon/pdfium-binaries, x64, no V8/XFA). Resolved at run time so the
// application starts (PDF/presentation features disabled) when the DLL is missing. Declarations follow the
// public PDFium C API (fpdfview.h). PDFium is not thread safe: use it from the GUI thread only.
struct PdfiumApi
{
    using Doc = void*;
    using Page = void*;
    using Bitmap = void*;

    void (*initLibrary)() = nullptr;
    Doc (*loadMemDocument)(const void*, int, const char*) = nullptr;
    unsigned long (*getLastError)() = nullptr;
    int (*getPageCount)(Doc) = nullptr;
    Page (*loadPage)(Doc, int) = nullptr;
    float (*getPageWidthF)(Page) = nullptr;
    float (*getPageHeightF)(Page) = nullptr;
    void (*closePage)(Page) = nullptr;
    void (*closeDocument)(Doc) = nullptr;
    Bitmap (*bitmapCreate)(int, int, int) = nullptr;
    int (*bitmapFillRect)(Bitmap, int, int, int, int, unsigned long) = nullptr;
    void* (*bitmapGetBuffer)(Bitmap) = nullptr;
    int (*bitmapGetStride)(Bitmap) = nullptr;
    void (*bitmapDestroy)(Bitmap) = nullptr;
    void (*renderPageBitmap)(Bitmap, Page, int, int, int, int, int, int) = nullptr;

    static PdfiumApi& instance();
    // Loads the DLL once. Empty path: <application dir>/pdfium.dll.
    bool load(const QString& dllPath = {});
    bool isAvailable() const { return m_loaded; }
    QString lastError() const { return m_error; }
    QString dllPath() const { return m_path; }

private:
    bool m_loaded = false;
    QString m_error;
    QString m_path;
    QLibrary* m_lib = nullptr;
};

// One opened PDF document (the whole file is read into memory, so the file is not locked).
class PdfDocument
{
public:
    enum class Error { None, EngineUnavailable, NotFound, Unreadable, Empty, Invalid, PasswordProtected, Unsupported, NoPages };

    struct Bitmap
    {
        int width = 0, height = 0, stride = 0;
        QByteArray data;   // BGRx, opaque (white page background)
        bool isNull() const { return width <= 0 || height <= 0; }
    };

    PdfDocument() = default;
    ~PdfDocument() { close(); }
    PdfDocument(const PdfDocument&) = delete;
    PdfDocument& operator=(const PdfDocument&) = delete;

    bool open(const QString& path);
    void close();
    bool isOpen() const { return m_doc != nullptr; }
    int pageCount() const { return m_pageCount; }
    QSizeF pageSize(int page) const;              // PDF points; empty when out of range
    // Renders the page scaled to fit inside boxW x boxH (aspect ratio preserved, never distorted).
    Bitmap render(int page, int boxW, int boxH) const;

    Error error() const { return m_error; }
    QString errorMessage() const { return m_message; }

private:
    bool fail(Error e, const QString& msg);

    void* m_doc = nullptr;
    QByteArray m_bytes;
    int m_pageCount = 0;
    std::vector<QSizeF> m_sizes;
    Error m_error = Error::None;
    QString m_message;
};

} // namespace Lux
