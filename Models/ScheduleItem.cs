namespace LuxPresenter.Models;

public class ScheduleItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = "Song";
    public ItemCategory Category { get; set; } = ItemCategory.Song;
    public PresentationItem Presentation { get; set; } = new();

    public static ScheduleItem FromPresentation(PresentationItem presentation)
    {
        return new ScheduleItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = presentation.Title,
            TypeLabel = presentation.Category.ToString(),
            Category = presentation.Category,
            Presentation = presentation.Clone()
        };
    }
}
