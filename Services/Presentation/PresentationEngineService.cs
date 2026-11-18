using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LuxPresenter.Models;
using PDFtoImage;
using SkiaSharp;

namespace LuxPresenter.Services.Presentation;

public class PresentationEngineService
{
    private static readonly ConcurrentDictionary<string, PresentationDocument> _docCache = new();

    public static PresentationDocument LoadDocument(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Presentation file not found", filePath);
        }

        string fullPath = Path.GetFullPath(filePath);
        if (_docCache.TryGetValue(fullPath, out var cached))
        {
            return cached;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        string docHash = GetPathHash(fullPath);
        string cacheDir = Path.Combine(AppPaths.PresentationCacheDirectory, docHash);
        Directory.CreateDirectory(cacheDir);

        string pdfPath;
        if (ext == ".pdf")
        {
            pdfPath = fullPath;
        }
        else if (ext == ".pptx" || ext == ".ppsx" || ext == ".odp" || ext == ".ppt" || ext == ".pps")
        {
            string cachedPdf = Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(filePath)}.pdf");
            DateTime sourceLastWrite = File.GetLastWriteTimeUtc(fullPath);

            if (File.Exists(cachedPdf) && File.GetLastWriteTimeUtc(cachedPdf) >= sourceLastWrite)
            {
                pdfPath = cachedPdf;
            }
            else
            {
                pdfPath = ConvertOfficeDocToPdf(fullPath, cacheDir);
            }
        }
        else
        {
            throw new NotSupportedException($"Format '{ext}' is not supported. Please import PPTX, PPSX, ODP, or PDF.");
        }

        var doc = GenerateDocumentFromPdf(fullPath, pdfPath, cacheDir);
        _docCache[fullPath] = doc;
        return doc;
    }

    private static string ConvertOfficeDocToPdf(string inputPath, string outputDir)
    {
        string loDir = AppPaths.ResolveLibreOfficeDirectory();
        string sofficeCom = Path.Combine(loDir, "program", "soffice.com");
        if (!File.Exists(sofficeCom))
        {
            sofficeCom = Path.Combine(loDir, "program", "soffice.exe");
        }
        if (!File.Exists(sofficeCom))
        {
            sofficeCom = @"E:\LibreOffice\program\soffice.com";
        }

        if (!File.Exists(sofficeCom))
        {
            throw new FileNotFoundException("LibreOffice executable not found. Please install or bundle LibreOffice.", sofficeCom);
        }

        string userProfileDir = Path.Combine(AppPaths.PresentationCacheDirectory, "_user_profile").Replace("\\", "/");
        string userUrl = $"file:///{userProfileDir}";

        var psi = new ProcessStartInfo
        {
            FileName = sofficeCom,
            Arguments = $"--headless --norestore --nologo --nodefault \"-env:UserInstallation={userUrl}\" --convert-to pdf:impress_pdf_Export \"{inputPath}\" --outdir \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            throw new InvalidOperationException("Failed to launch LibreOffice conversion process.");
        }

        proc.WaitForExit(45000);

        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string expectedPdf = Path.Combine(outputDir, $"{baseName}.pdf");
        if (!File.Exists(expectedPdf))
        {
            throw new IOException($"Presentation to PDF conversion failed. Expected output file: {expectedPdf}");
        }

        return expectedPdf;
    }

    private static PresentationDocument GenerateDocumentFromPdf(string originalFilePath, string pdfPath, string cacheDir)
    {
        byte[] pdfBytes = File.ReadAllBytes(pdfPath);
        int pageCount = Conversion.GetPageCount(pdfBytes);

        var doc = new PresentationDocument
        {
            Id = GetPathHash(originalFilePath),
            Title = Path.GetFileNameWithoutExtension(originalFilePath),
            FilePath = originalFilePath,
            CanvasWidth = 1920,
            CanvasHeight = 1080
        };

        string thumbsDir = Path.Combine(cacheDir, "thumbs");
        string previewDir = Path.Combine(cacheDir, "previews");
        Directory.CreateDirectory(thumbsDir);
        Directory.CreateDirectory(previewDir);

        for (int i = 0; i < pageCount; i++)
        {
            int slideNum = i + 1;
            string previewImg = Path.Combine(previewDir, $"slide_{slideNum}.png");
            string thumbImg = Path.Combine(thumbsDir, $"thumb_{slideNum}.jpg");

            if (!File.Exists(previewImg) || !File.Exists(thumbImg))
            {
                using var bitmap = Conversion.ToImage(pdfBytes, page: i);
                if (!File.Exists(previewImg))
                {
                    using var fs = File.OpenWrite(previewImg);
                    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 95);
                    data.SaveTo(fs);
                }

                if (!File.Exists(thumbImg))
                {
                    // Scale down for crisp fast thumbnail
                    int thumbWidth = 240;
                    int thumbHeight = (int)Math.Max(135, (double)bitmap.Height * thumbWidth / bitmap.Width);
                    using var thumbBmp = bitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKSamplingOptions.Default);
                    if (thumbBmp != null)
                    {
                        using var tfs = File.OpenWrite(thumbImg);
                        using var tdata = thumbBmp.Encode(SKEncodedImageFormat.Jpeg, 85);
                        tdata.SaveTo(tfs);
                    }
                    else
                    {
                        File.Copy(previewImg, thumbImg, true);
                    }
                }
            }

            var slide = new PresentationSlide
            {
                Index = slideNum,
                BackgroundColor = "#FFFFFF",
                ThumbnailPath = thumbImg,
                ImagePreviewPath = previewImg
            };

            doc.Slides.Add(slide);
        }

        return doc;
    }

    public static string GenerateAndSaveThumbnail(PresentationSlide slide, string docId, int slideIndex)
    {
        if (!string.IsNullOrEmpty(slide.ThumbnailPath) && File.Exists(slide.ThumbnailPath))
        {
            return slide.ThumbnailPath;
        }

        return slide.ImagePreviewPath ?? string.Empty;
    }

    public static string GetPathHash(string path)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void DeleteCacheForDocument(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            _docCache.TryRemove(fullPath, out _);

            string docHash = GetPathHash(fullPath);
            string cacheDir = Path.Combine(AppPaths.PresentationCacheDirectory, docHash);
            SafeDeleteDirectory(cacheDir);
        }
        catch { }
    }

    public static void CleanupOrphanedCaches(IEnumerable<string>? activeFilePaths)
    {
        try
        {
            string cacheBaseDir = AppPaths.PresentationCacheDirectory;
            if (!Directory.Exists(cacheBaseDir)) return;

            var activeHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (activeFilePaths != null)
            {
                foreach (var path in activeFilePaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        try
                        {
                            activeHashes.Add(GetPathHash(Path.GetFullPath(path)));
                        }
                        catch { }
                    }
                }
            }

            var dirs = Directory.GetDirectories(cacheBaseDir);
            foreach (var dir in dirs)
            {
                string dirName = Path.GetFileName(dir);

                // _user_profile is LibreOffice's temporary profile during conversion
                if (dirName.Equals("_user_profile", StringComparison.OrdinalIgnoreCase))
                {
                    SafeDeleteDirectory(dir);
                    continue;
                }

                // If not in active presentations, remove cache folder
                if (!activeHashes.Contains(dirName))
                {
                    SafeDeleteDirectory(dir);
                }
            }

            var files = Directory.GetFiles(cacheBaseDir);
            foreach (var file in files)
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void SafeDeleteDirectory(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath)) return;

            foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch { }
            }

            Directory.Delete(dirPath, recursive: true);
        }
        catch { }
    }
}
