using LuxPresenter.ViewModels;

namespace LuxPresenter.Models;

public class AlertMessageItem : ViewModelBase
{
    private int _count = 3;
    private string _text = string.Empty;

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }
}
