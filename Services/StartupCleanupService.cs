using System.Linq;
using LuxPresenter.Services.Presentation;

namespace LuxPresenter.Services;

/// <summary>
/// Handles startup and shutdown cleanup of orphaned presentation caches,
/// missing media entries, and unreferenced thumbnails.
/// </summary>
public class StartupCleanupService
{
    private readonly IDataService _dataService;

    public StartupCleanupService(IDataService dataService)
    {
        _dataService = dataService;
    }

    public void CleanupOrphanedData()
    {
        try
        {
            // 1. Presentation caches
            var activeDocs = _dataService.GetPresentationDocs().Select(d => d.FilePath);
            PresentationEngineService.CleanupOrphanedCaches(activeDocs);

            // 2. Media and Backgrounds missing from disk
            _dataService.CleanupOrphanedMedia();

            // 3. Thumbnails cache cleanup (delete any thumbnail not belonging to active media/backgrounds)
            var activeMedia = _dataService.GetMediaItems().Select(m => m.FilePath);
            var activeBackgrounds = _dataService.GetBackgrounds().Select(b => b.FilePath);
            var defaultBg = _dataService.GetSettings().LastUsedBackground;
            var allActivePaths = activeMedia.Concat(activeBackgrounds);
            if (!string.IsNullOrWhiteSpace(defaultBg))
            {
                allActivePaths = allActivePaths.Append(defaultBg);
            }

            ThumbnailService.CleanupOrphanedThumbnails(allActivePaths);
        }
        catch
        {
            // Best effort cleanup: do not crash application if cleanup fails
        }
    }
}
