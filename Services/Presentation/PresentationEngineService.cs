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

            if (File.Exists(cachedPdf) && new FileInfo(cachedPdf).Length > 0 && File.GetLastWriteTimeUtc(cachedPdf) >= sourceLastWrite.AddSeconds(-2))
            {
                pdfPath = cachedPdf;
            }
            else
            {
                // Fallback: check if any valid PDF exists in this document's cache folder
                var existingPdfs = Directory.GetFiles(cacheDir, "*.pdf");
                if (existingPdfs.Length > 0 && new FileInfo(existingPdfs[0]).Length > 0 && File.GetLastWriteTimeUtc(existingPdfs[0]) >= sourceLastWrite.AddSeconds(-2))
                {
                    pdfPath = existingPdfs[0];
                }
                else
                {
                    pdfPath = ConvertOfficeDocToPdf(fullPath, cacheDir);
                }
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

        // Clean up any stale LibreOffice lock files in the user profile directory
        try
        {
            string actualProfileDir = Path.Combine(AppPaths.PresentationCacheDirectory, "_user_profile");
            if (Directory.Exists(actualProfileDir))
            {
                foreach (var lockFile in Directory.GetFiles(actualProfileDir, ".~lock*", SearchOption.AllDirectories))
                {
                    try { File.Delete(lockFile); } catch { }
                }
            }
        }
        catch { }

        var psi = new ProcessStartInfo
        {
            FileName = sofficeCom,
            Arguments = $"--headless --norestore --nologo --nodefault \"-env:UserInstallation={userUrl}\" --convert-to pdf:impress_pdf_Export \"{inputPath}\" --outdir \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        if (!proc.Start())
        {
            throw new InvalidOperationException("Failed to launch LibreOffice conversion process.");
        }

        // Asynchronously read stdout and stderr to prevent OS pipe buffer deadlock
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        // 120-second timeout for large presentations or cold start
        bool finished = proc.WaitForExit(120000);
        if (!finished)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"LibreOffice conversion timed out after 120 seconds for '{Path.GetFileName(inputPath)}'.");
        }

        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string expectedPdf = Path.Combine(outputDir, $"{baseName}.pdf");

        // Verify expected PDF or check if LibreOffice generated a slightly differently named PDF in outputDir
        if (!File.Exists(expectedPdf) || new FileInfo(expectedPdf).Length == 0)
        {
            var pdfs = Directory.GetFiles(outputDir, "*.pdf");
            if (pdfs.Length > 0)
            {
                var candidate = pdfs.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
                if (candidate != null && new FileInfo(candidate).Length > 0)
                {
                    expectedPdf = candidate;
                }
            }
        }

        if (!File.Exists(expectedPdf) || new FileInfo(expectedPdf).Length == 0)
        {
            string err = stderrBuilder.ToString().Trim();
            string msg = string.IsNullOrWhiteSpace(err) ? stdoutBuilder.ToString().Trim() : err;
            throw new IOException($"Presentation to PDF conversion failed. Expected output file: {expectedPdf}\nProcess Output: {msg}");
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

            if (!File.Exists(previewImg) || !File.Exists(thumbImg) || new FileInfo(previewImg).Length == 0 || new FileInfo(thumbImg).Length == 0)
            {
                using var bitmap = Conversion.ToImage(pdfBytes, page: i);
                if (!File.Exists(previewImg) || new FileInfo(previewImg).Length == 0)
                {
                    using var fs = File.Create(previewImg);
                    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 95);
                    data.SaveTo(fs);
                }

                if (!File.Exists(thumbImg) || new FileInfo(thumbImg).Length == 0)
                {
                    // Scale down for crisp fast thumbnail
                    int thumbWidth = 240;
                    int thumbHeight = (int)Math.Max(135, (double)bitmap.Height * thumbWidth / bitmap.Width);
                    using var thumbBmp = bitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKSamplingOptions.Default);
                    if (thumbBmp != null)
                    {
                        using var tfs = File.Create(thumbImg);
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
