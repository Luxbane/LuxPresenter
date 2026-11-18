using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.ViewModels;

public class ScripturesViewModel : ViewModelBase
{
    private readonly IDataService _dataService;
    private BibleBook? _selectedBook;
    private int _chapter = 1;
    private BibleVerse? _selectedVerse;
    private string _searchQuery = string.Empty;
    private string _selectedVersion = "TB"; // Default to Indonesian TB for church service
    private bool _isUpdatingSearchQuery = false;
    private bool _isBookPickerOpen = false;

    public ObservableCollection<BibleBook> Books { get; } = new();
    public ObservableCollection<BibleBook> OldTestamentBooks { get; } = new();
    public ObservableCollection<BibleBook> NewTestamentBooks { get; } = new();
    public ObservableCollection<int> AvailableChapters { get; } = new();
    public ObservableCollection<BibleVerse> Verses { get; } = new();
    public ObservableCollection<string> Versions { get; } = new() { "TB", "KJV" };

    public BibleBook? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (SetProperty(ref _selectedBook, value))
            {
                UpdateAvailableChapters();
                if (_chapter < 1 || (value != null && _chapter > value.ChapterCount))
                {
                    _chapter = 1;
                    OnPropertyChanged(nameof(Chapter));
                }
                LoadVerses();
                UpdateSearchQueryDisplay();
            }
        }
    }

    public int Chapter
    {
        get => _chapter;
        set
        {
            int clamped = value;
            if (SelectedBook != null)
            {
                if (clamped < 1) clamped = 1;
                if (clamped > SelectedBook.ChapterCount) clamped = SelectedBook.ChapterCount;
            }
            else if (clamped < 1) clamped = 1;

            if (SetProperty(ref _chapter, clamped))
            {
                LoadVerses();
                UpdateSearchQueryDisplay();
            }
        }
    }

    public string SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (value == "TB" && !_dataService.HasVersion("TB"))
            {
                RequestImportBible?.Invoke();
                if (!_dataService.HasVersion("TB"))
                {
                    return;
                }
            }

            if (SetProperty(ref _selectedVersion, value))
            {
                int currentBookId = SelectedBook?.Id ?? 1;
                int currentChapter = Chapter;
                LoadBooks(currentBookId);
                Chapter = currentChapter;
                LoadVerses();
                UpdateSearchQueryDisplay();
            }
        }
    }

    public BibleVerse? SelectedVerse
    {
        get => _selectedVerse;
        set
        {
            _selectedVerse = value;
            OnPropertyChanged();
            if (value != null)
            {
                VerseSelectedForPreview?.Invoke(value);
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                if (!_isUpdatingSearchQuery)
                {
                    OnSearchQueryChanged();
                }
            }
        }
    }

    public bool IsBookPickerOpen
    {
        get => _isBookPickerOpen;
        set => SetProperty(ref _isBookPickerOpen, value);
    }

    public event Action<BibleVerse>? VerseDoubleClicked;
    public event Action<BibleVerse>? VerseSelectedForPreview;
    public event Action<BibleVerse>? AddToScheduleRequested;
    public event Action? RequestImportBible;

    public ICommand SendToPreviewCommand { get; }
    public ICommand SendToLiveCommand { get; }
    public ICommand AddToScheduleCommand { get; }
    public ICommand DoubleClickCommand { get; }
    public ICommand NextChapterCommand { get; }
    public ICommand PrevChapterCommand { get; }
    public ICommand ToggleBookPickerCommand { get; }
    public ICommand SelectBookCommand { get; }
    public ICommand ImportBibleCommand { get; }

    public ScripturesViewModel(IDataService dataService)
    {
        _dataService = dataService;
        _selectedVersion = _dataService.HasVersion("TB") ? "TB" : "KJV";

        ImportBibleCommand = new RelayCommand(() => RequestImportBible?.Invoke());

        SendToPreviewCommand = new RelayCommand(() =>
        {
            if (SelectedVerse != null) VerseSelectedForPreview?.Invoke(SelectedVerse);
        }, () => SelectedVerse != null);

        SendToLiveCommand = new RelayCommand(() =>
        {
            if (SelectedVerse != null) VerseDoubleClicked?.Invoke(SelectedVerse);
        }, () => SelectedVerse != null);

        AddToScheduleCommand = new RelayCommand(() =>
        {
            if (SelectedVerse != null) AddToScheduleRequested?.Invoke(SelectedVerse);
        }, () => SelectedVerse != null);

        DoubleClickCommand = new RelayCommand(p =>
        {
            if (p is BibleVerse verse) VerseDoubleClicked?.Invoke(verse);
            else if (SelectedVerse != null) VerseDoubleClicked?.Invoke(SelectedVerse);
        });

        NextChapterCommand = new RelayCommand(() =>
        {
            if (SelectedBook == null) return;
            if (Chapter < SelectedBook.ChapterCount)
            {
                Chapter++;
            }
            else
            {
                int nextIdx = Books.IndexOf(SelectedBook) + 1;
                if (nextIdx < Books.Count)
                {
                    _chapter = 1;
                    SelectedBook = Books[nextIdx];
                }
            }
        });

        PrevChapterCommand = new RelayCommand(() =>
        {
            if (SelectedBook == null) return;
            if (Chapter > 1)
            {
                Chapter--;
            }
            else
            {
                int prevIdx = Books.IndexOf(SelectedBook) - 1;
                if (prevIdx >= 0)
                {
                    var prevBook = Books[prevIdx];
                    _chapter = prevBook.ChapterCount;
                    SelectedBook = prevBook;
                }
            }
        });

        ToggleBookPickerCommand = new RelayCommand(() =>
        {
            IsBookPickerOpen = !IsBookPickerOpen;
        });

        SelectBookCommand = new RelayCommand(p =>
        {
            if (p is BibleBook b)
            {
                SelectedBook = b;
                Chapter = 1;
                IsBookPickerOpen = false;
            }
        });

        LoadBooks(1);
    }

    public void RefreshAfterBibleImport(string version)
    {
        _selectedVersion = version;
        OnPropertyChanged(nameof(SelectedVersion));
        int currentBookId = SelectedBook?.Id ?? 1;
        LoadBooks(currentBookId);
        LoadVerses();
        UpdateSearchQueryDisplay();
    }

    private void LoadBooks(int preserveBookId = 1)
    {
        Books.Clear();
        OldTestamentBooks.Clear();
        NewTestamentBooks.Clear();

        var list = _dataService.GetBooks(_selectedVersion);
        foreach (var b in list)
        {
            Books.Add(b);
            if (b.IsOldTestament)
                OldTestamentBooks.Add(b);
            else
                NewTestamentBooks.Add(b);
        }

        var target = Books.FirstOrDefault(b => b.Id == preserveBookId) ?? Books.FirstOrDefault();
        _selectedBook = null;
        SelectedBook = target;
    }

    private void UpdateAvailableChapters()
    {
        AvailableChapters.Clear();
        int count = SelectedBook?.ChapterCount ?? 1;
        for (int i = 1; i <= count; i++)
        {
            AvailableChapters.Add(i);
        }
    }

    private void LoadVerses()
    {
        if (SelectedBook == null) return;
        Verses.Clear();

        var list = _dataService.GetChapterVerses(SelectedBook.Name, Chapter, SelectedVersion);
        foreach (var v in list)
        {
            Verses.Add(v);
        }

        if (Verses.Count > 0)
        {
            SelectedVerse = Verses[0];
        }
    }

    private void UpdateSearchQueryDisplay()
    {
        if (SelectedBook == null) return;
        _isUpdatingSearchQuery = true;
        SearchQuery = $"{SelectedBook.Name} {Chapter}:1";
        _isUpdatingSearchQuery = false;
    }

    private void OnSearchQueryChanged()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            LoadVerses();
            return;
        }

        string q = _searchQuery.Trim();

        // Check if query is a reference: "Joh 3:16", "Wahyu 6 6", "Genesis 1 1", "Kejadian 1:1", "1 Kor 13 4", "Gen 1", "Yoh 3", "Mat 5:1"
        var refMatch = Regex.Match(q, @"^([1-3]?\s*[A-Za-z\-]+(?:\s+[A-Za-z\-]+)*)\s*(\d+)(?:\s*(?:[:.,]|\s+|ayat|ay\.?|verse|v\.?)\s*(\d+)(?:\s*-\s*\d+)?)?$", RegexOptions.IgnoreCase);
        if (refMatch.Success)
        {
            string bookPart = refMatch.Groups[1].Value.Trim();
            if (int.TryParse(refMatch.Groups[2].Value, out int ch))
            {
                int bookId = _dataService.ResolveBookId(bookPart);
                var matchedBook = bookId > 0 ? Books.FirstOrDefault(b => b.Id == bookId) : null;

                if (matchedBook == null)
                {
                    string cleanBook = bookPart.Replace(" ", "");
                    matchedBook = Books.FirstOrDefault(b =>
                        b.Name.Equals(bookPart, StringComparison.OrdinalIgnoreCase) ||
                        b.EnglishName.Equals(bookPart, StringComparison.OrdinalIgnoreCase) ||
                        b.IndonesianName.Equals(bookPart, StringComparison.OrdinalIgnoreCase) ||
                        b.ShortName.Equals(bookPart, StringComparison.OrdinalIgnoreCase) ||
                        b.Name.Replace(" ", "").Equals(cleanBook, StringComparison.OrdinalIgnoreCase) ||
                        b.EnglishName.Replace(" ", "").Equals(cleanBook, StringComparison.OrdinalIgnoreCase) ||
                        b.IndonesianName.Replace(" ", "").Equals(cleanBook, StringComparison.OrdinalIgnoreCase) ||
                        b.ShortName.Replace(" ", "").Equals(cleanBook, StringComparison.OrdinalIgnoreCase) ||
                        (b.ShortName != null && cleanBook.StartsWith(b.ShortName, StringComparison.OrdinalIgnoreCase)) ||
                        b.IndonesianName.StartsWith(bookPart, StringComparison.OrdinalIgnoreCase) ||
                        b.EnglishName.StartsWith(bookPart, StringComparison.OrdinalIgnoreCase));
                }

                if (matchedBook != null)
                {
                    bool bookChanged = SelectedBook != matchedBook;
                    bool chapterChanged = _chapter != ch;

                    if (bookChanged)
                    {
                        _selectedBook = matchedBook;
                        OnPropertyChanged(nameof(SelectedBook));
                        UpdateAvailableChapters();
                    }

                    if (chapterChanged || bookChanged || Verses.Count == 0)
                    {
                        _chapter = ch;
                        OnPropertyChanged(nameof(Chapter));

                        Verses.Clear();
                        var chVerses = _dataService.GetChapterVerses(matchedBook.Name, ch, SelectedVersion);
                        foreach (var v in chVerses)
                        {
                            Verses.Add(v);
                        }
                    }

                    if (refMatch.Groups[3].Success && int.TryParse(refMatch.Groups[3].Value, out int vr))
                    {
                        SelectedVerse = Verses.FirstOrDefault(v => v.Verse == vr) ?? Verses.FirstOrDefault();
                    }
                    else if (Verses.Count > 0 && SelectedVerse == null)
                    {
                        SelectedVerse = Verses[0];
                    }
                    return;
                }
            }
        }

        // Full-text search
        Verses.Clear();
        var searchResults = _dataService.SearchScriptures(q, SelectedVersion);
        foreach (var v in searchResults)
        {
            Verses.Add(v);
        }

        if (Verses.Count > 0)
        {
            SelectedVerse = Verses[0];
        }
    }
}
