using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.ViewModels;

public class SongsViewModel : ViewModelBase
{
    private readonly IDataService _dataService;
    private string _titleSearchQuery = string.Empty;
    private string _lyricsSearchQuery = string.Empty;
    private bool _favoritesOnly = false;
    private Song? _selectedSong;
    private bool _isActive = false;
    private List<Song> _allSongs = new();
    private List<Song> _sortedSongsByTitle = new();
    private List<SongSearchEntry> _searchEntries = new();
    private List<LyricLineCandidate> _lyricCandidates = new();
    private readonly DispatcherTimer _debounceTimer;
    private string? _previewTargetQuery;

    public ObservableRangeCollection<Song> FilteredSongs { get; } = new();

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string? PreviewTargetQuery
    {
        get => _previewTargetQuery;
        set
        {
            _previewTargetQuery = value;
            OnPropertyChanged(nameof(ActiveSearchQuery));
        }
    }

    public void SetPreviewTargetQuery(string? query)
    {
        _previewTargetQuery = query;
        OnPropertyChanged(nameof(ActiveSearchQuery));
    }

    public string TitleSearchQuery
    {
        get => _titleSearchQuery;
        set
        {
            if (SetProperty(ref _titleSearchQuery, value))
            {
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(_lyricsSearchQuery))
                {
                    _lyricsSearchQuery = string.Empty;
                    OnPropertyChanged(nameof(LyricsSearchQuery));
                }
                DebounceApplyFilter();
            }
        }
    }

    public string LyricsSearchQuery
    {
        get => _lyricsSearchQuery;
        set
        {
            if (SetProperty(ref _lyricsSearchQuery, value))
            {
                if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(_titleSearchQuery))
                {
                    _titleSearchQuery = string.Empty;
                    OnPropertyChanged(nameof(TitleSearchQuery));
                }
                DebounceApplyFilter();
            }
        }
    }

    public string SearchQuery
    {
        get => !string.IsNullOrEmpty(_titleSearchQuery) ? _titleSearchQuery : _lyricsSearchQuery;
        set => TitleSearchQuery = value;
    }

    public string ActiveSearchQuery => !string.IsNullOrWhiteSpace(_previewTargetQuery)
        ? _previewTargetQuery
        : (!string.IsNullOrWhiteSpace(_lyricsSearchQuery) ? _lyricsSearchQuery : _titleSearchQuery);

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (SetProperty(ref _favoritesOnly, value))
            {
                ApplyFilterImmediate();
            }
        }
    }

    public Song? SelectedSong
    {
        get => _selectedSong;
        set
        {
            if (SetProperty(ref _selectedSong, value) && value != null)
            {
                SongSelectedForPreview?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Explicitly called when user clicks or navigates to a song in the song list
    /// </summary>
    public void UserSelectSong(Song song)
    {
        _previewTargetQuery = null;
        if (SelectedSong != song)
        {
            SelectedSong = song;
        }
        else
        {
            SongSelectedForPreview?.Invoke(song);
        }
    }

    public bool HasSongs => _allSongs.Count > 0;
    public string SongsDirectoryPath => AppPaths.SongsDirectory;

    public event Action<Song>? SongDoubleClicked;
    public event Action<Song>? SongSelectedForPreview;
    public event Action<Song>? AddToScheduleRequested;
    public event Action<Song?>? EditSongRequested;
    public event Action? RequestImportSongBackup;
    public event Action? RequestOpenSongsDirectory;
    public event Action? RequestLoadSampleSongs;

    public ICommand SendToPreviewCommand { get; }
    public ICommand SendToLiveCommand { get; }
    public ICommand AddToScheduleCommand { get; }
    public ICommand ToggleFavoritesFilterCommand { get; }
    public ICommand DoubleClickCommand { get; }
    public ICommand NewSongCommand { get; }
    public ICommand EditSongCommand { get; }
    public ICommand DeleteSongCommand { get; }
    public ICommand ImportSongBackupCommand { get; }
    public ICommand OpenSongsFolderCommand { get; }
    public ICommand LoadSampleSongsCommand { get; }

    public SongsViewModel(IDataService dataService)
    {
        _dataService = dataService;

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            ApplyFilterCore();
        };

        SendToPreviewCommand = new RelayCommand(() =>
        {
            if (SelectedSong != null) UserSelectSong(SelectedSong);
        }, () => SelectedSong != null);

        SendToLiveCommand = new RelayCommand(() =>
        {
            if (SelectedSong != null) SongDoubleClicked?.Invoke(SelectedSong);
        }, () => SelectedSong != null);

        AddToScheduleCommand = new RelayCommand(() =>
        {
            if (SelectedSong != null) AddToScheduleRequested?.Invoke(SelectedSong);
        }, () => SelectedSong != null);

        ToggleFavoritesFilterCommand = new RelayCommand(() =>
        {
            FavoritesOnly = !FavoritesOnly;
        });

        DoubleClickCommand = new RelayCommand(p =>
        {
            if (p is Song song)
            {
                UserSelectSong(song);
                SongDoubleClicked?.Invoke(song);
            }
            else if (SelectedSong != null)
            {
                UserSelectSong(SelectedSong);
                SongDoubleClicked?.Invoke(SelectedSong);
            }
        });

        NewSongCommand = new RelayCommand(() =>
        {
            EditSongRequested?.Invoke(null);
        });

        EditSongCommand = new RelayCommand(() =>
        {
            if (SelectedSong != null) EditSongRequested?.Invoke(SelectedSong);
        }, () => SelectedSong != null);

        DeleteSongCommand = new RelayCommand(() =>
        {
            if (SelectedSong != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete '{SelectedSong.Title}'?",
                    "Delete Song",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _dataService.DeleteSong(SelectedSong.Id);
                    ReloadSongs();
                }
            }
        }, () => SelectedSong != null);

        ImportSongBackupCommand = new RelayCommand(() => RequestImportSongBackup?.Invoke());
        OpenSongsFolderCommand = new RelayCommand(() => RequestOpenSongsDirectory?.Invoke());
        LoadSampleSongsCommand = new RelayCommand(() => RequestLoadSampleSongs?.Invoke());

        ReloadSongs();
    }

    public void ReloadSongs(string? targetSongId = null)
    {
        var target = targetSongId ?? SelectedSong?.Id;
        _allSongs = _dataService.GetSongs();
        _sortedSongsByTitle = _allSongs.OrderBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        _searchEntries = _allSongs.Select(s => new SongSearchEntry(s)).ToList();
        BuildLyricCandidates();
        OnPropertyChanged(nameof(HasSongs));
        OnPropertyChanged(nameof(SongsDirectoryPath));
        _debounceTimer.Stop();
        ApplyFilterCore(target);
    }

    private void BuildLyricCandidates()
    {
        var list = new List<LyricLineCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var song in _allSongs)
        {
            if (string.IsNullOrWhiteSpace(song.LyricsRaw)) continue;

            var lines = song.LyricsRaw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
                    (trimmed.EndsWith(":") && !trimmed.Contains(" ")))
                {
                    continue;
                }

                string key = $"{trimmed}|{song.Id}";
                if (seen.Add(key))
                {
                    list.Add(new LyricLineCandidate(trimmed, song));
                }
            }
        }

        _lyricCandidates = list
            .OrderBy(c => c.LineText, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.Song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Song? GetTitleSuggestion(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return null;

        return _sortedSongsByTitle.FirstOrDefault(s =>
            (!FavoritesOnly || s.IsFavorite) &&
            s.Title.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
    }

    public LyricLineCandidate? GetLyricSuggestion(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return null;

        return _lyricCandidates.FirstOrDefault(c =>
            (!FavoritesOnly || c.Song.IsFavorite) &&
            c.LineText.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
    }

    public bool HasTitleMatch(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return true;
        if (_sortedSongsByTitle.Count == 0) return true;

        return _sortedSongsByTitle.Any(s =>
            (!FavoritesOnly || s.IsFavorite) &&
            s.Title.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
    }

    public bool HasLyricMatch(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return true;
        if (_lyricCandidates.Count == 0) return true;

        return _lyricCandidates.Any(c =>
            (!FavoritesOnly || c.Song.IsFavorite) &&
            c.LineText.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
    }

    public void ApplySearch(string query, bool isTitle, string? targetSongId = null, string? targetSlideQuery = null)
    {
        _previewTargetQuery = targetSlideQuery;

        if (isTitle)
        {
            _titleSearchQuery = query;
            _lyricsSearchQuery = string.Empty;
            OnPropertyChanged(nameof(TitleSearchQuery));
            OnPropertyChanged(nameof(LyricsSearchQuery));
        }
        else
        {
            _lyricsSearchQuery = query;
            _titleSearchQuery = string.Empty;
            OnPropertyChanged(nameof(TitleSearchQuery));
            OnPropertyChanged(nameof(LyricsSearchQuery));
        }

        OnPropertyChanged(nameof(ActiveSearchQuery));
        ApplyFilterImmediate(targetSongId);
    }

    private void DebounceApplyFilter()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void ApplyFilterImmediate(string? targetSongId = null)
    {
        _debounceTimer.Stop();
        ApplyFilterCore(targetSongId);
    }

    private static readonly Dictionary<string, string> ContractionSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        { "dibri", "diberi" }, { "diberi", "dibri" },
        { "sbab", "sebab" }, { "sebab", "sbab" },
        { "tlah", "telah" }, { "telah", "tlah" },
        { "karna", "karena" }, { "karena", "karna" },
        { "sgala", "segala" }, { "segala", "sgala" },
        { "bsar", "besar" }, { "besar", "bsar" },
        { "prisai", "perisai" }, { "perisai", "prisai" },
        { "blajar", "belajar" }, { "belajar", "blajar" },
        { "sperti", "seperti" }, { "seperti", "sperti" },
        { "slalu", "selalu" }, { "selalu", "slalu" }
    };

    public static string NormalizeSearchText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (char c in input)
        {
            // Strip apostrophes and single/double smart quotes:
            // ' (39), ` (96), \u2019, \u2018, \u201C, \u201D, 0x91, 0x92, 0x93, 0x94
            if (c == (char)39 || c == (char)96 || c == '\u2019' || c == '\u2018' || c == '\u201C' || c == '\u201D' ||
                c == (char)0x91 || c == (char)0x92 || c == (char)0x93 || c == (char)0x94)
            {
                continue; // strip without space, e.g. dib'ri -> dibri
            }

            // Replace punctuation, dashes, hyphens, slashes with spaces:
            if (char.IsPunctuation(c) || char.IsSymbol(c) || c == '\u2013' || c == '\u2014')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        var raw = sb.ToString();
        var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    private sealed class SongSearchEntry
    {
        public Song Song { get; }
        public string CleanTitle { get; }
        public string CompactTitle { get; }
        public string CleanAuthor { get; }
        public string CleanLyrics { get; }
        public string CompactLyrics { get; }
        public string CleanFull { get; }
        public string CompactFull { get; }

        public SongSearchEntry(Song song)
        {
            Song = song;
            CleanTitle = NormalizeSearchText(song.Title);
            CompactTitle = CleanTitle.Replace(" ", string.Empty);
            CleanAuthor = NormalizeSearchText(song.Author);
            CleanLyrics = NormalizeSearchText(song.LyricsRaw);
            CompactLyrics = CleanLyrics.Replace(" ", string.Empty);
            CleanFull = $"{CleanTitle} {CleanAuthor} {CleanLyrics}";
            CompactFull = $"{CompactTitle}{CleanAuthor.Replace(" ", string.Empty)}{CompactLyrics}";
        }
    }

    private void ApplyFilterCore(string? targetSongId = null)
    {
        var target = targetSongId ?? SelectedSong?.Id;

        var titleQ = _titleSearchQuery.Trim();
        var lyricsQ = _lyricsSearchQuery.Trim();
        bool hasTitle = !string.IsNullOrWhiteSpace(titleQ);
        bool hasLyrics = !string.IsNullOrWhiteSpace(lyricsQ);
        bool hasSearchQuery = hasTitle || hasLyrics;

        List<Song> results;

        if (!hasSearchQuery)
        {
            var query = _allSongs.AsEnumerable();
            if (FavoritesOnly)
            {
                query = query.Where(s => s.IsFavorite);
            }
            results = query.OrderBy(s => s.Title).ToList();
        }
        else
        {
            string rawQuery = hasTitle ? titleQ : lyricsQ;
            bool isTitleOnly = hasTitle;

            string normQ = NormalizeSearchText(rawQuery);
            string compactQ = normQ.Replace(" ", string.Empty);
            var tokens = normQ.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var scored = new List<(int Score, Song Song)>();

            foreach (var entry in _searchEntries)
            {
                if (FavoritesOnly && !entry.Song.IsFavorite)
                {
                    continue;
                }

                int score = 0;

                if (isTitleOnly)
                {
                    if (entry.CleanTitle == normQ) score += 2000;
                    else if (entry.CleanTitle.StartsWith(normQ, StringComparison.OrdinalIgnoreCase)) score += 1600;
                    else if (entry.CompactTitle.StartsWith(compactQ, StringComparison.OrdinalIgnoreCase)) score += 1400;
                    else if (entry.CleanTitle.Contains(normQ, StringComparison.OrdinalIgnoreCase)) score += 1200;
                    else if (entry.CompactTitle.Contains(compactQ, StringComparison.OrdinalIgnoreCase)) score += 1000;
                    else
                    {
                        int matchedTokens = 0;
                        foreach (var token in tokens)
                        {
                            if (entry.CleanTitle.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                entry.CompactTitle.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                (ContractionSynonyms.TryGetValue(token, out var syn) && (entry.CleanTitle.Contains(syn, StringComparison.OrdinalIgnoreCase) || entry.CompactTitle.Contains(syn, StringComparison.OrdinalIgnoreCase))))
                            {
                                matchedTokens++;
                            }
                        }

                        if (matchedTokens == tokens.Length) score += 700;
                    }
                }
                else
                {
                    // Full-text search (Title, Author, Lyrics)
                    if (entry.CleanTitle == normQ) score += 2000;
                    else if (entry.CleanTitle.StartsWith(normQ, StringComparison.OrdinalIgnoreCase)) score += 1600;
                    else if (entry.CompactTitle.StartsWith(compactQ, StringComparison.OrdinalIgnoreCase)) score += 1400;
                    else if (entry.CleanTitle.Contains(normQ, StringComparison.OrdinalIgnoreCase)) score += 1200;
                    else if (entry.CompactTitle.Contains(compactQ, StringComparison.OrdinalIgnoreCase)) score += 1000;
                    else if (entry.CleanLyrics.Contains(normQ, StringComparison.OrdinalIgnoreCase)) score += 800;
                    else if (entry.CompactLyrics.Contains(compactQ, StringComparison.OrdinalIgnoreCase)) score += 700;

                    // Multi-token matching across Title, Author, Lyrics
                    int matchedTokens = 0;
                    foreach (var token in tokens)
                    {
                        if (entry.CleanFull.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                            entry.CompactFull.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                            (ContractionSynonyms.TryGetValue(token, out var syn) &&
                             (entry.CleanFull.Contains(syn, StringComparison.OrdinalIgnoreCase) || entry.CompactFull.Contains(syn, StringComparison.OrdinalIgnoreCase))))
                        {
                            matchedTokens++;
                        }
                    }

                    if (matchedTokens == tokens.Length)
                    {
                        score += 500;
                    }
                }

                if (score > 0)
                {
                    scored.Add((score, entry.Song));
                }
            }

            results = scored
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Song.Title)
                .Select(x => x.Song)
                .ToList();
        }

        FilteredSongs.ReplaceAll(results);

        if (!string.IsNullOrWhiteSpace(target))
        {
            var match = FilteredSongs.FirstOrDefault(s => s.Id == target);
            if (match != null)
            {
                if (SelectedSong != match)
                {
                    SelectedSong = match;
                }
                else if (hasSearchQuery)
                {
                    SongSelectedForPreview?.Invoke(match);
                }
                return;
            }
        }

        if (FilteredSongs.Count > 0)
        {
            var first = FilteredSongs[0];
            if (SelectedSong != first)
            {
                SelectedSong = first;
            }
            else
            {
                SongSelectedForPreview?.Invoke(first);
            }
        }
        else
        {
            SelectedSong = null;
        }
    }
}

public sealed class LyricLineCandidate
{
    public string LineText { get; }
    public Song Song { get; }

    public LyricLineCandidate(string lineText, Song song)
    {
        LineText = lineText;
        Song = song;
    }
}
