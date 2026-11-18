using System.Collections.ObjectModel;
using LuxPresenter.Models;

namespace LuxPresenter.Services;

public interface IAlertService
{
    ObservableCollection<AlertMessageItem> RunningMessages { get; }
    AlertMessageItem? CurrentActiveMessage { get; }
    bool IsAlertHidden { get; set; }
    bool IsAlertVisible { get; }

    event Action? AlertStateChanged;

    void AddMessage(string text, int count);
    void RemoveMessage(AlertMessageItem item);
    void ToggleHide();
    void NotifyPassCompleted();
}
