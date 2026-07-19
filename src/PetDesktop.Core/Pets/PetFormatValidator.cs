namespace PetDesktop.Core.Pets;

public static class PetFormatValidator
{
    public static PetLayout Resolve(int? version, int width, int height)
    {
        PetLayout? expected = version switch
        {
            1 => PetLayout.V1,
            2 => PetLayout.V2,
            _ => null,
        };

        if (version is null)
        {
            if (width == PetLayout.V1.AtlasWidth && height == PetLayout.V1.AtlasHeight) return PetLayout.V1;
            if (width == PetLayout.V2.AtlasWidth && height == PetLayout.V2.AtlasHeight) return PetLayout.V2;
        }

        if (expected is not null && width == expected.AtlasWidth && height == expected.AtlasHeight)
        {
            return expected;
        }

        var expectation = expected is null
            ? "a missing spriteVersionNumber can be inferred only for 1536x1872 (v1) or 1536x2288 (v2); declared versions must be 1 or 2"
            : $"version {version} requires {expected.AtlasWidth}x{expected.AtlasHeight}";

        throw new PetFormatException(
            $"Unsupported Codex pet atlas: version {version} reported {width}x{height}; {expectation}.");
    }
}
