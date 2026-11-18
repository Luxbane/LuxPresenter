using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.ViewModels;

public class ScheduleViewModel : ViewModelBase
{
    private readonly IDataService _dataService;
    private ScheduleItem? _selectedItem;
    private bool _isActive = false;
    private bool _hasUnsavedChanges = false;
    private string? _currentScheduleFilePath;
    private bool _isBatchUpdating = false;

    public ObservableCollection<ScheduleItem> Items { get; } = new();

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public string? CurrentScheduleFilePath
    {
        get => _currentScheduleFilePath;
        set => SetProperty(ref _currentScheduleFilePath, value);
    }

    public ScheduleItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value != null)
            {
                ItemSelectedForPreview?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Explicitly called on user click/interaction to ensure preview activates even if already selected
    /// </summary>
    public void UserSelectItem(ScheduleItem item)
    {
        _selectedItem = item;
        OnPropertyChanged(nameof(SelectedItem));
        ItemSelectedForPreview?.Invoke(item);
    }

    public event Action<ScheduleItem>? ItemDoubleClicked;
    public event Action<ScheduleItem>? ItemSelectedForPreview;
    public event Action? AddItemRequested;

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ClearScheduleCommand { get; }
    public ICommand DoubleClickCommand { get; }

    public ScheduleViewModel(IDataService dataService)
    {
        _dataService = dataService;

        Items.CollectionChanged += (s, e) =>
        {
            if (!_isBatchUpdating)
            {
                HasUnsavedChanges = true;
            }
        };

        AddItemCommand = new RelayCommand(() =>
        {
            AddItemRequested?.Invoke();
        });

        RemoveItemCommand = new RelayCommand(() =>
        {
            if (SelectedItem != null)
            {
                int idx = Items.IndexOf(SelectedItem);
                Items.Remove(SelectedItem);
                if (Items.Count > 0)
                {
                    SelectedItem = Items[Math.Clamp(idx, 0, Items.Count - 1)];
                }
            }
        }, () => SelectedItem != null);

        MoveUpCommand = new RelayCommand(() =>
        {
            if (SelectedItem == null) return;
            int idx = Items.IndexOf(SelectedItem);
            if (idx > 0)
            {
                var item = SelectedItem;
                Items.Move(idx, idx - 1);
                SelectedItem = item;
            }
        });

        MoveDownCommand = new RelayCommand(() =>
        {
            if (SelectedItem == null) return;
            int idx = Items.IndexOf(SelectedItem);
            if (idx >= 0 && idx < Items.Count - 1)
            {
                var item = SelectedItem;
                Items.Move(idx, idx + 1);
                SelectedItem = item;
            }
        });

        ClearScheduleCommand = new RelayCommand(() =>
        {
            ClearSchedule();
        });

        DoubleClickCommand = new RelayCommand(p =>
        {
            if (p is ScheduleItem item)
            {
                ItemDoubleClicked?.Invoke(item);
            }
            else if (SelectedItem != null)
            {
                ItemDoubleClicked?.Invoke(SelectedItem);
            }
        });
    }

    public void ClearSchedule()
    {
        _isBatchUpdating = true;
        try
        {
            Items.Clear();
            SelectedItem = null;
            CurrentScheduleFilePath = null;
            HasUnsavedChanges = false;
        }
        finally
        {
            _isBatchUpdating = false;
        }
    }

    public void AddToSchedule(PresentationItem presentation)
    {
        var scheduleItem = ScheduleItem.FromPresentation(presentation);
        Items.Add(scheduleItem);
        SelectedItem = scheduleItem;
        ItemSelectedForPreview?.Invoke(scheduleItem);
    }

    public void SaveToFile(string filePath)
    {
        var fileData = new ScheduleFileData
        {
            Version = "1.0",
            SavedAt = DateTime.Now,
            Items = Items.Select(item => new ScheduleItemData
            {
                Id = item.Id,
                Title = item.Title,
                TypeLabel = item.TypeLabel,
                Category = item.Category,
                Presentation = new PresentationData
                {
                    Id = item.Presentation.Id,
                    Title = item.Presentation.Title,
                    Subtitle = item.Presentation.Subtitle,
                    Category = item.Presentation.Category,
                    BackgroundPath = item.Presentation.BackgroundPath,
                    Slides = item.Presentation.Slides.Select(s => new SlideData
                    {
                        Id = s.Id,
                        Label = s.Label,
                        Content = s.Content,
                        Title = s.Title,
                        Subtitle = s.Subtitle,
                        CustomBackgroundPath = s.CustomBackgroundPath,
                        Type = s.Type,
                        FontFamily = s.FontFamily,
                        FontSize = s.FontSize,
                        IsBold = s.IsBold,
                        IsItalic = s.IsItalic,
                        IsUnderline = s.IsUnderline,
                        HasShadow = s.HasShadow,
                        HasOutline = s.HasOutline,
                        FontColor = s.FontColor
                    }).ToList()
                }
            }).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(fileData, options);
        File.WriteAllText(filePath, json);

        CurrentScheduleFilePath = filePath;
        HasUnsavedChanges = false;
    }

    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        string json = File.ReadAllText(filePath);
        var fileData = JsonSerializer.Deserialize<ScheduleFileData>(json);
        if (fileData == null) return;

        _isBatchUpdating = true;
        try
        {
            Items.Clear();
            foreach (var itemData in fileData.Items)
            {
                var presentation = new PresentationItem
                {
                    Id = itemData.Presentation.Id,
                    Title = itemData.Presentation.Title,
                    Subtitle = itemData.Presentation.Subtitle,
                    Category = itemData.Presentation.Category,
                    BackgroundPath = itemData.Presentation.BackgroundPath
                };

                foreach (var s in itemData.Presentation.Slides)
                {
                    presentation.Slides.Add(new Slide
                    {
                        Id = s.Id,
                        Label = s.Label,
                        Content = s.Content,
                        Title = s.Title,
                        Subtitle = s.Subtitle,
                        CustomBackgroundPath = s.CustomBackgroundPath,
                        Type = s.Type,
                        FontFamily = s.FontFamily,
                        FontSize = s.FontSize,
                        IsBold = s.IsBold,
                        IsItalic = s.IsItalic,
                        IsUnderline = s.IsUnderline,
                        HasShadow = s.HasShadow,
                        HasOutline = s.HasOutline,
                        FontColor = s.FontColor
                    });
                }

                var scheduleItem = new ScheduleItem
                {
                    Id = itemData.Id,
                    Title = itemData.Title,
                    TypeLabel = itemData.TypeLabel,
                    Category = itemData.Category,
                    Presentation = presentation
                };

                Items.Add(scheduleItem);
            }

            if (Items.Count > 0)
            {
                UserSelectItem(Items[0]);
            }
            else
            {
                SelectedItem = null;
            }

            CurrentScheduleFilePath = filePath;
            HasUnsavedChanges = false;
        }
        finally
        {
            _isBatchUpdating = false;
        }
    }
}
