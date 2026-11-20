using System.Windows.Input;

using LuxPresenter.Models;

using LuxPresenter.Services;



namespace LuxPresenter.ViewModels;



public class MainViewModel : ViewModelBase

{

    private readonly IDataService _dataService;

    private readonly IProjectionService _projectionService;

    private AppSettings _settings;



    private bool _isBlackActive = false;

    private bool _isClearActive = false;

    private bool _isLogoActive = false;

    private bool _isLiveOutputActive = true;

    private string _statusText = "Ready";

    private string _displayStatusText = "1 Screen Detected";



    public ScheduleViewModel ScheduleVM { get; }

    public SongsViewModel SongsVM { get; }

    public ScripturesViewModel ScripturesVM { get; }

    public MediaViewModel MediaVM { get; }

    public PresentationViewModel PresentationVM { get; }

    public SlideViewerViewModel PreviewVM { get; }

    public SlideViewerViewModel LiveVM { get; }



    public string ActiveGlobalBackground { get; set; } = "#001833";



    public bool IsBlackActive

    {

        get => _isBlackActive;

        set => SetProperty(ref _isBlackActive, value);

    }



    public bool IsClearActive

    {

        get => _isClearActive;

        set => SetProperty(ref _isClearActive, value);

    }



    public bool IsLogoActive

    {

        get => _isLogoActive;

        set => SetProperty(ref _isLogoActive, value);

    }



    public bool IsLiveOutputActive

    {

        get => _isLiveOutputActive;

        set

        {

            if (SetProperty(ref _isLiveOutputActive, value))

            {

                _projectionService.ToggleLive(value);

                if (value)

                {

                    _projectionService.ShowProjectionWindow();

                }

                else

                {

                    _projectionService.HideProjectionWindow();

                }

            }

        }

    }



    public string StatusText

    {

        get => _statusText;

        set => SetProperty(ref _statusText, value);

    }



    public string DisplayStatusText

    {

        get => _displayStatusText;

        set => SetProperty(ref _displayStatusText, value);

    }



    private bool _isOnlineMode = false;

    public bool IsOnlineMode

    {

        get => _isOnlineMode;

        set => SetProperty(ref _isOnlineMode, value);

    }



    public enum ActivePreviewSource

    {

        None,

        Schedule,

        SongList,

        Scripture,

        Media,

        Presentation

    }



    public ActivePreviewSource CurrentPreviewSource { get; private set; } = ActivePreviewSource.None;

    public ScheduleItem? ActiveScheduleItem { get; private set; }

    public Song? ActiveSongItem { get; private set; }

    public BibleVerse? ActiveScriptureVerse { get; private set; }

    public MediaItem? ActiveMediaItem { get; private set; }



    public ICommand GoLiveCommand { get; }

    public ICommand BlackCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand LogoCommand { get; }

    public ICommand LiveToggleCommand { get; }

    public ICommand OpenProjectorWindowCommand { get; }

    public ICommand EditActiveSongCommand { get; }



    public event Action<Song?>? OpenSongEditorRequested;

    public event Action? OpenOptionsRequested;



    public MainViewModel(IDataService dataService, IProjectionService projectionService)

