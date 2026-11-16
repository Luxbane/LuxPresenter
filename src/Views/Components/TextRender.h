#pragma once

#include <QFont>
#include <QPainter>
#include <QString>

namespace Lux {

// Text layout/painting shared by the slide canvas and the song-editor preview; the Qt
// counterpart of OutlinedTextBlock.cs (outline pen drawn under the fill, optional shadow).
struct TextBlockStyle
{
    QFont font;                               // pixel size must be set
    Qt::Alignment hAlign = Qt::AlignHCenter;  // AlignLeft / AlignHCenter / AlignRight
    qreal width = 568;                        // wrapping width in the painter's units
    qreal strokeThickness = 3.0;
    bool outline = true;
    bool shadow = true;
    bool wrap = true;                         // false: lines break only at line breaks (Options > Auto wrap)
    qreal shadowDepth = 3.5;                  // WPF DropShadowEffect.ShadowDepth, in painter units
};

struct TextBlockMetrics
{
    qreal width = 0;   // widest line
    qreal height = 0;  // sum of line heights
};

TextBlockMetrics measureTextBlock(const QString& text, const TextBlockStyle& style);

// Draws the block with its top-left at `origin` (units of the current painter transform).
void drawTextBlock(QPainter* p, const QString& text, const TextBlockStyle& style, const QPointF& origin);

// OutlinedTextBlock.CalculateOptimalFontSize: binary search (7 steps) for the largest
// pixel size that fits maxW x maxH, between minFs and targetMaxFs.
qreal optimalFontSize(const QString& text, QFont font, qreal maxW, qreal maxH, qreal targetMaxFs, qreal minFs = 34.0, bool wrap = true);

// Label placement (Options > Songs > Label location / size). Design pixels at 1920x1080.
struct LabelPlacement { bool top = true; Qt::Alignment h = Qt::AlignHCenter; };
LabelPlacement labelPlacement(const QString& location);
qreal labelPixelSize(const QString& size);     // Small 28, Normal 36, Large 48
inline qreal labelBandHeight(qreal px) { return px * 1.55; }
} // namespace Lux

