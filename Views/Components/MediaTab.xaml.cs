using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class MediaTab : UserControl
{
    public MediaTab()
    {
        InitializeComponent();
    }

    private void OnMediaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MediaViewModel vm && sender is ListBoxItem item && item.DataContext is MediaItem media)
        {
            vm.DoubleClickCommand.Execute(media);
        }
    }

    private void OnMediaClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MediaViewModel vm && sender is ListBoxItem item && item.DataContext is MediaItem media)
        {
            vm.UserSelectMedia(media);
        }
    }

    private void OnMediaKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back)
        {
            if (DataContext is MediaViewModel vm && vm.DeleteMediaCommand.CanExecute(null))
            {
                vm.DeleteMediaCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnMediaOptionsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MediaViewModel vm && sender is FrameworkElement fe)
        {
            var cm = new ContextMenu();

            var gridItem = new MenuItem
            {
                Header = "Grid View",
                IsCheckable = true,
                IsChecked = vm.IsGridView
            };
            gridItem.Click += (s, args) => vm.IsGridView = true;

            var listItem = new MenuItem
            {
                Header = "List View",
                IsCheckable = true,
                IsChecked = !vm.IsGridView
            };
            listItem.Click += (s, args) => vm.IsGridView = false;

            cm.Items.Add(gridItem);
            cm.Items.Add(listItem);

            cm.PlacementTarget = fe;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            cm.IsOpen = true;
        }
    }
}