    {

        _dataService = dataService;

        _projectionService = projectionService;



        ScheduleVM = new ScheduleViewModel(_dataService);

        SongsVM = new SongsViewModel(_dataService);

        ScripturesVM = new ScripturesViewModel(_dataService);

        MediaVM = new MediaViewModel(_dataService);

        PresentationVM = new PresentationViewModel(_dataService);



        PreviewVM = new SlideViewerViewModel("Preview", isLiveMode: false);

        LiveVM = new SlideViewerViewModel("Live", isLiveMode: true);



        // Load saved background or fallback

        _settings = _dataService.GetSettings();

        if (!string.IsNullOrWhiteSpace(_settings.LastUsedBackground) &&

            (_settings.LastUsedBackground.StartsWith("#") || System.IO.File.Exists(_settings.LastUsedBackground)))

        {

            ActiveGlobalBackground = _settings.LastUsedBackground;

        }

        else

        {

            ActiveGlobalBackground = "#001833";

        }

        PreviewVM.Background = ActiveGlobalBackground;

        LiveVM.Background = ActiveGlobalBackground;

        _projectionService.SetBackground(ActiveGlobalBackground);



        // 1. Instant Zero-Latency "Go Live"

        GoLiveCommand = new RelayCommand(() =>

        {

            if (!IsLiveOutputActive)

            {

                IsLiveOutputActive = true;

            }



            // Automatically stop/pause Preview playback so Preview and Live audio never clash

            if (PreviewVM.IsPlaying)

            {

                PreviewVM.IsPlaying = false;

            }



            // If in Media Player mode (Video)

            if (PreviewVM.IsMediaMode)

            {

                var media = new MediaItem

                {

                    Name = PreviewVM.ItemTitle,

                    FilePath = PreviewVM.MediaFilePath ?? string.Empty

                };



                LiveVM.LoadMedia(media);

                LiveVM.IsLooping = PreviewVM.IsLooping;

                LiveVM.IsPlaying = true;

                PresentationVM.ClearLiveMarks();

                _projectionService.SetLooping(LiveVM.IsLooping);

                _projectionService.SetLiveMedia(media.FilePath, playImmediately: true);

                _projectionService.ShowProjectionWindow();

                StatusText = $"Live Media: {LiveVM.ItemTitle}";

                return;

            }



            // If in Presentation Document mode (PPTX/ODP/PDF)

            if (PreviewVM.IsPresentationDocMode && PreviewVM.CurrentPresentationSlide != null && PreviewVM.CurrentPresentationDoc != null)

            {

                LiveVM.LoadPresentationSlide(PreviewVM.CurrentPresentationSlide, PreviewVM.CurrentPresentationDoc);

                LiveVM.IsActivePane = true;

                PreviewVM.IsActivePane = false;

                _projectionService.SetLivePresentation(PreviewVM.CurrentPresentationDoc, PreviewVM.CurrentPresentationSlide.Index - 1);

                _projectionService.ShowProjectionWindow();

                PresentationVM.MarkLiveSlide(PreviewVM.CurrentPresentationSlide.Index);

                StatusText = $"Live Presentation: {PreviewVM.CurrentPresentationDoc.Title} - Slide {PreviewVM.CurrentPresentationSlide.Index}";

                return;

            }



            if (PreviewVM.Slides.Count == 0) return;



            var bg = !string.IsNullOrWhiteSpace(PreviewVM.Background) ? PreviewVM.Background : ActiveGlobalBackground;



            var presentation = new PresentationItem

            {

                Title = PreviewVM.ItemTitle,

                Subtitle = PreviewVM.ItemSubtitle,

                BackgroundPath = bg

            };



            foreach (var s in PreviewVM.Slides)

            {

                presentation.Slides.Add(s);

            }



            // Instantly transfer to Live pane

            LiveVM.LoadItem(presentation, PreviewVM.SelectedIndex);

            LiveVM.Background = bg;

            LiveVM.IsActivePane = true;

            PreviewVM.IsActivePane = false;

            PresentationVM.ClearLiveMarks();

            StatusText = $"Live: {LiveVM.ItemTitle} - {LiveVM.CurrentSlide?.Label}";



            // Sync with projector state first so projection window receives new slide immediately

            _projectionService.SetLiveSlide(LiveVM.CurrentSlide, LiveVM.Background, LiveVM.NextSlide, LiveVM.ItemTitle);

            _projectionService.ShowProjectionWindow();

        });



        // 2. Toolbar Quick Overlays

        BlackCommand = new RelayCommand(() =>

        {

            _projectionService.ToggleBlack();

            IsBlackActive = _projectionService.State.IsBlack;

            LiveVM.IsBlack = IsBlackActive;

        });



        ClearCommand = new RelayCommand(() =>

        {

            _projectionService.ToggleClear();

            IsClearActive = _projectionService.State.IsClear;

            LiveVM.IsClear = IsClearActive;

        });



        LogoCommand = new RelayCommand(() =>

        {

            _projectionService.ToggleLogo();

            IsLogoActive = _projectionService.State.IsLogo;

        });



        LiveToggleCommand = new RelayCommand(() =>

        {

            IsLiveOutputActive = !IsLiveOutputActive;

        });



        OpenProjectorWindowCommand = new RelayCommand(() =>

        {

            OpenOptionsRequested?.Invoke();

            UpdateDisplayStatus();

        });



        // Edit whichever song is currently loaded in Preview (meja kerja kiri)

        EditActiveSongCommand = new RelayCommand(() =>

        {

            var title = PreviewVM.ItemTitle?.Trim();

            if (string.IsNullOrWhiteSpace(title))

            {

                if (SongsVM.SelectedSong != null)

                {

                    OpenSongEditorRequested?.Invoke(SongsVM.SelectedSong);

                }

                return;

            }



            var songs = _dataService.GetSongs();

            var song = songs.FirstOrDefault(s => s.Title.Equals(title, StringComparison.OrdinalIgnoreCase))

                    ?? songs.FirstOrDefault(s => s.Title.StartsWith(title, StringComparison.OrdinalIgnoreCase))

                    ?? songs.FirstOrDefault(s => title.StartsWith(s.Title, StringComparison.OrdinalIgnoreCase));



            if (song == null && PreviewVM.Slides.Count > 0)

            {

                var lyrics = string.Join("\n\n", PreviewVM.Slides.Select(s =>

                    (!string.IsNullOrWhiteSpace(s.Label) ? $"[{s.Label}]\n" : "") + s.Content));



                song = new Song

                {

                    Title = title,

                    Author = PreviewVM.ItemSubtitle?.Replace("By ", "").Trim() ?? string.Empty,

                    LyricsRaw = lyrics,

                    DefaultBackground = PreviewVM.Background

                };

            }



            if (song != null)

            {

                OpenSongEditorRequested?.Invoke(song);

            }

        });



        // 3. Live slide clicking / arrow key navigation

        LiveVM.SlideChanged += slide =>

        {

            if (slide != null)

            {

                _projectionService.SetLiveSlide(slide, LiveVM.Background, LiveVM.NextSlide, LiveVM.ItemTitle);

                StatusText = $"Live: {LiveVM.ItemTitle} - {slide.Label}";

            }

        };



        // Double-clicking a stanza in Preview pane -> immediately Go Live with this stanza!

        PreviewVM.SlideDoubleClicked += (slide, index) =>

        {

            GoLiveCommand.Execute(null);

        };



        // Double-clicking a stanza in Live pane -> sync to projector

        LiveVM.SlideDoubleClicked += (slide, index) =>

        {

            _projectionService.SetLiveSlide(slide, LiveVM.Background, LiveVM.NextSlide, LiveVM.ItemTitle);

            StatusText = $"Live: {LiveVM.ItemTitle} - {slide.Label}";

        };



        LiveVM.PlaybackStateChanged += isPlaying =>

        {

            if (isPlaying)

            {

                _projectionService.PlayVideo();

            }

            else

            {

                _projectionService.PauseVideo();

            }

        };



        LiveVM.LoopingChanged += isLooping =>

        {

            _projectionService.SetLooping(isLooping);

        };



        LiveVM.SeekRequested += percent =>

        {

            _projectionService.SeekVideo(percent);

        };



        _projectionService.VideoProgressChanged += (pos, dur) =>

        {

            if (LiveVM.IsMediaMode && !LiveVM.IsDraggingSlider && dur.TotalSeconds > 0)

            {

                LiveVM.CurrentPosition = (pos.TotalSeconds / dur.TotalSeconds) * 100.0;

                LiveVM.TimeDisplayText = $"{pos.Minutes}:{pos.Seconds:D2} / {dur.Minutes}:{dur.Seconds:D2}";

            }

        };



        // 4. Hook Schedule events

        ScheduleVM.AddItemRequested += () =>

        {

            if (PreviewVM.Slides.Count > 0)

            {

                var pres = new PresentationItem

                {

                    Title = PreviewVM.ItemTitle,

                    Subtitle = PreviewVM.ItemSubtitle,

                    BackgroundPath = null // Separate state: do not snapshot active global background

                };

                foreach (var s in PreviewVM.Slides)

                {

                    pres.Slides.Add(s);

                }

                ScheduleVM.AddToSchedule(pres);

                StatusText = $"Added to Schedule: {pres.Title}";

            }

            else if (SongsVM.SelectedSong != null)

            {

                var pres = SongsVM.SelectedSong.ToPresentationItem(null, _settings);

                ScheduleVM.AddToSchedule(pres);

                StatusText = $"Added to Schedule: {pres.Title}";

            }

        };



        ScheduleVM.ItemSelectedForPreview += item =>

        {

            CurrentPreviewSource = ActivePreviewSource.Schedule;

            ScheduleVM.IsActive = true;

            SongsVM.IsActive = false;

            ActiveScheduleItem = item;



            // Background remains current active global background unless the item itself is an explicit Media item

            var bg = ActiveGlobalBackground;

            if (item.Category == ItemCategory.Media &&

                !string.IsNullOrWhiteSpace(item.Presentation.BackgroundPath) &&

                !item.Presentation.BackgroundPath.StartsWith("#") &&

                System.IO.File.Exists(item.Presentation.BackgroundPath))

            {

                bg = item.Presentation.BackgroundPath;

            }



            PreviewVM.LoadItem(item.Presentation);

            PreviewVM.Background = bg;

            StatusText = $"Preview: {item.Title}";

        };



        ScheduleVM.ItemDoubleClicked += item =>

        {

            CurrentPreviewSource = ActivePreviewSource.Schedule;

            ScheduleVM.IsActive = true;

            SongsVM.IsActive = false;

            ActiveScheduleItem = item;



            var bg = ActiveGlobalBackground;

            if (item.Category == ItemCategory.Media &&

                !string.IsNullOrWhiteSpace(item.Presentation.BackgroundPath) &&

                !item.Presentation.BackgroundPath.StartsWith("#") &&

                System.IO.File.Exists(item.Presentation.BackgroundPath))

            {

                bg = item.Presentation.BackgroundPath;

            }



            PreviewVM.LoadItem(item.Presentation);

            PreviewVM.Background = bg;

            GoLiveCommand.Execute(null);

        };



        // 5. Hook Songs events

        SongsVM.SongSelectedForPreview += song =>

        {

            CurrentPreviewSource = ActivePreviewSource.SongList;

            SongsVM.IsActive = true;

            ScheduleVM.IsActive = false;

            ActiveSongItem = song;

            ActiveScheduleItem = null;



            var pres = song.ToPresentationItem(null, _settings);

            int slideIdx = FindMatchingSlideIndex(pres.Slides, SongsVM.ActiveSearchQuery);

            PreviewVM.LoadItem(pres, slideIdx);

            var bg = !string.IsNullOrWhiteSpace(pres.BackgroundPath) && System.IO.File.Exists(pres.BackgroundPath) ? pres.BackgroundPath : ActiveGlobalBackground;

            PreviewVM.Background = bg;

            StatusText = $"Preview: {song.Title}";

        };



        SongsVM.SongDoubleClicked += song =>

        {

            CurrentPreviewSource = ActivePreviewSource.SongList;

            SongsVM.IsActive = true;

            ScheduleVM.IsActive = false;

            ActiveSongItem = song;

            ActiveScheduleItem = null;



            var pres = song.ToPresentationItem(null, _settings);

            int slideIdx = FindMatchingSlideIndex(pres.Slides, SongsVM.ActiveSearchQuery);

            PreviewVM.LoadItem(pres, slideIdx);

            var bg = !string.IsNullOrWhiteSpace(pres.BackgroundPath) && System.IO.File.Exists(pres.BackgroundPath) ? pres.BackgroundPath : ActiveGlobalBackground;

            PreviewVM.Background = bg;

            GoLiveCommand.Execute(null);

        };



        SongsVM.AddToScheduleRequested += song =>

        {

            ScheduleVM.AddToSchedule(song.ToPresentationItem(null, _settings));

        };



        SongsVM.EditSongRequested += song =>

        {

            OpenSongEditorRequested?.Invoke(song);

        };



        // 6. Hook Scriptures events

        ScripturesVM.VerseSelectedForPreview += verse =>

        {

            CurrentPreviewSource = ActivePreviewSource.Scripture;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveScriptureVerse = verse;

            ActiveScheduleItem = null;



            var pres = new PresentationItem

            {

                Title = verse.Reference,

                Subtitle = verse.Version,

                Category = ItemCategory.Scripture

            };

            pres.Slides.Add(verse.ToSlide(_settings));

            PreviewVM.LoadItem(pres);

            PreviewVM.Background = ActiveGlobalBackground;

            StatusText = $"Preview: {verse.Reference} ({verse.Version})";

        };



        ScripturesVM.VerseDoubleClicked += verse =>

        {

            CurrentPreviewSource = ActivePreviewSource.Scripture;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveScriptureVerse = verse;

            ActiveScheduleItem = null;



            var pres = new PresentationItem

            {

                Title = verse.Reference,

                Subtitle = verse.Version,

                Category = ItemCategory.Scripture

            };

            pres.Slides.Add(verse.ToSlide(_settings));

            PreviewVM.LoadItem(pres);

            PreviewVM.Background = ActiveGlobalBackground;

            GoLiveCommand.Execute(null);

        };



        ScripturesVM.AddToScheduleRequested += verse =>

        {

            var pres = new PresentationItem

            {

                Title = verse.Reference,

                Subtitle = verse.Version,

                Category = ItemCategory.Scripture

            };

            pres.Slides.Add(verse.ToSlide(_settings));

            ScheduleVM.AddToSchedule(pres);

        };



        // 7. Hook Media video/playback events (Matching Image 2: standalone video play)

        MediaVM.MediaSelectedForPreview += media =>

        {

            CurrentPreviewSource = ActivePreviewSource.Media;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveMediaItem = media;



            PreviewVM.LoadMedia(media);

            PreviewVM.IsPlaying = false; // Do NOT autoplay on select

            PreviewVM.IsLooping = false; // Loop off by default

            StatusText = $"Preview Media: {media.Name}";

        };



        MediaVM.MediaDoubleClicked += media =>

        {

            CurrentPreviewSource = ActivePreviewSource.Media;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveMediaItem = media;



            PreviewVM.LoadMedia(media);

            GoLiveCommand.Execute(null);

        };



        // 8. Hook Presentation document events

        PresentationVM.SlideStagedForPreview += (slide, doc) =>

        {

            CurrentPreviewSource = ActivePreviewSource.Presentation;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveMediaItem = null;

            ActiveScriptureVerse = null;



            PreviewVM.LoadPresentationSlide(slide, doc);

            StatusText = $"Preview: {doc.Title} - Slide {slide.Index}";

        };



        PresentationVM.SlideDoubleClickedForLive += (slide, doc) =>

        {

            CurrentPreviewSource = ActivePreviewSource.Presentation;

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            ActiveMediaItem = null;

            ActiveScriptureVerse = null;



            PreviewVM.LoadPresentationSlide(slide, doc);

            GoLiveCommand.Execute(null);

        };



        LiveVM.PresentationSlideChanged += slide =>

        {

            if (slide != null && LiveVM.CurrentPresentationDoc != null)

            {

                _projectionService.NavigatePresentationSlide(slide.Index - 1);

                PresentationVM.MarkLiveSlide(slide.Index);

                StatusText = $"Live Presentation: {LiveVM.CurrentPresentationDoc.Title} - Slide {slide.Index}";

            }

        };



        UpdateDisplayStatus();

        LoadInitialPreview();

    }



