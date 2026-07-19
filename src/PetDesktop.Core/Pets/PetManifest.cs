using System.Text.Json.Serialization;

namespace PetDesktop.Core.Pets;

public sealed record PetManifest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("spriteVersionNumber")] int? SpriteVersionNumber,
    [property: JsonPropertyName("spritesheetPath")] string SpritesheetPath);
