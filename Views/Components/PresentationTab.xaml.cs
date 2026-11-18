using System.Windows.Controls;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class PresentationTab : UserControl
{
    public PresentationTab()
    {
        InitializeComponent();
    }

    private void OnSlideClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is PresentationSlide slide && DataContext is PresentationViewModel vm)
        {
            vm.SelectedSlide = slide;
        }
    }

    private void OnSlideDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is PresentationSlide slide && DataContext is PresentationViewModel vm)
        {
            vm.DoubleClickSlideCommand.Execute(slide);
        }
    }
}
