namespace PetDesktop.App.Windows;

internal enum GdiSelectionCleanupOrder
{
    BitmapThenDeviceContext,
    DeviceContextThenBitmap,
}

internal static class GdiSelectionCleanupPolicy
{
    internal static GdiSelectionCleanupOrder GetOrderAfterRestore(nint restoreResult)
    {
        return restoreResult == nint.Zero || restoreResult == new nint(-1)
            ? GdiSelectionCleanupOrder.DeviceContextThenBitmap
            : GdiSelectionCleanupOrder.BitmapThenDeviceContext;
    }
}
