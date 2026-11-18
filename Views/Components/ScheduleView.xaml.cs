using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class ScheduleView : UserControl
{
    private Point _dragStartPoint;

    public ScheduleView()
    {
        InitializeComponent();
    }

    private void OnListBoxItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ScheduleViewModel vm && sender is ListBoxItem item && item.DataContext is ScheduleItem scheduleItem)
        {
            vm.DoubleClickCommand.Execute(scheduleItem);
        }
    }

    private void OnScheduleItemClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ScheduleViewModel vm && sender is ListBoxItem item && item.DataContext is ScheduleItem scheduleItem)
        {
            vm.UserSelectItem(scheduleItem);
        }
    }

    private void OnSchedulePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnScheduleMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (ScheduleListBox.SelectedItem is ScheduleItem item)
                {
                    DragDrop.DoDragDrop(ScheduleListBox, item, DragDropEffects.Move);
                }
            }
        }
    }

    private void OnScheduleDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Song)) ||
            e.Data.GetDataPresent(typeof(ScheduleItem)) ||
            e.Data.GetDataPresent(typeof(PresentationItem)))
        {
            e.Effects = e.Data.GetDataPresent(typeof(ScheduleItem)) ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void OnScheduleDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not ScheduleViewModel vm) return;

        int targetIdx = Math.Clamp(GetDropIndex(e), 0, vm.Items.Count);

        if (e.Data.GetData(typeof(Song)) is Song song)
        {
            var pres = song.ToPresentationItem();
            var item = ScheduleItem.FromPresentation(pres);
            vm.Items.Insert(targetIdx, item);
            vm.SelectedItem = item;
            e.Handled = true;
        }
        else if (e.Data.GetData(typeof(PresentationItem)) is PresentationItem pres)
        {
            var item = ScheduleItem.FromPresentation(pres);
            vm.Items.Insert(targetIdx, item);
            vm.SelectedItem = item;
            e.Handled = true;
        }
        else if (e.Data.GetData(typeof(ScheduleItem)) is ScheduleItem draggedItem)
        {
            int oldIndex = vm.Items.IndexOf(draggedItem);
            if (oldIndex >= 0)
            {
                if (oldIndex < targetIdx)
                {
                    targetIdx--;
                }
                targetIdx = Math.Clamp(targetIdx, 0, Math.Max(0, vm.Items.Count - 1));
                if (oldIndex != targetIdx)
                {
                    vm.Items.Move(oldIndex, targetIdx);
                }
                vm.SelectedItem = draggedItem;
            }
            e.Handled = true;
        }
    }

    private void OnScheduleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && DataContext is ScheduleViewModel vm)
        {
            if (vm.RemoveItemCommand.CanExecute(null))
            {
                vm.RemoveItemCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter) && DataContext is ScheduleViewModel vmSchedule)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ScheduleListBox.SelectedItem is ScheduleItem item)
                {
                    vmSchedule.UserSelectItem(item);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private int GetDropIndex(DragEventArgs e)
    {
        if (DataContext is not ScheduleViewModel vm || vm.Items.Count == 0)
            return 0;

        Point dropPos = e.GetPosition(ScheduleListBox);
        var hitResult = VisualTreeHelper.HitTest(ScheduleListBox, dropPos);
        if (hitResult != null)
        {
            var container = FindVisualParent<ListBoxItem>(hitResult.VisualHit);
            if (container != null)
            {
                int index = ScheduleListBox.ItemContainerGenerator.IndexFromContainer(container);
                if (index >= 0)
                {
                    Point relativePos = e.GetPosition(container);
                    if (relativePos.Y > container.ActualHeight / 2.0)
                    {
                        index++;
                    }
                    return index;
                }
            }
        }

        return vm.Items.Count;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void OnGearClick(object sender, MouseButtonEventArgs e)
    {
        if (ScheduleHeaderMenu != null)
        {
            ScheduleHeaderMenu.PlacementTarget = sender as UIElement;
            ScheduleHeaderMenu.IsOpen = true;
        }
    }

    private void OnSchedulePropertiesButtonClick(object sender, RoutedEventArgs e)
    {
        if (ScheduleHeaderMenu != null)
        {
            ScheduleHeaderMenu.PlacementTarget = sender as UIElement;
            ScheduleHeaderMenu.IsOpen = true;
        }
    }

    private void OnNewScheduleMenuClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NewSchedule();
        }
    }

    private void OnOpenScheduleMenuClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenSchedule();
        }
    }

    private void OnSaveScheduleMenuClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.SaveSchedule(false);
        }
    }

    private void OnSaveScheduleAsMenuClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.SaveSchedule(true);
        }
    }

    private void OnClearScheduleMenuClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ScheduleViewModel vm && vm.Items.Count > 0)
        {
            var res = MessageBox.Show("Are you sure you want to clear the schedule?", "Clear Schedule", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                vm.ClearSchedule();
            }
        }
    }
}