    public void SetActiveBackground(string bg, bool updateLive = false)

    {

        if (string.IsNullOrWhiteSpace(bg)) return;

        ActiveGlobalBackground = bg;



        var settings = _dataService.GetSettings();

        settings.LastUsedBackground = bg;

        _dataService.SaveSettings(settings);



        PreviewVM.Background = bg;

        if (PreviewVM.CurrentSlide != null)

        {

            PreviewVM.CurrentSlide.CustomBackgroundPath = bg;

        }

        foreach (var s in PreviewVM.Slides)

        {

            s.CustomBackgroundPath = bg;

        }



        if (updateLive)

        {

            LiveVM.Background = bg;

            if (LiveVM.CurrentSlide != null)

            {

                LiveVM.CurrentSlide.CustomBackgroundPath = bg;

            }

            foreach (var s in LiveVM.Slides)

            {

                s.CustomBackgroundPath = bg;

            }

            if (LiveVM.CurrentSlide != null)

            {

                _projectionService.SetLiveSlide(LiveVM.CurrentSlide, bg, LiveVM.NextSlide, LiveVM.ItemTitle);

            }

            else

            {

                _projectionService.SetBackground(bg);

            }

        }

    }



    private class SavedPreviewPresentation

    {

        public PresentationItem Presentation { get; set; } = new();

