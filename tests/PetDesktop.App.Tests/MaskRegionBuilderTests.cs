using System.Windows;
using PetDesktop.App.Imaging;

namespace PetDesktop.App.Tests;

public sealed class MaskRegionBuilderTests
{
    [Fact]
    public void BuildRunsReturnsNoRunsForTransparentMask()
    {
        var mask = new AlphaHitTestMask(3, 2, new byte[6]);

        Assert.Empty(MaskRegionBuilder.BuildRuns(mask));
    }

    [Fact]
    public void BuildRunsMergesOnlyConsecutivePixelsOnOneScanline()
    {
        var mask = new AlphaHitTestMask(5, 1, [16, 16, 0, 16, 0]);

        Assert.Equal(
            [new Int32Rect(0, 0, 2, 1), new Int32Rect(3, 0, 1, 1)],
            MaskRegionBuilder.BuildRuns(mask));
    }

    [Fact]
    public void BuildRunsDoesNotMergeIdenticalRunsAcrossRows()
    {
        var mask = new AlphaHitTestMask(3, 3,
        [
            0, 16, 16,
            0, 16, 16,
            0, 0, 0,
        ]);

        Assert.Equal(
            [new Int32Rect(1, 0, 2, 1), new Int32Rect(1, 1, 2, 1)],
            MaskRegionBuilder.BuildRuns(mask));
    }
}
