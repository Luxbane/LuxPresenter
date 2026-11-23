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
            var startupContext = await StartupCoordinator.InitializeAsync((progress, status) => splash.ReportProgress(progress, status));

            splash.ReportProgress(96, "Loading application interface...");
            await Task.Yield();

            var mainWindow = new MainWindow(startupContext);
            MainWindow = mainWindow;

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