        public int SelectedIndex { get; set; } = 0;

        public string Background { get; set; } = string.Empty;

        public ActivePreviewSource Source { get; set; } = ActivePreviewSource.None;

        public ScheduleItem? ScheduleItem { get; set; }

        public Song? SongItem { get; set; }

        public BibleVerse? ScriptureVerse { get; set; }

        public bool IsScheduleActive { get; set; }

        public bool IsSongsActive { get; set; }

    }



    private SavedPreviewPresentation? _savedSongPresentation;

    private int _currentLibraryTabIndex = 0;



    private void SaveCurrentSongPresentationState()

    {

        if (PreviewVM.IsMediaMode) return;

        if (PreviewVM.Slides.Count == 0 && string.IsNullOrWhiteSpace(PreviewVM.ItemTitle)) return;



        var pres = new PresentationItem

        {

            Title = PreviewVM.ItemTitle,

            Subtitle = PreviewVM.ItemSubtitle,

            BackgroundPath = PreviewVM.Background

        };



        foreach (var s in PreviewVM.Slides)

        {

            pres.Slides.Add(s);

        }



        _savedSongPresentation = new SavedPreviewPresentation

        {

            Presentation = pres,

            SelectedIndex = Math.Clamp(PreviewVM.SelectedIndex, 0, Math.Max(0, pres.Slides.Count - 1)),

            Background = !string.IsNullOrWhiteSpace(PreviewVM.Background) ? PreviewVM.Background : ActiveGlobalBackground,

            Source = CurrentPreviewSource,

            ScheduleItem = ActiveScheduleItem,

            SongItem = ActiveSongItem,

            ScriptureVerse = ActiveScriptureVerse,

            IsScheduleActive = ScheduleVM.IsActive,

            IsSongsActive = SongsVM.IsActive

        };

    }



