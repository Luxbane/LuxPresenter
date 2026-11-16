#pragma once

#include <QImage>
#include <QWidget>

namespace Lux {

// Shows one page of a PDF (or a converted presentation) scaled to fit the widget on black, aspect ratio preserved.
// The page is rendered for the widget's own pixel size, so it stays sharp at any pane size and scale factor.
class PageView : public QWidget
{
    Q_OBJECT
public:
    explicit PageView(QWidget* parent = nullptr);

    void setPage(const QString& pdfPath, int page);   // 0-based
    void clear();
    QString pdfPath() const { return m_pdf; }
    int page() const { return m_page; }
    // The rendered image currently used (device pixels; null when nothing could be rendered). Test hook.
    QImage currentImage();
    QString message() const { return m_message; }
    void setMessage(const QString& text);   // shown instead of a page (e.g. an error)

    QSize sizeHint() const override { return {480, 270}; }

protected:
    void paintEvent(QPaintEvent*) override;

private:
    QString m_pdf;
    int m_page = -1;
    QString m_message;
};

} // namespace Lux
