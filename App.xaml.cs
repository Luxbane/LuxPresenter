using System;
using System.Windows;
using LuxPresenter.Services;

namespace LuxPresenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var dataService = new DataService();
            var settings = dataService.GetSettings();
            ThemeService.ApplyTheme(settings.IsDarkMode);
        }
        catch { }

        try
        {
            var screens = DisplayHelper.GetScreens();
            var lines = new System.Collections.Generic.List<string> { $"[Display Log - {DateTime.Now}]" };
            foreach (var s in screens)
            {
                lines.Add($"Screen {s.Index}: {s.DeviceName}, Primary={s.IsPrimary}, Physical={s.PhysicalWidth}x{s.PhysicalHeight}@({s.PhysicalLeft},{s.PhysicalTop}), DIP={s.DipWidth}x{s.DipHeight}@({s.DipLeft},{s.DipTop}), Scale={s.DpiScaleX}x{s.DpiScaleY}");
            }
            System.IO.File.WriteAllLines("display_log.txt", lines);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("display_log.txt", $"Error getting screens: {ex}");
        }

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
