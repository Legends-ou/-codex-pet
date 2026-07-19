namespace PetDesktop.Core.Positioning;

public sealed record DisplayWorkArea(string Id, int X, int Y, int Width, int Height, bool IsPrimary)
{
    public int Right => checked(X + Width);

    public int Bottom => checked(Y + Height);

    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Width > 0 && Height > 0;
}

public sealed record PetPlacement(int X, int Y)
{
    public bool IsFullyInside(DisplayWorkArea area, int width = 96, int height = 104) =>
        X >= area.X && Y >= area.Y && X + width <= area.Right && Y + height <= area.Bottom;
}

public sealed class PlacementResolver
{
    private const int SafeMargin = 24;
    private readonly int _safeMargin;

    public PlacementResolver(int safeMargin = SafeMargin)
    {
        if (safeMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(safeMargin), safeMargin, "Safe margin must be non-negative.");
        }

        _safeMargin = safeMargin;
    }

    public PetPlacement Resolve(
        SavedPlacement? savedPlacement,
        IReadOnlyList<DisplayWorkArea> displays,
        int petWidth,
        int petHeight)
    {
        var primary = GetPrimaryDisplay(displays);
        ValidatePetFits(petWidth, petHeight, primary);

        var display = savedPlacement is { IsValid: true }
            ? displays.FirstOrDefault(candidate => candidate.Id == savedPlacement.DisplayId && candidate.IsValid) ?? primary
            : primary;
        ValidatePetFits(petWidth, petHeight, display);

        if (savedPlacement is not { IsValid: true } || display.Id != savedPlacement.DisplayId)
        {
            return DefaultPlacement(display, petWidth, petHeight);
        }

        var x = display.X + (int)Math.Round((display.Width - petWidth) * savedPlacement.RelativeX, MidpointRounding.AwayFromZero);
        var y = display.Y + (int)Math.Round((display.Height - petHeight) * savedPlacement.RelativeY, MidpointRounding.AwayFromZero);
        return Clamp(new PetPlacement(x, y), display, petWidth, petHeight);
    }

    public static SavedPlacement Capture(DisplayWorkArea display, PetPlacement placement, int petWidth, int petHeight)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(placement);
        if (!display.IsValid)
        {
            throw new ArgumentException("Display work area must have an ID and positive dimensions.", nameof(display));
        }

        ValidatePetFits(petWidth, petHeight, display);
        var clamped = Clamp(placement, display, petWidth, petHeight);
        var horizontalRange = display.Width - petWidth;
        var verticalRange = display.Height - petHeight;
        return new SavedPlacement(
            display.Id,
            horizontalRange == 0 ? 0 : (double)(clamped.X - display.X) / horizontalRange,
            verticalRange == 0 ? 0 : (double)(clamped.Y - display.Y) / verticalRange);
    }

    private static DisplayWorkArea GetPrimaryDisplay(IReadOnlyList<DisplayWorkArea> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var validDisplays = displays.Where(display => display is not null && display.IsValid).ToArray();
        if (validDisplays.Length == 0)
        {
            throw new ArgumentException("At least one valid display work area is required.", nameof(displays));
        }

        return validDisplays.FirstOrDefault(display => display.IsPrimary) ?? validDisplays[0];
    }

    private static void ValidatePetFits(int petWidth, int petHeight, DisplayWorkArea display)
    {
        if (petWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(petWidth), petWidth, "Pet width must be positive.");
        }

        if (petHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(petHeight), petHeight, "Pet height must be positive.");
        }

        if (petWidth > display.Width || petHeight > display.Height)
        {
            throw new ArgumentException("Pet dimensions must fit inside the display work area.");
        }
    }

    private PetPlacement DefaultPlacement(DisplayWorkArea display, int petWidth, int petHeight) =>
        Clamp(new PetPlacement(display.Right - petWidth - _safeMargin, display.Bottom - petHeight - _safeMargin), display, petWidth, petHeight);

    private static PetPlacement Clamp(PetPlacement placement, DisplayWorkArea display, int petWidth, int petHeight) =>
        new(
            Math.Clamp(placement.X, display.X, display.Right - petWidth),
            Math.Clamp(placement.Y, display.Y, display.Bottom - petHeight));
}
