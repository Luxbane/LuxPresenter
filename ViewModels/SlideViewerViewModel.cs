using System.Collections.ObjectModel;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services.Presentation;

namespace LuxPresenter.ViewModels;

public class SlideViewerViewModel : ViewModelBase
{
    private string _paneTitle = "Slide";
    private string _itemTitle = string.Empty;
    private string _itemSubtitle = string.Empty;
    private Slide? _currentSlide;
    private int _selectedIndex = -1;
    private string _background = "#001833";
    private bool _isLiveMode = false;
    private bool _isActivePane = false;
    private bool _isBlack = false;
    private bool _isClear = false;

    // Media Player Mode (Image 2)
    private bool _isMediaMode = false;
    private string? _mediaFilePath;
    private bool _isPlaying = false;
    private bool _isLooping = false;
    private double _volume = 1.0;
    private double _currentPosition = 0;
    private string _timeDisplayText = "0:00 / 0:00";

    // Presentation Document Mode
    private bool _isPresentationDocMode = false;
    private PresentationDocument? _currentPresentationDoc;
    private PresentationSlide? _currentPresentationSlide;

    public bool IsActivePane
    {
        get => _isActivePane;
        set => SetProperty(ref _isActivePane, value);
    }

    public string PaneTitle
    {
        get => _paneTitle;
        set => SetProperty(ref _paneTitle, value);
    }

    public string ItemTitle
    {
        get => _itemTitle;
        set => SetProperty(ref _itemTitle, value);
    }

    public string ItemSubtitle
    {
        get => _itemSubtitle;
        set => SetProperty(ref _itemSubtitle, value);
    }

    public Slide? CurrentSlide
    {
        get => _currentSlide;
        set
        {
            if (SetProperty(ref _currentSlide, value))
            {
                OnPropertyChanged(nameof(NextSlide));
            }
        }
    }

