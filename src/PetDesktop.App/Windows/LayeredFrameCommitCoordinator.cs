namespace PetDesktop.App.Windows;

internal readonly record struct LayeredBitmapUploadResult
{
    private LayeredBitmapUploadResult(string cleanupFailure)
    {
        CleanupFailure = cleanupFailure;
    }

    internal static LayeredBitmapUploadResult Success => default;

    internal string? CleanupFailure { get; }

    internal bool HasCleanupFailure => CleanupFailure is not null;

    internal static LayeredBitmapUploadResult WithCleanupFailure(string cleanupFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cleanupFailure);
        return new LayeredBitmapUploadResult(cleanupFailure);
    }
}

internal static class LayeredFrameCommitCoordinator
{
    internal const string CleanupFailureDataKey = "PetDesktop.GdiCleanupFailures";

    internal static void CompleteAfterSuccessfulUpload(
        LayeredBitmapUploadResult uploadResult,
        Action applyRegion,
        Action hideWindow)
    {
        ArgumentNullException.ThrowIfNull(applyRegion);
        ArgumentNullException.ThrowIfNull(hideWindow);

        try
        {
            applyRegion();
        }
        catch (Exception regionException)
        {
            hideWindow();

            if (uploadResult.HasCleanupFailure)
            {
                AppendCleanupFailure(regionException, uploadResult.CleanupFailure!);
            }

            throw;
        }

        if (!uploadResult.HasCleanupFailure)
        {
            return;
        }

        hideWindow();
        throw new InvalidOperationException(
            $"The layered bitmap was uploaded and its matching region was applied, "
            + $"but GDI cleanup failed: {uploadResult.CleanupFailure}");
    }

    internal static void AppendCleanupFailure(Exception exception, string cleanupFailure)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(cleanupFailure);

        var existing = exception.Data[CleanupFailureDataKey] as string;
        exception.Data[CleanupFailureDataKey] = string.IsNullOrWhiteSpace(existing)
            ? cleanupFailure
            : $"{existing} {cleanupFailure}";
    }
}
