using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.Converters;

public class HexOrImageBrushConverter : IValueConverter
{
    private static readonly Brush DefaultFallbackBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageBrush> _brushCache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            // Case 1: Value is a BackgroundEntry (from BackgroundOptionsWindow thumbnail grid)
            if (value is LuxPresenter.Views.BackgroundEntry bgEntry)
            {
                return bgEntry.ThumbnailBrush;
            }

            // Case 2: Value is a BackgroundItem
            if (value is BackgroundItem bgItem)
            {
                if (!string.IsNullOrWhiteSpace(bgItem.FilePath) && File.Exists(bgItem.FilePath))
                {
                    var brush = LoadImageBrush(bgItem.FilePath);
                    if (brush != null) return brush;
                    if (bgItem.Type == BackgroundType.Video)
                    {
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B2A"));
                    }
                }

                if (!string.IsNullOrWhiteSpace(bgItem.HexColor) && bgItem.HexColor.StartsWith("#"))
                {
                    var color = (Color)ColorConverter.ConvertFromString(bgItem.HexColor);
                    return new SolidColorBrush(color);
                }
            }

            // Case 3: Value is a MediaItem
            if (value is MediaItem media)
            {
                if (!string.IsNullOrWhiteSpace(media.FilePath) && File.Exists(media.FilePath))
                {
                    var brush = LoadImageBrush(media.FilePath);
                    if (brush != null) return brush;
                }

                if (!string.IsNullOrWhiteSpace(media.HexColor) && media.HexColor.StartsWith("#"))
                {
                    var color = (Color)ColorConverter.ConvertFromString(media.HexColor);
                    return new SolidColorBrush(color);
                }
            }

            // Case 3: Value is a string (Hex code or Image Path)
            if (value is string pathOrHex && !string.IsNullOrWhiteSpace(pathOrHex))
            {
                if (pathOrHex.StartsWith("#"))
                {
                    var color = (Color)ColorConverter.ConvertFromString(pathOrHex);
                    return new SolidColorBrush(color);
                }

                if (File.Exists(pathOrHex))
                {
                    var brush = LoadImageBrush(pathOrHex);
                    if (brush != null) return brush;
                }
            }
        }
        catch
        {
            // Fail gracefully without crashing the UI
        }

        return DefaultFallbackBrush;
    }

    private static ImageBrush? LoadImageBrush(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (_brushCache.TryGetValue(path, out var cachedBrush))
            return cachedBrush;

        if (ThumbnailService.TryGetCachedBrush(path, out var brush) && brush is ImageBrush ib)
        {
            _brushCache[path] = ib;
            return ib;
        }

        try
        {
            var thumb = ThumbnailService.GetThumbnail(path, 160);
            if (thumb != null)
            {
                var newBrush = new ImageBrush(thumb) { Stretch = Stretch.UniformToFill };
                if (newBrush.CanFreeze) newBrush.Freeze();
                _brushCache[path] = newBrush;
                return newBrush;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToActiveBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")); // Red/Alert
    public Brush InactiveBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b) return ActiveBrush;
        return InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a MediaItem to a string icon ("▶" for video, "" for image/color).
/// Used only if needed in template; the BackgroundEntry.IsVideo bool is preferred.
/// </summary>
public class MediaTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LuxPresenter.Models.MediaType t)
        {
            if (t == LuxPresenter.Models.MediaType.Video) return "▶";
            if (t == LuxPresenter.Models.MediaType.Audio) return "🎵";
            if (t == LuxPresenter.Models.MediaType.Image) return "🖼";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