    public Slide? NextSlide
    {
        get
        {
            int nextIdx = SelectedIndex + 1;
            if (nextIdx >= 0 && nextIdx < Slides.Count)
            {
                return Slides[nextIdx];
            }
            return null;
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                if (value >= 0 && value < Slides.Count)
                {
                    CurrentSlide = Slides[value];
                }
                else
                {
                    CurrentSlide = null;
                }
                OnPropertyChanged(nameof(NextSlide));
                SlideChanged?.Invoke(CurrentSlide);
                if (value >= 0)
                {
                    RequestScrollIntoView();
                }
            }
        }
    }

    public string Background
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }

    public bool IsLiveMode
    {
        get => _isLiveMode;
        set => SetProperty(ref _isLiveMode, value);
    }

    public bool IsBlack
    {
        get => _isBlack;
        set => SetProperty(ref _isBlack, value);
    }

    public bool IsClear
    {
        get => _isClear;
        set => SetProperty(ref _isClear, value);
    }

    public bool IsMediaMode
    {
        get => _isMediaMode;
        set => SetProperty(ref _isMediaMode, value);
    }

    public string? MediaFilePath
    {
        get => _mediaFilePath;
        set => SetProperty(ref _mediaFilePath, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                PlaybackStateChanged?.Invoke(_isPlaying);
            }
        }
    }

    public event Action<bool>? LoopingChanged;
    public event Action<double>? SeekRequested;

    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (SetProperty(ref _isLooping, value))
            {
                LoopingChanged?.Invoke(_isLooping);
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    public double CurrentPosition
    {
        get => _currentPosition;
        set => SetProperty(ref _currentPosition, value);
    }

    public bool IsDraggingSlider { get; set; } = false;

    public string TimeDisplayText
    {
        get => _timeDisplayText;
        set => SetProperty(ref _timeDisplayText, value);
    }

    public ObservableCollection<Slide> Slides { get; } = new();

    public event Action<Slide?>? SlideChanged;
    public event Action<Slide, int>? SlideDoubleClicked;
    public event Action<bool>? PlaybackStateChanged;
    public event Action<int>? ScrollRequested;

    public void RequestScrollIntoView()
    {
        if (SelectedIndex >= 0)
        {
            ScrollRequested?.Invoke(SelectedIndex);
        }
    }

    public void SelectFirstSlide()
    {
        if (IsPresentationDocMode && CurrentPresentationDoc != null && CurrentPresentationDoc.Slides.Count > 0)
        {
            LoadPresentationSlide(CurrentPresentationDoc.Slides[0], CurrentPresentationDoc);
            PresentationSlideChanged?.Invoke(CurrentPresentationSlide);
            if (IsLiveMode)
            {
                // Native ProjectionWindow slide display
            }
            return;
        }

        if (Slides.Count > 0)
        {
            SelectedIndex = 0;
            RequestScrollIntoView();
        }
    }

    public void SelectLastSlide()
    {
        if (IsPresentationDocMode && CurrentPresentationDoc != null && CurrentPresentationDoc.Slides.Count > 0)
        {
            int lastIdx = CurrentPresentationDoc.Slides.Count - 1;
            LoadPresentationSlide(CurrentPresentationDoc.Slides[lastIdx], CurrentPresentationDoc);
            PresentationSlideChanged?.Invoke(CurrentPresentationSlide);
            if (IsLiveMode)
            {
                // Native ProjectionWindow slide display
            }
            return;
        }

        if (Slides.Count > 0)
        {
            SelectedIndex = Slides.Count - 1;
            RequestScrollIntoView();
        }
    }

    public void RequestGoLiveForSlide(Slide slide, int index)
    {
        SlideDoubleClicked?.Invoke(slide, index);
    }

    public ICommand SelectSlideCommand { get; }
    public ICommand NextSlideCommand { get; }
    public ICommand PrevSlideCommand { get; }
    public ICommand FirstSlideCommand { get; }
    public ICommand LastSlideCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand ToggleLoopCommand { get; }
    public ICommand RewindCommand { get; }
    public ICommand ForwardCommand { get; }

    public SlideViewerViewModel(string paneTitle, bool isLiveMode = false)
    {
        PaneTitle = paneTitle;
        IsLiveMode = isLiveMode;

        SelectSlideCommand = new RelayCommand(p =>
        {
            if (p is Slide slide)
            {
                int idx = Slides.IndexOf(slide);
                if (idx >= 0) SelectedIndex = idx;
            }
            else if (p is int idx && idx >= 0 && idx < Slides.Count)
            {
                SelectedIndex = idx;
            }
        });

        NextSlideCommand = new RelayCommand(async () =>
        {
            if (IsPresentationDocMode && CurrentPresentationDoc != null && CurrentPresentationSlide != null)
            {
                int nextIdx = CurrentPresentationSlide.Index; // Index is 1-based, so this is 0-based next slide
                if (nextIdx < CurrentPresentationDoc.Slides.Count)
                {
                    LoadPresentationSlide(CurrentPresentationDoc.Slides[nextIdx], CurrentPresentationDoc);
                    PresentationSlideChanged?.Invoke(CurrentPresentationSlide);
                    if (IsLiveMode)
                    {
                        // Native ProjectionWindow slide display
                    }
                }
                return;
            }

            if (SelectedIndex < Slides.Count - 1)
            {
                SelectedIndex++;
            }
            else if (SelectedIndex == -1 && Slides.Count > 0)
            {
                SelectedIndex = 0;
            }
        });

        PrevSlideCommand = new RelayCommand(async () =>
        {
            if (IsPresentationDocMode && CurrentPresentationDoc != null && CurrentPresentationSlide != null)
            {
                int prevIdx = CurrentPresentationSlide.Index - 2; // Index is 1-based, so Index - 2 is 0-based prev slide
                if (prevIdx >= 0 && prevIdx < CurrentPresentationDoc.Slides.Count)
                {
                    LoadPresentationSlide(CurrentPresentationDoc.Slides[prevIdx], CurrentPresentationDoc);
                    PresentationSlideChanged?.Invoke(CurrentPresentationSlide);
                    if (IsLiveMode)
                    {
                        // Native ProjectionWindow slide display
                    }
                }
                return;
            }

            if (SelectedIndex > 0)
            {
                SelectedIndex--;
            }
        });

        FirstSlideCommand = new RelayCommand(SelectFirstSlide);
        LastSlideCommand = new RelayCommand(SelectLastSlide);

        PlayPauseCommand = new RelayCommand(() =>
        {
            IsPlaying = !IsPlaying;
        });

        ToggleLoopCommand = new RelayCommand(() =>
        {
            IsLooping = !IsLooping;
        });

        RewindCommand = new RelayCommand(() =>
        {
            CurrentPosition = 0;
            RequestSeek(0);
        });

        ForwardCommand = new RelayCommand(() =>
        {
            CurrentPosition = 100;
            RequestSeek(100);
        });
    }

    public void RequestSeek(double percent)
    {
        SeekRequested?.Invoke(percent);
    }

    public void LoadItem(PresentationItem item, int slideIndex = 0)
    {
        IsMediaMode = false;
        IsPresentationDocMode = false;
        CurrentPresentationDoc = null;
        CurrentPresentationSlide = null;
        IsPlaying = false;
        MediaFilePath = null;
        ItemTitle = item.Title;
        ItemSubtitle = item.Subtitle;

        if (!string.IsNullOrEmpty(item.BackgroundPath))
        {
            if (item.BackgroundPath.StartsWith("#") || System.IO.File.Exists(item.BackgroundPath))
            {
                Background = item.BackgroundPath;
            }
        }
        else if (string.IsNullOrEmpty(Background))
        {
            Background = "#001833";
        }

        // If slides are already identical (same count and contents), just switch SelectedIndex smoothly
        bool isSameSlides = Slides.Count == item.Slides.Count && Slides.Count > 0;
        if (isSameSlides)
        {
            for (int i = 0; i < Slides.Count; i++)
            {
                if (Slides[i].Content != item.Slides[i].Content || Slides[i].Label != item.Slides[i].Label)
                {
                    isSameSlides = false;
                    break;
                }
            }
        }

        if (isSameSlides)
        {
            int targetIdx = Math.Clamp(slideIndex, 0, Slides.Count - 1);
            if (SelectedIndex == targetIdx)
            {
                CurrentSlide = Slides[targetIdx];
                SlideChanged?.Invoke(CurrentSlide);
            }
            else
            {
                SelectedIndex = targetIdx;
            }
            RequestScrollIntoView();
            return;
        }

        Slides.Clear();
        foreach (var s in item.Slides)
        {
            Slides.Add(s);
        }

        if (Slides.Count > 0)
        {
            int targetIdx = Math.Clamp(slideIndex, 0, Slides.Count - 1);
            SelectedIndex = targetIdx;
            CurrentSlide = Slides[targetIdx];
        }
        else
        {
            SelectedIndex = -1;
            CurrentSlide = null;
        }
    }

    public void LoadMedia(MediaItem media)
    {
        IsMediaMode = true;
        IsPresentationDocMode = false;
        CurrentPresentationDoc = null;
        CurrentPresentationSlide = null;
        MediaFilePath = media.FilePath;
        ItemTitle = media.Name;
        ItemSubtitle = "Media";
        IsPlaying = false;
        IsLooping = false;
        CurrentPosition = 0;
        TimeDisplayText = "0:00 / 0:00";
        Slides.Clear();
        SelectedIndex = -1;
        CurrentSlide = null;
    }

    public void LoadEmptyMedia()
    {
        IsMediaMode = true;
        IsPresentationDocMode = false;
        CurrentPresentationDoc = null;
        CurrentPresentationSlide = null;
        MediaFilePath = null;
        ItemTitle = "Media";
        ItemSubtitle = string.Empty;
        IsPlaying = false;
        IsLooping = false;
        CurrentPosition = 0;
        TimeDisplayText = "0:00 / 0:00";
        Slides.Clear();
        SelectedIndex = -1;
        CurrentSlide = null;
    }

    public void LoadEmptyPresentation()
    {
        IsMediaMode = false;
        IsPresentationDocMode = true;
        CurrentPresentationDoc = null;
        CurrentPresentationSlide = null;
        MediaFilePath = null;
        ItemTitle = "Presentation";
        ItemSubtitle = "No document selected";
        IsPlaying = false;
        IsLooping = false;
        CurrentPosition = 0;
        Slides.Clear();
        SelectedIndex = -1;
        CurrentSlide = null;
    }

    public bool IsPresentationDocMode
    {
        get => _isPresentationDocMode;
        set => SetProperty(ref _isPresentationDocMode, value);
    }

    public PresentationDocument? CurrentPresentationDoc
    {
        get => _currentPresentationDoc;
        set => SetProperty(ref _currentPresentationDoc, value);
    }

    public PresentationSlide? CurrentPresentationSlide
    {
        get => _currentPresentationSlide;
        set
        {
            if (SetProperty(ref _currentPresentationSlide, value))
            {
                PresentationSlideChanged?.Invoke(value);
            }
        }
    }

    public event Action<PresentationSlide?>? PresentationSlideChanged;

    public void LoadPresentationSlide(PresentationSlide slide, PresentationDocument doc)
    {
        IsMediaMode = false;
        IsPresentationDocMode = true;
        CurrentPresentationDoc = doc;
        CurrentPresentationSlide = slide;
        ItemTitle = doc.Title;
        ItemSubtitle = $"Slide {slide.Index} of {doc.Slides.Count}";
        Slides.Clear();
        SelectedIndex = -1;
        CurrentSlide = null;
    }

    public void Clear()
    {
        IsMediaMode = false;
        IsPresentationDocMode = false;
        CurrentPresentationDoc = null;
        CurrentPresentationSlide = null;
        IsPlaying = false;
        MediaFilePath = null;
        Slides.Clear();
        ItemTitle = string.Empty;
        ItemSubtitle = string.Empty;
        SelectedIndex = -1;
        CurrentSlide = null;
    }
}
