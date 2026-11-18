using System;
using System.Windows;
using System.Windows.Media;

namespace LuxPresenter.Services;

/// <summary>
/// Service managing dynamic Light and Dark mode theming across LuxPresenter.
/// Dark mode uses a sophisticated dark charcoal gray palette (not pitch black).
/// </summary>
public static class ThemeService
{
    public static bool IsDarkMode { get; private set; }

    public static event Action<bool>? ThemeChanged;

    public static void ApplyTheme(bool isDark)
    {
        IsDarkMode = isDark;

        var res = Application.Current?.Resources;
        if (res == null) return;

        if (isDark)
        {
            // =========================================================================
            // DARK MODE: Elegant Dark Gray Palette (NOT pitch black #000000)
            // =========================================================================

            // Window and Framing
            res["WindowBgBrush"] = new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x2C)); // #24262C
            res["PanelBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x25));  // #1F2025
            res["SurfaceBgBrush"] = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x34)); // #2B2D34

            // Headers & Toolbars (Subtle Dark Charcoal Gradients)
            var headerGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x32, 0x35, 0x3E), 0.0),
                    new GradientStop(Color.FromRgb(0x27, 0x29, 0x30), 1.0)
                }
            };
            res["HeaderBgBrush"] = headerGrad;

            var toolbarGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x31, 0x33, 0x3B), 0.0),
                    new GradientStop(Color.FromRgb(0x25, 0x27, 0x2E), 1.0)
                }
            };
            res["ToolbarBgBrush"] = toolbarGrad;

            // Status Bar & Menu
            res["StatusBarBgBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x24, 0x29));
            res["MenuBgBrush"] = new SolidColorBrush(Color.FromRgb(0x26, 0x28, 0x2E));

            // Borders, Separators & Splitters
            res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x40, 0x49));   // #3D4049
            res["SplitterBgBrush"] = new SolidColorBrush(Color.FromRgb(0x38, 0x3A, 0x42));

            // Typography
            res["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xED, 0xF0)); // Soft white
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9E, 0xA4, 0xB0)); // Silver gray
            res["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x6D, 0x73, 0x82));

            // Lists, Tables & Trees
            res["ListBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x24)); // #1E1F24 dark gray
            res["ListItemTextBrush"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xED, 0xF0));

            // Input Controls (TextBoxes, Search Boxes)
            res["InputBgBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x19, 0x1D));
            res["InputBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x44, 0x47, 0x52));
            res["InputFgBrush"] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));

            // Interactive States & Buttons
            res["ButtonBgBrush"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x30, 0x38));
            res["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x42, 0x45, 0x50));
            res["ButtonHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x47));
            res["ButtonHoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x56, 0x5A, 0x67));

            // Inactive / Unfocused Selection
            res["InactiveSelectionBgBrush"] = new SolidColorBrush(Color.FromRgb(0x35, 0x38, 0x42));
            res["InactiveSelectionBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x4B, 0x4E, 0x5B));
            res["InactiveSelectionTextBrush"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));

            // Tabs
            res["TabControlBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x25));
            res["TabItemBgBrush"] = new SolidColorBrush(Color.FromRgb(0x27, 0x29, 0x30));
            res["TabItemSelectedBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x25));
            res["TabItemFgBrush"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xED, 0xF0));

            // Scrollbars
            res["ScrollbarTrackBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x20, 0x25));
            res["ScrollbarThumbBrush"] = new SolidColorBrush(Color.FromRgb(0x46, 0x49, 0x54));

            // Specific controls
            res["DonateBtnTextBrush"] = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)); // Warm gold
        }
        else
        {
            // =========================================================================
            // LIGHT MODE: Classic Worship Presentation Look
            // =========================================================================

            res["WindowBgBrush"] = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
            res["PanelBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["SurfaceBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF8));

            var headerGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xF9, 0xFA, 0xFB), 0.0),
                    new GradientStop(Color.FromRgb(0xDF, 0xE4, 0xEA), 1.0)
                }
            };
            res["HeaderBgBrush"] = headerGrad;

            var toolbarGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xFF, 0xFF, 0xFF), 0.0),
                    new GradientStop(Color.FromRgb(0xE6, 0xEB, 0xF0), 1.0)
                }
            };
            res["ToolbarBgBrush"] = toolbarGrad;

            res["StatusBarBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF8));
            res["MenuBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF3, 0xF7));

            res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xB2, 0xBA, 0xC2));
            res["SplitterBgBrush"] = new SolidColorBrush(Color.FromRgb(0xB2, 0xBA, 0xC2));

            res["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            res["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

            res["ListBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["ListItemTextBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));

            res["InputBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["InputBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            res["InputFgBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));

            res["ButtonBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            res["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            res["ButtonHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            res["ButtonHoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

            res["InactiveSelectionBgBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            res["InactiveSelectionBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            res["InactiveSelectionTextBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));

            res["TabControlBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["TabItemBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF8));
            res["TabItemSelectedBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["TabItemFgBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));

            res["ScrollbarTrackBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF8));
            res["ScrollbarThumbBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

            // Specific controls
            res["DonateBtnTextBrush"] = new SolidColorBrush(Color.FromRgb(0x85, 0x4D, 0x0E)); // Warm amber/brown
        }

        ThemeChanged?.Invoke(isDark);
    }
}
