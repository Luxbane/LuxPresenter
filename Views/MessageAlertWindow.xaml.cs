using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LuxPresenter.Models;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class MessageAlertWindow : Window
{
    private readonly IAlertService _alertService;
    private readonly IDataService _dataService;

    public MessageAlertWindow(IDataService? dataService = null)
    {
        InitializeComponent();
        _alertService = AlertService.Instance;
        _dataService = dataService ?? new DataService();

        LvRunningMessages.ItemsSource = _alertService.RunningMessages;

        LoadPresetMessages();

        UpdateButtonStates();
        _alertService.AlertStateChanged += OnAlertStateChanged;
        Closed += (s, e) => _alertService.AlertStateChanged -= OnAlertStateChanged;

        CmbMessage.PreviewKeyDown += OnCmbMessagePreviewKeyDown;
    }

    private void LoadPresetMessages()
    {
        CmbMessage.Items.Clear();

        var settings = _dataService.GetSettings();
        if (settings.PresetAlertMessages != null)
        {
            foreach (var msg in settings.PresetAlertMessages)
            {
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    CmbMessage.Items.Add(msg);
                }
            }
        }

        if (CmbMessage.Items.Count > 0)
        {
            CmbMessage.SelectedIndex = 0;
        }
        else
        {
            CmbMessage.Text = string.Empty;
        }
    }

    private void SavePresetMessages()
    {
        try
        {
            var settings = _dataService.GetSettings();
            settings.PresetAlertMessages = CmbMessage.Items
                .Cast<object>()
                .Select(x => x?.ToString()?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _dataService.SaveSettings(settings);
        }
        catch { }
    }

    private void OnCmbMessagePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && (Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Control || CmbMessage.IsDropDownOpen))
        {
            OnDeletePresetClicked(sender, e);
            e.Handled = true;
        }
    }

    private void OnAlertStateChanged()
    {
        Dispatcher.BeginInvoke(UpdateButtonStates);
    }

    private void UpdateButtonStates()
    {
        BtnHide.Content = _alertService.IsAlertHidden ? "Show" : "Hide";
    }

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        var text = CmbMessage.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!int.TryParse(TxtCount.Text, out int count) || count <= 0)
        {
            count = 3;
        }

        _alertService.AddMessage(text, count);

        // Add to combobox items if not present
        bool exists = false;
        foreach (var item in CmbMessage.Items)
        {
            if (string.Equals(item?.ToString()?.Trim(), text, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        if (!exists)
        {
            CmbMessage.Items.Add(text);
            SavePresetMessages();
        }

        UpdateButtonStates();
    }

    private void OnDeletePresetClicked(object sender, RoutedEventArgs e)
    {
        string? targetText = CmbMessage.SelectedItem?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(targetText))
        {
            targetText = CmbMessage.Text?.Trim();
        }

        if (string.IsNullOrWhiteSpace(targetText))
        {
            MessageBox.Show("Please select a preset message from the dropdown list to delete.", "Delete Preset Message", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        object? matchItem = null;
        foreach (var item in CmbMessage.Items)
        {
            if (string.Equals(item?.ToString()?.Trim(), targetText, StringComparison.OrdinalIgnoreCase))
            {
                matchItem = item;
                break;
            }
        }

        if (matchItem == null)
        {
            MessageBox.Show($"\"{targetText}\" is not in the preset list.", "Delete Preset Message", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete this preset message from the list?\n\n\"{matchItem}\"",
            "Delete Preset Message",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            CmbMessage.Items.Remove(matchItem);
            SavePresetMessages();

            if (CmbMessage.Items.Count > 0)
            {
                CmbMessage.SelectedIndex = 0;
            }
            else
            {
                CmbMessage.Text = string.Empty;
            }
        }
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (LvRunningMessages.SelectedItem is AlertMessageItem selected)
        {
            _alertService.RemoveMessage(selected);
        }
        else if (_alertService.RunningMessages.Count > 0)
        {
            _alertService.RemoveMessage(_alertService.RunningMessages[0]);
        }
        UpdateButtonStates();
    }

    private void OnHideClicked(object sender, RoutedEventArgs e)
    {
        _alertService.ToggleHide();
        UpdateButtonStates();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCountUpClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtCount.Text, out int val))
        {
            if (val < 99) TxtCount.Text = (val + 1).ToString();
        }
        else
        {
            TxtCount.Text = "3";
        }
    }

    private void OnCountDownClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtCount.Text, out int val))
        {
            if (val > 1) TxtCount.Text = (val - 1).ToString();
        }
        else
        {
            TxtCount.Text = "3";
        }
    }
}
