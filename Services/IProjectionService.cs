using LuxPresenter.Models;

namespace LuxPresenter.Services;

public class ProjectionState
{
    public Slide? ActiveSlide { get; set; }
    public string ActiveBackground { get; set; } = "#001833";
    public bool IsLive { get; set; } = true;
    public bool IsBlack { get; set; } = false;
    public bool IsClear { get; set; } = false;
    public bool IsLogo { get; set; } = false;
    public string LogoPath { get; set; } = string.Empty;
    public bool IsVideoMode { get; set; } = false;
    public string? MediaFilePath { get; set; }
    public bool IsVideoPlaying { get; set; } = false;
    public double VideoVolume { get; set; } = 1.0;
    public bool IsVideoLooping { get; set; } = true;
    public string VideoBackgroundEngine { get; set; } = "libmpv"; // "libmpv" or "MediaElement"

    // Presentation Document Mode
    public bool IsPresentationDocMode { get; set; } = false;
    public PresentationDocument? LivePresentationDoc { get; set; }
    public int LivePresentationSlideIndex { get; set; } = 0;

    // Formatting & Transitions
    public string SongTextAlignment { get; set; } = "Center";
    public string SongVerticalAlignment { get; set; } = "Center";
    public string SongTransition { get; set; } = "Blend";
    public string SlideTransition { get; set; } = "Blend";
    public string MediaTransition { get; set; } = "Blend";
    public string ScreenTransition { get; set; } = "Blend";
    public int TransitionDurationMs { get; set; } = 500;
    public bool ShowVerseLabels { get; set; } = false;

    // Word Formatting
    public bool CapitalizeAllWords { get; set; } = true;
    public bool CapitalizeFirstCharEachLine { get; set; } = false;
    public bool AutoCapitalizeKeywords { get; set; } = false;
    public string CapitalizeKeywords { get; set; } = string.Empty;
    public string SongFontFamily { get; set; } = "Arial, Tahoma";
    public bool SongFontBold { get; set; } = true;
    public int SongFontSize { get; set; } = 13;
    public int SongFontHeightPercent { get; set; } = 14;

    // Stage Display (Confidence Monitor)
    public Slide? ActiveNextSlide { get; set; }
    public string ActiveItemTitle { get; set; } = string.Empty;
    public string StageMessage { get; set; } = string.Empty;
    public bool IsStageDisplayEnabled { get; set; } = false;
    public int StageMonitorIndex { get; set; } = -1; // -1 = Windowed Preview
}

public interface IProjectionService
{
    ProjectionState State { get; }
    event Action<ProjectionState>? StateChanged;
    event Action<TimeSpan, TimeSpan>? VideoProgressChanged;

    void SetLiveSlide(Slide? slide, string? background = null, Slide? nextSlide = null, string itemTitle = "");
    void SetLiveMedia(string mediaPath, bool playImmediately = true);
    void SetLivePresentation(PresentationDocument doc, int slideIndex);
    void NavigatePresentationSlide(int slideIndex);
    void CloseLivePresentation();
    void PlayVideo();
    void PauseVideo();
    void SeekVideo(double percent);
    void SetLooping(bool isLooping);
    void ToggleLive(bool? forceState = null);
    void ToggleBlack();
    void ToggleClear();
    void ToggleLogo();
    void SetBackground(string background);

    void ShowProjectionWindow();
    void HideProjectionWindow();
    bool IsProjectorConnected { get; }
    int DisplayCount { get; }

    // Stage Display (Confidence Monitor)
    void ShowStageDisplayWindow(int monitorIndex = -1);
    void HideStageDisplayWindow();
    void ToggleStageDisplayWindow();
    bool IsStageDisplayVisible { get; }
    void SetStageMessage(string message);
    void ClearStageMessage();

    int SelectedMonitorIndex { get; set; }
    int MarginTop { get; set; }
    int MarginBottom { get; set; }
    int MarginLeft { get; set; }
    int MarginRight { get; set; }
    void ApplyMonitorSettings(int monitorIndex, int marginTop, int marginBottom, int marginLeft, int marginRight);
    void UpdateSettings(AppSettings settings);
}
