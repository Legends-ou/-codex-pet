using PetDesktop.App.Windows;

namespace PetDesktop.App.Tests;

public sealed class LayeredFrameCommitCoordinatorTests
{
    [Fact]
    public void UploadCleanupFailureAppliesMatchingRegionBeforeHidingAndReporting()
    {
        var events = new List<string>();
        var uploadResult = LayeredBitmapUploadResult.WithCleanupFailure("DeleteDC returned 0.");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LayeredFrameCommitCoordinator.CompleteAfterSuccessfulUpload(
                uploadResult,
                () => events.Add("region"),
                () => events.Add("hide")));

        Assert.Equal(["region", "hide"], events);
        Assert.Contains("DeleteDC returned 0.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegionFailureHidesAndPreservesRegionExceptionWithUploadCleanupDetails()
    {
        var events = new List<string>();
        var uploadResult = LayeredBitmapUploadResult.WithCleanupFailure("DeleteObject returned 0.");
        var regionException = new InvalidOperationException("SetWindowRgn returned 0.");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            LayeredFrameCommitCoordinator.CompleteAfterSuccessfulUpload(
                uploadResult,
                () =>
                {
                    events.Add("region");
                    throw regionException;
                },
                () => events.Add("hide")));

        Assert.Same(regionException, actual);
        Assert.Equal(["region", "hide"], events);
        Assert.Contains(
            "DeleteObject returned 0.",
            Assert.IsType<string>(actual.Data[LayeredFrameCommitCoordinator.CleanupFailureDataKey]),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CleanUploadAndRegionCommitDoesNotHide()
    {
        var events = new List<string>();

        LayeredFrameCommitCoordinator.CompleteAfterSuccessfulUpload(
            LayeredBitmapUploadResult.Success,
            () => events.Add("region"),
            () => events.Add("hide"));

        Assert.Equal(["region"], events);
    }
}
