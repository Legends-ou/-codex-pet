using PetDesktop.App.Imaging;

namespace PetDesktop.App.Tests;

public sealed class AlphaHitTestMaskTests
{
    [Fact]
    public void IsHitUsesInclusiveDefaultThresholdAndRejectsNegativeCoordinates()
    {
        var mask = new AlphaHitTestMask(2, 1, [15, 16]);

        Assert.False(mask.IsHit(0, 0));
        Assert.True(mask.IsHit(1, 0));
        Assert.False(mask.IsHit(-1, 0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void ConstructorRejectsNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AlphaHitTestMask(width, height, []));
    }

    [Fact]
    public void ConstructorRejectsMismatchedAlphaLength()
    {
        Assert.Throws<ArgumentException>(() =>
            new AlphaHitTestMask(2, 2, [16, 16, 16]));
    }

    [Fact]
    public void ConstructorRejectsDimensionsWhoseAreaExceedsArrayCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AlphaHitTestMask(int.MaxValue, 2, []));
    }

    [Fact]
    public void IsHitRejectsCoordinatesOnOrBeyondEveryUpperBoundary()
    {
        var mask = new AlphaHitTestMask(2, 2, [16, 16, 16, 16]);

        Assert.False(mask.IsHit(2, 0));
        Assert.False(mask.IsHit(0, 2));
        Assert.False(mask.IsHit(int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void IsHitUsesConfiguredThreshold()
    {
        var mask = new AlphaHitTestMask(2, 1, [31, 32], threshold: 32);

        Assert.False(mask.IsHit(0, 0));
        Assert.True(mask.IsHit(1, 0));
    }

    [Fact]
    public void ConstructorRejectsZeroThresholdToKeepFullyTransparentPixelsOutsideHitRegion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AlphaHitTestMask(1, 1, [0], threshold: 0));
    }
}
