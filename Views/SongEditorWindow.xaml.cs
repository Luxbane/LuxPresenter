using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuxPresenter.Models;

namespace LuxPresenter.Views;

public partial class SongEditorWindow : Window
{
    public Song? ResultSong { get; private set; }
    private string _currentBackground = string.Empty;

    public SongEditorWindow(Song? existingSong = null)
    {
        InitializeComponent();

        if (existingSong != null)
        {
            Title = "Editor";
            TxtTitle.Text = existingSong.Title;
            TxtLyrics.Text = existingSong.LyricsRaw;
            _currentBackground = existingSong.DefaultBackground ?? string.Empty;
            ResultSong = existingSong;

            SelectFontFamily(existingSong.FontFamily);
            TxtFontSize.Text = existingSong.FontSize > 0 ? existingSong.FontSize.ToString() : "13";
            BtnBold.IsChecked = existingSong.IsBold;
            BtnItalic.IsChecked = existingSong.IsItalic;
            BtnUnderline.IsChecked = existingSong.IsUnderline;
            BtnShadow.IsChecked = existingSong.HasShadow;
            BtnOutline.IsChecked = existingSong.HasOutline;
        }
        else
        {
            Title = "Editor";
            TxtTitle.Text = "New Song";
            TxtLyrics.Text = "[Verse 1]\nEnter lyrics here...";
            ResultSong = new Song();

            SelectFontFamily("Tahoma");
            TxtFontSize.Text = "13";
            BtnBold.IsChecked = true;
            BtnItalic.IsChecked = false;
            BtnUnderline.IsChecked = false;
            BtnShadow.IsChecked = true;
            BtnOutline.IsChecked = true;
        }

        TxtLyrics.SelectionChanged += (s, e) => UpdatePreview();
        TxtLyrics.PreviewMouseUp += (s, e) => UpdatePreview();
        TxtLyrics.KeyUp += (s, e) => UpdatePreview();
        UpdatePreview();
    }

