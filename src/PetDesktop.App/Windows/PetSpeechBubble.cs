using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using PetDesktop.Core.Configuration;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace PetDesktop.App.Windows;

internal sealed class PetSpeechBubble
{
    private readonly Window _window;
    private readonly Border _surface;
    private readonly Path _tail;
    private readonly TextBlock _text;
    private readonly StackPanel _content;
    private readonly DispatcherTimer _hideTimer;
    private bool _tailPointsDown = true;
    private int _lastPetX = int.MinValue;
    private int _lastPetY = int.MinValue;
    private int _lastPetWidth;
    private int _lastPetHeight;

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
        var body = new StackPanel();
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
        _text.Foreground = light
            ? new SolidColorBrush(MediaColor.FromRgb(36, 36, 38))
            : new SolidColorBrush(MediaColor.FromRgb(230, 230, 235));
    }

    public void Show(string text, int x, int y, int petWidth, int petHeight)
    {
        _text.Text = text;
        if (!_window.IsVisible) _window.Show();
        _window.UpdateLayout();
        ResetFollowPosition();
        Place(x, y, petWidth, petHeight, force: true);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void Follow(int x, int y, int petWidth, int petHeight)
    {
        if (!_window.IsVisible) return;
        Place(x, y, petWidth, petHeight, force: false);
    }

    private void Place(int x, int y, int petWidth, int petHeight, bool force)
    {
        if (!force
            && x == _lastPetX
            && y == _lastPetY
            && petWidth == _lastPetWidth
            && petHeight == _lastPetHeight)
        {
            return;
        }

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x + (petWidth / 2), y + (petHeight / 2)));
        var area = screen.WorkingArea;
        SetTailDirection(pointsDown: true);
        _window.UpdateLayout();
        var desired = GetPhysicalSize();
        var petCenter = x + (petWidth / 2d);
        var left = Math.Clamp(petCenter - (desired.Width / 2d), area.Left + 8d, area.Right - desired.Width - 8d);
        var above = y - desired.Height - 12 >= area.Top + 8;
        if (!above)
        {
            SetTailDirection(pointsDown: false);
            _window.UpdateLayout();
            desired = GetPhysicalSize();
            left = Math.Clamp(petCenter - (desired.Width / 2d), area.Left + 8d, area.Right - desired.Width - 8d);
        }

        var tailInset = Math.Clamp(petCenter - left - 9d, 20d, Math.Max(20d, desired.Width - 27d));
        _tail.Margin = new Thickness(tailInset, -1, 0, 0);
        var top = above
            ? Math.Max(area.Top + 8, y - desired.Height - 12)
            : Math.Min(area.Bottom - desired.Height - 8, y + petHeight + 10);
        MoveToPhysicalScreenPosition((int)Math.Round(left), (int)Math.Round(top));
        _lastPetX = x;
        _lastPetY = y;
        _lastPetWidth = petWidth;
        _lastPetHeight = petHeight;
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _window.Hide();
        ResetFollowPosition();
    }

    private System.Windows.Size GetPhysicalSize()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (!NativeMethods.GetWindowRect(handle, out var rectangle))
        {
            return new System.Windows.Size(
                Math.Max(1d, _window.ActualWidth),
                Math.Max(1d, _window.ActualHeight));
        }

        return new System.Windows.Size(
            Math.Max(1, rectangle.Right - rectangle.Left),
            Math.Max(1, rectangle.Bottom - rectangle.Top));
    }

    private void MoveToPhysicalScreenPosition(int left, int top)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        _ = NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopmost,
            left,
            top,
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    private void ResetFollowPosition()
    {
        _lastPetX = int.MinValue;
        _lastPetY = int.MinValue;
        _lastPetWidth = 0;
        _lastPetHeight = 0;
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