    private void RestoreSavedSongPresentationState()

    {

        // 1. Unload/stop preview video player if active

        if (PreviewVM.IsMediaMode)

        {

            PreviewVM.IsPlaying = false;

            PreviewVM.MediaFilePath = null;

            PreviewVM.IsMediaMode = false;

        }



        // 2. Restore exact saved presentation snapshot

        if (_savedSongPresentation != null)

        {

            var saved = _savedSongPresentation;

            CurrentPreviewSource = saved.Source;

            ActiveScheduleItem = saved.ScheduleItem;

            ActiveSongItem = saved.SongItem;

            ActiveScriptureVerse = saved.ScriptureVerse;

            ScheduleVM.IsActive = saved.IsScheduleActive;

            SongsVM.IsActive = saved.IsSongsActive;



            PreviewVM.LoadItem(saved.Presentation, saved.SelectedIndex);

            PreviewVM.Background = saved.Background;

            StatusText = $"Preview: {saved.Presentation.Title}";

            return;

        }



        // 3. Fallback if no snapshot exists yet

        if (CurrentPreviewSource == ActivePreviewSource.Schedule && ActiveScheduleItem != null)

        {

            ScheduleVM.IsActive = true;

            SongsVM.IsActive = false;

            PreviewVM.LoadItem(ActiveScheduleItem.Presentation);

            PreviewVM.Background = ActiveGlobalBackground;

            StatusText = $"Preview: {ActiveScheduleItem.Title}";

        }

        else if (ActiveSongItem != null)

        {

            CurrentPreviewSource = ActivePreviewSource.SongList;

            SongsVM.IsActive = true;

            ScheduleVM.IsActive = false;

            var pres = ActiveSongItem.ToPresentationItem(null, _settings);

            PreviewVM.LoadItem(pres);

            PreviewVM.Background = ActiveGlobalBackground;

            StatusText = $"Preview: {ActiveSongItem.Title}";

        }

    }



