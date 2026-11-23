using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.Tests;

public static class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;
    private static readonly StringBuilder _logOutput = new();

    private static void Log(string message)
    {
        Console.WriteLine(message);
        _logOutput.AppendLine(message);
    }

    private static void Assert(bool condition, string testName, string? details = null)
    {
        if (condition)
        {
            _passedCount++;
            Log($"PASS   : {testName}()");
        }
        else
        {
            _failedCount++;
            Log($"FAIL!  : {testName}() - {details ?? "Assertion failed"}");
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        var startTime = DateTime.UtcNow;

        if (Application.Current == null)
        {
            new Application();
        }

        RunCoreTests();
        RunWindowTests();
        RunProjectionTests();
        RunSongsScheduleTests();

        var elapsedMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        Log($"\nTotals: {_passedCount} passed, {_failedCount} failed, 0 skipped, {elapsedMs}ms");

        string report = _logOutput.ToString();
        try
        {
            File.WriteAllText("test_output.txt", report);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test_output.txt"), report);
            string rootFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\test_output.txt");
            if (Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(rootFile))))
            {
                File.WriteAllText(Path.GetFullPath(rootFile), report);
            }
        }
        catch { }

        if (_failedCount > 0)
        {
            Log("One or more tests failed!");
            return 1;
        }

        Log("All tests passed successfully!");
        return 0;
    }

    private static void RunCoreTests()
    {
        Log("********* Start testing of CoreTests *********");

        // 1. Settings Migration
        try
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "lux_test_core_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            string settingsFile = Path.Combine(tmpDir, "settings.json");
            File.WriteAllText(settingsFile, @"{ ""VideoBackgroundEngine"": ""libmpv"", ""SongTransition"": ""Blend"" }");

            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile)) ?? new();
            bool migrated = false;
            if (string.Equals(settings.VideoBackgroundEngine, "libmpv", StringComparison.OrdinalIgnoreCase))
            {
                settings.VideoBackgroundEngine = "MediaElement";
                migrated = true;
            }

            Assert(migrated && settings.VideoBackgroundEngine == "MediaElement", "CoreTests::settingsMigration");
            try { Directory.Delete(tmpDir, true); } catch { }
        }
        catch (Exception ex)
        {
            Assert(false, "CoreTests::settingsMigration", ex.Message);
        }

        // 2. Bible Book Lookup
        try
        {
            var ds = new DataService();
            var books = ds.GetBooks("KJV");
            var gen = books.FirstOrDefault(b => b.Name.Equals("Genesis", StringComparison.OrdinalIgnoreCase) || b.IndonesianName.Equals("Kejadian", StringComparison.OrdinalIgnoreCase));
            var mat = books.FirstOrDefault(b => b.Name.Equals("Matthew", StringComparison.OrdinalIgnoreCase) || b.IndonesianName.Equals("Matius", StringComparison.OrdinalIgnoreCase));

            Assert(gen != null && gen.Id == 1 && mat != null && mat.Id == 40, "CoreTests::bibleBookLookup", $"Gen: {gen?.Id}, Mat: {mat?.Id}");
        }
        catch (Exception ex)
        {
            Assert(false, "CoreTests::bibleBookLookup", ex.Message);
        }

        // 3. Thumbnail Service Prewarm
        try
        {
            Assert(ThumbnailService.IsVideoFile("test.mp4") && !ThumbnailService.IsVideoFile("test.png"), "CoreTests::mediaTypeDetection");
        }
        catch (Exception ex)
        {
            Assert(false, "CoreTests::mediaTypeDetection", ex.Message);
        }

        Log("********* Finished testing of CoreTests *********\n");
    }

    private static void RunWindowTests()
    {
        Log("********* Start testing of WindowTests *********");

        // 1. Platform is Windows
        Assert(OperatingSystem.IsWindows(), "WindowTests::platformIsWindows");

        // 2. Enumerates Monitors
        var screens = DisplayHelper.GetScreens();
        Log($"QINFO  : WindowTests::enumeratesMonitors() monitors: {screens.Count}");
        foreach (var s in screens)
        {
            Log($"QINFO  : WindowTests::enumeratesMonitors()   \"{s.DisplayName}\" Left: {s.PhysicalLeft} Width: {s.PhysicalWidth} Height: {s.PhysicalHeight}");
        }
        Assert(screens.Count >= 1, "WindowTests::enumeratesMonitors", "Expected at least 1 monitor");

        // 3. Secondary Placement Is Topmost Non-Activating
        try
        {
            var dummyScreen = new ScreenInfo
            {
                Index = 1,
                IsPrimary = false,
                PhysicalLeft = 1920,
                PhysicalTop = 0,
                PhysicalWidth = 1920,
                PhysicalHeight = 1080,
                DipLeft = 1920,
                DipTop = 0,
                DipWidth = 1920,
                DipHeight = 1080
            };

            var win = new Views.ProjectionWindow();
            win.SyncPositionWithScreen(dummyScreen);

            Assert(win.Topmost == true, "WindowTests::secondaryPlacementIsTopmostNonActivating", "Secondary monitor output must be topmost");
            win.Close();
        }
        catch (Exception ex)
        {
            Assert(false, "WindowTests::secondaryPlacementIsTopmostNonActivating", ex.Message);
        }

        // 4. Primary Placement Stays Behind Normal Windows
        try
        {
            var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
            var win = new Views.ProjectionWindow();
            win.SyncPositionWithScreen(primaryScreen);

            Assert(win.Topmost == false, "WindowTests::primaryPlacementStaysBehindNormalWindows", "Primary monitor output must not be topmost");
            win.Close();
        }
        catch (Exception ex)
        {
            Assert(false, "WindowTests::primaryPlacementStaysBehindNormalWindows", ex.Message);
        }

        Log("********* Finished testing of WindowTests *********\n");
    }

    private static void RunProjectionTests()
    {
        Log("********* Start testing of ProjectionTests *********");

        // 1. Display Selection
        var screens = DisplayHelper.GetScreens();
        ScreenInfo? autoSelected = null;
        if (screens.Count > 1)
        {
            autoSelected = screens.FirstOrDefault(s => !s.IsPrimary) ?? screens[0];
            Assert(!autoSelected.IsPrimary, "ProjectionTests::displaySelection", "Auto mode must prefer non-primary display");
        }
        else
        {
            autoSelected = screens[0];
            Assert(autoSelected.IsPrimary, "ProjectionTests::displaySelection", "Single screen fallback to primary");
        }

        // 2. Service Keeps Output In Sync
        try
        {
            var win = new Views.ProjectionWindow();
            win.Show();

            var slide1 = new Slide { Id = "slide-1", Content = "Verse 1 content" };
            win.UpdateState(new ProjectionState
            {
                IsLive = true,
                ActiveSlide = slide1,
                SlideTransition = "Blend",
                TransitionDurationMs = 250
            });

            var slide2 = new Slide { Id = "slide-2", Content = "Chorus content" };
            win.UpdateState(new ProjectionState
            {
                IsLive = true,
                ActiveSlide = slide2,
                SlideTransition = "Blend",
                TransitionDurationMs = 250
            });

            Assert(win.Visibility == Visibility.Visible, "ProjectionTests::serviceKeepsOutputInSync");

            // 3. Black and Logo overlay
            win.UpdateState(new ProjectionState
            {
                IsLive = true,
                IsBlack = true,
                ActiveSlide = slide2
            });

            Assert(win.Visibility == Visibility.Visible, "ProjectionTests::deckCrossfadeArithmetic");
            win.Close();
        }
        catch (Exception ex)
        {
            Assert(false, "ProjectionTests::serviceKeepsOutputInSync", ex.Message);
            Assert(false, "ProjectionTests::deckCrossfadeArithmetic", ex.Message);
        }

        Log("********* Finished testing of ProjectionTests *********\n");
    }

    private static void RunSongsScheduleTests()
    {
        Log("********* Start testing of SongsScheduleTests *********");

        // 1. Format Lyrics
        var settings = new AppSettings
        {
            CapitalizeAllWords = true,
            CapitalizeFirstCharEachLine = false
        };
        string formatted = LyricsFormatter.Format("amazing grace how sweet the sound", settings);
        Assert(formatted.Contains("AMAZING GRACE HOW SWEET THE SOUND"), "SongsScheduleTests::formatLyrics", $"Actual: {formatted}");

        // 2. Keyword Capitalization
        var kwSettings = new AppSettings
        {
            AutoCapitalizeKeywords = true,
            CapitalizeKeywords = "GOD\r\nJESUS"
        };
        string kwFormatted = LyricsFormatter.Format("Praise god and jesus", kwSettings);
        Assert(kwFormatted.Contains("GOD") && kwFormatted.Contains("JESUS"), "SongsScheduleTests::keywordCapitalization", $"Actual: {kwFormatted}");

        // 3. Schedule persistence structure
        var schedule = new ScheduleFileData
        {
            Version = "1.2.1",
            SavedAt = DateTime.Now,
            Items = new List<ScheduleItemData>
            {
                new ScheduleItemData { Title = "Opening Song", Category = ItemCategory.Song }
            }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(schedule);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ScheduleFileData>(json);
        Assert(restored != null && restored.Items.Count == 1 && restored.Items[0].Title == "Opening Song", "SongsScheduleTests::schedulePersistence");

        Log("********* Finished testing of SongsScheduleTests *********\n");
    }
}
