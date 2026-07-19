using System.Drawing;
using System.Windows.Forms;

namespace PetDesktop.App.Menus;

internal sealed class RoundSlider : Control
{
    private int _value;
    private readonly PetMenuPalette _palette;

    public RoundSlider(int minimum, int maximum, int value, PetMenuPalette palette)
    {
        Minimum = minimum;
        Maximum = Math.Max(minimum, maximum);
        _value = Math.Clamp(value, Minimum, Maximum);
        Size = new Size(196, 32);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        _palette = palette;
        BackColor = palette.Surface;
    }

    public int Minimum { get; }
    public int Maximum { get; }
    public int Value => _value;
    public event Action<int>? ValueChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(_palette.Surface);
        var track = new Rectangle(12, 14, Width - 24, 4);
        using var background = new SolidBrush(_palette.Track);
        using var fill = new SolidBrush(_palette.Accent);
        e.Graphics.FillRoundedRectangle(background, track, new Size(4, 4));
        var fraction = Maximum == Minimum ? 0d : (double)(_value - Minimum) / (Maximum - Minimum);
        var centerX = track.Left + (int)Math.Round(track.Width * fraction);
        e.Graphics.FillRoundedRectangle(fill, new Rectangle(track.Left, track.Top, Math.Max(4, centerX - track.Left), track.Height), new Size(4, 4));
        using var knob = new SolidBrush(_palette.Knob);
        using var outline = new Pen(_palette.KnobOutline);
        e.Graphics.FillEllipse(knob, centerX - 8, 8, 16, 16);
        e.Graphics.DrawEllipse(outline, centerX - 8, 8, 16, 16);
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); SetFromX(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (e.Button == MouseButtons.Left) SetFromX(e.X); }

    private void SetFromX(int x)
    {
        var fraction = Math.Clamp((x - 12d) / Math.Max(1, Width - 24d), 0d, 1d);
        var value = Minimum + (int)Math.Round((Maximum - Minimum) * fraction);
        if (value == _value) return;
        _value = value;
        Invalidate();
        ValueChanged?.Invoke(value);
    }
}
