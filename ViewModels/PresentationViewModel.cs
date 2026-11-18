using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;
using LuxPresenter.Services.Presentation;
using Microsoft.Win32;

namespace LuxPresenter.ViewModels;

public class PresentationViewModel : ViewModelBase
{
    private readonly IDataService _dataService;
    private PresentationDocItem? _selectedDoc;
    private PresentationDocument? _loadedDoc;
    private PresentationSlide? _selectedSlide;
    private int _selectedSlideIndex = -1;
    private string _searchFilter = string.Empty;
    private bool _isLoading = false;

    public ObservableCollection<PresentationDocItem> PresentationDocs { get; } = new();
    public ObservableCollection<PresentationSlide> Slides { get; } = new();

    public event Action<PresentationSlide, PresentationDocument>? SlideStagedForPreview;
    public event Action<PresentationSlide, PresentationDocument>? SlideDoubleClickedForLive;

    public PresentationDocItem? SelectedDoc
    {
        get => _selectedDoc;
        set
        {
            if (SetProperty(ref _selectedDoc, value) && value != null)
            {
                _ = LoadDocumentSlidesAsync(value);
            }
        }
    }

    public PresentationDocument? LoadedDoc
    {
        get => _loadedDoc;
        private set => SetProperty(ref _loadedDoc, value);
    }

    public PresentationSlide? SelectedSlide
    {
        get => _selectedSlide;
        set
        {
            if (SetProperty(ref _selectedSlide, value))
            {
                if (value != null && LoadedDoc != null)
                {
                    // Update preview selection indicator
                    foreach (var s in Slides)
                    {
                        s.IsSelectedInPreview = (s == value);
                    }
                    SlideStagedForPreview?.Invoke(value, LoadedDoc);
                }
            }
        }
    }

    public int SelectedSlideIndex
    {
        get => _selectedSlideIndex;
        set
        {
            if (SetProperty(ref _selectedSlideIndex, value))
            {
                if (value >= 0 && value < Slides.Count)
                {
                    SelectedSlide = Slides[value];
                }
                else
                {
                    SelectedSlide = null;
                }
            }
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand AddDocumentCommand { get; }
    public ICommand DeleteDocumentCommand { get; }
    public ICommand SelectSlideCommand { get; }
    public ICommand DoubleClickSlideCommand { get; }

    public PresentationViewModel(IDataService dataService)
    {
        _dataService = dataService;

        AddDocumentCommand = new RelayCommand(OnAddDocument);
        DeleteDocumentCommand = new RelayCommand(OnDeleteDocument, () => SelectedDoc != null);
        SelectSlideCommand = new RelayCommand(p => { if (p is PresentationSlide s) OnSelectSlide(s); });
        DoubleClickSlideCommand = new RelayCommand(p => { if (p is PresentationSlide s) OnDoubleClickSlide(s); });

        LoadStoredDocuments();
    }

    public void LoadStoredDocuments()
    {
        PresentationDocs.Clear();
        var docs = _dataService.GetPresentationDocs();
        foreach (var d in docs)
        {
            if (File.Exists(d.FilePath))
            {
                PresentationDocs.Add(d);
            }
        }
    }

    private void ApplySearchFilter()
    {
        var all = _dataService.GetPresentationDocs();
        PresentationDocs.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchFilter)
            ? all
            : all.Where(d => d.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                             d.FilePath.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var d in filtered)
        {
            if (File.Exists(d.FilePath))
            {
                PresentationDocs.Add(d);
            }
        }
    }

    private async void OnAddDocument()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Presentation Documents (*.pptx;*.ppsx;*.odp;*.pdf)|*.pptx;*.ppsx;*.odp;*.pdf|PowerPoint Presentations (*.pptx;*.ppsx)|*.pptx;*.ppsx|OpenDocument Presentations (*.odp)|*.odp|PDF Documents (*.pdf)|*.pdf|Legacy PowerPoint (*.ppt;*.pps)|*.ppt;*.pps|All Files (*.*)|*.*",
            Title = "Import Presentation Document into LuxPresenter"
        };

        if (ofd.ShowDialog() == true)
        {
            string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
            if (ext == ".ppt" || ext == ".pps")
            {
                MessageBox.Show(
                    "Legacy PowerPoint 97-2003 binary formats (.ppt / .pps) require conversion.\n\nFor optimal fidelity and presentation display, please save the presentation as modern .pptx or use LibreOffice Impress.",
                    "Legacy Binary Format (.ppt)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                IsLoading = true;
                string filePath = ofd.FileName;
                string docName = Path.GetFileNameWithoutExtension(filePath);

                var doc = await Task.Run(() => PresentationEngineService.LoadDocument(filePath));

                var docItem = new PresentationDocItem
                {
                    Name = docName,
                    FilePath = filePath,
                    FileExtension = ext,
                    SlideCount = doc.Slides.Count,
                    DateAdded = DateTime.Now
                };

                _dataService.AddPresentationDoc(docItem);
                LoadStoredDocuments();

                SelectedDoc = PresentationDocs.FirstOrDefault(d => d.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import presentation: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private void OnDeleteDocument()
    {
        if (SelectedDoc == null) return;

        var res = MessageBox.Show($"Are you sure you want to remove '{SelectedDoc.Name}' from the presentation library?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            string filePath = SelectedDoc.FilePath;
            PresentationEngineService.DeleteCacheForDocument(filePath);
            _dataService.DeletePresentationDoc(SelectedDoc.Id);
            Slides.Clear();
            LoadedDoc = null;
            SelectedDoc = null;
            LoadStoredDocuments();
        }
    }

    public async Task LoadDocumentSlidesAsync(PresentationDocItem docItem)
    {
        try
        {
            IsLoading = true;
            Slides.Clear();

            var doc = await Task.Run(() => PresentationEngineService.LoadDocument(docItem.FilePath));
            LoadedDoc = doc;

            foreach (var s in doc.Slides)
            {
                Slides.Add(s);
            }

            // Auto-select first slide on document load so Preview is immediately available
            if (SelectedSlide == null && Slides.Count > 0)
            {
                SelectedSlide = Slides[0];
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load slides: {ex.Message}", "Error Loading Slides", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSelectSlide(PresentationSlide? slide)
    {
        if (slide != null)
        {
            SelectedSlide = slide;
        }
    }

    private void OnDoubleClickSlide(PresentationSlide? slide)
    {
        if (slide != null && LoadedDoc != null)
        {
            SelectedSlide = slide;
            SlideDoubleClickedForLive?.Invoke(slide, LoadedDoc);
        }
    }

    public void MarkLiveSlide(int slideIndex)
    {
        foreach (var s in Slides)
        {
            s.IsCurrentlyLive = (s.Index == slideIndex);
        }
    }

    public void ClearLiveMarks()
    {
        foreach (var s in Slides)
        {
            s.IsCurrentlyLive = false;
        }
    }
}
