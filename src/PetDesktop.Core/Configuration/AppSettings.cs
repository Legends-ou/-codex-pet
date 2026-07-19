using PetDesktop.Core.Positioning;

namespace PetDesktop.Core.Configuration;

public enum PetSize
{
    Small,
    Standard,
    Large,
}

public enum AppTheme
{
    Dark,
    Light,
}

public sealed record AppSettings(
    int SchemaVersion,
    string? SelectedPetId,
    PetSize Size,
    bool AlwaysOnTop,
    bool StartWithWindows,
    SavedPlacement? Placement,
    int ScalePercent = 100,
    AppTheme Theme = AppTheme.Dark)
{
    public const int CurrentSchemaVersion = 3;

    public static AppSettings Default { get; } = new(CurrentSchemaVersion, null, PetSize.Standard, true, false, null, 100, AppTheme.Dark);

    public AppSettings Normalize()
    {
        var selectedPetId = string.IsNullOrWhiteSpace(SelectedPetId) ? null : SelectedPetId.Trim();
        var size = Enum.IsDefined(Size) ? Size : PetSize.Standard;
        var placement = Placement is { IsValid: true } ? Placement : null;
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            SelectedPetId = selectedPetId,
            Size = size,
            Placement = placement,
            ScalePercent = Math.Clamp(ScalePercent, 50, 300),
            Theme = Enum.IsDefined(Theme) ? Theme : AppTheme.Dark,
        };
    }
}
