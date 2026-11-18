using System.Collections.ObjectModel;
using System.Windows;
using LuxPresenter.Models;

namespace LuxPresenter.Services;

public class AlertService : IAlertService
{
    private static AlertService? _instance;
    public static AlertService Instance => _instance ??= new AlertService();

    public ObservableCollection<AlertMessageItem> RunningMessages { get; } = new();

    private AlertMessageItem? _currentActiveMessage;
    public AlertMessageItem? CurrentActiveMessage
    {
        get => _currentActiveMessage;
        private set => _currentActiveMessage = value;
    }

    private bool _isAlertHidden = false;
    public bool IsAlertHidden
    {
        get => _isAlertHidden;
        set
        {
            if (_isAlertHidden != value)
            {
                _isAlertHidden = value;
                AlertStateChanged?.Invoke();
            }
        }
    }

    public bool IsAlertVisible => !_isAlertHidden && CurrentActiveMessage != null && CurrentActiveMessage.Count > 0;

    public event Action? AlertStateChanged;

    public void AddMessage(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (count <= 0) count = 1;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var item = new AlertMessageItem
            {
                Text = text.Trim(),
                Count = count
            };

            RunningMessages.Add(item);

            if (CurrentActiveMessage == null)
            {
                CurrentActiveMessage = item;
                IsAlertHidden = false;
            }

            AlertStateChanged?.Invoke();
        });
    }

    public void RemoveMessage(AlertMessageItem item)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            bool wasActive = CurrentActiveMessage == item;
            RunningMessages.Remove(item);

            if (wasActive)
            {
                CurrentActiveMessage = RunningMessages.FirstOrDefault();
            }

            AlertStateChanged?.Invoke();
        });
    }

    public void ToggleHide()
    {
        IsAlertHidden = !IsAlertHidden;
    }

    public void NotifyPassCompleted()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (CurrentActiveMessage == null) return;

            CurrentActiveMessage.Count--;

            if (CurrentActiveMessage.Count <= 0)
            {
                RunningMessages.Remove(CurrentActiveMessage);
                CurrentActiveMessage = RunningMessages.FirstOrDefault();
            }

            AlertStateChanged?.Invoke();
        });
    }
}
