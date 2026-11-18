using LuxPresenter.Models;
using LuxPresenter.ViewModels;

namespace LuxPresenter.Views;

/// <summary>
/// A wrapper around BackgroundItem that adds IsSelected state for the Background Options thumbnail grid.
/// </summary>
public class BackgroundEntry : ViewModelBase
{
    private bool _isSelected;

    public BackgroundItem BackgroundItem { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsVideo => BackgroundItem.Type == BackgroundType.Video;

    public string FilePath => BackgroundItem.FilePath;
    public string HexColor => BackgroundItem.HexColor;
    public string Name => BackgroundItem.Name;

    public BackgroundEntry(BackgroundItem item) => BackgroundItem = item;
}
