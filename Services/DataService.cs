using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LuxPresenter.Models;

namespace LuxPresenter.Services;

public class DataService : IDataService
{
    private readonly string _dataDir;
    private string _songsFile;
    private readonly string _mediaFile;
    private readonly string _presentationsFile;
    private readonly string _biblesFile;
    private readonly string _backgroundsFile;
    private readonly string _settingsFile;

    private List<Song> _songs = new();
    private List<MediaItem> _media = new();
    private List<PresentationDocItem> _presentationDocs = new();
    private List<BackgroundItem> _backgrounds = new();
    private List<BibleVerse> _verses = new();
    private List<BibleBook> _books = new();
    private readonly Dictionary<(string version, int bookId, int chapter), List<BibleVerse>> _chapterLookup = new();
    private readonly Dictionary<string, int> _bookNameToId = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings = new();

    private static readonly (int Id, string Eng, string Indo, string ShortEng, string ShortIndo, int Chapters, bool IsOT)[] AllBookMetadata = new[]
    {
        (1, "Genesis", "Kejadian", "Gen", "Kej", 50, true),
        (2, "Exodus", "Keluaran", "Exo", "Kel", 40, true),
        (3, "Leviticus", "Imamat", "Lev", "Im", 27, true),
        (4, "Numbers", "Bilangan", "Num", "Bil", 36, true),
        (5, "Deuteronomy", "Ulangan", "Deu", "Ul", 34, true),
        (6, "Joshua", "Yosua", "Jos", "Yos", 24, true),
        (7, "Judges", "Hakim-hakim", "Jud", "Hak", 21, true),
        (8, "Ruth", "Rut", "Rut", "Rut", 4, true),
        (9, "1 Samuel", "1 Samuel", "1Sa", "1Sam", 31, true),
        (10, "2 Samuel", "2 Samuel", "2Sa", "2Sam", 24, true),
        (11, "1 Kings", "1 Raja-raja", "1Ki", "1Raj", 22, true),
        (12, "2 Kings", "2 Raja-raja", "2Ki", "2Raj", 25, true),
        (13, "1 Chronicles", "1 Tawarikh", "1Ch", "1Taw", 29, true),
        (14, "2 Chronicles", "2 Tawarikh", "2Ch", "2Taw", 36, true),
        (15, "Ezra", "Ezra", "Ezr", "Ezr", 10, true),
        (16, "Nehemiah", "Nehemia", "Neh", "Neh", 13, true),
        (17, "Esther", "Ester", "Est", "Est", 10, true),
        (18, "Job", "Ayub", "Job", "Ayb", 42, true),
        (19, "Psalms", "Mazmur", "Psa", "Mzm", 150, true),
        (20, "Proverbs", "Amsal", "Pro", "Ams", 31, true),
        (21, "Ecclesiastes", "Pengkhotbah", "Ecc", "Pkh", 12, true),
        (22, "Song of Solomon", "Kidung Agung", "Sng", "Kid", 8, true),
        (23, "Isaiah", "Yesaya", "Isa", "Yes", 66, true),
        (24, "Jeremiah", "Yeremia", "Jer", "Yer", 52, true),
        (25, "Lamentations", "Ratapan", "Lam", "Rat", 5, true),
        (26, "Ezekiel", "Yehezkiel", "Ezk", "Yeh", 48, true),
        (27, "Daniel", "Daniel", "Dan", "Dan", 12, true),
        (28, "Hosea", "Hosea", "Hos", "Hos", 14, true),
        (29, "Joel", "Yoel", "Joe", "Yl", 3, true),
        (30, "Amos", "Amos", "Amo", "Am", 9, true),
        (31, "Obadiah", "Obaja", "Oba", "Ob", 1, true),
        (32, "Jonah", "Yunus", "Jon", "Yun", 4, true),
        (33, "Micah", "Mikha", "Mic", "Mik", 7, true),
        (34, "Nahum", "Nahum", "Nah", "Nah", 3, true),
        (35, "Habakkuk", "Habakuk", "Hab", "Hab", 3, true),
        (36, "Zephaniah", "Zefanya", "Zep", "Zef", 3, true),
        (37, "Haggai", "Hagai", "Hag", "Hag", 2, true),
        (38, "Zechariah", "Zakharia", "Zec", "Za", 14, true),
        (39, "Malachi", "Maleakhi", "Mal", "Mal", 4, true),
        (40, "Matthew", "Matius", "Mat", "Mat", 28, false),
        (41, "Mark", "Markus", "Mrk", "Mrk", 16, false),
        (42, "Luke", "Lukas", "Luk", "Luk", 24, false),
        (43, "John", "Yohanes", "Jhn", "Yoh", 21, false),
        (44, "Acts", "Kisah Para Rasul", "Act", "Kis", 28, false),
        (45, "Romans", "Roma", "Rom", "Rom", 16, false),
        (46, "1 Corinthians", "1 Korintus", "1Co", "1Kor", 16, false),
        (47, "2 Corinthians", "2 Korintus", "2Co", "2Kor", 13, false),
        (48, "Galatians", "Galatia", "Gal", "Gal", 6, false),
        (49, "Ephesians", "Efesus", "Eph", "Ef", 6, false),
        (50, "Philippians", "Filipi", "Php", "Flp", 4, false),
        (51, "Colossians", "Kolose", "Col", "Kol", 4, false),
        (52, "1 Thessalonians", "1 Tesalonika", "1Th", "1Tes", 5, false),
        (53, "2 Thessalonians", "2 Tesalonika", "2Th", "2Tes", 3, false),
        (54, "1 Timothy", "1 Timotius", "1Ti", "1Tim", 6, false),
        (55, "2 Timothy", "2 Timotius", "2Ti", "2Tim", 4, false),
        (56, "Titus", "Titus", "Tit", "Tit", 3, false),
        (57, "Filemon", "Filemon", "Phm", "Flm", 1, false),
        (58, "Hebrews", "Ibrani", "Heb", "Ibr", 13, false),
        (59, "James", "Yakobus", "Jas", "Yak", 5, false),
        (60, "1 Peter", "1 Petrus", "1Pe", "1Pet", 5, false),
        (61, "2 Peter", "2 Petrus", "2Pe", "2Pet", 3, false),
        (62, "1 John", "1 Yohanes", "1Jn", "1Yoh", 5, false),
        (63, "2 John", "2 Yohanes", "2Jn", "2Yoh", 1, false),
        (64, "3 John", "3 Yohanes", "3Jn", "3Yoh", 1, false),
        (65, "Jude", "Yudas", "Jud", "Yud", 1, false),
        (66, "Revelation", "Wahyu", "Rev", "Why", 22, false)
    };

