using PetDesktop.App.Windows;

namespace PetDesktop.App.Tests;

public sealed class GdiSelectionCleanupPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RestoreFailureRequiresDeviceContextDeletionBeforeBitmapDeletion(long restoreResult)
    {
        Assert.Equal(
            GdiSelectionCleanupOrder.DeviceContextThenBitmap,
            GdiSelectionCleanupPolicy.GetOrderAfterRestore(new nint(restoreResult)));
    }

    [Fact]
    public void SuccessfulRestoreAllowsBitmapDeletionBeforeDeviceContextDeletion()
    {
        Assert.Equal(
            GdiSelectionCleanupOrder.BitmapThenDeviceContext,
            GdiSelectionCleanupPolicy.GetOrderAfterRestore(new nint(42)));
    }
}
