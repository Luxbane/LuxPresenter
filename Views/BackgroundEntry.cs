using System.IO;
using System.Windows;
using System.Windows.Media;
using LuxPresenter.Models;
using LuxPresenter.Services;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views;

/// <summary>
/// A wrapper around BackgroundItem that adds IsSelected state and fast asynchronous thumbnail loading
/// for the Background Options thumbnail grid.
/// </summary>
public class BackgroundEntry : ViewModelBase
{
    private static readonly Brush DefaultFallbackBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B2A"));
    static BackgroundEntry()
    {
        if (DefaultFallbackBrush.CanFreeze) DefaultFallbackBrush.Freeze();
    }

    private bool _isSelected;
    private Brush _thumbnailBrush = DefaultFallbackBrush;

    public BackgroundItem BackgroundItem { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsVideo => BackgroundItem.Type == BackgroundType.Video;

    public string FilePath => BackgroundItem.FilePath;
    public string HexColor => BackgroundItem.HexColor;
    public string Name => BackgroundItem.Name;

    public Brush ThumbnailBrush
    {
        get => _thumbnailBrush;
        private set => SetProperty(ref _thumbnailBrush, value);
    }

    public BackgroundEntry(BackgroundItem item)
    {
        BackgroundItem = item;
        InitializeThumbnail();
    }

    private void InitializeThumbnail()
    {
        // 1. Color background
        if (!string.IsNullOrWhiteSpace(HexColor) && HexColor.StartsWith("#"))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(HexColor);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                ThumbnailBrush = brush;
                return;
            }
            catch { }
        }

        // 2. File background (Image or Video)
        if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
        {
            // Fast path: if thumbnail already exists in memory or disk cache, load instantly (<0.5ms)
            if (ThumbnailService.TryGetCachedBrush(FilePath, out var cachedBrush) && cachedBrush != null)
            {
                ThumbnailBrush = cachedBrush;
                return;
            }

            // Async path: set dark fallback brush immediately so window opens with 0 latency,
            // and fetch/generate thumbnail via embedded libmpv in background
            ThumbnailService.RequestThumbnailBrushAsync(FilePath, brush =>
            {
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    ThumbnailBrush = brush;
                });
            });
        }
    }
}