    public DataService()
    {
        _dataDir = AppPaths.DataDirectory;
        Directory.CreateDirectory(_dataDir);

        _mediaFile = Path.Combine(_dataDir, "media.json");
        _presentationsFile = Path.Combine(_dataDir, "presentations.json");
        _biblesFile = Path.Combine(_dataDir, "bibles.json");
        _backgroundsFile = Path.Combine(_dataDir, "backgrounds.json");
        _settingsFile = Path.Combine(_dataDir, "settings.json");

        InitializeBooks();
        LoadSettings();

        if (!string.IsNullOrWhiteSpace(_settings.CustomSongDatabaseDirectory) &&
            Directory.Exists(_settings.CustomSongDatabaseDirectory))
        {
            AppPaths.SetSongsDirectory(_settings.CustomSongDatabaseDirectory);
        }

        _songsFile = Path.Combine(AppPaths.SongsDirectory, "songs.json");

        LoadOrSeedData();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                if (!string.IsNullOrWhiteSpace(_settings.LastUsedBackground) &&
                    !_settings.LastUsedBackground.StartsWith("#") &&
                    !File.Exists(_settings.LastUsedBackground))
                {
                    _settings.LastUsedBackground = string.Empty;
                    SaveSettingsToFile();
                }
            }
            catch { _settings = new(); }
        }
        else
        {
            _settings = new();
            SaveSettingsToFile();
        }
    }

    private void InitializeBooks()
    {
        _bookNameToId.Clear();
        foreach (var m in AllBookMetadata)
        {
            _bookNameToId[m.Eng] = m.Id;
            _bookNameToId[m.Indo] = m.Id;
            _bookNameToId[m.ShortEng] = m.Id;
            _bookNameToId[m.ShortIndo] = m.Id;
            if (m.Eng.Contains(" ")) _bookNameToId[m.Eng.Replace(" ", "")] = m.Id;
            if (m.Indo.Contains(" ")) _bookNameToId[m.Indo.Replace(" ", "")] = m.Id;
        }

        // Common abbreviation aliases for fast scripture navigation
        _bookNameToId["Gen"] = 1; _bookNameToId["Kej"] = 1;
        _bookNameToId["Exo"] = 2; _bookNameToId["Kel"] = 2;
        _bookNameToId["Lev"] = 3; _bookNameToId["Im"] = 3; _bookNameToId["Ima"] = 3;
        _bookNameToId["Num"] = 4; _bookNameToId["Bil"] = 4;
        _bookNameToId["Deu"] = 5; _bookNameToId["Ul"] = 5; _bookNameToId["Ula"] = 5;
        _bookNameToId["Jos"] = 6; _bookNameToId["Yos"] = 6;
        _bookNameToId["Jud"] = 7; _bookNameToId["Hak"] = 7;
        _bookNameToId["Rut"] = 8;
        _bookNameToId["1Sam"] = 9; _bookNameToId["1Sa"] = 9;
        _bookNameToId["2Sam"] = 10; _bookNameToId["2Sa"] = 10;
        _bookNameToId["1Raj"] = 11; _bookNameToId["1Ki"] = 11;
        _bookNameToId["2Raj"] = 12; _bookNameToId["2Ki"] = 12;
        _bookNameToId["1Taw"] = 13; _bookNameToId["1Ch"] = 13;
        _bookNameToId["2Taw"] = 14; _bookNameToId["2Ch"] = 14;
        _bookNameToId["Ezr"] = 15;
        _bookNameToId["Neh"] = 16;
        _bookNameToId["Est"] = 17;
        _bookNameToId["Ayb"] = 18; _bookNameToId["Job"] = 18;
        _bookNameToId["Mzm"] = 19; _bookNameToId["Psa"] = 19; _bookNameToId["Ps"] = 19;
        _bookNameToId["Ams"] = 20; _bookNameToId["Pro"] = 20;
        _bookNameToId["Pkh"] = 21; _bookNameToId["Ecc"] = 21;
        _bookNameToId["Kid"] = 22; _bookNameToId["Sng"] = 22;
        _bookNameToId["Yes"] = 23; _bookNameToId["Isa"] = 23;
        _bookNameToId["Yer"] = 24; _bookNameToId["Jer"] = 24;
        _bookNameToId["Rat"] = 25; _bookNameToId["Lam"] = 25;
        _bookNameToId["Yeh"] = 26; _bookNameToId["Ezk"] = 26;
        _bookNameToId["Dan"] = 27;
        _bookNameToId["Hos"] = 28;
        _bookNameToId["Yl"] = 29; _bookNameToId["Yoe"] = 29; _bookNameToId["Joe"] = 29;
        _bookNameToId["Am"] = 30; _bookNameToId["Amo"] = 30;
        _bookNameToId["Ob"] = 31; _bookNameToId["Oba"] = 31;
        _bookNameToId["Yun"] = 32; _bookNameToId["Jon"] = 32;
        _bookNameToId["Mik"] = 33; _bookNameToId["Mic"] = 33;
        _bookNameToId["Nah"] = 34;
        _bookNameToId["Hab"] = 35;
        _bookNameToId["Zef"] = 36; _bookNameToId["Zep"] = 36;
        _bookNameToId["Hag"] = 37;
        _bookNameToId["Za"] = 38; _bookNameToId["Zak"] = 38; _bookNameToId["Zec"] = 38;
        _bookNameToId["Mal"] = 39;
        _bookNameToId["Mat"] = 40;
        _bookNameToId["Mrk"] = 41; _bookNameToId["Mark"] = 41;
        _bookNameToId["Luk"] = 42; _bookNameToId["Luke"] = 42;
        _bookNameToId["Yoh"] = 43; _bookNameToId["John"] = 43; _bookNameToId["Jhn"] = 43;
        _bookNameToId["Kis"] = 44; _bookNameToId["Acts"] = 44; _bookNameToId["Act"] = 44;
        _bookNameToId["Rom"] = 45; _bookNameToId["Roma"] = 45;
        _bookNameToId["1Kor"] = 46; _bookNameToId["1Co"] = 46; _bookNameToId["1Cor"] = 46;
        _bookNameToId["2Kor"] = 47; _bookNameToId["2Co"] = 47; _bookNameToId["2Cor"] = 47;
        _bookNameToId["Gal"] = 48;
        _bookNameToId["Ef"] = 49; _bookNameToId["Efe"] = 49; _bookNameToId["Eph"] = 49;
        _bookNameToId["Flp"] = 50; _bookNameToId["Php"] = 50; _bookNameToId["Phil"] = 50;
        _bookNameToId["Kol"] = 51; _bookNameToId["Col"] = 51;
        _bookNameToId["1Tes"] = 52; _bookNameToId["1Th"] = 52;
        _bookNameToId["2Tes"] = 53; _bookNameToId["2Th"] = 53;
        _bookNameToId["1Tim"] = 54; _bookNameToId["1Ti"] = 54;
        _bookNameToId["2Tim"] = 55; _bookNameToId["2Ti"] = 55;
        _bookNameToId["Tit"] = 56;
        _bookNameToId["Flm"] = 57; _bookNameToId["Phm"] = 57;
        _bookNameToId["Ibr"] = 58; _bookNameToId["Heb"] = 58;
        _bookNameToId["Yak"] = 59; _bookNameToId["Jas"] = 59;
        _bookNameToId["1Pet"] = 60; _bookNameToId["1Pe"] = 60;
        _bookNameToId["2Pet"] = 61; _bookNameToId["2Pe"] = 61;
        _bookNameToId["1Yoh"] = 62; _bookNameToId["1Jn"] = 62;
        _bookNameToId["2Yoh"] = 63; _bookNameToId["2Jn"] = 63;
        _bookNameToId["3Yoh"] = 64; _bookNameToId["3Jn"] = 64;
        _bookNameToId["Yud"] = 65; _bookNameToId["Jud"] = 65;
        _bookNameToId["Why"] = 66; _bookNameToId["Rev"] = 66; _bookNameToId["Wahyu"] = 66; _bookNameToId["Revelation"] = 66;

        _books = GetBooks("KJV");
    }

    private void LoadOrSeedData()
    {
        // Songs
        if (File.Exists(_songsFile))
        {
            try
            {
                var json = File.ReadAllText(_songsFile);
                _songs = JsonSerializer.Deserialize<List<Song>>(json) ?? new();
                bool updated = false;
                foreach (var song in _songs)
                {
                    if (song.FontSize <= 0 || song.FontSize == 10)
                    {
                        song.FontSize = 13;
                        updated = true;
                    }
                }
                if (updated)
                {
                    SaveSongsToFile();
                }
            }
            catch { _songs = new(); }
        }
        else
        {
            // Check if there was an existing songs.json in legacy Data folder
            string legacySongs = Path.Combine(_dataDir, "songs.json");
            if (File.Exists(legacySongs))
            {
                try
                {
                    var json = File.ReadAllText(legacySongs);
                    var oldSongs = JsonSerializer.Deserialize<List<Song>>(json);
                    if (oldSongs != null && oldSongs.Count > 0)
                    {
                        _songs = oldSongs;
                        SaveSongsToFile();
                        goto LoadMediaSection;
                    }
                }
                catch { }
            }

            // 3. Clean fresh start: songs list starts empty so user can use their own database
            _songs = new List<Song>();
            SaveSongsToFile();
        }

    LoadMediaSection:

        // Media
        if (File.Exists(_mediaFile))
        {
            try
            {
                var json = File.ReadAllText(_mediaFile);
                var loaded = JsonSerializer.Deserialize<List<MediaItem>>(json) ?? new();
                _media = loaded.Where(m => !string.IsNullOrWhiteSpace(m.FilePath) && File.Exists(m.FilePath)).ToList();
                if (_media.Count != loaded.Count)
                {
                    SaveMediaToFile();
                }
            }
            catch { _media = GetSeedMedia(); }
        }
        else
        {
            _media = GetSeedMedia();
            SaveMediaToFile();
        }

        // Presentations
        if (File.Exists(_presentationsFile))
        {
            try
            {
                var json = File.ReadAllText(_presentationsFile);
                var loaded = JsonSerializer.Deserialize<List<PresentationDocItem>>(json) ?? new();
                _presentationDocs = loaded.Where(p => !string.IsNullOrWhiteSpace(p.FilePath) && File.Exists(p.FilePath)).ToList();
                if (_presentationDocs.Count != loaded.Count)
                {
                    SavePresentationDocsToFile();
                }
            }
            catch { _presentationDocs = new(); }
        }

        // Backgrounds (for Songs and Scriptures only)
        if (File.Exists(_backgroundsFile))
        {
            try
            {
                var json = File.ReadAllText(_backgroundsFile);
                var loaded = JsonSerializer.Deserialize<List<BackgroundItem>>(json) ?? new();
                _backgrounds = loaded.Where(b => string.IsNullOrWhiteSpace(b.FilePath) || b.FilePath.StartsWith("#") || File.Exists(b.FilePath)).ToList();
                if (_backgrounds.Count != loaded.Count)
                {
                    SaveBackgroundsToFile();
                }
            }
            catch { _backgrounds = new(); }
        }
        else
        {
            _backgrounds = new();
            SaveBackgroundsToFile();
        }

        // Scriptures
        string appDirBibles = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "bibles.json");
        if (File.Exists(_biblesFile))
        {
            if (new FileInfo(_biblesFile).Length < 1_000_000 && File.Exists(appDirBibles) && new FileInfo(appDirBibles).Length > 1_000_000)
            {
                try { File.Copy(appDirBibles, _biblesFile, true); } catch { }
            }
        }
        else if (File.Exists(appDirBibles))
        {
            try { File.Copy(appDirBibles, _biblesFile, true); } catch { }
        }

        if (File.Exists(_biblesFile))
        {
            try
            {
                var json = File.ReadAllText(_biblesFile);
                _verses = JsonSerializer.Deserialize<List<BibleVerse>>(json) ?? new();
            }
            catch { _verses = GetSeedVerses(); }
        }
        else
        {
            _verses = GetSeedVerses();
            SaveBiblesToFile();
        }

        BuildBibleIndices();
    }

    private void BuildBibleIndices()
    {
        _chapterLookup.Clear();
        foreach (var v in _verses)
        {
            if (v.BookId <= 0 && _bookNameToId.TryGetValue(v.Book, out int resolvedId))
            {
                v.BookId = resolvedId;
            }

            var key = (v.Version.ToUpperInvariant(), v.BookId, v.Chapter);
            if (!_chapterLookup.TryGetValue(key, out var list))
            {
                list = new List<BibleVerse>();
                _chapterLookup[key] = list;
            }
            list.Add(v);
        }
    }

    public AppSettings GetSettings() => _settings;

    public void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        SaveSettingsToFile();
    }

    private void SaveSettingsToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    public List<Song> GetSongs() => _songs.OrderBy(s => s.Title).ToList();

    public void SaveSong(Song song)
    {
        var existing = _songs.FirstOrDefault(s => s.Id == song.Id);
        if (existing != null)
        {
            int index = _songs.IndexOf(existing);
            _songs[index] = song;
        }
        else
        {
            _songs.Add(song);
        }
        SaveSongsToFile();
    }

    public void DeleteSong(string songId)
    {
        _songs.RemoveAll(s => s.Id == songId);
        SaveSongsToFile();
    }

    public void AddSongs(IEnumerable<Song> songs, bool replaceAll = false)
    {
        if (replaceAll)
        {
            _songs.Clear();
        }

        foreach (var song in songs)
        {
            var existing = _songs.FirstOrDefault(s => s.Title.Equals(song.Title, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.LyricsRaw = song.LyricsRaw;
                existing.Author = song.Author;
                existing.Copyright = song.Copyright;
            }
            else
            {
                _songs.Add(song);
            }
        }
        SaveSongsToFile();
    }

    public void ApplyGlobalFontToAllSongs(string fontFamily, int fontSize, bool isBold)
    {
        foreach (var song in _songs)
        {
            if (fontSize > 0) song.FontSize = fontSize;
            if (!string.IsNullOrWhiteSpace(fontFamily)) song.FontFamily = fontFamily;
        }
        SaveSongsToFile();
    }

    public void LoadSampleSongs()
    {
        var samples = GetSeedSongs();
        AddSongs(samples, replaceAll: false);
    }

    public void SwitchSongsDirectory(string newDirectory)
    {
        AppPaths.SetSongsDirectory(newDirectory);
        _songsFile = Path.Combine(AppPaths.SongsDirectory, "songs.json");
        _settings.CustomSongDatabaseDirectory = newDirectory;
        SaveSettingsToFile();

        if (File.Exists(_songsFile))
        {
            try
            {
                var json = File.ReadAllText(_songsFile);
                _songs = JsonSerializer.Deserialize<List<Song>>(json) ?? new();
            }
            catch { _songs = new(); }
        }
        else
        {
            _songs = new List<Song>();
            SaveSongsToFile();
        }
    }

    public int ResolveBookId(string bookNameOrAbbr)
    {
        if (string.IsNullOrWhiteSpace(bookNameOrAbbr)) return 0;
        string trimmed = bookNameOrAbbr.Trim();
        if (_bookNameToId.TryGetValue(trimmed, out int bId)) return bId;

        string clean = trimmed.Replace(" ", "");
        if (_bookNameToId.TryGetValue(clean, out bId)) return bId;

        var meta = AllBookMetadata.FirstOrDefault(m =>
            m.Eng.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            m.Indo.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            m.ShortEng.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            m.ShortIndo.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            m.Eng.Replace(" ", "").Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            m.Indo.Replace(" ", "").Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            m.Eng.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) ||
            m.Indo.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith(m.ShortEng, StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith(m.ShortIndo, StringComparison.OrdinalIgnoreCase));

        return meta.Id;
    }

    public List<BibleVerse> SearchScriptures(string query, string version = "KJV")
    {
        string ver = string.IsNullOrEmpty(version) ? "KJV" : version.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(query))
        {
            return _verses.Where(v => v.Version.Equals(ver, StringComparison.OrdinalIgnoreCase)).Take(20).ToList();
        }

        string q = query.Trim();

        // Smart reference matching: "Joh 3:16", "Wahyu 6 6", "Genesis 1 1", "Kejadian 1:1", "1 Kor 13 4", "Yoh 3", "Gen 1"
        var refMatch = System.Text.RegularExpressions.Regex.Match(q, @"^([1-3]?\s*[A-Za-z\-]+(?:\s+[A-Za-z\-]+)*)\s*(\d+)(?:\s*(?:[:.,]|\s+|ayat|ay\.?|verse|v\.?)\s*(\d+)(?:\s*-\s*\d+)?)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (refMatch.Success)
        {
            string bookPart = refMatch.Groups[1].Value.Trim();
            int bId = ResolveBookId(bookPart);
            if (bId > 0 && int.TryParse(refMatch.Groups[2].Value, out int ch))
            {
                if (_chapterLookup.TryGetValue((ver, bId, ch), out var chVerses))
                {
                    if (refMatch.Groups[3].Success && int.TryParse(refMatch.Groups[3].Value, out int vr))
                    {
                        var exact = chVerses.Where(v => v.Verse == vr).ToList();
                        if (exact.Count > 0) return exact;
                    }
                    return chVerses.OrderBy(v => v.Verse).ToList();
                }
            }
        }

        // Full-text search
        var lower = q.ToLowerInvariant();
        return _verses.Where(v =>
            v.Version.Equals(ver, StringComparison.OrdinalIgnoreCase) &&
            (v.Reference.ToLowerInvariant().Contains(lower) || v.Text.ToLowerInvariant().Contains(lower))
        ).Take(50).ToList();
    }

    public List<BibleVerse> GetChapterVerses(string book, int chapter, string version = "KJV")
    {
        string ver = string.IsNullOrEmpty(version) ? "KJV" : version.ToUpperInvariant();
        int bookId = ResolveBookId(book);

        if (bookId > 0 && _chapterLookup.TryGetValue((ver, bookId, chapter), out var list))
        {
            return list.OrderBy(v => v.Verse).ToList();
        }

        return _verses.Where(v =>
            (v.Book.Equals(book, StringComparison.OrdinalIgnoreCase) || (bookId > 0 && v.BookId == bookId)) &&
            v.Chapter == chapter &&
            (string.IsNullOrEmpty(version) || v.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
        ).OrderBy(v => v.Verse).ToList();
    }

    public List<BibleBook> GetBooks(string version = "KJV")
    {
        bool isTb = string.Equals(version, "TB", StringComparison.OrdinalIgnoreCase);
        return AllBookMetadata.Select(m => new BibleBook
        {
            Id = m.Id,
            Name = isTb ? m.Indo : m.Eng,
            EnglishName = m.Eng,
            IndonesianName = m.Indo,
            ShortName = isTb ? m.ShortIndo : m.ShortEng,
            ChapterCount = m.Chapters,
            IsOldTestament = m.IsOT
        }).ToList();
    }

    public bool HasVersion(string version)
    {
        return _verses.Any(v => v.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
    }

    public int ImportBible(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath)) return 0;
        var json = File.ReadAllText(jsonFilePath, System.Text.Encoding.UTF8);
        var newVerses = JsonSerializer.Deserialize<List<BibleVerse>>(json);
        if (newVerses == null || newVerses.Count == 0) return 0;

        string importedVer = newVerses[0].Version.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(importedVer)) importedVer = "TB";

        foreach (var v in newVerses)
        {
            if (string.IsNullOrWhiteSpace(v.Version)) v.Version = importedVer;
            if (v.BookId <= 0 && _bookNameToId.TryGetValue(v.Book, out int resolvedId))
            {
                v.BookId = resolvedId;
            }
        }

        // Remove existing verses of the same version to prevent duplicates
        _verses.RemoveAll(v => v.Version.Equals(importedVer, StringComparison.OrdinalIgnoreCase));
        _verses.AddRange(newVerses);

        SaveBiblesToFile();
        BuildBibleIndices();
        return newVerses.Count;
    }

    public List<MediaItem> GetMediaItems() => _media;

    public void AddMediaItem(MediaItem media)
    {
        _media.Add(media);
        SaveMediaToFile();
    }

    public void DeleteMediaItem(string mediaId)
    {
        _media.RemoveAll(m => m.Id == mediaId);
        SaveMediaToFile();
    }

    public List<PresentationDocItem> GetPresentationDocs() => _presentationDocs.OrderByDescending(p => p.DateAdded).ToList();

    public void AddPresentationDoc(PresentationDocItem doc)
    {
        _presentationDocs.RemoveAll(p => p.FilePath.Equals(doc.FilePath, StringComparison.OrdinalIgnoreCase));
        _presentationDocs.Insert(0, doc);
        SavePresentationDocsToFile();
    }

    public void DeletePresentationDoc(string docId)
    {
        var toRemove = _presentationDocs.FirstOrDefault(p => p.Id == docId);
        if (toRemove != null && !string.IsNullOrWhiteSpace(toRemove.FilePath))
        {
            LuxPresenter.Services.Presentation.PresentationEngineService.DeleteCacheForDocument(toRemove.FilePath);
        }

        _presentationDocs.RemoveAll(p => p.Id == docId);
        SavePresentationDocsToFile();
    }

    private void SavePresentationDocsToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_presentationDocs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_presentationsFile, json);
        }
        catch { }
    }

    public List<BackgroundItem> GetBackgrounds() => _backgrounds;

    public void AddBackground(BackgroundItem item)
    {
        _backgrounds.Add(item);
        SaveBackgroundsToFile();
    }

    public void AddBackgrounds(IEnumerable<BackgroundItem> items)
    {
        _backgrounds.AddRange(items);
        SaveBackgroundsToFile();
    }

    public void DeleteBackground(string id)
    {
        _backgrounds.RemoveAll(b => b.Id == id);
        SaveBackgroundsToFile();
    }

    public void SaveBackgrounds(IEnumerable<BackgroundItem> items)
    {
        _backgrounds = items.ToList();
        SaveBackgroundsToFile();
    }

    public void SaveSchedule(string filePath, IEnumerable<ScheduleItem> items)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    public List<ScheduleItem> LoadSchedule(string filePath)
    {
        if (!File.Exists(filePath)) return new();
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<ScheduleItem>>(json) ?? new();
    }

    private void SaveSongsToFile()
    {
        var json = JsonSerializer.Serialize(_songs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_songsFile, json);
    }

    private void SaveMediaToFile()
    {
        var json = JsonSerializer.Serialize(_media, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_mediaFile, json);
    }

    private void SaveBackgroundsToFile()
    {
        var json = JsonSerializer.Serialize(_backgrounds, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_backgroundsFile, json);
    }

    private void SaveBiblesToFile()
    {
        var json = JsonSerializer.Serialize(_verses, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_biblesFile, json);
    }

    private List<Song> GetSeedSongs()
    {
        return new List<Song>
        {
            new()
            {
                Title = "O Come All Ye Faithful",
                Author = "C. Frederick Oakeley",
                Copyright = "Public Domain",
                Key = "G",
                LyricsRaw = "[Verse 1]\nO come all ye faithful,\nJoyful and triumphant,\nO come ye, O come ye to Bethlehem;\nCome and behold Him,\nBorn the King of angels:\n\n[Chorus 1]\nO come, let us adore Him,\nO come, let us adore Him,\nO come, let us adore Him,\nChrist the Lord.\n\n[Verse 2]\nGod of God,\nLight of light,\nLo! He abhors not the virgin's womb;\nVery God,\nBegotten not created:\n\n[Verse 3]\nSing, choirs of angels,\nSing in exultation,\nSing all ye citizens of heaven above:\nGlory to God,\nIn the highest:"
            },
            new()
            {
                Title = "O Come O Come Emmanuel",
                Author = "John Mason Neale",
                Copyright = "Public Domain",
                Key = "Em",
                LyricsRaw = "[Verse 1]\nO come, O come, Emmanuel,\nAnd ransom captive Israel,\nThat mourns in lonely exile here,\nUntil the Son of God appear.\n\n[Chorus]\nRejoice! Rejoice! Emmanuel\nShall come to thee, O Israel."
            },
            new()
            {
                Title = "O Could I Speak The Matchless Worth",
                Author = "Samuel Medley",
                Copyright = "Public Domain",
                Key = "A",
                LyricsRaw = "[Verse 1]\nO could I speak the matchless worth,\nO could I sound the glories forth\nWhich in my Savior shine!"
            },
            new()
            {
                Title = "O For A Faith That Will Not Shrink",
                Author = "William H. Bathurst",
                Copyright = "Public Domain",
                Key = "F",
                LyricsRaw = "[Verse 1]\nO for a faith that will not shrink,\nTho' pressed by ev'ry foe,\nThat will not tremble on the brink\nOf any earthly woe!"
            },
            new()
            {
                Title = "Amazing Grace",
                Author = "John Newton",
                Copyright = "Public Domain",
                Key = "G",
                LyricsRaw = "[Verse 1]\nAmazing grace! How sweet the sound\nThat saved a wretch like me!\nI once was lost, but now am found;\nWas blind, but now I see.\n\n[Verse 2]\n'Twas grace that taught my heart to fear,\nAnd grace my fears relieved;\nHow precious did that grace appear\nThe hour I first believed.\n\n[Verse 3]\nThrough many dangers, toils and snares,\nI have already come;\n'Tis grace hath brought me safe thus far,\nAnd grace will lead me home.\n\n[Verse 4]\nWhen we've been there ten thousand years,\nBright shining as the sun,\nWe've no less days to sing God's praise\nThan when we'd first begun."
            },
            new()
            {
                Title = "Holy, Holy, Holy! Lord God Almighty",
                Author = "Reginald Heber",
                Copyright = "Public Domain",
                Key = "E",
                LyricsRaw = "[Verse 1]\nHoly, holy, holy! Lord God Almighty!\nEarly in the morning our song shall rise to Thee;\nHoly, holy, holy, merciful and mighty!\nGod in three Persons, blessed Trinity!\n\n[Verse 2]\nHoly, holy, holy! All the saints adore Thee,\nCasting down their golden crowns around the glassy sea;\nCherubim and seraphim falling down before Thee,\nWhich wert, and art, and evermore shalt be.\n\n[Verse 3]\nHoly, holy, holy! Though the darkness hide Thee,\nThough the eye of sinful man Thy glory may not see;\nOnly Thou art holy; there is none beside Thee,\nPerfect in power, in love, and purity."
            },
            new()
            {
                Title = "It Is Well With My Soul",
                Author = "Horatio Spafford",
                Copyright = "Public Domain",
                Key = "C",
                LyricsRaw = "[Verse 1]\nWhen peace, like a river, attendeth my way,\nWhen sorrows like sea billows roll;\nWhatever my lot, Thou hast taught me to say,\nIt is well, it is well with my soul.\n\n[Chorus]\nIt is well with my soul,\nIt is well, it is well with my soul.\n\n[Verse 2]\nThough Satan should buffet, though trials should come,\nLet this blest assurance control,\nThat Christ has regarded my helpless estate,\nAnd hath shed His own blood for my soul.\n\n[Verse 3]\nAnd Lord, haste the day when my faith shall be sight,\nThe clouds be rolled back as a scroll;\nThe trump shall resound, and the Lord shall descend,\nEven so, it is well with my soul."
            }
        };
    }

    private List<MediaItem> GetSeedMedia()
    {
        return new List<MediaItem>();
    }

    private List<BibleVerse> GetSeedVerses()
    {
        return new List<BibleVerse>
        {
            new() { Book = "Genesis", Chapter = 1, Verse = 1, Text = "In the beginning God created the heaven and the earth.", Version = "KJV" },
            new() { Book = "Genesis", Chapter = 1, Verse = 2, Text = "And the earth was without form, and void; and darkness was upon the face of the deep. And the Spirit of God moved upon the face of the waters.", Version = "KJV" },
            new() { Book = "Genesis", Chapter = 1, Verse = 3, Text = "And God said, Let there be light: and there was light.", Version = "KJV" },
            new() { Book = "Genesis", Chapter = 1, Verse = 4, Text = "And God saw the light, that it was good: and God divided the light from the darkness.", Version = "KJV" },
            new() { Book = "Genesis", Chapter = 1, Verse = 5, Text = "And God called the light Day, and the darkness he called Night. And the evening and the morning were the first day.", Version = "KJV" },

            new() { Book = "Psalms", Chapter = 23, Verse = 1, Text = "The LORD is my shepherd; I shall not want.", Version = "KJV" },
            new() { Book = "Psalms", Chapter = 23, Verse = 2, Text = "He maketh me to lie down in green pastures: he leadeth me beside the still waters.", Version = "KJV" },
            new() { Book = "Psalms", Chapter = 23, Verse = 3, Text = "He restoreth my soul: he leadeth me in the paths of righteousness for his name's sake.", Version = "KJV" },
            new() { Book = "Psalms", Chapter = 23, Verse = 4, Text = "Yea, though I walk through the valley of the shadow of death, I will fear no evil: for thou art with me; thy rod and thy staff they comfort me.", Version = "KJV" },
            new() { Book = "Psalms", Chapter = 23, Verse = 5, Text = "Thou preparest a table before me in the presence of mine enemies: thou anointest my head with oil; my cup runneth over.", Version = "KJV" },
            new() { Book = "Psalms", Chapter = 23, Verse = 6, Text = "Surely goodness and mercy shall follow me all the days of my life: and I will dwell in the house of the LORD for ever.", Version = "KJV" },

            new() { Book = "John", Chapter = 3, Verse = 16, Text = "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life.", Version = "KJV" },
            new() { Book = "John", Chapter = 3, Verse = 17, Text = "For God sent not his Son into the world to condemn the world; but that the world through him might be saved.", Version = "KJV" },

            new() { Book = "Romans", Chapter = 8, Verse = 28, Text = "And we know that all things work together for good to them that love God, to them who are the called according to his purpose.", Version = "KJV" },
            new() { Book = "Philippians", Chapter = 4, Verse = 13, Text = "I can do all things through Christ which strengtheneth me.", Version = "KJV" },

            // Terjemahan Baru (TB) samples
            new() { Book = "Genesis", Chapter = 1, Verse = 1, Text = "Pada mulanya Allah menciptakan langit dan bumi.", Version = "TB" },
            new() { Book = "Psalms", Chapter = 23, Verse = 1, Text = "TUHAN adalah gembalaku, takkan kekurangan aku.", Version = "TB" },
            new() { Book = "John", Chapter = 3, Verse = 16, Text = "Karena begitu besar kasih Allah akan dunia ini, sehingga Ia telah mengaruniakan Anak-Nya yang tunggal, supaya setiap orang yang percaya kepada-Nya tidak binasa, melainkan beroleh hidup yang kekal.", Version = "TB" },
            new() { Book = "Philippians", Chapter = 4, Verse = 13, Text = "Segala perkara dapat kutanggung di dalam Dia yang memberi kekuatan kepadaku.", Version = "TB" }
        };
    }
}
