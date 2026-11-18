namespace LuxPresenter.Models;

public enum BackgroundType
{
    Image,
    Video,
    Color
}

public class BackgroundItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public BackgroundType Type { get; set; } = BackgroundType.Image;
    public string HexColor { get; set; } = "#001833";
    public bool IsDefault { get; set; } = false;
}
