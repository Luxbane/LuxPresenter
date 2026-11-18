using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LuxPresenter.Services;

public static class ThumbnailService
{
    private static readonly ConcurrentDictionary<string, ImageSource> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".wmv", ".avi", ".mov", ".mkv", ".m4v", ".mpg", ".mpeg", ".webm", ".ts", ".flv", ".mts", ".m2ts"
    };

    private static readonly string CacheDir = AppPaths.ThumbnailCacheDirectory;

    static ThumbnailService()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
        }
        catch { }
    }

    public static bool IsVideoFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
    }

    public static ImageSource? GetVideoThumbnail(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return null;

        if (_memoryCache.TryGetValue(videoPath, out var cached))
            return cached;

        var cacheFilePath = GetCacheFilePath(videoPath);

        // 1. Check local on-disk cache file
        if (File.Exists(cacheFilePath))
        {
            try
            {
                var bmp = LoadBitmapFromFile(cacheFilePath);
                if (bmp != null)
                {
                    _memoryCache[videoPath] = bmp;
                    return bmp;
                }
            }
            catch { }
        }

        // 2. Try Windows Shell Thumbnail API (IShellItemImageFactory)
        try
        {
            var shellBmp = GetShellThumbnail(videoPath, 240, 160);
            if (shellBmp != null)
            {
                SaveBitmapToJpeg(shellBmp, cacheFilePath);
                _memoryCache[videoPath] = shellBmp;
                return shellBmp;
            }
        }
        catch { }

        // 3. Try FFmpeg extraction (e.g. from Shotcut or system)
        try
        {
            var ffmpegBmp = ExtractFrameViaFfmpeg(videoPath, cacheFilePath);
            if (ffmpegBmp != null)
            {
                _memoryCache[videoPath] = ffmpegBmp;
                return ffmpegBmp;
            }
        }
        catch { }

        // 4. Fallback: generate a stylish video slate thumbnail
        try
        {
            var fallback = CreateFallbackThumbnail(Path.GetFileNameWithoutExtension(videoPath));
            _memoryCache[videoPath] = fallback;
            return fallback;
        }
        catch
        {
            return null;
        }
    }

    public static string GetCacheFilePath(string videoPath)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(videoPath.ToLowerInvariant()));
        var hash = Convert.ToHexString(hashBytes).Substring(0, 16);
        return Path.Combine(CacheDir, $"{hash}.jpg");
    }

    public static void DeleteThumbnail(string videoOrMediaPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(videoOrMediaPath)) return;
            var path = GetCacheFilePath(videoOrMediaPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            _memoryCache.TryRemove(videoOrMediaPath, out _);
        }
        catch { }
    }

    public static void CleanupOrphanedThumbnails(IEnumerable<string> activePaths)
    {
        try
        {
            var validFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in activePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var thumbFile = Path.GetFileName(GetCacheFilePath(path));
                    validFileNames.Add(thumbFile);
                }
            }

            string[] candidateDirs = {
                AppPaths.ThumbnailCacheDirectory,
                Path.Combine(AppPaths.DataDirectory, "Thumbnail")
            };

            foreach (var dir in candidateDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.*"))
                {
                    string fileName = Path.GetFileName(file);
                    if (!validFileNames.Contains(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    #region Windows Shell Thumbnail API

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] SIIGBF flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly Guid ShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    private static BitmapSource? GetShellThumbnail(string filePath, int width, int height)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ShellItemImageFactoryGuid, out var factory);
            if (hr != 0 || factory == null) return null;

            // IMPORTANT: Use SIIGBF_THUMBNAILONLY (0x08) so it NEVER returns the file icon (e.g. VLC traffic cone)
            hr = factory.GetImage(new SIZE(width, height), SIIGBF.SIIGBF_THUMBNAILONLY, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero) return null;

            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
        }
    }

    #endregion

    #region FFmpeg Extraction Fallback

    private static string? FindFfmpeg()
    {
        string[] candidates =
        {
            @"C:\Program Files\Shotcut\ffmpeg.exe",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo("where", "ffmpeg.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p != null)
            {
                var line = p.StandardOutput.ReadLine();
                p.WaitForExit(1000);
                if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim()))
                    return line.Trim();
            }
        }
        catch { }

        return null;
    }

    private static BitmapSource? ExtractFrameViaFfmpeg(string videoPath, string outputPath)
    {
        var ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg)) return null;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-ss 00:00:00.5 -i \"{videoPath}\" -vframes 1 -vf \"scale=240:150:force_original_aspect_ratio=decrease\" \"{outputPath}\" -y",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null) return null;

        bool exited = process.WaitForExit(3500);
        if (!exited)
        {
            try { process.Kill(); } catch { }
            return null;
        }

        if (File.Exists(outputPath))
        {
            return LoadBitmapFromFile(outputPath);
        }

        return null;
    }

    #endregion

    private static BitmapSource? LoadBitmapFromFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveBitmapToJpeg(BitmapSource source, string outputPath)
    {
        try
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
        catch { }
    }

    private static BitmapSource CreateFallbackThumbnail(string title)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")), null, new Rect(0, 0, 160, 110));
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")), null, new Rect(0, 0, 160, 10));
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")), null, new Rect(0, 100, 160, 10));

            for (int x = 6; x < 160; x += 18)
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(x, 2, 8, 6));
                dc.DrawRectangle(Brushes.White, null, new Rect(x, 102, 8, 6));
            }

            var iconText = new FormattedText("🎬", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI Emoji, Segoe UI, Arial"), 24, Brushes.White, 96);
            dc.DrawText(iconText, new Point(80 - iconText.Width / 2, 36));

            var shortTitle = title.Length > 16 ? title.Substring(0, 14) + "..." : title;
            var titleText = new FormattedText(shortTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI, Tahoma"), 10, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")), 96);
            dc.DrawText(titleText, new Point(80 - titleText.Width / 2, 74));
        }

        var rtb = new RenderTargetBitmap(160, 110, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
