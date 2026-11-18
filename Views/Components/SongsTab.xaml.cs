using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views.Components;

public partial class SongsTab : UserControl
{
    private Point _dragStartPoint;

    public SongsTab()
    {
        InitializeComponent();
        SongsListView.SelectionChanged += (s, e) =>
        {
            if (SongsListView.SelectedItem != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (SongsListView.SelectedItem != null)
                    {
                        SongsListView.ScrollIntoView(SongsListView.SelectedItem);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    private void OnSongItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SongsViewModel vm && sender is ListViewItem item && item.DataContext is Song song)
        {
            vm.DoubleClickCommand.Execute(song);
        }
    }

    private void OnSongItemClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SongsViewModel vm && sender is ListViewItem item && item.DataContext is Song song)
        {
            vm.UserSelectSong(song);
        }
    }

    private void OnListViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnListViewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (SongsListView.SelectedItem is Song song)
                {
                    DragDrop.DoDragDrop(SongsListView, song, DragDropEffects.Copy);
                }
            }
        }
    }

    private void OnListViewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && DataContext is SongsViewModel vmDelete)
        {
            if (vmDelete.DeleteSongCommand.CanExecute(null))
            {
                vmDelete.DeleteSongCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter && DataContext is SongsViewModel vmEnter)
        {
            if (SongsListView.SelectedItem is Song s)
            {
                vmEnter.DoubleClickCommand.Execute(s);
                e.Handled = true;
            }
        }
        else if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown || e.Key == Key.Home || e.Key == Key.End) && DataContext is SongsViewModel vm)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SongsListView.SelectedItem is Song s)
                {
                    vm.UserSelectSong(s);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
    private bool _isUpdatingTitle = false;
    private bool _isBackspacingTitle = false;

    private bool _isUpdatingLyrics = false;
    private bool _isBackspacingLyrics = false;

    private void OnTitleSearchPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _isBackspacingTitle = false;
        if (DataContext is not SongsViewModel vm) return;
        if (string.IsNullOrEmpty(e.Text)) return;

        int start = Math.Clamp(TitleSearchTextBox.SelectionStart, 0, TitleSearchTextBox.Text.Length);
        string currentTyped = TitleSearchTextBox.Text.Substring(0, start);
        string proposed = currentTyped + e.Text;

        if (!vm.HasTitleMatch(proposed))
        {
            e.Handled = true;
        }
    }

    private void OnTitleSearchPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SongsViewModel vm) return;

        if (e.Key == Key.Space)
        {
            _isBackspacingTitle = false;
            int start = Math.Clamp(TitleSearchTextBox.SelectionStart, 0, TitleSearchTextBox.Text.Length);
            string currentTyped = TitleSearchTextBox.Text.Substring(0, start);
            string proposed = currentTyped + " ";

            if (!vm.HasTitleMatch(proposed))
            {
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string pasteText = Clipboard.GetText();
                    int start = Math.Clamp(TitleSearchTextBox.SelectionStart, 0, TitleSearchTextBox.Text.Length);
                    string currentTyped = TitleSearchTextBox.Text.Substring(0, start);
                    string proposed = currentTyped + pasteText;

                    if (!vm.HasTitleMatch(proposed))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
            catch { }
        }
        else if (e.Key == Key.Back)
        {
            _isBackspacingTitle = true;
            if (TitleSearchTextBox.SelectionLength > 0)
            {
                int start = TitleSearchTextBox.SelectionStart;
                _isUpdatingTitle = true;
                string newTyped = start > 0 ? TitleSearchTextBox.Text.Substring(0, start - 1) : string.Empty;
                TitleSearchTextBox.Text = newTyped;
                TitleSearchTextBox.SelectionStart = newTyped.Length;
                TitleSearchTextBox.SelectionLength = 0;
                _isUpdatingTitle = false;

                vm.ApplySearch(newTyped, isTitle: true, targetSongId: null, targetSlideQuery: null);
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Delete)
        {
            _isBackspacingTitle = true;
        }
        else if (e.Key == Key.Enter)
        {
            _isBackspacingTitle = false;
            if (TitleSearchTextBox.SelectionLength > 0)
            {
                _isUpdatingTitle = true;
                TitleSearchTextBox.SelectionStart = TitleSearchTextBox.Text.Length;
                TitleSearchTextBox.SelectionLength = 0;
                _isUpdatingTitle = false;
            }

            vm.ApplyFilterImmediate();
            if (vm.SelectedSong != null)
            {
                vm.DoubleClickCommand.Execute(vm.SelectedSong);
            }
            e.Handled = true;
            return;
        }
        else if ((e.Key == Key.Tab || e.Key == Key.Right) && TitleSearchTextBox.SelectionLength > 0 &&
                 TitleSearchTextBox.SelectionStart + TitleSearchTextBox.SelectionLength == TitleSearchTextBox.Text.Length)
        {
            TitleSearchTextBox.SelectionStart = TitleSearchTextBox.Text.Length;
            TitleSearchTextBox.SelectionLength = 0;
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Down)
        {
            SongsListView.Focus();
            if (SongsListView.SelectedIndex < SongsListView.Items.Count - 1)
            {
                SongsListView.SelectedIndex++;
                if (SongsListView.SelectedItem != null)
                {
                    SongsListView.ScrollIntoView(SongsListView.SelectedItem);
                }
            }
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Up)
        {
            SongsListView.Focus();
            if (SongsListView.SelectedIndex > 0)
            {
                SongsListView.SelectedIndex--;
                if (SongsListView.SelectedItem != null)
                {
                    SongsListView.ScrollIntoView(SongsListView.SelectedItem);
                }
            }
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Escape)
        {
            _isUpdatingTitle = true;
            TitleSearchTextBox.Text = string.Empty;
            _isUpdatingTitle = false;

            _isUpdatingLyrics = true;
            LyricsSearchTextBox.Text = string.Empty;
            _isUpdatingLyrics = false;

            vm.ApplySearch(string.Empty, isTitle: true);
            e.Handled = true;
            return;
        }
    }

    private void OnTitleSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTitle) return;
        if (DataContext is not SongsViewModel vm) return;

        if (!string.IsNullOrEmpty(LyricsSearchTextBox.Text))
        {
            _isUpdatingLyrics = true;
            LyricsSearchTextBox.Text = string.Empty;
            _isUpdatingLyrics = false;
        }

        string currentText = TitleSearchTextBox.Text;

        if (_isBackspacingTitle || string.IsNullOrEmpty(currentText))
        {
            _isBackspacingTitle = false;
            vm.ApplySearch(currentText, isTitle: true, targetSongId: null, targetSlideQuery: null);
            return;
        }

        string typed = TitleSearchTextBox.SelectionLength > 0 && TitleSearchTextBox.SelectionStart > 0
            ? currentText.Substring(0, TitleSearchTextBox.SelectionStart)
            : currentText;

        var suggestion = vm.GetTitleSuggestion(typed);
        if (suggestion != null && suggestion.Title.Length >= typed.Length)
        {
            string continuation = suggestion.Title.Substring(typed.Length);
            string fullText = typed + continuation;

            _isUpdatingTitle = true;
            TitleSearchTextBox.Text = fullText;
            TitleSearchTextBox.Select(typed.Length, continuation.Length);
            _isUpdatingTitle = false;

            vm.ApplySearch(typed, isTitle: true, targetSongId: suggestion.Id, targetSlideQuery: suggestion.Title);
        }
        else
        {
            vm.ApplySearch(typed, isTitle: true, targetSongId: null, targetSlideQuery: null);
        }
    }

    private void OnLyricsSearchPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _isBackspacingLyrics = false;
        if (DataContext is not SongsViewModel vm) return;
        if (string.IsNullOrEmpty(e.Text)) return;

        int start = Math.Clamp(LyricsSearchTextBox.SelectionStart, 0, LyricsSearchTextBox.Text.Length);
        string currentTyped = LyricsSearchTextBox.Text.Substring(0, start);
        string proposed = currentTyped + e.Text;

        if (!vm.HasLyricMatch(proposed))
        {
            e.Handled = true;
        }
    }

    private void OnLyricsSearchPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SongsViewModel vm) return;

        if (e.Key == Key.Space)
        {
            _isBackspacingLyrics = false;
            int start = Math.Clamp(LyricsSearchTextBox.SelectionStart, 0, LyricsSearchTextBox.Text.Length);
            string currentTyped = LyricsSearchTextBox.Text.Substring(0, start);
            string proposed = currentTyped + " ";

            if (!vm.HasLyricMatch(proposed))
            {
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string pasteText = Clipboard.GetText();
                    int start = Math.Clamp(LyricsSearchTextBox.SelectionStart, 0, LyricsSearchTextBox.Text.Length);
                    string currentTyped = LyricsSearchTextBox.Text.Substring(0, start);
                    string proposed = currentTyped + pasteText;

                    if (!vm.HasLyricMatch(proposed))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
            catch { }
        }
        else if (e.Key == Key.Back)
        {
            _isBackspacingLyrics = true;
            if (LyricsSearchTextBox.SelectionLength > 0)
            {
                int start = LyricsSearchTextBox.SelectionStart;
                _isUpdatingLyrics = true;
                string newTyped = start > 0 ? LyricsSearchTextBox.Text.Substring(0, start - 1) : string.Empty;
                LyricsSearchTextBox.Text = newTyped;
                LyricsSearchTextBox.SelectionStart = newTyped.Length;
                LyricsSearchTextBox.SelectionLength = 0;
                _isUpdatingLyrics = false;

                vm.ApplySearch(newTyped, isTitle: false, targetSongId: null, targetSlideQuery: null);
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Delete)
        {
            _isBackspacingLyrics = true;
        }
        else if (e.Key == Key.Enter)
        {
            _isBackspacingLyrics = false;
            if (LyricsSearchTextBox.SelectionLength > 0)
            {
                _isUpdatingLyrics = true;
                LyricsSearchTextBox.SelectionStart = LyricsSearchTextBox.Text.Length;
                LyricsSearchTextBox.SelectionLength = 0;
                _isUpdatingLyrics = false;
            }

            vm.ApplyFilterImmediate();
            if (vm.SelectedSong != null)
            {
                vm.DoubleClickCommand.Execute(vm.SelectedSong);
            }
            e.Handled = true;
            return;
        }
        else if ((e.Key == Key.Tab || e.Key == Key.Right) && LyricsSearchTextBox.SelectionLength > 0 &&
                 LyricsSearchTextBox.SelectionStart + LyricsSearchTextBox.SelectionLength == LyricsSearchTextBox.Text.Length)
        {
            LyricsSearchTextBox.SelectionStart = LyricsSearchTextBox.Text.Length;
            LyricsSearchTextBox.SelectionLength = 0;
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Down)
        {
            SongsListView.Focus();
            if (SongsListView.SelectedIndex < SongsListView.Items.Count - 1)
            {
                SongsListView.SelectedIndex++;
                if (SongsListView.SelectedItem != null)
                {
                    SongsListView.ScrollIntoView(SongsListView.SelectedItem);
                }
            }
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Up)
        {
            SongsListView.Focus();
            if (SongsListView.SelectedIndex > 0)
            {
                SongsListView.SelectedIndex--;
                if (SongsListView.SelectedItem != null)
                {
                    SongsListView.ScrollIntoView(SongsListView.SelectedItem);
                }
            }
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.Escape)
        {
            _isUpdatingTitle = true;
            TitleSearchTextBox.Text = string.Empty;
            _isUpdatingTitle = false;

            _isUpdatingLyrics = true;
            LyricsSearchTextBox.Text = string.Empty;
            _isUpdatingLyrics = false;

            vm.ApplySearch(string.Empty, isTitle: false);
            e.Handled = true;
            return;
        }
    }

    private void OnLyricsSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingLyrics) return;
        if (DataContext is not SongsViewModel vm) return;

        if (!string.IsNullOrEmpty(TitleSearchTextBox.Text))
        {
            _isUpdatingTitle = true;
            TitleSearchTextBox.Text = string.Empty;
            _isUpdatingTitle = false;
        }

        string currentText = LyricsSearchTextBox.Text;

        if (_isBackspacingLyrics || string.IsNullOrEmpty(currentText))
        {
            _isBackspacingLyrics = false;
            vm.ApplySearch(currentText, isTitle: false, targetSongId: null, targetSlideQuery: null);
            return;
        }

        string typed = LyricsSearchTextBox.SelectionLength > 0 && LyricsSearchTextBox.SelectionStart > 0
            ? currentText.Substring(0, LyricsSearchTextBox.SelectionStart)
            : currentText;

        var suggestion = vm.GetLyricSuggestion(typed);
        if (suggestion != null && suggestion.LineText.Length >= typed.Length)
        {
            string continuation = suggestion.LineText.Substring(typed.Length);
            string fullText = typed + continuation;

            _isUpdatingLyrics = true;
            LyricsSearchTextBox.Text = fullText;
            LyricsSearchTextBox.Select(typed.Length, continuation.Length);
            _isUpdatingLyrics = false;

            vm.ApplySearch(typed, isTitle: false, targetSongId: suggestion.Song.Id, targetSlideQuery: suggestion.LineText);
        }
        else
        {
            vm.ApplySearch(typed, isTitle: false, targetSongId: null, targetSlideQuery: null);
        }
    }
}
