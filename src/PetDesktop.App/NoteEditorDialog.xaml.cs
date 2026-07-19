using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using PetDesktop.Core.Configuration;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using Brush = System.Windows.Media.Brush;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace PetDesktop.App;

public sealed partial class NoteEditorDialog : Window
{
    private SolidColorBrush _calendarText = Brush("#F5F5F7");
    private SolidColorBrush _calendarMuted = Brush("#A1A1A6");
    private readonly HashSet<DayOfWeek> _weeklyDays = [];
    private readonly ComboBox MonthlyDayInput = new() { Width = 92 };

    public NoteEditorDialog(AppTheme theme, string? text = null, DateTime? dueAt = null, string repeat = "single")
    {
        InitializeComponent();
        RepeatInput.Style = (Style)Resources["RepeatCombo"];
        MonthlyDayInput.Style = (Style)Resources["RepeatCombo"];
        MonthlyDayInput.ItemsSource = Enumerable.Range(1, 31).Select(day => day.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        ApplyTheme(theme);
        HourInput.ItemsSource = Enumerable.Range(0, 24).Select(hour => hour.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        MinuteInput.ItemsSource = Enumerable.Range(0, 60).Select(minute => minute.ToString("D2", CultureInfo.InvariantCulture)).ToArray();

        var initial = dueAt ?? DateTime.Now.AddMinutes(1);
        initial = initial.AddSeconds(-initial.Second).AddMilliseconds(-initial.Millisecond);
        NoteTextInput.Text = text ?? string.Empty;
        ReminderEnabled.IsChecked = dueAt is not null;
        DateCalendar.SelectedDate = initial.Date;
        UpdateDateDisplay(initial.Date);
        HourInput.SelectedIndex = initial.Hour;
        MinuteInput.SelectedIndex = initial.Minute;

        SelectRepeat(repeat);
        ReminderChanged(this, new RoutedEventArgs());
    }

    public NoteDraft? Draft { get; private set; }

    private void ApplyTheme(AppTheme theme)
    {
        var light = theme == AppTheme.Light;
        SetBrushResource("Canvas", light ? "#F4F5F7" : "#1C1C1E");
        SetBrushResource("Panel", light ? "#FFFFFF" : "#2C2C2E");
        SetBrushResource("Input", light ? "#F7F8FA" : "#3A3A3C");
        SetBrushResource("Text", light ? "#1C1C1E" : "#F5F5F7");
        SetBrushResource("Muted", light ? "#636366" : "#A1A1A6");
        SetBrushResource("Accent", light ? "#007AFF" : "#0A84FF");
        SetBrushResource("Stroke", light ? "#D8DADF" : "#3D3D40");
        var canvas = (SolidColorBrush)Resources["Canvas"];
        var panel = (SolidColorBrush)Resources["Panel"];
        var input = (SolidColorBrush)Resources["Input"];
        var text = (SolidColorBrush)Resources["Text"];
        var muted = (SolidColorBrush)Resources["Muted"];
        var accent = (SolidColorBrush)Resources["Accent"];
        var stroke = (SolidColorBrush)Resources["Stroke"];
        var hover = Brush(light ? "#E3F0FF" : "#223C5B");
        _calendarText = text;
        _calendarMuted = muted;
        Resources["WheelSelected"] = accent;
        Resources["WheelItem"] = text;
        SetBrushResource("OnAccent", "#FFFFFF");

        // Calendar templates read system brushes instead of the Calendar Foreground property.
        // Override those resources locally so every date cell stays legible in both themes.
        DateCalendar.Resources[System.Windows.SystemColors.ControlTextBrushKey] = text;
        DateCalendar.Resources[System.Windows.SystemColors.WindowTextBrushKey] = text;
        DateCalendar.Resources[System.Windows.SystemColors.GrayTextBrushKey] = muted;
        DateCalendar.Resources[System.Windows.SystemColors.ControlBrushKey] = panel;
        DateCalendar.Resources[System.Windows.SystemColors.WindowBrushKey] = panel;
        DateCalendar.Resources[System.Windows.SystemColors.HighlightBrushKey] = accent;
        DateCalendar.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = Brush("#FFFFFF");
        DateCalendar.Resources["CalendarText"] = text;
        DateCalendar.Resources["CalendarMuted"] = muted;
        DateCalendar.Resources["CalendarAccent"] = accent;
        DateCalendar.Resources["CalendarOnAccent"] = Brush("#FFFFFF");
        DateCalendar.Resources["CalendarHover"] = hover;
        DateCalendar.Background = panel;
        DateCalendar.Foreground = text;
        CalendarPopupSurface.Background = panel;
        CalendarPopupSurface.BorderBrush = stroke;
        RepeatInput.Foreground = text;
        RepeatInput.Background = input;
        RepeatInput.BorderBrush = stroke;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyRepeatTheme);
    }

    private static SolidColorBrush Brush(string color) => new((MediaColor)MediaColorConverter.ConvertFromString(color));

    private void ApplyRepeatTheme()
    {
        foreach (var label in Descendants(this).OfType<TextBlock>().Where(block => block.Text is "重复" or "重复日" or "间隔" or "天"))
        {
            label.Foreground = _calendarMuted;
        }
    }

    private void SetBrushResource(string key, string color)
    {
        if (Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = (MediaColor)MediaColorConverter.ConvertFromString(color);
            return;
        }

        Resources[key] = Brush(color);
    }

    private void SelectRepeat(string repeat)
    {
        if (repeat.StartsWith("ndays:", StringComparison.Ordinal) && int.TryParse(repeat[6..], out var days))
        {
            RepeatInput.SelectedIndex = 4;
            RepeatDaysInput.Text = days.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (repeat.StartsWith("weekly:", StringComparison.Ordinal))
        {
            foreach (var value in repeat[7..].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(value, out var day) && Enum.IsDefined((DayOfWeek)day)) _weeklyDays.Add((DayOfWeek)day);
            }
            RepeatInput.SelectedIndex = 2;
            return;
        }

        if (repeat.StartsWith("monthly:", StringComparison.Ordinal)
            && int.TryParse(repeat[8..], out var monthlyDay)
            && monthlyDay is >= 1 and <= 31)
        {
            RepeatInput.SelectedIndex = 3;
            MonthlyDayInput.SelectedIndex = monthlyDay - 1;
            return;
        }

        RepeatInput.SelectedIndex = repeat switch
        {
            "daily" => 1,
            "weekly" => 2,
            "monthly" => 3,
            _ => 0,
        };
    }

    private void ReminderChanged(object sender, RoutedEventArgs e)
    {
        // Checked can fire while WPF is still assigning named elements from XAML.
        // The constructor applies the final state again after InitializeComponent.
        if (DatePickerButton is null || DateCalendar is null || TimeControls is null || RepeatInput is null || RepeatDaysInput is null)
        {
            return;
        }

        var enabled = ReminderEnabled.IsChecked == true;
        DatePickerButton.IsEnabled = enabled;
        if (!enabled) DatePopup.IsOpen = false;
        TimeControls.IsEnabled = enabled;
        RepeatInput.IsEnabled = enabled;
        RepeatDaysInput.IsEnabled = enabled;
        if (!enabled)
        {
            RepeatDaysPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            RepeatChanged(RepeatInput, null!);
        }
    }

    private void RepeatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepeatInput.SelectedIndex is not (2 or 3 or 4))
        {
            RepeatDaysPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RepeatDaysPanel.Visibility = Visibility.Visible;
        RepeatDaysPanel.Children.Clear();
        RepeatDaysPanel.Orientation = Orientation.Vertical;
        if (RepeatInput.SelectedIndex == 4)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = "间隔", Foreground = (Brush)Resources["Muted"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            RepeatDaysInput.Width = 76;
            RepeatDaysInput.Text ??= "2";
            row.Children.Add(RepeatDaysInput);
            row.Children.Add(new TextBlock { Text = "天", Foreground = (Brush)Resources["Muted"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            RepeatDaysPanel.Children.Add(row);
            return;
        }

        if (RepeatInput.SelectedIndex == 3)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = "每月日期", Foreground = (Brush)Resources["Muted"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            if (MonthlyDayInput.SelectedIndex < 0)
            {
                MonthlyDayInput.SelectedIndex = Math.Clamp((DateCalendar.SelectedDate ?? DateTime.Today).Day - 1, 0, 30);
            }
            row.Children.Add(MonthlyDayInput);
            RepeatDaysPanel.Children.Add(row);
            return;
        }

        RepeatDaysPanel.Children.Add(new TextBlock { Text = "重复日", Foreground = (Brush)Resources["Muted"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var daysPanel = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        {
            var selected = _weeklyDays.Count == 0 ? day == DateCalendar.SelectedDate?.DayOfWeek : _weeklyDays.Contains(day);
            var button = CreateDayButton(DayName(day), selected);
            button.Click += (_, _) =>
            {
                var isSelected = !_weeklyDays.Contains(day);
                if (isSelected) _weeklyDays.Add(day);
                else _weeklyDays.Remove(day);
                ApplyDayButtonAppearance(button, isSelected);
            };
            daysPanel.Children.Add(button);
        }
        RepeatDaysPanel.Children.Add(daysPanel);
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var text = NoteTextInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ValidationMessage.Text = "先写下这张便签想提醒你的事。";
            NoteTextInput.Focus();
            return;
        }

        DateTime? dueAt = null;
        var repeat = "single";
        if (ReminderEnabled.IsChecked == true)
        {
            if (DateCalendar.SelectedDate is not { } date || HourInput.SelectedIndex < 0 || MinuteInput.SelectedIndex < 0)
            {
                ValidationMessage.Text = "请选择完整的提醒日期和时间。";
                return;
            }

            dueAt = date.Date.AddHours(HourInput.SelectedIndex).AddMinutes(MinuteInput.SelectedIndex);
            repeat = RepeatInput.SelectedIndex switch
            {
                1 => "daily",
                2 when _weeklyDays.Count > 0 => $"weekly:{string.Join(',', _weeklyDays.OrderBy(day => ((int)day + 6) % 7).Select(day => (int)day))}",
                2 => string.Empty,
                3 when MonthlyDayInput.SelectedIndex >= 0 => $"monthly:{MonthlyDayInput.SelectedIndex + 1}",
                3 => string.Empty,
                4 when int.TryParse(RepeatDaysInput.Text, out var days) && days > 0 => $"ndays:{days}",
                4 => string.Empty,
                _ => "single",
            };
            if (string.IsNullOrEmpty(repeat))
            {
                ValidationMessage.Text = RepeatInput.SelectedIndex == 2 ? "请至少选择一个每周重复日。" : "“每 N 天”需要填写大于 0 的天数。";
                if (RepeatInput.SelectedIndex == 4) RepeatDaysInput.Focus();
                return;
            }
        }

        Draft = new NoteDraft(text, dueAt, repeat);
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OpenDatePicker(object sender, RoutedEventArgs e)
    {
        DatePopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyRenderedCalendarTheme);
    }

    private void ApplyRenderedCalendarTheme()
    {
        foreach (var element in Descendants(DateCalendar))
        {
            switch (element)
            {
                case System.Windows.Controls.Primitives.CalendarDayButton day:
                    // Local values take precedence over the system calendar theme.
                    day.Foreground = _calendarText;
                    break;
                case System.Windows.Controls.Primitives.CalendarButton month:
                    month.Foreground = _calendarText;
                    break;
                case System.Windows.Controls.Button button:
                    button.Foreground = _calendarText;
                    break;
                case TextBlock label:
                    label.Foreground = _calendarText;
                    break;
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private void DateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DateCalendar.SelectedDate is not { } date) return;
        UpdateDateDisplay(date);
        DatePopup.IsOpen = false;
    }

    private void UpdateDateDisplay(DateTime date) => DateDisplay.Text = date.ToString("yyyy年M月d日", CultureInfo.InvariantCulture);

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日",
    };

    private Button CreateDayButton(string text, bool selected)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 6),
            Padding = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ApplyDayButtonAppearance(button, selected);
        return button;
    }

    private void ApplyDayButtonAppearance(Button button, bool selected)
    {
        button.Background = (Brush)Resources[selected ? "Accent" : "Input"];
        button.BorderBrush = (Brush)Resources[selected ? "Accent" : "Stroke"];
        button.Foreground = (Brush)Resources[selected ? "OnAccent" : "Text"];
    }
}

public sealed record NoteDraft(string Text, DateTime? DueAt, string Repeat);
