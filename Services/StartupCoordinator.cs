using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Services;

/// <summary>
/// Encapsulates all ready-to-use core services and settings needed by MainWindow.
/// </summary>
public class StartupContext
{
    public IDataService DataService { get; }
    public IProjectionService ProjectionService { get; }
    public StartupCleanupService CleanupService { get; }
    public DialogService DialogService { get; }
    public MainViewModel ViewModel { get; }
    public AppSettings Settings { get; }

    public StartupContext(
        IDataService dataService,
        IProjectionService projectionService,
        StartupCleanupService cleanupService,
        DialogService dialogService,
        MainViewModel viewModel,
        AppSettings settings)
    {
        DataService = dataService;
        ProjectionService = projectionService;
        CleanupService = cleanupService;
        DialogService = dialogService;
        ViewModel = viewModel;
        Settings = settings;
    }
}

/// <summary>
/// Coordinates the application startup pipeline: loading settings, applying theme,
/// detecting displays, initializing core services, prewarming projection, and running cleanup.
/// Decouples startup orchestration from the UI window constructor.
/// </summary>
public static class StartupCoordinator
{
    /// <summary>
    /// Executes the startup initialization sequence asynchronously, yielding between
    /// steps to allow the splash screen UI to smoothly animate and repaint.
    /// </summary>
    public static async Task<StartupContext> InitializeAsync(Action<double, string>? reportProgress = null)
    {
        reportProgress?.Invoke(10, "Loading application settings...");
        await Task.Yield();

        var dataService = new DataService();
        AppSettings settings;
        try
        {
            settings = dataService.GetSettings();
            ThemeService.ApplyTheme(settings.IsDarkMode);
            reportProgress?.Invoke(22, "Applying appearance settings...");
        }
        catch
        {
            settings = new AppSettings();
            reportProgress?.Invoke(22, "Using default appearance settings...");
        }
        await Task.Yield();

        reportProgress?.Invoke(32, "Detecting connected displays...");
        await Task.Yield();
        LogConnectedDisplays();

        reportProgress?.Invoke(48, "Loading presentation and media engine...");
        await Task.Yield();
        var cleanupService = new StartupCleanupService(dataService);
        var dialogService = new DialogService();

        reportProgress?.Invoke(62, "Loading songs, media, presentations, and Bible data...");
        await Task.Yield();
        var projectionService = new ProjectionService();

        reportProgress?.Invoke(78, "Preparing preview and live output...");
        await Task.Yield();
        var viewModel = new MainViewModel(dataService, projectionService);

        reportProgress?.Invoke(88, "Cleaning temporary presentation data...");
        await Task.Yield();
        cleanupService.CleanupOrphanedData();

        reportProgress?.Invoke(94, "Pre-warming display shaders & hardware acceleration...");
        await Task.Yield();
        projectionService.Prewarm();

        return new StartupContext(
            dataService,
            projectionService,
            cleanupService,
            dialogService,
            viewModel,
            settings
        );
    }

    /// <summary>
    /// Synchronous initialization sequence for fallback and testing scenarios.
    /// </summary>
    public static StartupContext Initialize(Action<double, string>? reportProgress = null)
    {
        reportProgress?.Invoke(10, "Loading application settings...");
        var dataService = new DataService();
        AppSettings settings;
        try
        {
            settings = dataService.GetSettings();
            ThemeService.ApplyTheme(settings.IsDarkMode);
            reportProgress?.Invoke(22, "Applying appearance settings...");
        }
        catch
        {
            settings = new AppSettings();
            reportProgress?.Invoke(22, "Using default appearance settings...");
        }

        reportProgress?.Invoke(32, "Detecting connected displays...");
        LogConnectedDisplays();

        reportProgress?.Invoke(48, "Loading presentation and media engine...");
        var cleanupService = new StartupCleanupService(dataService);
        var dialogService = new DialogService();

        reportProgress?.Invoke(62, "Loading songs, media, presentations, and Bible data...");
        var projectionService = new ProjectionService();

        reportProgress?.Invoke(78, "Preparing preview and live output...");
        var viewModel = new MainViewModel(dataService, projectionService);

        reportProgress?.Invoke(88, "Cleaning temporary presentation data...");
        cleanupService.CleanupOrphanedData();

        reportProgress?.Invoke(94, "Pre-warming display shaders & hardware acceleration...");
        projectionService.Prewarm();

        return new StartupContext(
            dataService,
            projectionService,
            cleanupService,
            dialogService,
            viewModel,
            settings
        );
    }

    private static void LogConnectedDisplays()
    {
        try
        {
            var screens = DisplayHelper.GetScreens();
            var lines = new List<string> { $"[Display Log - {DateTime.Now}]" };
            foreach (var screen in screens)
            {
                lines.Add($"Screen {screen.Index}: {screen.DeviceName}, Primary={screen.IsPrimary}, Physical={screen.PhysicalWidth}x{screen.PhysicalHeight}@({screen.PhysicalLeft},{screen.PhysicalTop}), DIP={screen.DipWidth}x{screen.DipHeight}@({screen.DipLeft},{screen.DipTop}), Scale={screen.DpiScaleX}x{screen.DpiScaleY}");
            }
            File.WriteAllLines("display_log.txt", lines);
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText("display_log.txt", $"Error getting screens: {ex}");
            }
            catch { }
        }
    }
}