    private int _lastActiveLibraryTabIndex = 0;

    public int LastActiveLibraryTabIndex => _lastActiveLibraryTabIndex;



    public void OnLibraryTabChanged(int tabIndex)

    {

        IsOnlineMode = (tabIndex == 4);

        if (tabIndex != 4)

        {

            _lastActiveLibraryTabIndex = tabIndex;

        }



        // When leaving a presentation tab to enter Media or Online, save the presentation state

        if (_currentLibraryTabIndex != 2 && !PreviewVM.IsMediaMode && !PreviewVM.IsPresentationDocMode)

        {

            SaveCurrentSongPresentationState();

        }



        _currentLibraryTabIndex = tabIndex;



        switch (tabIndex)
        {
            case 0: // Songs tab
                if (PreviewVM.IsPlaying)
                {
                    PreviewVM.IsPlaying = false;
                    PreviewVM.MediaFilePath = null;
                }
                PreviewVM.IsMediaMode = false;
                PreviewVM.IsPresentationDocMode = false;

                if (SongsVM.SelectedSong == null && SongsVM.FilteredSongs.Count > 0)
                {
                    SongsVM.SelectedSong = SongsVM.FilteredSongs.First();
                }
                else if (SongsVM.SelectedSong != null)
                {
                    var pres = SongsVM.SelectedSong.ToPresentationItem(null, _settings);
                    int slideIdx = FindMatchingSlideIndex(pres.Slides, SongsVM.ActiveSearchQuery);
                    PreviewVM.LoadItem(pres, slideIdx);
                    var bg = !string.IsNullOrWhiteSpace(pres.BackgroundPath) && System.IO.File.Exists(pres.BackgroundPath) 
                        ? pres.BackgroundPath 
                        : ActiveGlobalBackground;
                    PreviewVM.Background = bg;
                    StatusText = $"Preview: {SongsVM.SelectedSong.Title}";
                }
                else
                {
                    RestoreSavedSongPresentationState();
                }
                break;

            case 1: // Scriptures tab
                if (PreviewVM.IsPlaying)
                {
                    PreviewVM.IsPlaying = false;
                    PreviewVM.MediaFilePath = null;
                }
                PreviewVM.IsMediaMode = false;
                PreviewVM.IsPresentationDocMode = false;

                var targetVerse = ActiveScriptureVerse ?? ScripturesVM.SelectedVerse;
                if (targetVerse != null)
                {
                    var pres = new PresentationItem
                    {
                        Title = targetVerse.Reference,
                        Subtitle = targetVerse.Version,
                        Category = ItemCategory.Scripture
                    };
                    pres.Slides.Add(targetVerse.ToSlide(_settings));
                    PreviewVM.LoadItem(pres);
                    PreviewVM.Background = ActiveGlobalBackground;
                    StatusText = $"Preview: {targetVerse.Reference}";
                }
                else
                {
                    RestoreSavedSongPresentationState();
                }
                break;

            case 2: // Media tab
                PreviewVM.IsPresentationDocMode = false;
                PreviewVM.IsMediaMode = true;

                // Auto-select first video or media item if none selected
                if (MediaVM.SelectedMedia == null && MediaVM.MediaItems.Count > 0)
                {
                    MediaVM.SelectedMedia = MediaVM.MediaItems.FirstOrDefault(m => m.Type == MediaType.Video) 
                                          ?? MediaVM.MediaItems[0];
                }

                if (MediaVM.SelectedMedia != null && !string.IsNullOrWhiteSpace(MediaVM.SelectedMedia.FilePath) && System.IO.File.Exists(MediaVM.SelectedMedia.FilePath))
                {
                    ActiveMediaItem = MediaVM.SelectedMedia;
                    PreviewVM.LoadMedia(ActiveMediaItem);
                    PreviewVM.IsPlaying = false; // Never autoplay on tab switch
                    StatusText = $"Preview: {ActiveMediaItem.Name}";
                }
                else
                {
                    ActiveMediaItem = null;
                    PreviewVM.LoadEmptyMedia();
                    StatusText = "Preview: Media";
                }
                break;

            case 3: // Presentation tab
                if (PreviewVM.IsPlaying)
                {
                    PreviewVM.IsPlaying = false;
                    PreviewVM.MediaFilePath = null;
                }
                PreviewVM.IsMediaMode = false;
                PreviewVM.IsPresentationDocMode = true;

                // Auto-select first document if none selected
                if (PresentationVM.SelectedDoc == null && PresentationVM.PresentationDocs.Count > 0)
                {
                    PresentationVM.SelectedDoc = PresentationVM.PresentationDocs.First();
                }

                // Auto-select first slide if document is loaded
                if (PresentationVM.SelectedSlide == null && PresentationVM.LoadedDoc != null && PresentationVM.LoadedDoc.Slides.Count > 0)
                {
                    PresentationVM.SelectedSlide = PresentationVM.LoadedDoc.Slides.First();
                }

                if (PresentationVM.SelectedSlide != null && PresentationVM.LoadedDoc != null)
                {
                    PreviewVM.LoadPresentationSlide(PresentationVM.SelectedSlide, PresentationVM.LoadedDoc);
                    StatusText = $"Preview Presentation: {PresentationVM.LoadedDoc.Title} - Slide {PresentationVM.SelectedSlide.Index}";
                }
                else
                {
                    PreviewVM.LoadEmptyPresentation();
                    StatusText = "Preview: Presentation";
                }
                break;

            case 4: // Online tab
                if (PreviewVM.IsPlaying)
                {
                    PreviewVM.IsPlaying = false;
                }
                break;
        }
    }



