using PetDesktop.App.Imaging;

namespace PetDesktop.App.Tests;

public sealed class FrameScalerTests
{
    [Fact]
    public void ScaleBgraUsesNearestPixelsAndPreservesFourChannels()
    {
        var source = new byte[]
        {
            1, 2, 3, 4, 10, 20, 30, 40,
        };

        var scaled = FrameScaler.ScaleBgra(source, 2, 1, 4, 1);

        Assert.Equal(new byte[]
        {
            1, 2, 3, 4, 1, 2, 3, 4, 10, 20, 30, 40, 10, 20, 30, 40,
        }, scaled);
    }
}
