using System;
using System.IO;

namespace LuxPresenter.Services;

public static class AppPaths
{
    private static string? _dataDirectory;

    public static string DataDirectory
    {
        get
        {
            if (_dataDirectory == null)
            {
                _dataDirectory = ResolveDataDirectory();
            }
            return _dataDirectory;
        }
    }

    public static string ThumbnailCacheDirectory => Path.Combine(DataDirectory, "Thumbnails");

    public static string PresentationCacheDirectory
    {
        get
        {
            string dir = Path.Combine(DataDirectory, "PresentationCache");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    public static string ResolveLibreOfficeDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localEngine = Path.Combine(baseDir, "Engines", "LibreOffice");
        if (Directory.Exists(localEngine)) return localEngine;

        if (Directory.Exists(@"E:\LibreOffice")) return @"E:\LibreOffice";

        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string standardPf = Path.Combine(pf, "LibreOffice");
        if (Directory.Exists(standardPf)) return standardPf;

        return localEngine;
    }

    public static string MediaDirectory => Path.Combine(DataDirectory, "Media");

    private static string? _songsDirectory;

    public static string SongsDirectory
    {
        get
        {
            if (_songsDirectory == null)
            {
                _songsDirectory = ResolveSongsDirectory();
            }
            return _songsDirectory;
        }
    }

    public static void SetSongsDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _songsDirectory = ResolveSongsDirectory();
        }
        else
        {
            _songsDirectory = path;
            try
            {
                Directory.CreateDirectory(path);
            }
            catch { }
        }
    }

    private static string ResolveSongsDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localDbDir = Path.Combine(baseDir, "Database");

        // 1. If running in a writable directory (Portable mode, e.g. USB drive or custom folder)
        if (IsDirectoryWritable(baseDir))
        {
            try
            {
                Directory.CreateDirectory(localDbDir);
                return localDbDir;
            }
            catch { }
        }

        // 2. Installed mode (e.g. C:\Program Files\FreeWorship):
        // Standard user Documents folder: Documents\FreeWorship\Database
        string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(myDocs))
        {
            myDocs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
        }

        string docsDbDir = Path.Combine(myDocs, "LuxPresenter", "Database");

        try
        {
            Directory.CreateDirectory(docsDbDir);
        }
        catch { }

        return docsDbDir;
    }

    private static string ResolveDataDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localDataDir = Path.Combine(baseDir, "Data");

        // 1. If running in a writable directory (e.g. Portable USB drive, D:\, etc.), keep portable mode
        if (IsDirectoryWritable(baseDir))
        {
            try
            {
                Directory.CreateDirectory(localDataDir);
                return localDataDir;
            }
            catch { }
        }

        // 2. Installed mode (e.g. C:\Program Files\FreeWorship\):
        // Standard Windows non-admin users cannot write to Program Files.
        // Use user's roaming AppData directory (%AppData%\FreeWorship\Data)
        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LuxPresenter",
            "Data"
        );

        try
        {
            Directory.CreateDirectory(appDataDir);

            // Backward compatibility: automatically migrate data from legacy FreeWorship if present
            string legacyAppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FreeWorship",
                "Data"
            );
            if (Directory.Exists(legacyAppDataDir))
            {
                CopyDirectoryIfMissing(legacyAppDataDir, appDataDir);
            }

            // Seed initial data and media from application installation directory if not present
            if (Directory.Exists(localDataDir))
            {
                CopyDirectoryIfMissing(localDataDir, appDataDir);
            }
        }
        catch { }

        return appDataDir;
    }

    private static bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath)) return false;

            string testFile = Path.Combine(dirPath, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectoryIfMissing(string sourceDir, string targetDir)
    {
        try
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, file);
                string dest = Path.Combine(targetDir, relative);
                if (!File.Exists(dest))
                {
                    string? destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(file, dest, overwrite: false);
                }
            }
        }
        catch { }
    }
}
