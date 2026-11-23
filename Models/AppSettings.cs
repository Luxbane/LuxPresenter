using System.Collections.Generic;

namespace LuxPresenter.Models;

public class AppSettings
{
    public string LastUsedBackground { get; set; } = string.Empty;
    public bool IsVideoLooping { get; set; } = true;
    public int ActiveLibraryTabIndex { get; set; } = 0;
    public string? CustomSongDatabaseDirectory { get; set; }
    public bool IsDarkMode { get; set; } = false;
    public bool IsMediaGridView { get; set; } = true;
    public double LeftPaneWidth { get; set; } = 380;

    public List<string> PresetAlertMessages { get; set; } = new();

    // Song Options (Images 7 & 8)
    public string SongTextAlignment { get; set; } = "Center"; // Left, Center, Right
    public string SongVerticalAlignment { get; set; } = "Top"; // Top, Center, Bottom
    public int SongFontSize { get; set; } = 13;
    public int SongFontHeightPercent { get; set; } = 14;
    public string SongFontFamily { get; set; } = "Arial";
    public bool SongFontBold { get; set; } = true;
    public bool OverrideSongFontGlobally { get; set; } = false;

    // Projection Margins
    public int MarginTop { get; set; } = 0;
    public int MarginBottom { get; set; } = 0;
    public int MarginLeft { get; set; } = 0;
    public int MarginRight { get; set; } = 0;

    // Verse/Chorus Label Options
    public bool ShowVerseLabels { get; set; } = false;
    public string LabelLocation { get; set; } = "Top Center";
    public string LabelSize { get; set; } = "Normal";
    public bool IndentLabel { get; set; } = false;

    // Word Formatting Options
    public bool AutoWrapWords { get; set; } = true;
    public bool CapitalizeAllWords { get; set; } = true;
    public bool CapitalizeFirstCharEachLine { get; set; } = false;
    public bool AutoCapitalizeKeywords { get; set; } = false;
    public string CapitalizeKeywords { get; set; } = "CHRIST\r\nFATHER\r\nGOD\r\nHE\r\nHIS\r\nHIM\r\nHIMSELF";

    // Transitions
    public string SongTransition { get; set; } = "Blend"; // Blend, Cut
    public string SlideTransition { get; set; } = "Blend"; // Blend, Cut
    public string MediaTransition { get; set; } = "Blend"; // Blend, Cut
    public string ScreenTransition { get; set; } = "Blend"; // Blend, Cut
    public int TransitionDurationMs { get; set; } = 500;

    // Video Background Engine: "libmpv" (Recommended) or "MediaElement" (Legacy WPF Fallback)
    public string VideoBackgroundEngine { get; set; } = "libmpv";
    public bool EngineDefaultedToLibMpv { get; set; } = false;

    // Stage Display (Confidence Monitor) Settings
    public bool EnableStageDisplay { get; set; } = false;
    public int StageDisplayMonitorIndex { get; set; } = -1; // -1 = Windowed Preview, 0, 1, 2... = Specific Screen
    public bool ShowStageNextSlide { get; set; } = true;
    public bool ShowStageClock { get; set; } = true;
    public bool EnableStageWebServer { get; set; } = false;
    public int StageWebServerPort { get; set; } = 8080;
}