    public void UpdateSettings(AppSettings settings)

    {

        _settings = settings;

    }



    public void ReloadCurrentPresentation()

    {

        int prevIdx = PreviewVM.SelectedIndex;



        if (CurrentPreviewSource == ActivePreviewSource.Schedule && ActiveScheduleItem != null)

        {

            ScheduleVM.IsActive = true;

            SongsVM.IsActive = false;

            PreviewVM.LoadItem(ActiveScheduleItem.Presentation, prevIdx >= 0 ? prevIdx : 0);

            PreviewVM.Background = ActiveGlobalBackground;

        }

        else if (CurrentPreviewSource == ActivePreviewSource.SongList && ActiveSongItem != null)

        {

            SongsVM.IsActive = true;

            ScheduleVM.IsActive = false;

            var pres = ActiveSongItem.ToPresentationItem(null, _settings);

            PreviewVM.LoadItem(pres, prevIdx >= 0 ? prevIdx : 0);

            PreviewVM.Background = ActiveGlobalBackground;

        }

        else if (CurrentPreviewSource == ActivePreviewSource.Scripture && ActiveScriptureVerse != null)

        {

            ScheduleVM.IsActive = false;

            SongsVM.IsActive = false;

            var pres = new PresentationItem

            {

                Title = ActiveScriptureVerse.Reference,

                Subtitle = ActiveScriptureVerse.Version,

                Category = ItemCategory.Scripture

            };

            pres.Slides.Add(ActiveScriptureVerse.ToSlide(_settings));

            PreviewVM.LoadItem(pres, prevIdx >= 0 ? prevIdx : 0);

            PreviewVM.Background = ActiveGlobalBackground;

        }

    }



