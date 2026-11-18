using LuxPresenter.Services;

namespace LuxPresenter.Models;

public class BibleVerse
{
    public int BookId { get; set; }
    public string Book { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Version { get; set; } = "KJV";

    public string Reference => $"{Book} {Chapter}:{Verse}";

    public Slide ToSlide(AppSettings? settings = null)
    {
        var effectiveSettings = settings ?? new AppSettings();
        return new Slide
        {
            Label = Reference,
            Title = Reference,
            Subtitle = Version,
            Content = LyricsFormatter.Format(Text, effectiveSettings),
            Type = SlideType.Scripture,
            FontSize = 13
        };
    }
}

public class BibleBook
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string IndonesianName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int ChapterCount { get; set; } = 1;
    public bool IsOldTestament { get; set; } = true;

    public override string ToString() => Name;
}
