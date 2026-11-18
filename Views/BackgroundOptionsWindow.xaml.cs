using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;
using LuxPresenter.ViewModels;
using Microsoft.Win32;

namespace LuxPresenter.Views;

public partial class BackgroundOptionsWindow : Window
{
    // All supported media file extensions for backgrounds (Images and Motion Videos)
    private const string MediaFilter =
        "All Background Media (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v;*.flv;*.webm;*.ts)|" +
        "*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v;*.flv;*.webm;*.ts|" +
        "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp)|" +
        "*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|" +
        "Video Backgrounds (*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v;*.flv;*.webm;*.ts)|" +
        "*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v;*.flv;*.webm;*.ts|" +
        "All Files (*.*)|*.*";

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".flv", ".m4v",
        ".mpg", ".mpeg", ".webm", ".ts", ".mts", ".m2ts"
    };

    private readonly IDataService _dataService;
    private readonly BackgroundWindowViewModel _vm;

    /// <summary>Returns true if user clicked OK and changed the selection/items.</summary>
    public bool Changed { get; private set; }

    /// <summary>The background chosen by the user when clicking OK.</summary>
    public BackgroundItem? SelectedBackground => _vm.SelectedBackground;

    /// <summary>True if user checked 'Preview Live Output'.</summary>
    public bool IsPreviewLiveOutputChecked => ChkPreviewLive.IsChecked == true;

    public BackgroundOptionsWindow(IDataService dataService, string previewText = "", string? currentBackground = null)
    {
        InitializeComponent();
        _dataService = dataService;

        _vm = new BackgroundWindowViewModel(dataService.GetBackgrounds(), previewText, currentBackground);
        DataContext = _vm;

        TxtPreviewContent.Text = string.IsNullOrWhiteSpace(previewText)
            ? "Lord You are beautiful,\nI raise my hands to You,\nNo one else can touch\nmy heart like You do."
            : previewText;

        Loaded += (s, e) =>
        {
            if (_vm.SelectedEntry != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var container = ThumbnailGrid.ItemContainerGenerator.ContainerFromItem(_vm.SelectedEntry) as FrameworkElement;
                    container?.BringIntoView();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back && _vm.SelectedEntry != null)
        {
            _vm.DeleteSelected();
            e.Handled = true;
        }
    }

    private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BackgroundEntry entry)
        {
            _vm.Select(entry);
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Add Background",
            Filter = MediaFilter,
            Multiselect = false
        };
        if (dlg.ShowDialog(this) == true)
        {
            _vm.AddFile(dlg.FileName);
        }
    }

    private void OnAddMultipleClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Add Multiple Backgrounds",
            Filter = MediaFilter,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            foreach (var f in dlg.FileNames)
            {
                _vm.AddFile(f);
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        _vm.DeleteSelected();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        // Commit background changes back to data service (backgrounds.json)
        _dataService.SaveBackgrounds(_vm.BackgroundItems.Select(b => b.BackgroundItem));

        try
        {
            var activeMedia = _dataService.GetMediaItems().Select(m => m.FilePath);
            var activeBackgrounds = _vm.BackgroundItems.Select(b => b.FilePath);
            ThumbnailService.CleanupOrphanedThumbnails(activeMedia.Concat(activeBackgrounds));
        }
        catch { }

        Changed = true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static BackgroundType DetectType(string path)
    {
        var ext = Path.GetExtension(path);
        return VideoExtensions.Contains(ext) ? BackgroundType.Video : BackgroundType.Image;
    }
}

// ─── ViewModel for the window ────────────────────────────────────────────────
public class BackgroundWindowViewModel : ViewModelBase
{
    private BackgroundEntry? _selected;
    private readonly string _previewText;

    public ObservableCollection<BackgroundEntry> BackgroundItems { get; } = new();

    public BackgroundEntry? SelectedEntry
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    /// <summary>The BackgroundItem of the currently selected thumbnail, or null.</summary>
    public BackgroundItem? SelectedBackground => _selected?.BackgroundItem;

    public bool HasSelectedBackground => _selected != null;
    public bool HasItems => BackgroundItems.Count > 0;
    public bool IsSelectedVideo => _selected?.IsVideo ?? false;

    public BackgroundWindowViewModel(IEnumerable<BackgroundItem> existing, string previewText, string? currentBackground = null)
    {
        _previewText = previewText;
        foreach (var b in existing)
        {
            BackgroundItems.Add(new BackgroundEntry(b));
        }

        BackgroundEntry? match = null;
        if (!string.IsNullOrWhiteSpace(currentBackground))
        {
            match = BackgroundItems.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.BackgroundItem.FilePath) &&
                (string.Equals(b.BackgroundItem.FilePath, currentBackground, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(b.BackgroundItem.FilePath), Path.GetFileName(currentBackground), StringComparison.OrdinalIgnoreCase)))
                ?? BackgroundItems.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.BackgroundItem.HexColor) &&
                string.Equals(b.BackgroundItem.HexColor, currentBackground, StringComparison.OrdinalIgnoreCase));
        }

        if (match != null)
        {
            Select(match);
        }
        else if (BackgroundItems.Count > 0)
        {
            Select(BackgroundItems[0]);
        }
    }

    public void Select(BackgroundEntry entry)
    {
        foreach (var e in BackgroundItems) e.IsSelected = false;
        entry.IsSelected = true;
        SelectedEntry = entry;
        OnPropertyChanged(nameof(SelectedBackground));
        OnPropertyChanged(nameof(HasSelectedBackground));
        OnPropertyChanged(nameof(IsSelectedVideo));
    }

    public void AddFile(string path)
    {
        var item = new BackgroundItem
        {
            Name = Path.GetFileNameWithoutExtension(path),
            FilePath = path,
            Type = BackgroundOptionsWindow.DetectType(path)
        };
        var entry = new BackgroundEntry(item);
        BackgroundItems.Add(entry);
        OnPropertyChanged(nameof(HasItems));
        Select(entry);
    }

    public void DeleteSelected()
    {
        if (_selected == null) return;
        var filePath = _selected.FilePath;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            ThumbnailService.DeleteThumbnail(filePath);
        }
        int idx = BackgroundItems.IndexOf(_selected);
        BackgroundItems.Remove(_selected);
        OnPropertyChanged(nameof(HasItems));
        SelectedEntry = null;
        OnPropertyChanged(nameof(SelectedBackground));
        OnPropertyChanged(nameof(HasSelectedBackground));

        // Auto-select adjacent item
        if (BackgroundItems.Count > 0)
        {
            Select(BackgroundItems[Math.Clamp(idx, 0, BackgroundItems.Count - 1)]);
        }
    }
}