    private void UpdateDisplayStatus()

    {

        int count = _projectionService.DisplayCount;

        if (count > 1)

        {

            DisplayStatusText = $"{count} Displays (Projector Output Active on Screen 2)";

        }

        else

        {

            DisplayStatusText = "1 Display (Single Screen Test Mode)";

        }

    }



    private void LoadInitialPreview()

    {

        // Start with the first song or verse loaded in preview like EW 2009 startup (Matching Image 1)

        var songs = _dataService.GetSongs();

        if (songs.Count > 0)

        {

            var first = songs[0];

            ActiveSongItem = first;

            CurrentPreviewSource = ActivePreviewSource.SongList;

            SongsVM.SelectedSong = first;

            SongsVM.IsActive = true;

            ScheduleVM.IsActive = false;



            var pres = first.ToPresentationItem(null, _settings);

            PreviewVM.LoadItem(pres);

            PreviewVM.Background = ActiveGlobalBackground;

            PreviewVM.IsActivePane = true;

            StatusText = $"Preview: {first.Title}";



            // Live pane initially has ONLY background, no slides, no text (Matching Image 1)

            LiveVM.Clear();

            LiveVM.Background = ActiveGlobalBackground;

            LiveVM.ItemTitle = "";

            LiveVM.IsActivePane = false;

            _projectionService.SetBackground(ActiveGlobalBackground);



            SaveCurrentSongPresentationState();

        }

    }


    private static int FindMatchingSlideIndex(IReadOnlyList<Slide> slides, string? rawQuery)
    {
        if (slides == null || slides.Count == 0 || string.IsNullOrWhiteSpace(rawQuery))
            return 0;

        string normQ = SongsViewModel.NormalizeSearchText(rawQuery).Trim();
        if (string.IsNullOrEmpty(normQ)) return 0;

        string compactQ = normQ.Replace(" ", string.Empty);
        var tokens = normQ.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // 1. Direct phrase match in slide content
        for (int i = 0; i < slides.Count; i++)
        {
            string content = SongsViewModel.NormalizeSearchText(slides[i].Content);
            if (content.Contains(normQ, StringComparison.OrdinalIgnoreCase) ||
                content.Replace(" ", string.Empty).Contains(compactQ, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        // 2. Token match: find slide containing the highest number of tokens
        if (tokens.Length > 0)
        {
            int bestIndex = -1;
            int maxMatches = 0;

            for (int i = 0; i < slides.Count; i++)
            {
                string content = SongsViewModel.NormalizeSearchText(slides[i].Content);
                int matches = 0;
                foreach (var token in tokens)
                {
                    if (content.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                }

                if (matches > maxMatches)
                {
                    maxMatches = matches;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0 && maxMatches > 0)
            {
                return bestIndex;
            }
        }

        return 0;
    }
}

