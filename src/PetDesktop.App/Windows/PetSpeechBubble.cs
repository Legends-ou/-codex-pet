using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PetDesktop.Core.Configuration;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace PetDesktop.App.Windows;

internal sealed class PetSpeechBubble
{
    private readonly Window _window;
    private readonly Border _surface;
    private readonly Border _accent;
    private readonly Path _tail;
    private readonly TextBlock _text;
    private readonly StackPanel _content;
    private readonly DispatcherTimer _hideTimer;
    private bool _tailPointsDown = true;

    public PetSpeechBubble()
    {
        _text = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 14,
            FontWeight = FontWeights.Normal,
            LineHeight = 22,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 280,
        };
        _accent = new Border
        {
            Width = 24,
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 0, 8),
        };
        var body = new StackPanel();
        body.Children.Add(_accent);
        body.Children.Add(_text);
        _surface = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 12, 16, 13),
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 5,
                Opacity = 0.18,
                Color = MediaColor.FromArgb(255, 0, 0, 0),
            },
            Child = body,
        };
        _tail = new Path
        {
            Data = Geometry.Parse("M 0,0 L 18,0 L 5,12 Z"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(28, -1, 0, 0),
        };
        _content = new StackPanel();
        _content.Children.Add(_surface);
        _content.Children.Add(_tail);
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = MediaBrushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = _content,
        };
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _hideTimer.Tick += (_, _) => Hide();
        ApplyTheme(AppTheme.Dark);
    }

    public void ApplyTheme(AppTheme theme)
    {
        var light = theme == AppTheme.Light;
        var background = new SolidColorBrush(MediaColor.FromArgb(248, light ? (byte)255 : (byte)31, light ? (byte)255 : (byte)31, light ? (byte)255 : (byte)34));
        _surface.Background = background;
        _surface.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(150, light ? (byte)218 : (byte)255, light ? (byte)218 : (byte)255, light ? (byte)218 : (byte)220));
        _tail.Fill = background;
        _accent.Background = new SolidColorBrush(light ? MediaColor.FromRgb(0, 122, 255) : MediaColor.FromRgb(10, 132, 255));
        _text.Foreground = light
            ? new SolidColorBrush(MediaColor.FromRgb(36, 36, 38))
            : new SolidColorBrush(MediaColor.FromRgb(230, 230, 235));
    }

    public void Show(string text, int x, int y, int petWidth, int petHeight)
    {
        _text.Text = text;
        Place(x, y, petWidth, petHeight, measure: true);
        if (!_window.IsVisible) _window.Show();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void Follow(int x, int y, int petWidth, int petHeight)
    {
        if (!_window.IsVisible) return;
        Place(x, y, petWidth, petHeight, measure: false);
    }

    private void Place(int x, int y, int petWidth, int petHeight, bool measure)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x + (petWidth / 2), y + (petHeight / 2)));
        var area = screen.WorkingArea;
        SetTailDirection(pointsDown: true);
        if (measure) _window.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = measure ? _window.DesiredSize : new System.Windows.Size(_window.ActualWidth, _window.ActualHeight);
        var petCenter = x + (petWidth / 2d);
        var left = Math.Clamp(petCenter - (desired.Width / 2d), area.Left + 8d, area.Right - desired.Width - 8d);
        var above = y - desired.Height - 12 >= area.Top + 8;
        if (!above)
        {
            SetTailDirection(pointsDown: false);
            if (measure) _window.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            desired = measure ? _window.DesiredSize : new System.Windows.Size(_window.ActualWidth, _window.ActualHeight);
            left = Math.Clamp(petCenter - (desired.Width / 2d), area.Left + 8d, area.Right - desired.Width - 8d);
        }

        var tailInset = Math.Clamp(petCenter - left - 9d, 20d, Math.Max(20d, desired.Width - 27d));
        _tail.Margin = new Thickness(tailInset, -1, 0, 0);
        _window.Left = left;
        _window.Top = above
            ? Math.Max(area.Top + 8, y - desired.Height - 12)
            : Math.Min(area.Bottom - desired.Height - 8, y + petHeight + 10);
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _window.Hide();
    }

    private void SetTailDirection(bool pointsDown)
    {
        if (_tailPointsDown == pointsDown && _content.Children.Count > 0) return;
        _tailPointsDown = pointsDown;
        _content.Children.Clear();
        if (pointsDown)
        {
            _tail.Data = Geometry.Parse("M 0,0 L 18,0 L 5,12 Z");
            _content.Children.Add(_surface);
            _content.Children.Add(_tail);
        }
        else
        {
            _tail.Data = Geometry.Parse("M 0,12 L 18,12 L 5,0 Z");
            _content.Children.Add(_tail);
            _content.Children.Add(_surface);
        }
    }

    public void Dispose()
    {
        _hideTimer.Stop();
        _window.Close();
    }
}
