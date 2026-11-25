using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;

namespace LuxPresenter.Services;

/// <summary>
/// Extracts a still video frame using embedded libmpv (vo=image).
/// Supports all video containers and codecs (including .flv, .mkv, .webm, .mp4, .avi, .mov)
/// with zero external process dependencies (no ffmpeg.exe required).
/// </summary>
public static class VideoPoster
{
    private static readonly object _posterLock = new();

    public static bool Generate(string videoPath, string outFilePath, int timeoutMs = 4500)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return false;

        if (File.Exists(outFilePath) && new FileInfo(outFilePath).Length > 0)
            return true;

        lock (_posterLock)
        {
            if (File.Exists(outFilePath) && new FileInfo(outFilePath).Length > 0)
                return true;

            string outDir = Path.GetDirectoryName(outFilePath) ?? AppPaths.ThumbnailCacheDirectory;
            string tmpDir = Path.Combine(outDir, "vpost_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tmpDir);

                IntPtr h = MpvNative.mpv_create();
                if (h == IntPtr.Zero) return false;

                try
                {
                    MpvNative.mpv_set_option_string(h, "vo", "image");
                    MpvNative.mpv_set_option_string(h, "vo-image-format", "jpg");
                    MpvNative.mpv_set_option_string(h, "vo-image-outdir", tmpDir);
                    MpvNative.mpv_set_option_string(h, "ao", "null");
                    MpvNative.mpv_set_option_string(h, "aid", "no");
                    MpvNative.mpv_set_option_string(h, "sid", "no");
                    MpvNative.mpv_set_option_string(h, "frames", "1");
                    MpvNative.mpv_set_option_string(h, "start", "0.5");
                    MpvNative.mpv_set_option_string(h, "hr-seek", "no");
                    MpvNative.mpv_set_option_string(h, "keep-open", "no");
                    MpvNative.mpv_set_option_string(h, "idle", "yes");
                    MpvNative.mpv_set_option_string(h, "config", "no");
                    MpvNative.mpv_set_option_string(h, "load-scripts", "no");
                    MpvNative.mpv_set_option_string(h, "ytdl", "no");
                    MpvNative.mpv_set_option_string(h, "terminal", "no");
                    MpvNative.mpv_set_option_string(h, "osc", "no");

                    if (MpvNative.mpv_initialize(h) < 0)
                        return false;

                    IntPtr[] args = new IntPtr[3];
                    args[0] = Marshal.StringToCoTaskMemUTF8("loadfile");
                    args[1] = Marshal.StringToCoTaskMemUTF8(videoPath);
                    args[2] = IntPtr.Zero;

                    try
                    {
                        MpvNative.mpv_command(h, args);
                    }
                    finally
                    {
                        if (args[0] != IntPtr.Zero) Marshal.FreeCoTaskMem(args[0]);
                        if (args[1] != IntPtr.Zero) Marshal.FreeCoTaskMem(args[1]);
                    }

                    var sw = Stopwatch.StartNew();
                    bool started = false;
                    string? produced = null;

                    while (sw.ElapsedMilliseconds < timeoutMs)
                    {
                        Thread.Sleep(20);
                        var files = Directory.GetFiles(tmpDir, "*.*");
                        string? idle = MpvNative.GetPropertyString(h, "idle-active");
                        bool isIdle = string.Equals(idle, "yes", StringComparison.OrdinalIgnoreCase);

                        if (!isIdle) started = true;

                        if (files.Length > 0)
                        {
                            produced = files[0];
                            break;
                        }

                        if (started && isIdle) break;
                    }

                    // Terminate and destroy mpv instance FIRST so that all file locks on output files are fully released
                    MpvNative.mpv_terminate_destroy(h);
                    h = IntPtr.Zero;

                    if (!string.IsNullOrEmpty(produced) && File.Exists(produced) && new FileInfo(produced).Length > 0)
                    {
                        SaveDownscaledThumbnail(produced, outFilePath, 240);
                        return true;
                    }
                }
                finally
                {
                    if (h != IntPtr.Zero)
                    {
                        MpvNative.mpv_terminate_destroy(h);
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tmpDir))
                        Directory.Delete(tmpDir, true);
                }
                catch { }
            }

            return false;
        }
    }

    private static void SaveDownscaledThumbnail(string sourcePath, string targetPath, int decodeWidth)
    {
        try
        {
            using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            string tmpTarget = targetPath + ".tmp";
            using (var outStream = new FileStream(tmpTarget, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(outStream);
            }
            File.Move(tmpTarget, targetPath, overwrite: true);
        }
        catch
        {
            // Fallback: direct copy if decoding/re-encoding fails
            try { File.Copy(sourcePath, targetPath, true); } catch { }
        }
    }
}
