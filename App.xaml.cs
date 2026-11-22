using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using LuxPresenter.Services;
using LuxPresenter.Views;

namespace LuxPresenter;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Application Error:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Fatal Error:\n{ex.Message}\n\n{ex.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        try
        {
            splash.ReportProgress(8, "Loading application settings...");
            await Task.Yield();

            try
            {
                var dataService = new DataService();
                var settings = dataService.GetSettings();
                ThemeService.ApplyTheme(settings.IsDarkMode);
                splash.ReportProgress(20, "Applying appearance settings...");
            }
            catch
            {
                splash.ReportProgress(20, "Using default appearance settings...");
            }
            await Task.Yield();

            splash.ReportProgress(28, "Detecting connected displays...");
            await Task.Yield();
            try
            {
                var screens = DisplayHelper.GetScreens();
                var lines = new List<string> { $"[Display Log - {DateTime.Now}]" };
                foreach (var screen in screens)
                {
                    lines.Add($"Screen {screen.Index}: {screen.DeviceName}, Primary={screen.IsPrimary}, Physical={screen.PhysicalWidth}x{screen.PhysicalHeight}@({screen.PhysicalLeft},{screen.PhysicalTop}), DIP={screen.DipWidth}x{screen.DipHeight}@({screen.DipLeft},{screen.DipTop}), Scale={screen.DpiScaleX}x{screen.DpiScaleY}");
                }
                System.IO.File.WriteAllLines("display_log.txt", lines);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("display_log.txt", $"Error getting screens: {ex}");
            }

            splash.ReportProgress(40, "Loading media engine (libmpv-2.dll)...");
            await Task.Yield();
            splash.ReportProgress(48, "Preparing presentation workspace...");

            var mainWindow = new MainWindow((progress, status) => splash.ReportProgress(progress, status));
            MainWindow = mainWindow;

            splash.ReportProgress(98, "Finalizing startup...");
            await Task.Yield();
            splash.ReportProgress(100, "Starting LuxPresenter...");
            await Task.Delay(150);

            splash.Close();
            mainWindow.Show();
            mainWindow.Activate();
        }
        catch (Exception ex)
        {
            splash.ReportProgress(100, "Unable to start LuxPresenter.");
            MessageBox.Show($"LuxPresenter could not start:\n\n{ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            splash.Close();
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            LuxPresenter.Services.Presentation.LibreOfficeSlideshowService.Instance.Dispose();
        }
        catch { }
        base.OnExit(e);
    }
}
