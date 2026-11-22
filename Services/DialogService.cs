using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using LuxPresenter.Models;
using LuxPresenter.Views;

namespace LuxPresenter.Services;

/// <summary>
/// Service responsible for managing all application dialogs, modal windows, and message boxes.
/// Ensures proper modal ownership and decouples UI window creation from coordinators and ViewModels.
/// </summary>
public class DialogService
{
    // 1. Windows & Editor Dialogs
    public Song? ShowSongEditor(Window owner, Song? song)
    {
        var editor = new SongEditorWindow(song) { Owner = owner };
        return editor.ShowDialog() == true ? editor.ResultSong : null;
    }

    public bool ShowOptions(Window owner, IProjectionService projectionService, IDataService dataService)
    {
        var options = new OptionsWindow(projectionService, dataService) { Owner = owner };
        return options.ShowDialog() == true;
    }

    public (bool Accepted, string? Background, bool ApplyToLive) ShowBackgroundDialog(Window owner, IDataService dataService, string previewText, string currentBg)
    {
        var dlg = new BackgroundOptionsWindow(dataService, previewText, currentBg) { Owner = owner };
        if (dlg.ShowDialog() == true && dlg.SelectedBackground != null)
        {
            var bg = !string.IsNullOrWhiteSpace(dlg.SelectedBackground.FilePath)
                ? dlg.SelectedBackground.FilePath
                : dlg.SelectedBackground.HexColor;
            return (true, bg, dlg.IsPreviewLiveOutputChecked);
        }
        return (false, null, false);
    }

    public void ShowAbout(Window owner)
    {
        var dlg = new AboutWindow { Owner = owner };
        dlg.ShowDialog();
    }

    // 2. Schedule File Dialogs
    public string? ShowOpenScheduleDialog(Window owner)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Schedule",
            Filter = "LuxPresenter Schedule (*.lps)|*.lps|Legacy FreeWorship Schedule (*.fws)|*.fws|JSON Schedule (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".lps"
        };
        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    public string? ShowSaveScheduleDialog(Window owner, string? currentFilePath)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save Schedule",
            Filter = "LuxPresenter Schedule (*.lps)|*.lps|Legacy FreeWorship Schedule (*.fws)|*.fws|JSON Schedule (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".lps",
            FileName = string.IsNullOrWhiteSpace(currentFilePath)
                ? $"Schedule_{DateTime.Now:yyyyMMdd}.lps"
                : (Path.GetExtension(currentFilePath).Equals(".fws", StringComparison.OrdinalIgnoreCase)
                    ? Path.ChangeExtension(Path.GetFileName(currentFilePath), ".lps")
                    : Path.GetFileName(currentFilePath))
        };
        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    // 3. Songs & Bible Import/Export Dialogs
    public string? ShowExportSongBackupDialog(Window owner)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Songs Profile (Backup)",
            Filter = "LuxPresenter Songs Backup (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = "LuxPresenter_Songs_Backup.json",
            DefaultExt = ".json"
        };
        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    public string? ShowImportSongBackupDialog(Window owner)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Songs Profile (Backup)",
            Filter = "LuxPresenter Songs Backup (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };
        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    public string? ShowImportBibleDialog(Window owner)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Bible Translation (.json)",
            Filter = "Bible Translation JSON (*.json)|*.json|All Files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    public string? ShowSelectFolderDialog(Window owner, string title, string? initialDirectory = null)
    {
        var dlg = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory ?? string.Empty
        };
        return dlg.ShowDialog(owner) == true ? dlg.FolderName : null;
    }

    // 4. Send Stage Message Dialog
    public (bool Confirmed, string Message, bool Clear) ShowSendStageMessageDialog(Window owner, string currentMessage)
    {
        var inputDlg = new Window
        {
            Title = "Send Message to Stage Display",
            Width = 440,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)owner.FindResource("WindowBgBrush"),
            FontFamily = (FontFamily)owner.FindResource("AppFont")
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text = "Enter stage alert message (visible only to singers/pastor on stage):",
            Foreground = (Brush)owner.FindResource("TextPrimaryBrush"),
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(lbl, 0);

        var txt = new TextBox
        {
            Height = 26,
            FontSize = 12,
            Text = currentMessage
        };
        Grid.SetRow(txt, 1);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(btnPanel, 2);

        bool confirmed = false;
        bool clear = false;
        string resultMsg = string.Empty;

        var btnClear = new Button
        {
            Content = "Clear Alert",
            Width = 90,
            Height = 26,
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnClear.Click += (s, ev) =>
        {
            confirmed = true;
            clear = true;
            resultMsg = string.Empty;
            inputDlg.Close();
        };

        var btnSend = new Button
        {
            Content = "Send to Stage",
            Width = 100,
            Height = 26,
            IsDefault = true
        };
        btnSend.Click += (s, ev) =>
        {
            confirmed = true;
            clear = false;
            resultMsg = txt.Text.Trim();
            inputDlg.Close();
        };

        btnPanel.Children.Add(btnClear);
        btnPanel.Children.Add(btnSend);

        grid.Children.Add(lbl);
        grid.Children.Add(txt);
        grid.Children.Add(btnPanel);

        inputDlg.Content = grid;
        txt.Focus();
        inputDlg.ShowDialog();

        return (confirmed, resultMsg, clear);
    }

    // 5. Message Boxes
    public void ShowInformation(Window? owner, string message, string title = "LuxPresenter")
    {
        if (owner != null)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(Window? owner, string message, string title = "LuxPresenter")
    {
        if (owner != null)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(Window? owner, string message, string title = "LuxPresenter")
    {
        if (owner != null)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public MessageBoxResult ShowConfirmation(Window? owner, string message, string title = "LuxPresenter", MessageBoxButton buttons = MessageBoxButton.YesNo)
    {
        if (owner != null)
            return MessageBox.Show(owner, message, title, buttons, MessageBoxImage.Question);
        else
            return MessageBox.Show(message, title, buttons, MessageBoxImage.Question);
    }
}
