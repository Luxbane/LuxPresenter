using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LuxPresenter.Views.Components;

public class OutlinedTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(2.5, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(new FontFamily("Tahoma"), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(76.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontStyleProperty =
        DependencyProperty.Register(nameof(FontStyle), typeof(FontStyle), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Center, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextWrapping.Wrap, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HasOutlineProperty =
        DependencyProperty.Register(nameof(HasOutline), typeof(bool), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasShadowProperty =
        DependencyProperty.Register(nameof(HasShadow), typeof(bool), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(true, OnShadowChanged));

    private static void OnShadowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OutlinedTextBlock otb)
        {
            otb.UpdateEffect();
        }
    }

    public OutlinedTextBlock()
    {
        UpdateEffect();
    }

    private void UpdateEffect()
    {
        if (HasShadow)
        {
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 8,
                ShadowDepth = 3.5,
                Direction = 315,
                Opacity = 0.85,
                RenderingBias = RenderingBias.Performance
            };
        }
        else
        {
            Effect = null;
        }
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public bool HasOutline
    {
        get => (bool)GetValue(HasOutlineProperty);
        set => SetValue(HasOutlineProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    private FormattedText CreateFormattedText(double maxTextWidth)
    {
        double pixelsPerDip = 1.0;
        try
        {
            pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch { }

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);
        var ft = new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Black,
            pixelsPerDip
        )
        {
            TextAlignment = TextAlignment
        };

        if (TextWrapping == TextWrapping.Wrap && maxTextWidth > 0 && !double.IsInfinity(maxTextWidth))
        {
            ft.MaxTextWidth = Math.Max(50, maxTextWidth - StrokeThickness * 2);
        }

        return ft;
    }

    public static void Prewarm()
    {
        try
        {
            var typeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var ft = new FormattedText(
                "LuxPresenter Warmup",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                48,
                Brushes.White,
                1.0
            );
            var geom = ft.BuildGeometry(new Point(0, 0));
            if (geom != null)
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    var pen = new Pen(Brushes.Black, 2.0);
                    dc.DrawGeometry(null, pen, geom);
                    dc.DrawGeometry(Brushes.White, null, geom);
                }
            }
        }
        catch { }
    }

    public static double CalculateOptimalFontSize(
        string text,
        FontFamily fontFamily,
        FontWeight fontWeight,
        FontStyle fontStyle,
        double maxW,
        double maxH,
        double targetMaxFs = 94.0,
        double minFs = 34.0)
    {
        if (string.IsNullOrWhiteSpace(text)) return targetMaxFs;

        var typeface = new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal);
        double low = minFs;
        double high = targetMaxFs;
        double best = minFs;

        // Binary search to find the largest font size that fits in maxW and maxH
        for (int i = 0; i < 7; i++)
        {
            double mid = (low + high) / 2.0;
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                mid,
                Brushes.Black,
                1.0)
            {
                MaxTextWidth = Math.Max(100, maxW),
                TextAlignment = TextAlignment.Center
            };

            if (ft.Height <= maxH && ft.Width <= maxW)
            {
                best = mid;
                low = mid + 0.5;
            }
            else
            {
                high = mid - 0.5;
            }
        }

        return Math.Round(best, 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text)) return new Size(0, 0);

        double maxW = Width > 0 ? Width : (!double.IsPositiveInfinity(availableSize.Width) && availableSize.Width > 0 ? availableSize.Width : 1720);
        var ft = CreateFormattedText(maxW);
        double pad = StrokeThickness * 2;
        return new Size(
            Math.Max(0, ft.Width + pad),
            Math.Max(0, ft.Height + pad)
        );
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        double renderWidth = Width > 0 ? Width : (ActualWidth > 0 ? ActualWidth : 1720);
        var ft = CreateFormattedText(renderWidth);

        double originX = StrokeThickness;
        double originY = StrokeThickness;

        if (TextAlignment == TextAlignment.Center)
        {
            originX = 0;
        }

        var geom = ft.BuildGeometry(new Point(originX, originY));
        if (geom != null)
        {
            if (geom.CanFreeze) geom.Freeze();

            // 1. Draw outline pen FIRST so it only shows outside the letters without shrinking the bold fill!
            if (HasOutline && Stroke != null && StrokeThickness > 0)
            {
                var pen = new Pen(Stroke, StrokeThickness)
                {
                    LineJoin = PenLineJoin.Round
                };
                if (pen.CanFreeze) pen.Freeze();
                dc.DrawGeometry(null, pen, geom);
            }

            // 2. Draw solid fill on top
            dc.DrawGeometry(Fill, null, geom);
        }
    }
}
