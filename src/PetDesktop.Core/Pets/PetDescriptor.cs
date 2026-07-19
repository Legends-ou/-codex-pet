namespace PetDesktop.Core.Pets;

public sealed record PetDescriptor(
    string Id,
    string DisplayName,
    string? Description,
    int? SpriteVersionNumber,
    string PetDirectoryPath,
    string ManifestPath,
    string SpritesheetPath);
