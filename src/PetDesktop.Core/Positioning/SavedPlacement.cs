namespace PetDesktop.Core.Positioning;

public sealed record SavedPlacement(string DisplayId, double RelativeX, double RelativeY)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(DisplayId) &&
        double.IsFinite(RelativeX) &&
        double.IsFinite(RelativeY) &&
        RelativeX is >= 0 and <= 1 &&
        RelativeY is >= 0 and <= 1;
}
