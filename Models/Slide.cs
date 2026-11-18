using System.Windows;

namespace LuxPresenter.Models;

public enum SlideType
{
    Title,
    Verse,
    Chorus,
    Bridge,
    Ending,
    Scripture,
    General
}

public class Slide
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = "Slide"; // e.g. "Verse 1", "Chorus", "Genesis 1:1"
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? CustomBackgroundPath { get; set; }
    public SlideType Type { get; set; } = SlideType.Verse;

    // Typography overrides per slide / song (Image 2)
    public string FontFamily { get; set; } = "Tahoma";
    public int FontSize { get; set; } = 13;
    public bool IsBold { get; set; } = true;
    public bool IsItalic { get; set; } = false;
    public bool IsUnderline { get; set; } = false;
    public bool HasShadow { get; set; } = true;
    public bool HasOutline { get; set; } = true;
    public string FontColor { get; set; } = "#FFFFFF";

    public Thickness ContainerMargin { get; set; } = new Thickness(1, 1, 1, 2);

    public Slide Clone()
    {
        return new Slide
        {
            Id = Guid.NewGuid().ToString(),
            Label = Label,
            Content = Content,
            Title = Title,
            Subtitle = Subtitle,
            CustomBackgroundPath = CustomBackgroundPath,
            Type = Type,
            FontFamily = FontFamily,
            FontSize = FontSize,
            IsBold = IsBold,
            IsItalic = IsItalic,
            IsUnderline = IsUnderline,
            HasShadow = HasShadow,
            HasOutline = HasOutline,
            FontColor = FontColor,
            ContainerMargin = ContainerMargin
        };
    }
}
