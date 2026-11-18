using System.Windows.Controls;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MainTabControl.Items.Count)
        {
            MainTabControl.SelectedIndex = index;
        }
    }

    private void OnTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == MainTabControl && DataContext is MainViewModel vm)
        {
            vm.OnLibraryTabChanged(MainTabControl.SelectedIndex);
        }
    }
}
