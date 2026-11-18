using System.Collections.ObjectModel;

namespace LuxPresenter.Models;

public enum ItemCategory
{
    Song,
    Scripture,
    Presentation,
    Media
}

public class PresentationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public ItemCategory Category { get; set; } = ItemCategory.Song;
    public string? BackgroundPath { get; set; }
    public ObservableCollection<Slide> Slides { get; set; } = new();

    public PresentationItem Clone()
    {
        var clone = new PresentationItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = Title,
            Subtitle = Subtitle,
            Category = Category,
            BackgroundPath = BackgroundPath
        };

        foreach (var s in Slides)
        {
            clone.Slides.Add(s.Clone());
        }

        return clone;
    }
}
