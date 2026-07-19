using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PetDesktop.Core.Configuration;
using PetDesktop.Core.Pets;
using PetDesktop.Core.Progress;
using PetDesktop.Core.Wellness;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace PetDesktop.App;

public partial class MainWindow : Window
{
    private static readonly string[] DefaultQuotes =
    [
        "“慢一点也没关系，今天还有时间。”",
        "“你已经比刚才更靠近目标了。”",
        "“把下一件事做好，就很了不起。”",
    ];
    private static readonly string[] DefaultWellnessPrompts =
    [
        "喝几口水，再继续也不迟。",
        "站起来走两步，让肩颈放松一下。",
        "看向远处二十秒，让眼睛休息一下。",
    ];
    private string _currentPage = "home";
    private AppTheme _theme = AppTheme.Dark;
    private readonly string _notesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PetDesktop", "notes.json");
    private List<NoteItem> _notes = [];
    private Guid? _selectedNoteId;
    private readonly string _progressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PetDesktop", "progress.json");
    private ProgressState _progress = new(0);
    private readonly Action<string, string> _notify;
    private readonly Action<string, PetAction> _showPetMessage;
    private readonly Action<AppTheme> _setTheme;
    private readonly Func<TimeSpan> _inputIdleTime;
    private readonly DispatcherTimer _reminderTimer;
    private WpfCheckBox WellnessEnabledInput = null!;
    private WpfTextBox WellnessInitialMinutesInput = null!;
    private WpfTextBox WellnessRepeatMinutesInput = null!;
    private WpfTextBox WellnessPromptInput = null!;
    private TextBlock WellnessStatus = null!;
    private WpfListBox WellnessPromptsList = null!;
    private Grid CompanionActivityGrid = null!;

