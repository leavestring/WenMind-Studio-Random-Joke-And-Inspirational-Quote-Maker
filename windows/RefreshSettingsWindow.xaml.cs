using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace WenMind.Daily;

internal partial class RefreshSettingsWindow : Window
{
    public RefreshSettingsWindow(int? minutes, Func<int?, bool> save)
    {
        InitializeComponent();
        MinutesInput.Text = (minutes ?? 30).ToString(CultureInfo.InvariantCulture);
        DailyMode.IsChecked = minutes is null;
        IntervalMode.IsChecked = minutes is not null;
        MinutesInput.TextChanged += (_, _) => ClearMessage();
        DailyMode.Checked += (_, _) => ClearMessage();
        IntervalMode.Checked += (_, _) => ClearMessage();
        CancelButton.Click += (_, _) => Close();
        SaveButton.Click += (_, _) => Save(save);
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
                return;
            args.Handled = true;
            Close();
        };
    }

    private void Save(Func<int?, bool> save)
    {
        int? minutes = null;
        if (IntervalMode.IsChecked == true)
        {
            if (!int.TryParse(MinutesInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                value is < AppSettings.MinimumMinutes or > AppSettings.MaximumMinutes)
            {
                ShowMessage($"请输入 {AppSettings.MinimumMinutes} 到 {AppSettings.MaximumMinutes} 之间的整数。");
                MinutesInput.Focus();
                MinutesInput.SelectAll();
                return;
            }
            minutes = value;
        }

        if (save(minutes))
            Close();
        else
            ShowMessage("设置未能保存，请重试。");
    }

    private void ShowMessage(string text)
    {
        Message.Text = text;
        Message.Visibility = Visibility.Visible;
    }

    private void ClearMessage() => Message.Visibility = Visibility.Collapsed;
}
