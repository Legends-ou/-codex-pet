using System.Globalization;
using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Tests;

public sealed class PetFormatValidatorTests
{
    [Theory]
    [InlineData(1, 1536, 1872)]
    [InlineData(2, 1536, 2288)]
    public void ResolveAcceptsCanonicalAtlasSize(int version, int width, int height)
    {
        var layout = PetFormatValidator.Resolve(version, width, height);

        Assert.Equal(version, layout.Version);
        Assert.Equal(width, layout.AtlasWidth);
        Assert.Equal(height, layout.AtlasHeight);
    }

    [Theory]
    [InlineData(1, 1536, 2288)]
    [InlineData(2, 1536, 1872)]
    [InlineData(3, 1536, 1872)]
    [InlineData(1, 1535, 1872)]
    [InlineData(1, 1537, 1872)]
    [InlineData(1, 1536, 1871)]
    [InlineData(1, 1536, 1873)]
    [InlineData(2, 1535, 2288)]
    [InlineData(2, 1536, 2289)]
    public void ResolveRejectsUnsupportedOrNonCanonicalAtlas(int version, int width, int height)
    {
        var error = Assert.Throws<PetFormatException>(
            () => PetFormatValidator.Resolve(version, width, height));

        Assert.Contains(version.ToString(CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Contains($"{width}x{height}", error.Message, StringComparison.Ordinal);
    }
}
