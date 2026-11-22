using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Services;

/// <summary>
/// Service responsible for intercepting and handling application-wide keyboard shortcuts.
/// Uses PreviewKeyDown so presentation navigation (Space, PageDown, Arrows) isn't swallowed by focused controls.
/// </summary>
public class KeyboardShortcutService
{
    private readonly MainViewModel _viewModel;
    private readonly Action? _toggleTheme;
    private readonly Action? _toggleStageDisplay;
    private readonly Action? _newSchedule;
    private readonly Action? _openSchedule;
    private readonly Action<bool>? _saveSchedule;

    private Window? _attachedWindow;

    public KeyboardShortcutService(
        MainViewModel viewModel,
        Action? toggleTheme = null,
        Action? toggleStageDisplay = null,
        Action? newSchedule = null,
        Action? openSchedule = null,
        Action<bool>? saveSchedule = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _toggleTheme = toggleTheme;
        _toggleStageDisplay = toggleStageDisplay;
        _newSchedule = newSchedule;
        _openSchedule = openSchedule;
        _saveSchedule = saveSchedule;
    }

    public void Attach(Window window)
    {
        Detach();
        _attachedWindow = window ?? throw new ArgumentNullException(nameof(window));
        _attachedWindow.PreviewKeyDown += OnPreviewKeyDown;
    }

    public void Detach()
    {
        if (_attachedWindow != null)
        {
            _attachedWindow.PreviewKeyDown -= OnPreviewKeyDown;
            _attachedWindow = null;
        }
    }

    public void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't intercept shortcuts if user is typing text in an input box (except Ctrl shortcuts if not copying)
        var isTypingInTextBox = e.OriginalSource is TextBox || Keyboard.FocusedElement is TextBox;

        // Ctrl + T: Toggle Dark / Light Mode
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
        {
            _toggleTheme?.Invoke();
            e.Handled = true;
            return;
        }

        // Ctrl + Shift + D: Toggle Stage Display
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D)
        {
            _toggleStageDisplay?.Invoke();
            e.Handled = true;
            return;
        }

        // Ctrl + B: Black
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)
        {
            _viewModel.BlackCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl + S / Ctrl + Shift + S: Save Schedule
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            _saveSchedule?.Invoke(true);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            _saveSchedule?.Invoke(false);
            e.Handled = true;
            return;
        }

        // Ctrl + O: Open Schedule
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            _openSchedule?.Invoke();
            e.Handled = true;
            return;
        }

        // Ctrl + N: New Schedule
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            _newSchedule?.Invoke();
            e.Handled = true;
            return;
        }

        // Ctrl + C: Clear (When NOT selecting text in an active TextBox)
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            if (isTypingInTextBox && e.OriginalSource is TextBox tb && tb.SelectionLength > 0)
            {
                // Standard copy behavior
                return;
            }

            _viewModel.ClearCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // If user is focused inside a search box, don't hijack Space, Enter, or Arrow keys
        if (isTypingInTextBox) return;

        switch (e.Key)
        {
            // Go Live: F5
            case Key.F5:
                _viewModel.GoLiveCommand.Execute(null);
                e.Handled = true;
                break;

            // Black toggle: F6 or Period (.) from presenter remote
            case Key.F6:
            case Key.OemPeriod:
                _viewModel.BlackCommand.Execute(null);
                e.Handled = true;
                break;

            // Clear toggle: F7
            case Key.F7:
                _viewModel.ClearCommand.Execute(null);
                e.Handled = true;
                break;

            // Logo toggle: F8
            case Key.F8:
                _viewModel.LogoCommand.Execute(null);
                e.Handled = true;
                break;

            // Advance live slide: PageDown, Space, Down Arrow, Right Arrow (Supports presenter remotes)
            case Key.PageDown:
            case Key.Space:
            case Key.Down:
            case Key.Right:
                _viewModel.LiveVM.NextSlideCommand.Execute(null);
                e.Handled = true;
                break;

            // Previous live slide: Up Arrow, Left Arrow, PageUp (Always operates on Live output / right preview)
            case Key.Up:
            case Key.Left:
            case Key.PageUp:
                _viewModel.LiveVM.PrevSlideCommand.Execute(null);
                e.Handled = true;
                break;

            // First slide (Top): Home
            case Key.Home:
                _viewModel.LiveVM.SelectFirstSlide();
                e.Handled = true;
                break;

            // Last slide (Bottom): End
            case Key.End:
                _viewModel.LiveVM.SelectLastSlide();
                e.Handled = true;
                break;
        }
    }
}
