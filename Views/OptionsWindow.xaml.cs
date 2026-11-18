using System.Windows;
using System.Windows.Controls;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class OptionsWindow : Window
{
    private readonly IProjectionService _projectionService;
    private readonly IDataService _dataService;
    private readonly List<ScreenInfo> _screens;
    private readonly AppSettings _settings;

    public OptionsWindow(IProjectionService projectionService, IDataService dataService)
    {
        InitializeComponent();
        _projectionService = projectionService;
        _dataService = dataService;
        _settings = _dataService.GetSettings();

        // 1. Setup Monitors
        _screens = DisplayHelper.GetScreens();
        CmbMonitors.ItemsSource = _screens;

        if (_projectionService.SelectedMonitorIndex >= 0 && _projectionService.SelectedMonitorIndex < _screens.Count)
        {
            CmbMonitors.SelectedIndex = _projectionService.SelectedMonitorIndex;
        }
        else
        {
            int defaultIdx = _screens.FindIndex(s => !s.IsPrimary);
            CmbMonitors.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
        }

        RbDarkMode.IsChecked = _settings.IsDarkMode;
        RbLightMode.IsChecked = !_settings.IsDarkMode;

        TxtMarginTop.Text = _settings.MarginTop.ToString();
        TxtMarginBottom.Text = _settings.MarginBottom.ToString();
        TxtMarginLeft.Text = _settings.MarginLeft.ToString();
        TxtMarginRight.Text = _settings.MarginRight.ToString();

        // 2. Setup Songs Options
        SelectComboBoxItem(CmbSongTextAlign, _settings.SongTextAlignment);
        SelectComboBoxItem(CmbSongVerticalAlign, _settings.SongVerticalAlignment);
        SelectComboBoxItem(CmbSongFontFamily, !string.IsNullOrWhiteSpace(_settings.SongFontFamily) ? _settings.SongFontFamily : "Arial");
        SelectComboBoxItem(CmbSongFontSize, (_settings.SongFontSize > 0 ? _settings.SongFontSize : 13).ToString());
        ChkSongFontBold.IsChecked = _settings.SongFontBold;
        TxtSongFontPercent.Text = _settings.SongFontHeightPercent.ToString();

        ChkShowVerseLabels.IsChecked = _settings.ShowVerseLabels;
        SelectComboBoxItem(CmbLabelLocation, _settings.LabelLocation);
        SelectComboBoxItem(CmbLabelSize, _settings.LabelSize);
        ChkIndentLabel.IsChecked = _settings.IndentLabel;

        ChkAutoWrap.IsChecked = _settings.AutoWrapWords;
        ChkCapitalizeAll.IsChecked = _settings.CapitalizeAllWords;
        ChkCapitalizeFirstLine.IsChecked = _settings.CapitalizeFirstCharEachLine;
        ChkAutoCapitalizeKeywords.IsChecked = _settings.AutoCapitalizeKeywords;
        TxtKeywords.Text = _settings.CapitalizeKeywords;

        SelectComboBoxItem(CmbSongTransition, _settings.SongTransition);
        SelectComboBoxItem(CmbSlideTransition, _settings.SlideTransition);
        SelectComboBoxItem(CmbMediaTransition, _settings.MediaTransition);
        SelectComboBoxItem(CmbScreenTransition, _settings.ScreenTransition);

        if (_settings.TransitionDurationMs <= 300)
            CmbTransitionDuration.SelectedIndex = 0;
        else if (_settings.TransitionDurationMs <= 600)
            CmbTransitionDuration.SelectedIndex = 1;
        else if (_settings.TransitionDurationMs <= 850)
            CmbTransitionDuration.SelectedIndex = 2;
        else
            CmbTransitionDuration.SelectedIndex = 3;

        if (string.Equals(_settings.VideoBackgroundEngine, "MediaElement", StringComparison.OrdinalIgnoreCase))
            CmbVideoBgEngine.SelectedIndex = 1;
        else
            CmbVideoBgEngine.SelectedIndex = 0;

        UpdateMonitorSample();

        CmbSongTextAlign.SelectionChanged += OnSongAlignmentChanged;
        CmbSongVerticalAlign.SelectionChanged += OnSongAlignmentChanged;
        ChkCapitalizeAll.Click += (s, e) => UpdateMonitorSample();
        ChkCapitalizeFirstLine.Click += (s, e) => UpdateMonitorSample();
        ChkAutoCapitalizeKeywords.Click += (s, e) => UpdateMonitorSample();
    }

    private void SelectComboBoxItem(ComboBox cmb, string? text)
    {
        if (cmb == null) return;
        if (!string.IsNullOrWhiteSpace(text))
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is ComboBoxItem item && string.Equals(item.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
        }
        if (cmb.SelectedIndex < 0 && cmb.Items.Count > 0)
        {
            cmb.SelectedIndex = 0;
        }
    }

    private string GetComboBoxSelectedText(ComboBox? cmb, string defaultValue)
    {
        if (cmb?.SelectedItem is ComboBoxItem item && item.Content != null)
        {
            return item.Content.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GeneralPanel == null || SongsPanel == null || AdvancedPanel == null || CategoryListBox == null) return;

        int idx = CategoryListBox.SelectedIndex;
        GeneralPanel.Visibility = (idx == 0) ? Visibility.Visible : Visibility.Collapsed;
        SongsPanel.Visibility = (idx == 1) ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = (idx == 2) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSongAlignmentChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMonitorSample();
    }

    private void UpdateMonitorSample()
    {
        if (TxtMonitorSample == null || CmbSongVerticalAlign == null || CmbSongTextAlign == null) return;

        var vAlign = GetComboBoxSelectedText(CmbSongVerticalAlign, "Center");
        var tAlign = GetComboBoxSelectedText(CmbSongTextAlign, "Center");

        if (string.Equals(vAlign, "Top", StringComparison.OrdinalIgnoreCase))
            TxtMonitorSample.VerticalAlignment = VerticalAlignment.Top;
        else if (string.Equals(vAlign, "Bottom", StringComparison.OrdinalIgnoreCase))
            TxtMonitorSample.VerticalAlignment = VerticalAlignment.Bottom;
        else
            TxtMonitorSample.VerticalAlignment = VerticalAlignment.Center;

        if (string.Equals(tAlign, "Left", StringComparison.OrdinalIgnoreCase))
            TxtMonitorSample.TextAlignment = TextAlignment.Left;
        else if (string.Equals(tAlign, "Right", StringComparison.OrdinalIgnoreCase))
            TxtMonitorSample.TextAlignment = TextAlignment.Right;
        else
            TxtMonitorSample.TextAlignment = TextAlignment.Center;

        const string sample = "Lord you are beautiful,\nI raise my hands to you,\nNo one else can touch\nmy heart like you do.";
        var tempSettings = new AppSettings
        {
            CapitalizeAllWords = ChkCapitalizeAll?.IsChecked == true,
            CapitalizeFirstCharEachLine = ChkCapitalizeFirstLine?.IsChecked == true,
            AutoCapitalizeKeywords = ChkAutoCapitalizeKeywords?.IsChecked == true,
            CapitalizeKeywords = TxtKeywords?.Text ?? string.Empty
        };
        TxtMonitorSample.Text = LyricsFormatter.Format(sample, tempSettings);
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        // 1. General Margins & Monitor
        int selectedIndex = CmbMonitors.SelectedIndex;
        int.TryParse(TxtMarginTop.Text, out int top);
        int.TryParse(TxtMarginBottom.Text, out int bottom);
        int.TryParse(TxtMarginLeft.Text, out int left);
        int.TryParse(TxtMarginRight.Text, out int right);

        _projectionService.ApplyMonitorSettings(selectedIndex, top, bottom, left, right);

        _settings.MarginTop = top;
        _settings.MarginBottom = bottom;
        _settings.MarginLeft = left;
        _settings.MarginRight = right;

        // 2. Song Settings
        _settings.SongTextAlignment = GetComboBoxSelectedText(CmbSongTextAlign, "Center");
        _settings.SongVerticalAlignment = GetComboBoxSelectedText(CmbSongVerticalAlign, "Center");
        _settings.SongFontFamily = GetComboBoxSelectedText(CmbSongFontFamily, "Arial");
        if (int.TryParse(GetComboBoxSelectedText(CmbSongFontSize, "13"), out int fontSize))
        {
            _settings.SongFontSize = fontSize > 0 ? fontSize : 13;
        }
        else
        {
            _settings.SongFontSize = 13;
        }
        _settings.SongFontBold = ChkSongFontBold.IsChecked == true;

        // Always apply global font to database songs when saved from Options
        _dataService.ApplyGlobalFontToAllSongs(_settings.SongFontFamily, _settings.SongFontSize, _settings.SongFontBold);

        if (int.TryParse(TxtSongFontPercent.Text, out int percent))
        {
            _settings.SongFontHeightPercent = percent;
        }

        _settings.ShowVerseLabels = ChkShowVerseLabels.IsChecked == true;
        _settings.LabelLocation = GetComboBoxSelectedText(CmbLabelLocation, "Top Center");
        _settings.LabelSize = GetComboBoxSelectedText(CmbLabelSize, "Normal");
        _settings.IndentLabel = ChkIndentLabel.IsChecked == true;

        _settings.AutoWrapWords = ChkAutoWrap.IsChecked == true;
        _settings.CapitalizeAllWords = ChkCapitalizeAll.IsChecked == true;
        _settings.CapitalizeFirstCharEachLine = ChkCapitalizeFirstLine.IsChecked == true;
        _settings.AutoCapitalizeKeywords = ChkAutoCapitalizeKeywords.IsChecked == true;
        _settings.CapitalizeKeywords = TxtKeywords.Text;

        _settings.SongTransition = GetComboBoxSelectedText(CmbSongTransition, "Blend");
        _settings.SlideTransition = GetComboBoxSelectedText(CmbSlideTransition, "Blend");
        _settings.MediaTransition = GetComboBoxSelectedText(CmbMediaTransition, "Blend");
        _settings.ScreenTransition = GetComboBoxSelectedText(CmbScreenTransition, "Blend");

        if (CmbTransitionDuration.SelectedIndex == 0)
            _settings.TransitionDurationMs = 250;
        else if (CmbTransitionDuration.SelectedIndex == 2)
            _settings.TransitionDurationMs = 750;
        else if (CmbTransitionDuration.SelectedIndex == 3)
            _settings.TransitionDurationMs = 1000;
        else
            _settings.TransitionDurationMs = 500;

        _settings.VideoBackgroundEngine = CmbVideoBgEngine.SelectedIndex == 1 ? "MediaElement" : "libmpv";

        _settings.IsDarkMode = RbDarkMode.IsChecked == true;
        ThemeService.ApplyTheme(_settings.IsDarkMode);

        _dataService.SaveSettings(_settings);
        _projectionService.UpdateSettings(_settings);

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
