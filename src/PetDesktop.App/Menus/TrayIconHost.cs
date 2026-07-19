using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PetDesktop.Core.Configuration;
using PetDesktop.Core.Pets;

namespace PetDesktop.App.Menus;

public sealed class TrayIconHost : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action<PetCommand> _commandHandler;
    private readonly Action<string> _selectPetHandler;
    private readonly Action<int> _scaleChanged;
    private ContextMenuStrip? _menu;
    private bool _disposed;

    public TrayIconHost(PetCommandModel model, Action<PetCommand> commandHandler, Action<string> selectPetHandler, Action<int> scaleChanged)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(commandHandler);
        ArgumentNullException.ThrowIfNull(selectPetHandler);
        ArgumentNullException.ThrowIfNull(scaleChanged);
        _commandHandler = commandHandler;
        _selectPetHandler = selectPetHandler;
        _scaleChanged = scaleChanged;
        _icon = new NotifyIcon { Icon = PetIcon.Create(), Text = "Pet Desktop", Visible = true };
        Update(model, [], 100, 100, AppTheme.Dark);
    }

    public event Action<bool>? MenuVisibilityChanged;

    public void Update(PetCommandModel model, IReadOnlyList<PetDescriptor> pets, int scalePercent, int maximumScalePercent, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pets);
        var previousMenu = _menu;
        _menu = BuildMenu(model, pets, scalePercent, maximumScalePercent, theme);
        _icon.ContextMenuStrip = _menu;
        previousMenu?.Dispose();
    }

    public void ShowMenu(Point screenPosition) => _menu?.Show(screenPosition);

    public void ShowStatus(string title, string message) => _icon.ShowBalloonTip(5000, title, message, ToolTipIcon.Warning);

    public void Dispose()
    {
        if (_disposed) return;
        _icon.Visible = false;
        _menu?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _disposed = true;
    }

    private ContextMenuStrip BuildMenu(PetCommandModel model, IReadOnlyList<PetDescriptor> pets, int scalePercent, int maximumScalePercent, AppTheme theme)
    {
        var palette = PetMenuPalette.From(theme);
        var menu = new ContextMenuStrip
        {
            Renderer = new PetMenuRenderer(palette),
            Font = new Font("Microsoft YaHei UI", 9F),
            ShowImageMargin = false,
            Padding = new Padding(8),
            BackColor = palette.Surface,
        };
        menu.Opened += (_, _) =>
        {
            ApplyRoundedRegion(menu);
            MenuVisibilityChanged?.Invoke(true);
        };
        menu.SizeChanged += (_, _) => ApplyRoundedRegion(menu);
        menu.Closed += (_, _) => MenuVisibilityChanged?.Invoke(false);
        AddCommand(menu.Items, model, PetCommand.OpenManagementCenter, "打开管理中心", showCheck: false);
        AddCommand(menu.Items, model, PetCommand.NewNote, "新建便签", showCheck: false);
        menu.Items.Add(new ToolStripSeparator());
        var changePet = new ToolStripMenuItem("\u66F4\u6362\u5BA0\u7269") { Enabled = pets.Count > 0, Margin = new Padding(2, 1, 2, 1) };
        foreach (var pet in pets)
        {
            var item = new ToolStripMenuItem(pet.DisplayName) { Margin = new Padding(2, 1, 2, 1) };
            item.Click += (_, _) => _selectPetHandler(pet.Id);
            changePet.DropDownItems.Add(item);
        }

        menu.Items.Add(changePet);
        var size = new ToolStripMenuItem("\u663E\u793A\u5927\u5C0F") { Margin = new Padding(2, 1, 2, 1) };
        var scaleLabel = new ToolStripLabel($"\u5F53\u524D\u5927\u5C0F  {scalePercent}%")
        {
            Padding = new Padding(8, 4, 0, 2),
            ForeColor = palette.SecondaryText,
            BackColor = palette.Surface,
        };
        size.DropDownItems.Add(scaleLabel);
        var slider = new RoundSlider(50, Math.Max(50, maximumScalePercent), scalePercent, palette);
        slider.ValueChanged += value =>
        {
            scaleLabel.Text = $"\u5F53\u524D\u5927\u5C0F  {value}%";
            _scaleChanged(value);
        };
        size.DropDownItems.Add(new ToolStripControlHost(slider)
        {
            BackColor = palette.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        });
        menu.Items.Add(size);
        var isAlwaysOnTop = model.Find(PetCommand.AlwaysOnTop)?.IsChecked == true;
        AddCommand(
            menu.Items,
            model,
            PetCommand.AlwaysOnTop,
            isAlwaysOnTop ? "\u53D6\u6D88\u59CB\u7EC8\u7F6E\u9876" : "\u59CB\u7EC8\u7F6E\u9876",
            showCheck: false);
        var startsWithWindows = model.Find(PetCommand.StartWithWindows)?.IsChecked == true;
        AddCommand(
            menu.Items,
            model,
            PetCommand.StartWithWindows,
            startsWithWindows ? "\u5173\u95ED\u5F00\u673A\u542F\u52A8" : "\u5F00\u542F\u5F00\u673A\u542F\u52A8",
            showCheck: false);
        AddCommand(menu.Items, model, PetCommand.ToggleTheme, theme == AppTheme.Dark ? "\u5207\u6362\u4E3A\u6D45\u8272\u5916\u89C2" : "\u5207\u6362\u4E3A\u6DF1\u8272\u5916\u89C2", showCheck: false);
        menu.Items.Add(new ToolStripSeparator());
        AddCommand(menu.Items, model, PetCommand.Rescan, "\u91CD\u65B0\u626B\u63CF");
        AddCommand(menu.Items, model, PetCommand.OpenPetsFolder, "\u6253\u5F00\u5BA0\u7269\u76EE\u5F55");
        menu.Items.Add(new ToolStripSeparator());
        AddCommand(menu.Items, model, PetCommand.Exit, "\u9000\u51FA");
        return menu;
    }

    private void AddCommand(ToolStripItemCollection target, PetCommandModel model, PetCommand command, string text, bool showCheck = true)
    {
        if (model.Find(command) is not { IsVisible: true } state) return;
        var item = new ToolStripMenuItem(text) { Checked = showCheck && state.IsChecked, Margin = new Padding(2, 1, 2, 1) };
        item.Click += (_, _) => _commandHandler(command);
        target.Add(item);
    }

    private static void ApplyRoundedRegion(ContextMenuStrip menu)
    {
        if (menu.Width < 24 || menu.Height < 24) return;
        using var path = new GraphicsPath();
        const int radius = 12;
        var bounds = new Rectangle(0, 0, menu.Width - 1, menu.Height - 1);
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        menu.Region = new Region(path);
    }
}
