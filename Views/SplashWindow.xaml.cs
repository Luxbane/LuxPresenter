using System;
using System.Windows;

namespace LuxPresenter.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void ReportProgress(double progress, string status)
    {
        LoadingProgress.Value = Math.Clamp(progress, 0, 100);
        LoadingStatus.Text = status;
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
    }
}
