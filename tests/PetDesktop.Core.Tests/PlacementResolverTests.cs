using PetDesktop.Core.Positioning;

namespace PetDesktop.Core.Tests;

public sealed class PlacementResolverTests
{
    private static readonly DisplayWorkArea Primary = new("primary", 0, 0, 1920, 1040, IsPrimary: true);
    private static readonly DisplayWorkArea Secondary = new("secondary", 1920, 0, 2560, 1440, IsPrimary: false);

    [Fact]
    public void ResolveMissingDisplayFallsBackToPrimaryWorkArea()
    {
        var placement = new PlacementResolver().Resolve(new("missing", 0.9, 0.8), [Primary], 96, 104);

        Assert.True(placement.IsFullyInside(Primary));
        Assert.Equal(1800, placement.X);
        Assert.Equal(912, placement.Y);
    }

    [Fact]
    public void ResolveRestoresRelativePlacementOnMatchingDisplay()
    {
        var placement = new PlacementResolver().Resolve(new("secondary", 0.5, 0.25), [Primary, Secondary], 96, 104);

        Assert.Equal(1920 + ((2560 - 96) / 2), placement.X);
        Assert.Equal((1440 - 104) / 4, placement.Y);
        Assert.True(placement.IsFullyInside(Secondary));
    }

    [Fact]
    public void CaptureAndResolveRoundTripRelativeLocation()
    {
        var resolver = new PlacementResolver();
        var saved = PlacementResolver.Capture(Secondary, new PetPlacement(3200, 1000), 96, 104);

        var restored = resolver.Resolve(saved, [Primary, Secondary], 96, 104);

        Assert.Equal(3200, restored.X);
        Assert.Equal(1000, restored.Y);
    }

    [Fact]
    public void ResolveRejectsNoDisplaysAndOversizedPet()
    {
        var resolver = new PlacementResolver();

        Assert.Throws<ArgumentException>(() => resolver.Resolve(null, [], 96, 104));
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.Resolve(null, [Primary], 0, 104));
        Assert.Throws<ArgumentException>(() => resolver.Resolve(null, [Primary], 2000, 104));
    }
}
