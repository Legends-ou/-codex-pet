using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Forms;
using PetDesktop.Core.Animation;
using PetDesktop.Core.Configuration;
using PetDesktop.App.Imaging;
using PetDesktop.App.Input;
using PetDesktop.App.Windows;
using PetDesktop.App.Menus;
using PetDesktop.App.Pets;
using PetDesktop.App.Platform;
using PetDesktop.Core.Pets;
using PetDesktop.Core.Positioning;

namespace PetDesktop.App;

public partial class App : System.Windows.Application, IDisposable
{
    private LayeredPetWindow? _prototypeWindow;
    private DecodedPetAsset? _asset;
    private AnimationStateMachine? _animation;
    private DispatcherTimer? _animationTimer;
    private long _lastAnimationTimestamp;
    private SingleInstanceGuard? _singleInstance;
    private TrayIconHost? _tray;
    private MainWindow? _managementWindow;
    private PetSpeechBubble? _speechBubble;
    private PetDirectoryWatcher? _petWatcher;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly PlacementResolver _placementResolver = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PetDesktop",
        "settings.json");
    private AppSettings _settings = AppSettings.Default;
    private PetPointerPoint? _lastKnownPosition;
    private int _displayWidth;
    private int _displayHeight;
    private double _currentScalePercent = 100;
    private double _targetScalePercent = 100;
    private IReadOnlyList<PetDescriptor> _pets = [];
    private string? _resourceRoot;
    private readonly DistributionKind _distributionKind = File.Exists(Path.Combine(AppContext.BaseDirectory, ".portable"))
        ? DistributionKind.Portable
        : DistributionKind.Installed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = SingleInstanceGuard.Acquire();
        if (!_singleInstance.IsPrimaryInstance)
        {
            System.Windows.MessageBox.Show(
                "Pet Desktop is already running. Find it in the system tray, or exit it before starting another copy.",
                "Pet Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _tray = new TrayIconHost(
            PetCommandModel.Build(_distributionKind, PetCommandState.Default),
            HandleCommand,
            SelectPet,
            SetScalePercent);
        _tray.MenuVisibilityChanged += OnMenuVisibilityChanged;
        _speechBubble = new PetSpeechBubble();
        _managementWindow = new MainWindow(_settings.Theme, (title, message) => _tray?.ShowStatus(title, message), ShowPetMessage, SetTheme, NativeMethods.GetInputIdleTime);
        _ = InitializeApplicationAsync();
    }

    private async Task InitializeApplicationAsync()
    {
        _settings = await _settingsStore.LoadAsync(_settingsPath, CancellationToken.None);
        try
        {
            // The registry is authoritative: either delivery form may have been
            // registered or removed outside the app since the last launch.
            _settings = _settings with { StartWithWindows = StartupRegistration.IsEnabled() };
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LocalDiagnostics.Write(exception);
        }
        _currentScalePercent = _settings.ScalePercent;
        _targetScalePercent = _settings.ScalePercent;
        _managementWindow?.ApplyTheme(_settings.Theme);
        _speechBubble?.ApplyTheme(_settings.Theme);
        _resourceRoot = PetResourcePaths.GetInstalledPetsRoot(AppContext.BaseDirectory);
        Directory.CreateDirectory(_resourceRoot);
        _petWatcher = new PetDirectoryWatcher(_resourceRoot, RequestReloadFromFileSystem);
        await ReloadPetAsync();
    }

    private async Task ReloadPetAsync(string? requestedPetId = null)
    {
        await _reloadGate.WaitAsync();
        try
        {
            if (_resourceRoot is null)
            {
                return;
            }

            _animation?.SetResourcePhase(ResourceAnimationPhase.Loading);
            var pets = await new PetCatalog(diagnostic =>
                LocalDiagnostics.WritePetPackageIssue(diagnostic.DirectoryPath, diagnostic.Reason))
                .ScanAsync(_resourceRoot, CancellationToken.None);
            UpdateTray(pets);
            if (pets.Count == 0)
            {
                _animation?.SetResourcePhase(ResourceAnimationPhase.None);
                _tray?.ShowStatus("Pet Desktop", "No valid pet package was found in the pets folder.");
                return;
            }

            var selectedPetId = requestedPetId ?? _settings.SelectedPetId;
            var pet = pets.FirstOrDefault(candidate => candidate.Id == selectedPetId) ?? pets[0];

            var candidate = await SkiaSpriteDecoder.DecodeAsync(
                pet.SpritesheetPath,
                pet.SpriteVersionNumber,
                CancellationToken.None);
            var layout = candidate.Version switch
            {
                1 => PetLayout.V1,
                2 => PetLayout.V2,
                _ => throw new PetFormatException("Decoded pet uses an unsupported layout version."),
            };
            _animation?.SetResourcePhase(ResourceAnimationPhase.Validating);

            if (_prototypeWindow is null)
            {
                var initialFrame = candidate.ActionFrames[PetAction.Idle][0];
                GetDisplaySize(initialFrame.Width, initialFrame.Height, _currentScalePercent, out _displayWidth, out _displayHeight);
                _prototypeWindow = new LayeredPetWindow(_displayWidth, _displayHeight);
                _prototypeWindow.Input += OnWindowInput;
                _prototypeWindow.PositionChanged += OnPetPositionChanged;
                _prototypeWindow.SetAlwaysOnTop(_settings.AlwaysOnTop);
            }

            _asset = candidate;
            _animation = new AnimationStateMachine(layout);
            var frame = candidate.ActionFrames[PetAction.Idle][0];
            GetDisplaySize(frame.Width, frame.Height, _currentScalePercent, out _displayWidth, out _displayHeight);
            var placement = _lastKnownPosition is { } knownPosition
                ? new PetPlacement(knownPosition.X, knownPosition.Y)
                : _placementResolver.Resolve(_settings.Placement, GetDisplays(), _displayWidth, _displayHeight);
            _lastKnownPosition = new PetPointerPoint(placement.X, placement.Y);
            _prototypeWindow.UpdateFrame(
                placement.X,
                placement.Y,
                _displayWidth,
                _displayHeight,
                FrameScaler.ScaleBgra(frame.CopyPixelBytes(), frame.Width, frame.Height, _displayWidth, _displayHeight));
            // A selection becomes persistent only after its atlas has decoded and displayed successfully.
            // This preserves the currently working pet if an external package is incomplete or corrupt.
            if (!string.Equals(_settings.SelectedPetId, pet.Id, StringComparison.Ordinal))
            {
                _settings = _settings with { SelectedPetId = pet.Id };
                _ = SaveSettingsAsync();
            }
            _lastAnimationTimestamp = Stopwatch.GetTimestamp();
            _animationTimer ??= new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }
        catch (Exception exception)
        {
            // Keep the currently displayed, fully decoded asset during an incomplete or corrupt update.
            LocalDiagnostics.Write(exception);
            _tray?.ShowStatus("Pet Desktop", "The pet could not be displayed. Open the local diagnostic log for details.");
            _animation?.SetResourcePhase(ResourceAnimationPhase.Failed);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_animationTimer is not null)
        {
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }

        _animation = null;
        _asset = null;
        _petWatcher?.Dispose();
        _petWatcher = null;
        if (_prototypeWindow is not null)
        {
            _prototypeWindow.Input -= OnWindowInput;
        }
        _prototypeWindow?.Dispose();
        _prototypeWindow = null;
        _speechBubble?.Dispose();
        _speechBubble = null;
        _tray?.Dispose();
        _tray = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
        _reloadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_prototypeWindow is null || _asset is null || _animation is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var currentPosition = _prototypeWindow.Position;
        var cursor = System.Windows.Forms.Cursor.Position;
        var petScreen = Screen.FromPoint(new System.Drawing.Point(
            currentPosition.X + (_displayWidth / 2),
            currentPosition.Y + (_displayHeight / 2)));
        var cursorScreen = Screen.FromPoint(cursor);
        var lookSector = string.Equals(petScreen.DeviceName, cursorScreen.DeviceName, StringComparison.OrdinalIgnoreCase)
            ? LookDirectionQuantizer.Quantize(
                cursor.X - (currentPosition.X + (_displayWidth / 2d)),
                cursor.Y - (currentPosition.Y + (_displayHeight / 2d)),
                deadZone: 24,
                maxRadius: 480)
            : null;
        _animation.SetLookSector(lookSector);
        var elapsed = Stopwatch.GetElapsedTime(_lastAnimationTimestamp, now);
        _animation.Tick(elapsed);
        _lastAnimationTimestamp = now;
        _currentScalePercent += (_targetScalePercent - _currentScalePercent) * Math.Min(1d, elapsed.TotalSeconds * 12d);

        var frame = _animation.CurrentAction is { } action
            ? _asset.ActionFrames[action][_animation.CurrentFrameIndex]
            : _asset.LookFrames[_animation.CurrentLookSector ?? 0];
        if (frame is null)
        {
            return;
        }

        GetDisplaySize(frame.Width, frame.Height, _currentScalePercent, out _displayWidth, out _displayHeight);

        _lastKnownPosition = currentPosition;
        var position = _lastKnownPosition.Value;
        _prototypeWindow.UpdateFrame(
            position.X,
            position.Y,
            _displayWidth,
            _displayHeight,
            FrameScaler.ScaleBgra(frame.CopyPixelBytes(), frame.Width, frame.Height, _displayWidth, _displayHeight));
    }

    private void OnWindowInput(PetInputResult result)
    {
        if (_animation is null)
        {
            return;
        }

        if (result.RequestWave)
        {
            _managementWindow?.ShowRandomQuote();
        }

        if (result.OpenMenu)
        {
            _animation.SetMenuOpen(true);
            if (System.Windows.Forms.Cursor.Position is { } cursor)
            {
                _tray?.ShowMenu(cursor);
            }
        }

        if ((result.StartDrag || result.MoveDrag) && result.HorizontalDelta != 0)
        {
            _animation.SetDragging(result.HorizontalDelta);
        }

        if (result.CompletedDrag)
        {
            _animation.SetDragging(0);
            SaveCurrentPlacement();
        }
    }

    private void HandleCommand(PetCommand command)
    {
        switch (command)
        {
            case PetCommand.OpenManagementCenter:
                ShowManagementCenter();
                break;
            case PetCommand.NewNote:
                _managementWindow?.CreateNote();
                break;
            case PetCommand.SizeSmall:
                SetSize(PetSize.Small);
                break;
            case PetCommand.SizeStandard:
                SetSize(PetSize.Standard);
                break;
            case PetCommand.SizeLarge:
                SetSize(PetSize.Large);
                break;
            case PetCommand.Rescan:
                _ = ReloadPetAsync();
                break;
            case PetCommand.AlwaysOnTop:
                SetAlwaysOnTop(!_settings.AlwaysOnTop);
                break;
            case PetCommand.StartWithWindows:
                SetStartWithWindows(!_settings.StartWithWindows);
                break;
            case PetCommand.ToggleTheme:
                SetTheme(_settings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
                break;
            case PetCommand.OpenPetsFolder:
                if (_resourceRoot is not null)
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", _resourceRoot) { UseShellExecute = true });
                }

                break;
            case PetCommand.Exit:
                Shutdown();
                break;
        }
    }

    private void RequestReloadFromFileSystem()
    {
        _ = Dispatcher.InvokeAsync(() => _ = ReloadPetAsync());
    }

    private void SelectPet(string petId)
    {
        _ = ReloadPetAsync(petId);
    }

    private void OnMenuVisibilityChanged(bool visible)
    {
        _animation?.SetMenuOpen(visible);
    }

    private void UpdateTray(IReadOnlyList<PetDescriptor> pets)
    {
        _pets = pets;
        _tray?.Update(
            PetCommandModel.Build(
                _distributionKind,
                new PetCommandState(pets.Count > 0, _settings.Size, _settings.AlwaysOnTop, _settings.StartWithWindows)),
            _pets,
            _settings.ScalePercent,
            GetMaximumScalePercent(),
            _settings.Theme);
    }

    private static DisplayWorkArea[] GetDisplays() => Screen.AllScreens
        .Select(screen => new DisplayWorkArea(
            screen.DeviceName,
            screen.WorkingArea.X,
            screen.WorkingArea.Y,
            screen.WorkingArea.Width,
            screen.WorkingArea.Height,
            screen.Primary))
        .ToArray();

    private static void GetDisplaySize(int sourceWidth, int sourceHeight, double scalePercent, out int width, out int height)
    {
        var scale = Math.Min(scalePercent, GetMaximumScalePercent()) / 100d;
        width = (int)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero);
        height = (int)Math.Round(sourceHeight * scale, MidpointRounding.AwayFromZero);
    }

    private void SetSize(PetSize size)
    {
        SetScalePercent(size switch
        {
            PetSize.Small => 75,
            PetSize.Standard => 100,
            PetSize.Large => 125,
            _ => 100,
        });
    }

    private void SetScalePercent(int scalePercent)
    {
        var boundedPercent = Math.Clamp(scalePercent, 50, GetMaximumScalePercent());
        if (_settings.ScalePercent == boundedPercent)
        {
            return;
        }

        _settings = _settings with { ScalePercent = boundedPercent };
        _targetScalePercent = boundedPercent;
        _ = SaveSettingsAsync();
    }

    private static int GetMaximumScalePercent()
    {
        var maximumHeight = Screen.PrimaryScreen?.WorkingArea.Height / 2 ?? 520;
        return Math.Max(50, (int)Math.Floor(maximumHeight * 100d / PetLayout.CanonicalCellHeight));
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        try
        {
            _prototypeWindow?.SetAlwaysOnTop(enabled);
            _settings = _settings with { AlwaysOnTop = enabled };
            _ = SaveSettingsAsync();
            UpdateTray(_pets);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // A failed z-order change leaves the last known valid setting intact.
        }
    }

    private void SetTheme(AppTheme theme)
    {
        _settings = _settings with { Theme = theme };
        _ = SaveSettingsAsync();
        UpdateTray(_pets);
        _managementWindow?.ApplyTheme(theme);
        _speechBubble?.ApplyTheme(theme);
    }

    private void ShowManagementCenter()
    {
        _managementWindow ??= new MainWindow(_settings.Theme, (title, message) => _tray?.ShowStatus(title, message), ShowPetMessage, SetTheme, NativeMethods.GetInputIdleTime);
        _managementWindow.Show();
        _managementWindow.Activate();
    }

    private void ShowPetMessage(string message, PetAction action)
    {
        _animation?.RequestOneShot(action);
        if (_prototypeWindow is not { } petWindow || _speechBubble is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var position = petWindow.Position;
        _speechBubble.Show(message, position.X, position.Y, _displayWidth, _displayHeight);
    }

    private void OnPetPositionChanged(PetPointerPoint position) =>
        _speechBubble?.Follow(position.X, position.Y, _displayWidth, _displayHeight);

    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            StartupRegistration.SetEnabled(enabled);
            _settings = _settings with { StartWithWindows = enabled };
            _ = SaveSettingsAsync();
            UpdateTray(_pets);
        }
        catch (System.Security.SecurityException)
        {
            // The user can continue using the app without startup registration.
        }
        catch (UnauthorizedAccessException)
        {
            // The user can continue using the app without startup registration.
        }
        catch (IOException)
        {
            // The user can continue using the app without startup registration.
        }
        catch (InvalidOperationException)
        {
            // The process executable may be unavailable in unusual host environments.
        }
    }

    private void SaveCurrentPlacement()
    {
        if (_prototypeWindow is null || _lastKnownPosition is null || _displayWidth <= 0 || _displayHeight <= 0)
        {
            return;
        }

        var displays = GetDisplays();
        var position = _lastKnownPosition.Value;
        var display = displays.FirstOrDefault(candidate =>
            position.X >= candidate.X && position.X < candidate.Right &&
            position.Y >= candidate.Y && position.Y < candidate.Bottom)
            ?? displays.First(display => display.IsPrimary);
        _settings = _settings with
        {
            Placement = PlacementResolver.Capture(
                display,
                new PetPlacement(position.X, position.Y),
                _displayWidth,
                _displayHeight),
        };
        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settingsPath, _settings, CancellationToken.None);
        }
        catch (IOException)
        {
            // Settings persistence must not interrupt the desktop pet.
        }
        catch (UnauthorizedAccessException)
        {
            // Settings persistence must not interrupt the desktop pet.
        }
    }
}
