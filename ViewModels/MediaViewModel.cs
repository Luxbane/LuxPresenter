using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;
using Microsoft.Win32;

namespace LuxPresenter.ViewModels;

public class MediaViewModel : ViewModelBase
{
    private readonly IDataService _dataService;
    private readonly List<MediaItem> _allMediaItems = new();
    private MediaItem? _selectedMedia;
    private int _selectedCategoryIndex = 0; // 0: Videos, 1: Images, 2: Audio
    private bool _isGridView = true;

    public ObservableCollection<MediaItem> MediaItems { get; } = new();

    public MediaItem? SelectedMedia
    {
        get => _selectedMedia;
        set
        {
            _selectedMedia = value;
            OnPropertyChanged();
            if (value != null)
            {
                MediaSelectedForPreview?.Invoke(value);
            }
        }
    }

    public int SelectedCategoryIndex
    {
        get => _selectedCategoryIndex;
        set
        {
            if (SetProperty(ref _selectedCategoryIndex, value))
            {
                ApplyCategoryFilter();
            }
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            if (SetProperty(ref _isGridView, value))
            {
                OnPropertyChanged(nameof(IsListView));
                try
                {
                    var settings = _dataService.GetSettings();
                    settings.IsMediaGridView = value;
                    _dataService.SaveSettings(settings);
                }
                catch { }
            }
        }
    }

    public bool IsListView => !_isGridView;

    public void UserSelectMedia(MediaItem media)
    {
        _selectedMedia = media;
        OnPropertyChanged(nameof(SelectedMedia));
        MediaSelectedForPreview?.Invoke(media);
    }

    public event Action<MediaItem>? MediaSelectedForPreview;
    public event Action<MediaItem>? MediaDoubleClicked;

    public ICommand SelectBackgroundCommand { get; }
    public ICommand AddMediaCommand { get; }
    public ICommand DeleteMediaCommand { get; }
    public ICommand DoubleClickCommand { get; }

    public MediaViewModel(IDataService dataService)
    {
        _dataService = dataService;

        try
        {
            _isGridView = _dataService.GetSettings().IsMediaGridView;
        }
        catch
        {
            _isGridView = true;
        }

        SelectBackgroundCommand = new RelayCommand(p =>
        {
            if (p is MediaItem m)
            {
                MediaSelectedForPreview?.Invoke(m);
            }
            else if (SelectedMedia != null)
            {
                MediaSelectedForPreview?.Invoke(SelectedMedia);
            }
        });

        DoubleClickCommand = new RelayCommand(p =>
        {
            if (p is MediaItem m)
            {
                MediaDoubleClicked?.Invoke(m);
            }
            else if (SelectedMedia != null)
            {
                MediaDoubleClicked?.Invoke(SelectedMedia);
            }
        });

        AddMediaCommand = new RelayCommand(() =>
        {
            string title;
            string filter;

            switch (SelectedCategoryIndex)
            {
                case 1: // Images
                    title = "Select Image File";
                    filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files (*.*)|*.*";
                    break;
                case 2: // Audio
                    title = "Select Audio File";
                    filter = "Audio Files (*.mp3;*.wav;*.m4a;*.wma;*.aac;*.flac;*.ogg)|*.mp3;*.wav;*.m4a;*.wma;*.aac;*.flac;*.ogg|All Files (*.*)|*.*";
                    break;
                default: // Videos
                    title = "Select Video File";
                    filter = "Video Files (*.mp4;*.wmv;*.avi;*.mov;*.mkv;*.m4v;*.mpg;*.webm;*.flv;*.ts)|*.mp4;*.wmv;*.avi;*.mov;*.mkv;*.m4v;*.mpg;*.webm;*.flv;*.ts|All Files (*.*)|*.*";
                    break;
            }

            var dlg = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            if (dlg.ShowDialog() == true)
            {
                var item = new MediaItem
                {
                    Name = Path.GetFileNameWithoutExtension(dlg.FileName),
                    FilePath = dlg.FileName,
                    Type = DetectMediaType(dlg.FileName)
                };

                _dataService.AddMediaItem(item);
                ReloadMedia();
                SelectedMedia = MediaItems.FirstOrDefault(m => m.Id == item.Id) ?? MediaItems.FirstOrDefault();
            }
        });

        DeleteMediaCommand = new RelayCommand(() =>
        {
            if (SelectedMedia != null)
            {
                _dataService.DeleteMediaItem(SelectedMedia.Id);
                ReloadMedia();
            }
        }, () => SelectedMedia != null);

        ReloadMedia();
    }

    public static MediaType DetectMediaType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".mp4" or ".avi" or ".wmv" or ".mov" or ".mkv" or ".m4v" or ".mpg" or ".mpeg" or ".webm" or ".flv" or ".ts")
            return MediaType.Video;
        if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp")
            return MediaType.Image;
        if (ext is ".mp3" or ".wav" or ".m4a" or ".wma" or ".aac" or ".flac" or ".ogg")
            return MediaType.Audio;
        return MediaType.Video;
    }

    public void ReloadMedia()
    {
        var currentId = SelectedMedia?.Id;
        _allMediaItems.Clear();
        foreach (var m in _dataService.GetMediaItems())
        {
            _allMediaItems.Add(m);
        }

        ApplyCategoryFilter(currentId);
    }

    private void ApplyCategoryFilter(string? restoreId = null)
    {
        var currentId = restoreId ?? SelectedMedia?.Id;
        MediaItems.Clear();

        IEnumerable<MediaItem> filtered = SelectedCategoryIndex switch
        {
            1 => _allMediaItems.Where(m => m.Type == MediaType.Image),
            2 => _allMediaItems.Where(m => m.Type == MediaType.Audio),
            _ => _allMediaItems.Where(m => m.Type == MediaType.Video || (m.Type != MediaType.Image && m.Type != MediaType.Audio))
        };

        foreach (var item in filtered)
        {
            MediaItems.Add(item);
        }

        // Restore selection or select first
        if (currentId != null)
        {
            SelectedMedia = MediaItems.FirstOrDefault(m => m.Id == currentId);
        }
        else if (MediaItems.Count > 0)
        {
            SelectedMedia = MediaItems[0];
        }
        else
        {
            SelectedMedia = null;
        }
    }
}