    public MainWindow(AppTheme theme, Action<string, string> notify, Action<string, PetAction> showPetMessage, Action<AppTheme> setTheme, Func<TimeSpan> inputIdleTime)
    {
        InitializeComponent();
        CreateActivityGrid();
        CreateWellnessPanel();
        _notify = notify;
        _showPetMessage = showPetMessage;
        _setTheme = setTheme;
        _inputIdleTime = inputIdleTime;
        ApplyTheme(theme);
        LoadNotes();
        LoadProgress();
        RefreshReminderAction();
        RefreshNotes();
        RefreshProgress();
        RefreshContentLists();
        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _progress = _progress with { LastUsageTrackedAt = DateTimeOffset.UtcNow };
        SaveProgress();
        _reminderTimer.Tick += (_, _) => { TrackUsageTime(); CheckReminders(); CheckQuoteSchedule(); CheckWellness(); };
        _reminderTimer.Start();
    }

    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;
        var light = theme == AppTheme.Light;
        Resources["AppBg"] = Brush(light ? "#F4F5F7" : "#1C1C1E");
        Resources["SidebarBg"] = Brush(light ? "#EEF0F3" : "#161617");
        Resources["Surface"] = Brush(light ? "#FFFFFF" : "#2C2C2E");
        Resources["SurfaceMuted"] = Brush(light ? "#F7F8FA" : "#3A3A3C");
        Resources["Border"] = Brush(light ? "#D8DADF" : "#3D3D40");
        Resources["TextPrimary"] = Brush(light ? "#242426" : "#E6E6EB");
        Resources["TextSecondary"] = Brush(light ? "#5E5E63" : "#B2B2B8");
        Resources["TextTertiary"] = Brush(light ? "#85858B" : "#818187");
        Resources["Primary"] = Brush(light ? "#007AFF" : "#0A84FF");
        Resources["OnPrimary"] = Brush("#FFFFFF");
        Resources["Selected"] = Brush(light ? "#E3F0FF" : "#223C5B");
        Resources["Danger"] = Brush(light ? "#E33A31" : "#FF6B63");
        ThemeToggle.Content = light ? "深色外观" : "浅色外观";
        ApplyWellnessInputTheme();
    }

    private static SolidColorBrush Brush(string color) => new((MediaColor)MediaColorConverter.ConvertFromString(color));

    private void ApplyWellnessInputTheme()
    {
        if (WellnessInitialMinutesInput is null) return;
        var foreground = (Brush)Resources["TextPrimary"];
        var background = (Brush)Resources["SurfaceMuted"];
        var border = (Brush)Resources["Border"];
        var accent = (Brush)Resources["Primary"];
        foreach (var input in new[] { WellnessInitialMinutesInput, WellnessRepeatMinutesInput })
        {
            input.Foreground = foreground;
            input.CaretBrush = accent;
            input.SelectionBrush = accent;
            input.Background = Brushes.Transparent;
            input.BorderThickness = new Thickness(0);
        }
    }

    private void CreateWellnessPanel()
    {
        var existingColumns = CompanionPage.ColumnDefinitions
            .Select(column => new ColumnDefinition { Width = column.Width })
            .ToArray();
        var existingContent = CompanionPage.Children.Cast<UIElement>().ToArray();
        CompanionPage.Children.Clear();
        CompanionPage.ColumnDefinitions.Clear();

        var pageContent = new Grid();
        foreach (var column in existingColumns)
        {
            pageContent.ColumnDefinitions.Add(column);
        }
        foreach (var child in existingContent)
        {
            pageContent.Children.Add(child);
        }

        var content = new StackPanel();
        content.Children.Add(ThemeText("健康互动", "TextPrimary", 16, FontWeights.SemiBold));
        content.Children.Add(ThemeText("只根据距上次输入的时长进行本地提醒；不会读取屏幕、窗口或输入内容。", "TextSecondary", margin: new Thickness(0, 5, 0, 12), wrap: true));
        WellnessEnabledInput = new WpfCheckBox { Content = "开启久坐关怀提醒" };
        WellnessEnabledInput.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
        WellnessEnabledInput.Checked += WellnessEnabledChanged;
        WellnessEnabledInput.Unchecked += WellnessEnabledChanged;
        content.Children.Add(WellnessEnabledInput);
        content.Children.Add(CreateMinutesRow("连续使用", out WellnessInitialMinutesInput, "分钟后首次提醒"));
        WellnessInitialMinutesInput.Text = "60";
        var repeatRow = CreateMinutesRow("后续每", out WellnessRepeatMinutesInput, "分钟提醒一次");
        WellnessRepeatMinutesInput.Text = "30";
        var save = new Button { Content = "保存", Style = (Style)Resources["SecondaryButton"], Margin = new Thickness(12, 0, 0, 0) };
        save.Click += SaveWellnessSettingsClick;
        repeatRow.Children.Add(save);
        content.Children.Add(repeatRow);
        WellnessStatus = ThemeText(string.Empty, "TextTertiary", 11, margin: new Thickness(0, 8, 0, 0));
        content.Children.Add(WellnessStatus);
        content.Children.Add(new Separator { Background = (Brush)Resources["Border"], Margin = new Thickness(0, 16, 0, 12) });
        content.Children.Add(ThemeText("自定义健康短句", "TextSecondary", 12));
        var promptRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        WellnessPromptInput = new WpfTextBox { Width = 260 };
        promptRow.Children.Add(WellnessPromptInput);
        var add = new Button { Content = "添加", Style = (Style)Resources["SecondaryButton"], Margin = new Thickness(8, 0, 0, 0) };
        add.Click += AddWellnessPromptClick;
        promptRow.Children.Add(add);
        content.Children.Add(promptRow);
        var delete = new Button { Content = "删除选中短句", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        delete.SetResourceReference(Control.ForegroundProperty, "Danger");
        delete.Click += DeleteSelectedWellnessPromptClick;
        content.Children.Add(delete);
        WellnessPromptsList = new WpfListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 96 };
        WellnessPromptsList.SelectionChanged += WellnessPromptSelectionChanged;
        content.Children.Add(WellnessPromptsList);

        var card = new Border { Style = (Style)Resources["Card"], Padding = new Thickness(18), Margin = new Thickness(0, 16, 0, 0), Child = content };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(pageContent);
        Grid.SetRow(card, 1);
        layout.Children.Add(card);

        CompanionPage.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = layout,
        });
    }

    private static StackPanel CreateMinutesRow(string prefix, out WpfTextBox input, string suffix)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var prefixLabel = new TextBlock { Text = prefix, VerticalAlignment = VerticalAlignment.Center };
        prefixLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        row.Children.Add(prefixLabel);
        input = CreateWellnessNumberInput();
        var numberFrame = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(0), Margin = new Thickness(8, 0, 4, 0), Child = input };
        numberFrame.SetResourceReference(Border.BackgroundProperty, "SurfaceMuted");
        numberFrame.SetResourceReference(Border.BorderBrushProperty, "Border");
        row.Children.Add(numberFrame);
        var suffixLabel = new TextBlock { Text = suffix, VerticalAlignment = VerticalAlignment.Center };
        suffixLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        row.Children.Add(suffixLabel);
        return row;
    }

    private static WpfTextBox CreateWellnessNumberInput()
    {
        var input = new WpfTextBox
        {
            Width = 72,
            Height = 34,
            Margin = new Thickness(8, 0, 4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Opacity = 1,
            Style = null,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 5, 8, 5),
        };
        input.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
        input.SetResourceReference(Control.BackgroundProperty, "SurfaceMuted");
        input.SetResourceReference(Control.BorderBrushProperty, "Border");
        return input;
    }

    private static TextBlock ThemeText(string text, string resourceKey, double fontSize = 13.5, FontWeight? fontWeight = null, Thickness? margin = null, bool wrap = false)
    {
        var block = new TextBlock { Text = text, FontSize = fontSize, Margin = margin ?? new Thickness(), TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (fontWeight is { } weight) block.FontWeight = weight;
        block.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
        return block;
    }

    private void ShowHome(object sender, RoutedEventArgs e) => Navigate("home");
    private void ShowNotes(object sender, RoutedEventArgs e) => Navigate("notes");
    private void ShowCompanion(object sender, RoutedEventArgs e) => Navigate("companion");
    private void ShowSettings(object sender, RoutedEventArgs e) => Navigate("settings");

    private void Navigate(string page)
    {
        _currentPage = page;
        (PageTitle.Text, PageSubtitle.Text, PrimaryAction.Content, ContentTitle.Text, ContentDetail.Text) = page switch
        {
            "notes" => ("便签与提醒", "创建、完成和安排下一件事。", "新建便签  +", "还没有便签", "新建后的便签会显示在这里；提醒功能将在下一步接入。"),
            "companion" => ("陪伴", "管理随机短句与自动成长进度。", "", "等级 1 · 0 / 3", "成长来自陪伴时长与完成的提醒事项。"),
            "settings" => ("外观与通用设置", "所有内容仅保存于本机。", "切换主题", "数据管理", "便签、提醒、短句与等级将提供彼此独立的清除操作。"),
            _ => ("今天", "把注意力留给下一件重要的事。", "新建便签  +", "暂时没有待办事项", "创建一条便签，或为它设置精确到分钟的提醒。"),
        };
        HomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
        NotesPage.Visibility = page == "notes" ? Visibility.Visible : Visibility.Collapsed;
        CompanionPage.Visibility = page == "companion" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        SetNavState(HomeNav, page == "home");
        SetNavState(NotesNav, page == "notes");
        SetNavState(CompanionNav, page == "companion");
        SetNavState(SettingsNav, page == "settings");
        NoteActions.Visibility = page == "home" && _notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SettingsActions.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PrimaryAction.Visibility = page == "companion" ? Visibility.Collapsed : Visibility.Visible;
        QuoteMinutesInput.Text = _progress.QuoteMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (page is "home" or "notes") RefreshNotes();
        RefreshContentLists();
    }

    private void SetNavState(System.Windows.Controls.Button button, bool active)
    {
        button.Background = active ? (System.Windows.Media.Brush)Resources["Selected"] : System.Windows.Media.Brushes.Transparent;
        button.Foreground = active ? (System.Windows.Media.Brush)Resources["Primary"] : (System.Windows.Media.Brush)Resources["TextSecondary"];
    }

    private void PrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (_currentPage == "settings")
        {
            ToggleTheme();
            return;
        }

        CreateNote();
    }

    private void ToggleThemeClick(object sender, RoutedEventArgs e) => ToggleTheme();
    private void ToggleTheme() => _setTheme(_theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    private void ReminderActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReminderActionInput.SelectedIndex < 0) return;
        var action = ReminderActionInput.SelectedIndex == 1 ? PetAction.Waving : PetAction.Jumping;
        if (_progress.ReminderAction == action) return;
        _progress = _progress with { ReminderAction = action };
        SaveProgress();
    }

    private void RefreshReminderAction() =>
        ReminderActionInput.SelectedIndex = NormalizeReminderAction(_progress.ReminderAction) == PetAction.Waving ? 1 : 0;

    private void ClearNotesClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("清除全部便签与提醒？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _notes.Clear(); SaveNotes(); RefreshNotes();
    }
    private void SaveQuoteFrequencyClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(QuoteMinutesInput.Text, out var minutes) || minutes <= 0) return;
        _progress = _progress with { QuoteMinutes = minutes }; SaveProgress(); ContentDetail.Text = $"短句频率已设为每 {minutes} 分钟。";
    }
    private void SaveWellnessSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WellnessInitialMinutesInput.Text, out var initial) || initial is < 1 or > 720 ||
            !int.TryParse(WellnessRepeatMinutesInput.Text, out var repeat) || repeat is < 1 or > 720)
        {
            WellnessStatus.Text = "请输入 1 到 720 之间的分钟数。";
            return;
        }

        _progress = _progress with { WellnessInitialMinutes = initial, WellnessRepeatMinutes = repeat };
        SaveProgress();
        WellnessStatus.Text = "健康提醒设置已保存。";
    }

    private void WellnessEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (WellnessEnabledInput is null) return;
        _progress = _progress with { WellnessEnabled = WellnessEnabledInput.IsChecked == true, WellnessSessionStartedAt = null, WellnessLastPromptAt = null, WellnessOnBreak = false };
        SaveProgress();
    }

    private void AddWellnessPromptClick(object sender, RoutedEventArgs e)
    {
        var prompt = WellnessPromptInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;
        var builtIns = WellnessBuiltIns();
        if (WellnessPromptsList.SelectedIndex is var index && index >= 0 && index < builtIns.Length)
        {
            builtIns[index] = prompt;
            _progress = _progress with { WellnessBuiltInPrompts = builtIns };
        }
        else
        {
            _progress = _progress with { WellnessCustomPrompts = [.. (_progress.WellnessCustomPrompts ?? []), prompt] };
        }
        WellnessPromptInput.Clear();
        SaveProgress();
        RefreshContentLists();
    }

    private void DeleteSelectedWellnessPromptClick(object sender, RoutedEventArgs e)
    {
        if (WellnessPromptsList.SelectedIndex < WellnessBuiltIns().Length || WellnessPromptsList.SelectedItem is not string prompt || !(_progress.WellnessCustomPrompts ?? []).Contains(prompt)) return;
        _progress = _progress with { WellnessCustomPrompts = (_progress.WellnessCustomPrompts ?? []).Where(item => item != prompt).ToArray() };
        SaveProgress();
        RefreshContentLists();
    }
    private void ResetLevelClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("将等级从 1 重新开始？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _progress = _progress with { Points = 0, UsageSeconds = 0 }; SaveProgress(); RefreshProgress();
    }
    private void AddCustomQuoteClick(object sender, RoutedEventArgs e)
    {
        var quote = CustomQuoteInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(quote)) return;
        _progress = _progress with { CustomQuotes = [.. (_progress.CustomQuotes ?? []), quote] };
        SaveProgress(); CustomQuoteInput.Clear(); ContentDetail.Text = "已添加自定义短句。";
        RefreshContentLists();
    }

    private void DeleteSelectedQuoteClick(object sender, RoutedEventArgs e)
    {
        if (QuotesList.SelectedItem is not string quote || !(_progress.CustomQuotes ?? []).Contains(quote)) return;
        _progress = _progress with { CustomQuotes = (_progress.CustomQuotes ?? []).Where(item => item != quote).ToArray() };
        SaveProgress();
        RefreshContentLists();
    }

    private void ClearCustomQuotesClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("清空所有自定义短句？内置短句不会受影响。", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _progress = _progress with { CustomQuotes = [] };
        SaveProgress();
        RefreshContentLists();
    }

    private void CompleteNote(object sender, RoutedEventArgs e)
    {
        var note = SelectedNote();
        if (note is null) return;
        _notes[_notes.IndexOf(note)] = note.DueAt is { } dueAt && note.Repeat != "single"
            ? note with { DueAt = AdvanceDue(dueAt, note.Repeat), Notified = false }
            : note with { Completed = true };
        SaveNotes();
        AddProgress(note.DueAt is null ? 1 : 2);
        RefreshProgress();
        RefreshNotes();
        RefreshContentLists();
    }

    private void DeleteNote(object sender, RoutedEventArgs e)
    {
        var note = SelectedNote();
        if (note is null) return;
        _notes.Remove(note);
        SaveNotes();
        RefreshNotes();
        RefreshContentLists();
    }

    private void EditNote(object sender, RoutedEventArgs e)
    {
        var note = SelectedNote();
        if (note is null) return;
        var editor = new NoteEditorDialog(_theme, note.Text, note.DueAt, note.Repeat);
        if (IsVisible) editor.Owner = this;
        if (editor.ShowDialog() != true || editor.Draft is not { } draft) return;
        _notes[_notes.IndexOf(note)] = note with { Text = draft.Text, DueAt = draft.DueAt, Repeat = draft.Repeat, Notified = false };
        SaveNotes();
        RefreshNotes();
        RefreshContentLists();
    }

    public void CreateNote()
    {
        var editor = new NoteEditorDialog(_theme);
        if (IsVisible) editor.Owner = this;
        if (editor.ShowDialog() != true || editor.Draft is not { } draft) return;
        _notes.Add(new NoteItem(Guid.NewGuid(), draft.Text, false, DateTimeOffset.UtcNow, draft.DueAt, false, draft.Repeat));
        SaveNotes();
        _currentPage = "notes";
        Navigate("notes");
    }

    private void SnoozeNote(object sender, RoutedEventArgs e)
    {
        var note = SelectedNote();
        if (note is null) return;
        _notes[_notes.IndexOf(note)] = note with { DueAt = DateTime.Now.AddMinutes(10), Notified = false };
        SaveNotes();
        RefreshNotes();
        RefreshContentLists();
    }

    private void RefreshNotes()
    {
        var note = _notes.LastOrDefault(item => !item.Completed);
        if (note is null)
        {
            ContentTitle.Text = "暂时没有待办事项";
            ContentDetail.Text = "创建一条便签，或为它设置精确到分钟的提醒。";
            NoteActions.Visibility = Visibility.Collapsed;
            return;
        }

        ContentTitle.Text = note.Text;
        ContentDetail.Text = note.DueAt is { } dueAt
            ? $"提醒时间：{dueAt:yyyy-MM-dd HH:mm}；重复：{note.Repeat}。"
            : "已保存到本机。你可以完成或删除这条便签。";
        NoteActions.Visibility = _currentPage is "home" or "notes" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadNotes()
    {
        try
        {
            _notes = JsonSerializer.Deserialize<List<NoteItem>>(File.ReadAllText(_notesPath)) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _notes = [];
        }
    }

    private void SaveNotes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_notesPath)!);
        File.WriteAllText(_notesPath, JsonSerializer.Serialize(_notes));
    }

    private void LoadProgress()
    {
        try { _progress = JsonSerializer.Deserialize<ProgressState>(File.ReadAllText(_progressPath)) ?? new(0); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { _progress = new(0); }
        _progress = _progress with
        {
            ReminderAction = NormalizeReminderAction(_progress.ReminderAction),
            WellnessInitialMinutes = Math.Clamp(_progress.WellnessInitialMinutes, 1, 720),
            WellnessRepeatMinutes = Math.Clamp(_progress.WellnessRepeatMinutes, 1, 720),
            DailyUsageSeconds = _progress.DailyUsageSeconds ?? [],
        };
    }

    private void SaveProgress()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_progressPath)!);
        File.WriteAllText(_progressPath, JsonSerializer.Serialize(_progress));
    }

    private void RefreshProgress()
    {
        var level = 1;
        var remaining = _progress.Points;
        while (remaining >= RequiredForLevel(level)) { remaining -= RequiredForLevel(level); level++; }
        LevelDisplay.Text = $"等级 {level}";
        ProgressDisplay.Text = $"{remaining} / {RequiredForLevel(level)} 次成长进度 · 陪伴 {CompanionDurationFormatter.Format(_progress.UsageSeconds)}";
        RefreshActivityGrid();
        QuoteDisplay.Text = (level % 3) switch
        {
            1 => "“慢一点也没关系，今天还有时间。”",
            2 => "“你已经比刚才更靠近目标了。”",
            _ => "“把下一件事做好，就很了不起。”",
        };
    }

    private static int RequiredForLevel(int level) => 3 + ((level - 1) * 2);

    private void CreateActivityGrid()
    {
        var parent = (StackPanel)VisualTreeHelper.GetParent(ProgressDisplay);
        var index = parent.Children.IndexOf(ProgressDisplay) + 1;
        var label = new TextBlock { Text = "近一年陪伴", FontSize = 11, Margin = new Thickness(0, 16, 0, 6) };
        label.SetResourceReference(Control.ForegroundProperty, "TextTertiary");
        CompanionActivityGrid = new Grid { Height = 42, HorizontalAlignment = HorizontalAlignment.Left };
        parent.Children.Insert(index, label);
        parent.Children.Insert(index + 1, CompanionActivityGrid);
    }

    private void RefreshActivityGrid()
    {
        CompanionActivityGrid.Children.Clear();
        CompanionActivityGrid.ColumnDefinitions.Clear();
        CompanionActivityGrid.RowDefinitions.Clear();
        for (var column = 0; column < 53; column++) CompanionActivityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        for (var row = 0; row < 7; row++) CompanionActivityGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });

        var usageByDay = _progress.DailyUsageSeconds ?? [];
        var today = DateTime.Today;
        var start = today.AddDays(-364).AddDays(-(int)today.AddDays(-364).DayOfWeek);
        for (var index = 0; index < 371; index++)
        {
            var date = start.AddDays(index);
            var isFuture = date > today;
            var seconds = isFuture ? 0 : usageByDay.GetValueOrDefault(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            var cell = new Border
            {
                Width = 4,
                Height = 4,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(1.5),
                Background = isFuture ? Brushes.Transparent : ActivityBrush(seconds),
                ToolTip = isFuture ? null : $"{date:yyyy-MM-dd} · {CompanionDurationFormatter.Format(seconds)}",
            };
            Grid.SetColumn(cell, index / 7);
            Grid.SetRow(cell, (int)date.DayOfWeek);
            CompanionActivityGrid.Children.Add(cell);
        }
    }

    private Brush ActivityBrush(long seconds) => seconds switch
    {
        <= 0 => (Brush)Resources["SurfaceMuted"],
        < 900 => Brush(_theme == AppTheme.Light ? "#D8E8FF" : "#1E3C5A"),
        < 3600 => Brush(_theme == AppTheme.Light ? "#8FC0FF" : "#2B6FAF"),
        < 10800 => Brush(_theme == AppTheme.Light ? "#3D92F5" : "#0A84FF"),
        _ => (Brush)Resources["Primary"],
    };

    private void RefreshContentLists()
    {
        var entries = _notes.Where(note => !note.Completed)
            .Select(note => new NoteListEntry(note.Id, note.DueAt is { } due ? $"• {note.Text}  ·  {due:MM-dd HH:mm}" : $"• {note.Text}"))
            .ToArray();
        NotesList.ItemsSource = entries;
        NotesList.SelectedItem = entries.FirstOrDefault(entry => entry.Id == _selectedNoteId);
        QuotesList.ItemsSource = DefaultQuotes.Concat(_progress.CustomQuotes ?? []).ToArray();
        WellnessPromptsList.ItemsSource = WellnessBuiltIns().Concat(_progress.WellnessCustomPrompts ?? []).ToArray();
        WellnessEnabledInput.IsChecked = _progress.WellnessEnabled;
        WellnessInitialMinutesInput.Text = _progress.WellnessInitialMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        WellnessRepeatMinutesInput.Text = _progress.WellnessRepeatMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void NotesSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _selectedNoteId = NotesList.SelectedItem is NoteListEntry entry ? entry.Id : null;

    private NoteItem? SelectedNote() => _selectedNoteId is { } id
        ? _notes.FirstOrDefault(note => note.Id == id && !note.Completed)
        : _notes.LastOrDefault(note => !note.Completed);

    private void CheckReminders()
    {
        var now = DateTime.Now;
        var changed = false;
        foreach (var note in _notes.Where(item => !item.Completed && !item.Notified && item.DueAt is not null && item.DueAt <= now).ToArray())
        {
            _notify("Pet Desktop 提醒", note.Text);
            _showPetMessage(note.Text, NormalizeReminderAction(_progress.ReminderAction));
            _notes[_notes.IndexOf(note)] = note with { Notified = true };
            changed = true;
        }
        if (changed) SaveNotes();
    }

    private void TrackUsageTime()
    {
        var now = DateTimeOffset.UtcNow;
        var last = _progress.LastUsageTrackedAt ?? now;
        var elapsedSeconds = Math.Clamp((long)(now - last).TotalSeconds, 0, 30);
        var before = _progress.UsageSeconds;
        var after = before + elapsedSeconds;
        var gained = (after / 900) - (before / 900);
        var dailyUsage = new Dictionary<string, long>(_progress.DailyUsageSeconds ?? []);
        var localDate = DateTimeOffset.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        dailyUsage[localDate] = dailyUsage.GetValueOrDefault(localDate) + elapsedSeconds;
        _progress = _progress with
        {
            UsageSeconds = after,
            LastUsageTrackedAt = now,
            Points = _progress.Points + checked((int)gained),
            DailyUsageSeconds = dailyUsage,
        };
        SaveProgress();
        if (gained > 0 || (after / 60) != (before / 60)) RefreshProgress();
    }

    private void AddProgress(int amount)
    {
        if (amount <= 0) return;
        _progress = _progress with { Points = _progress.Points + amount };
        SaveProgress();
        RefreshProgress();
    }

    public void ShowRandomQuote()
    {
        var quotes = DefaultQuotes.Concat(_progress.CustomQuotes ?? []).ToArray();
        if (quotes.Length == 0) return;
        var quote = quotes[Random.Shared.Next(quotes.Length)];
        QuoteDisplay.Text = quote;
        _showPetMessage(quote, PetAction.Waving);
        _progress = _progress with { LastQuoteAt = DateTimeOffset.UtcNow };
        SaveProgress();
    }

    private void CheckQuoteSchedule()
    {
        var now = DateTimeOffset.UtcNow;
        if (_progress.LastQuoteAt is { } lastQuote && now - lastQuote < TimeSpan.FromMinutes(_progress.QuoteMinutes)) return;
        ShowRandomQuote();
    }

    private void CheckWellness()
    {
        var policy = new WellnessPolicy(_progress.WellnessEnabled, _progress.WellnessInitialMinutes, _progress.WellnessRepeatMinutes);
        var state = new WellnessState(_progress.WellnessSessionStartedAt, _progress.WellnessLastPromptAt, _progress.WellnessOnBreak);
        var evaluation = WellnessPromptEvaluator.Evaluate(policy, state, DateTimeOffset.UtcNow, _inputIdleTime());
        _progress = _progress with
        {
            WellnessSessionStartedAt = evaluation.State.SessionStartedAt,
            WellnessLastPromptAt = evaluation.State.LastPromptAt,
            WellnessOnBreak = evaluation.State.OnBreak,
        };
        if (!evaluation.PromptDue)
        {
            return;
        }

        var prompts = WellnessBuiltIns().Concat(_progress.WellnessCustomPrompts ?? []).ToArray();
        var prompt = prompts[Random.Shared.Next(prompts.Length)];
        _notify("健康提醒", prompt);
        _showPetMessage(prompt, PetAction.Waving);
        SaveProgress();
    }

    private static bool IsValidRepeat(string repeat) => repeat is "single" or "daily" or "weekly" or "monthly" ||
        (repeat.StartsWith("ndays:", StringComparison.Ordinal) && int.TryParse(repeat[6..], out var days) && days > 0) ||
        (repeat.StartsWith("weekly:", StringComparison.Ordinal) && repeat[7..].Split(',', StringSplitOptions.RemoveEmptyEntries).All(day => int.TryParse(day, out var value) && value is >= 0 and <= 6)) ||
        (repeat.StartsWith("monthly:", StringComparison.Ordinal) && int.TryParse(repeat[8..], out var dayOfMonth) && dayOfMonth is >= 1 and <= 31);

    private static DateTime AdvanceDue(DateTime dueAt, string repeat) => repeat switch
    {
        "daily" => dueAt.AddDays(1),
        "weekly" => dueAt.AddDays(7),
        _ when repeat.StartsWith("weekly:", StringComparison.Ordinal) => AdvanceWeeklyDue(dueAt, repeat[7..]),
        "monthly" => dueAt.AddMonths(1),
        _ when repeat.StartsWith("monthly:", StringComparison.Ordinal) => AdvanceMonthlyDue(dueAt, int.Parse(repeat[8..], System.Globalization.CultureInfo.InvariantCulture)),
        _ when repeat.StartsWith("ndays:", StringComparison.Ordinal) => dueAt.AddDays(int.Parse(repeat[6..], System.Globalization.CultureInfo.InvariantCulture)),
        _ => dueAt,
    };

    private static DateTime AdvanceMonthlyDue(DateTime dueAt, int requestedDay)
    {
        var nextMonth = dueAt.AddMonths(1);
        var actualDay = Math.Min(requestedDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return new DateTime(nextMonth.Year, nextMonth.Month, actualDay, dueAt.Hour, dueAt.Minute, dueAt.Second, dueAt.Kind);
    }

    private static DateTime AdvanceWeeklyDue(DateTime dueAt, string selectedDays)
    {
        var days = selectedDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Select(value => (DayOfWeek)value).ToHashSet();
        for (var offset = 1; offset <= 7; offset++)
        {
            var candidate = dueAt.AddDays(offset);
            if (days.Contains(candidate.DayOfWeek)) return candidate;
        }
        return dueAt.AddDays(7);
    }

    private static PetAction NormalizeReminderAction(PetAction action) =>
        action is PetAction.Jumping or PetAction.Waving ? action : PetAction.Jumping;

    private string[] WellnessBuiltIns() => _progress.WellnessBuiltInPrompts is { Length: > 0 } values && values.Length == DefaultWellnessPrompts.Length
        ? [.. values]
        : [.. DefaultWellnessPrompts];

    private void WellnessPromptSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WellnessPromptsList.SelectedItem is string prompt) WellnessPromptInput.Text = prompt;
    }

    private sealed record NoteItem(Guid Id, string Text, bool Completed, DateTimeOffset CreatedAt, DateTime? DueAt = null, bool Notified = false, string Repeat = "single");
    private sealed record NoteListEntry(Guid Id, string Display) { public override string ToString() => Display; }
    private sealed record ProgressState(
        int Points,
        int QuoteMinutes = 60,
        DateTimeOffset? LastQuoteAt = null,
        string[]? CustomQuotes = null,
        long UsageSeconds = 0,
        Dictionary<string, long>? DailyUsageSeconds = null,
        DateTimeOffset? LastUsageTrackedAt = null,
        PetAction ReminderAction = PetAction.Jumping,
        bool WellnessEnabled = true,
        int WellnessInitialMinutes = 60,
        int WellnessRepeatMinutes = 30,
        string[]? WellnessBuiltInPrompts = null,
        string[]? WellnessCustomPrompts = null,
        DateTimeOffset? WellnessSessionStartedAt = null,
        DateTimeOffset? WellnessLastPromptAt = null,
        bool WellnessOnBreak = false);

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
