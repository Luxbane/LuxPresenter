namespace LuxPresenter.Models;

public enum MediaType
{
    Image,
    Video,
    Color,
    Audio
}

public class MediaItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaType Type { get; set; } = MediaType.Image;
    public string HexColor { get; set; } = "#001A33"; // Default deep blue
    public bool IsDefault { get; set; } = false;
}
