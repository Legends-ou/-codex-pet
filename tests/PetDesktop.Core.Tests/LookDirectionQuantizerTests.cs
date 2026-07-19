using PetDesktop.Core.Animation;

namespace PetDesktop.Core.Tests;

public sealed class LookDirectionQuantizerTests
{
    [Theory]
    [InlineData(0, -100, 0)]
    [InlineData(100, 0, 4)]
    [InlineData(0, 100, 8)]
    [InlineData(-100, 0, 12)]
    [InlineData(100, -100, 2)]
    [InlineData(100, 100, 6)]
    public void QuantizeUsesClockwiseSectorsFromNorth(double x, double y, int expected) =>
        Assert.Equal(expected, LookDirectionQuantizer.Quantize(x, y, deadZone: 24, maxRadius: 480));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(24, 0)]
    [InlineData(481, 0)]
    public void QuantizeReturnsNullInsideDeadZoneOrOutsideRadius(double x, double y) =>
        Assert.Null(LookDirectionQuantizer.Quantize(x, y, deadZone: 24, maxRadius: 480));
}
