using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Tests;

public sealed class PetResourcePathsTests
{
    [Fact]
    public void GetInstalledPetsRootUsesApplicationBaseDirectory()
    {
        var root = PetResourcePaths.GetInstalledPetsRoot(Path.Combine(Path.GetTempPath(), "PetDesktop", "app"));

        Assert.Equal(Path.Combine(Path.GetTempPath(), "PetDesktop", "app", "pets"), root);
    }
}
