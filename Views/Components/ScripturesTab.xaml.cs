using System.Windows.Controls;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class ScripturesTab : UserControl
{
    public ScripturesTab()
    {
        InitializeComponent();
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ScripturesViewModel vm && vm.SelectedVerse != null)
        {
            vm.DoubleClickCommand.Execute(vm.SelectedVerse);
            e.Handled = true;
        }
    }

    private void OnVersesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {
            listBox.ScrollIntoView(listBox.SelectedItem);
        }
    }

    private void OnVersesListBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ScripturesViewModel vm && vm.SelectedVerse != null)
        {
            vm.DoubleClickCommand.Execute(vm.SelectedVerse);
            e.Handled = true;
        }
    }

    private void OnVerseItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ScripturesViewModel vm)
        {
            if (sender is ListBoxItem item && item.DataContext is BibleVerse verse)
            {
                vm.DoubleClickCommand.Execute(verse);
            }
            else if (vm.SelectedVerse != null)
            {
                vm.DoubleClickCommand.Execute(vm.SelectedVerse);
            }
        }
    }
}
