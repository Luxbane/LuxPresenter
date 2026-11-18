using LuxPresenter.Models;

namespace LuxPresenter.Services;

public interface IDataService
{
    List<Song> GetSongs();
    void SaveSong(Song song);
    void DeleteSong(string songId);
    void AddSongs(IEnumerable<Song> songs, bool replaceAll = false);
    void ApplyGlobalFontToAllSongs(string fontFamily, int fontSize, bool isBold);
    void LoadSampleSongs();
    void SwitchSongsDirectory(string newDirectory);

    List<BibleVerse> SearchScriptures(string query, string version = "KJV");
    List<BibleVerse> GetChapterVerses(string book, int chapter, string version = "KJV");
    List<BibleBook> GetBooks(string version = "KJV");
    int ResolveBookId(string bookNameOrAbbr);
    bool HasVersion(string version);
    int ImportBible(string jsonFilePath);

    List<MediaItem> GetMediaItems();
    void AddMediaItem(MediaItem media);
    void DeleteMediaItem(string mediaId);
    void CleanupOrphanedMedia();

    List<PresentationDocItem> GetPresentationDocs();
    void AddPresentationDoc(PresentationDocItem doc);
    void DeletePresentationDoc(string docId);

    List<BackgroundItem> GetBackgrounds();
    void AddBackground(BackgroundItem item);
    void AddBackgrounds(IEnumerable<BackgroundItem> items);
    void DeleteBackground(string id);
    void SaveBackgrounds(IEnumerable<BackgroundItem> items);

    void SaveSchedule(string filePath, IEnumerable<ScheduleItem> items);
    List<ScheduleItem> LoadSchedule(string filePath);

    AppSettings GetSettings();
    void SaveSettings(AppSettings settings);
}
