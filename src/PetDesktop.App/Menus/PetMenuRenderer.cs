using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PetDesktop.App.Menus;

internal sealed class PetMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly PetMenuPalette _palette;

    public PetMenuRenderer(PetMenuPalette palette) => _palette = palette;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(_palette.Surface);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.Item.Selected ? _palette.Hover : _palette.Surface);
        if (!e.Item.Selected)
        {
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            return;
        }

        var bounds = Rectangle.Inflate(e.Item.ContentRectangle, 2, 0);
        using var path = RoundedPath(bounds, 8);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, _palette.Text, e.TextFormat);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(_palette.Border);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_palette.Border);
        var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var path = RoundedPath(bounds, 12);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.Item.Selected ? _palette.Hover : _palette.Surface);
        if (!e.Item.Selected)
        {
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            return;
        }

        var bounds = Rectangle.Inflate(e.Item.ContentRectangle, 2, 0);
        using var path = RoundedPath(bounds, 8);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_palette.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