    private void SelectFontFamily(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) fontName = "Tahoma";
        for (int i = 0; i < CmbFontFamily.Items.Count; i++)
        {
            if (CmbFontFamily.Items[i] is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString() ?? item.Content?.ToString() ?? string.Empty;
                var contentText = item.Content?.ToString() ?? string.Empty;
                if (string.Equals(tag, fontName, StringComparison.OrdinalIgnoreCase) ||
                    contentText.Contains(fontName, StringComparison.OrdinalIgnoreCase))
                {
                    CmbFontFamily.SelectedIndex = i;
                    return;
                }
            }
        }
        CmbFontFamily.SelectedIndex = 0;
    }

    private string GetSelectedFontFamily()
    {
        if (CmbFontFamily.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }
        return "Tahoma";
    }

    private void OnLyricsOrTitleChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void OnFormattingChanged(object? sender, RoutedEventArgs? e)
    {
        UpdatePreview();
    }

    private void OnResetAttributesClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SelectFontFamily("Tahoma");
        TxtFontSize.Text = "13";
        BtnBold.IsChecked = true;
        BtnItalic.IsChecked = false;
        BtnUnderline.IsChecked = false;
        BtnShadow.IsChecked = true;
        BtnOutline.IsChecked = true;
        UpdatePreview();
    }

    private void OnBackgroundSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbBackground.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentBackground = tag;
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (TxtPreviewContent == null || TxtLyrics == null) return;

        // Apply active typography settings
        string fontName = GetSelectedFontFamily();
        TxtPreviewContent.FontFamily = new FontFamily(fontName);
        int.TryParse(TxtFontSize.Text, out int fSize);
        if (fSize <= 0) fSize = 13;
        TxtPreviewContent.FontSize = fSize * 4.0;
        TxtPreviewContent.FontWeight = BtnBold.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        TxtPreviewContent.FontStyle = BtnItalic.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
        TxtPreviewContent.HasOutline = BtnOutline.IsChecked == true;
        TxtPreviewContent.HasShadow = BtnShadow.IsChecked == true;

        var raw = TxtLyrics.Text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            TxtPreviewContent.Text = TxtTitle.Text;
            TxtPreviewFooter.Text = TxtTitle.Text;
            ApplyBackground(_currentBackground);
            return;
        }

        // Calculate caret in normalized '\n' string
        int rawCaret = Math.Clamp(TxtLyrics.CaretIndex, 0, raw.Length);
        int normalizedCaret = 0;
        for (int i = 0; i < rawCaret; i++)
        {
            if (raw[i] != '\r') normalizedCaret++;
        }

        string normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');

        int start = 0;
        int prevBreak = normalized.LastIndexOf("\n\n", Math.Max(0, normalizedCaret - 1), StringComparison.Ordinal);
        if (prevBreak >= 0)
        {
            start = prevBreak + 2;
        }

        int end = normalized.Length;
        int nextBreak = normalized.IndexOf("\n\n", normalizedCaret, StringComparison.Ordinal);
        if (nextBreak >= 0)
        {
            end = nextBreak;
        }

        string activeBlock = start < end ? normalized.Substring(start, end - start).Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(activeBlock))
        {
            int lineStart = normalized.LastIndexOf('\n', Math.Max(0, normalizedCaret - 1));
            lineStart = lineStart >= 0 ? lineStart + 1 : 0;
            int lineEnd = normalized.IndexOf('\n', normalizedCaret);
            if (lineEnd < 0) lineEnd = normalized.Length;
            activeBlock = normalized.Substring(lineStart, lineEnd - lineStart).Trim();
        }

        if (string.IsNullOrWhiteSpace(activeBlock))
        {
            activeBlock = TxtTitle.Text;
        }

        // Clean section tag from preview if present
        var lines = activeBlock.Split('\n');
        string previewText = activeBlock;
        if (lines.Length > 1 && lines[0].Trim().StartsWith("[") && lines[0].Trim().EndsWith("]"))
        {
            previewText = string.Join("\n", lines.Skip(1)).Trim();
        }
        else if (lines.Length > 0 && lines[0].Trim().EndsWith(":") && !lines[0].Contains(" "))
        {
            previewText = string.Join("\n", lines.Skip(1)).Trim();
        }

        TxtPreviewContent.Text = previewText;
        TxtPreviewFooter.Text = $"{TxtTitle.Text} (Preview)";

        ApplyBackground(_currentBackground);
    }

    private void ApplyBackground(string bg)
    {
        if (SlidePreviewGrid == null) return;

        if (string.IsNullOrWhiteSpace(bg))
        {
            SlidePreviewGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            return;
        }

        if (bg.StartsWith("#"))
        {
            try
            {
                SlidePreviewGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            }
            catch
            {
                SlidePreviewGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            }
        }
        else
        {
            string fullPath = bg;
            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bg);
            }

            if (File.Exists(fullPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.EndInit();

                    SlidePreviewGrid.Background = new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                }
                catch
                {
                    SlidePreviewGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
                }
            }
            else
            {
                SlidePreviewGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001833"));
            }
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (ResultSong == null)
        {
            ResultSong = new Song();
        }

        ResultSong.Title = TxtTitle.Text.Trim();
        ResultSong.LyricsRaw = TxtLyrics.Text;
        ResultSong.DefaultBackground = _currentBackground;

        ResultSong.FontFamily = GetSelectedFontFamily();
        int.TryParse(TxtFontSize.Text, out int fSize);
        ResultSong.FontSize = fSize > 0 ? fSize : 13;
        ResultSong.IsBold = BtnBold.IsChecked == true;
        ResultSong.IsItalic = BtnItalic.IsChecked == true;
        ResultSong.IsUnderline = BtnUnderline.IsChecked == true;
        ResultSong.HasShadow = BtnShadow.IsChecked == true;
        ResultSong.HasOutline = BtnOutline.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
