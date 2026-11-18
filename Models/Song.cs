using LuxPresenter.Services;

namespace LuxPresenter.Models;

public class Song
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool IsFavorite { get; set; } = false;
    public string LyricsRaw { get; set; } = string.Empty;
    public string? DefaultBackground { get; set; }

    // Typography overrides (Image 2)
    public string FontFamily { get; set; } = "Tahoma";
    public int FontSize { get; set; } = 13;
    public bool IsBold { get; set; } = true;
    public bool IsItalic { get; set; } = false;
    public bool IsUnderline { get; set; } = false;
    public bool HasShadow { get; set; } = true;
    public bool HasOutline { get; set; } = true;
    public string FontColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Parses the raw lyrics into presentation slides (Verse 1, Chorus, etc.)
    /// Standard convention: double line breaks or labels like [Verse 1], [Chorus]
    /// </summary>
    public PresentationItem ToPresentationItem(string? globalBackground = null, AppSettings? settings = null)
    {
        string? bg = null;
        if (!string.IsNullOrWhiteSpace(DefaultBackground) &&
            (DefaultBackground.StartsWith("#") || System.IO.File.Exists(DefaultBackground)))
        {
            bg = DefaultBackground;
        }

        var effectiveSettings = settings ?? new AppSettings();

        var item = new PresentationItem
        {
            Title = Title,
            Subtitle = string.IsNullOrWhiteSpace(Author) ? string.Empty : $"By {Author}",
            Category = ItemCategory.Song,
            BackgroundPath = bg
        };

        if (string.IsNullOrWhiteSpace(LyricsRaw))
        {
            item.Slides.Add(new Slide
            {
                Label = "Slide 1",
                Title = Title,
                Content = LyricsFormatter.Format(Title, effectiveSettings),
                CustomBackgroundPath = bg,
                FontSize = effectiveSettings.SongFontSize > 0 ? effectiveSettings.SongFontSize : (FontSize > 0 ? FontSize : 13),
                FontFamily = !string.IsNullOrWhiteSpace(effectiveSettings.SongFontFamily) ? effectiveSettings.SongFontFamily : (!string.IsNullOrWhiteSpace(FontFamily) ? FontFamily : "Arial"),
                IsBold = effectiveSettings.SongFontBold || IsBold,
                IsItalic = IsItalic,
                IsUnderline = IsUnderline,
                HasShadow = HasShadow,
                HasOutline = HasOutline,
                FontColor = FontColor
            });
            return item;
        }

        var normalized = LyricsRaw.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var parts = System.Text.RegularExpressions.Regex.Split(normalized, @"(\n{2,})");
        
        // parts will be: [block0, sep0, block1, sep1, block2, ...]
        var blockList = new List<(string Content, string SepAfter)>();
        for (int i = 0; i < parts.Length; i += 2)
        {
            var content = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(content)) continue;
            var sep = (i + 1 < parts.Length) ? parts[i + 1] : string.Empty;
            blockList.Add((content, sep));
        }

        for (int b = 0; b < blockList.Count; b++)
        {
            var (trimmedBlock, sepAfter) = blockList[b];

            string label = string.Empty;
            string content = trimmedBlock;
            var type = SlideType.Verse;

            // Check if block has header tag like [Verse 1] or Chorus:
            var lines = trimmedBlock.Split('\n');
            if (lines.Length > 1 && lines[0].Trim().StartsWith("[") && lines[0].Trim().EndsWith("]"))
            {
                label = lines[0].Trim().Trim('[', ']');
                content = string.Join("\n", lines.Skip(1)).Trim();
                if (label.Contains("Chorus", StringComparison.OrdinalIgnoreCase)) type = SlideType.Chorus;
                else if (label.Contains("Bridge", StringComparison.OrdinalIgnoreCase)) type = SlideType.Bridge;
                else if (label.Contains("Ending", StringComparison.OrdinalIgnoreCase)) type = SlideType.Ending;
            }
            else if (lines.Length > 0 && lines[0].Trim().EndsWith(":") && !lines[0].Contains(" "))
            {
                label = lines[0].Trim().TrimEnd(':');
                content = string.Join("\n", lines.Skip(1)).Trim();
                if (label.Contains("Chorus", StringComparison.OrdinalIgnoreCase)) type = SlideType.Chorus;
                else if (label.Contains("Bridge", StringComparison.OrdinalIgnoreCase)) type = SlideType.Bridge;
            }
            else
            {
                // Unlabeled continuation block — do NOT show "Slide 2", "Slide 3", etc.
                label = string.Empty;
            }

            int nlCount = sepAfter.Count(c => c == '\n');
            bool isStanzaEnd = false;

            if (b + 1 < blockList.Count)
            {
                // 1. Explicit double-enter / blank line in raw lyrics (3 or more newlines)
                if (nlCount >= 3)
                {
                    isStanzaEnd = true;
                }
                else
                {
                    // 2. Or the next block starts with a section header tag [Chorus], [Verse 2], etc.
                    var nextLines = blockList[b + 1].Content.Split('\n');
                    if (nextLines.Length > 0)
                    {
                        var firstLine = nextLines[0].Trim();
                        if ((firstLine.StartsWith("[") && firstLine.EndsWith("]")) ||
                            (firstLine.EndsWith(":") && !firstLine.Contains(" ")))
                        {
                            isStanzaEnd = true;
                        }
                    }
                }
            }

            // Normal slides inside the same stanza: comfortable spacing matching EasyWorship
            // Stanza break (Verse -> Chorus): distinct 20px bottom gap corresponding to double Enter
            var margin = isStanzaEnd
                ? new System.Windows.Thickness(1, 1, 1, 20)
                : new System.Windows.Thickness(1, 1, 1, 2);

            var formattedContent = LyricsFormatter.Format(content, effectiveSettings);

            item.Slides.Add(new Slide
            {
                Label = label,
                Title = Title,
                Content = formattedContent,
                Type = type,
                CustomBackgroundPath = bg,
                ContainerMargin = margin,
                FontFamily = !string.IsNullOrWhiteSpace(effectiveSettings.SongFontFamily) ? effectiveSettings.SongFontFamily : (!string.IsNullOrWhiteSpace(FontFamily) ? FontFamily : "Arial"),
                FontSize = effectiveSettings.SongFontSize > 0 ? effectiveSettings.SongFontSize : (FontSize > 0 ? FontSize : 13),
                IsBold = effectiveSettings.SongFontBold || IsBold,
                IsItalic = IsItalic,
                IsUnderline = IsUnderline,
                HasShadow = HasShadow,
                HasOutline = HasOutline,
                FontColor = FontColor
            });
        }

        if (item.Slides.Count == 0)
        {
            item.Slides.Add(new Slide
            {
                Label = "Slide 1",
                Title = Title,
                Content = LyricsFormatter.Format(Title, effectiveSettings),
                CustomBackgroundPath = bg,
                FontSize = effectiveSettings.SongFontSize > 0 ? effectiveSettings.SongFontSize : (FontSize > 0 ? FontSize : 13),
                FontFamily = !string.IsNullOrWhiteSpace(effectiveSettings.SongFontFamily) ? effectiveSettings.SongFontFamily : (!string.IsNullOrWhiteSpace(FontFamily) ? FontFamily : "Arial"),
                IsBold = effectiveSettings.SongFontBold || IsBold,
                IsItalic = IsItalic,
                IsUnderline = IsUnderline,
                HasShadow = HasShadow,
                HasOutline = HasOutline,
                FontColor = FontColor
            });
        }

        return item;
    }
}
