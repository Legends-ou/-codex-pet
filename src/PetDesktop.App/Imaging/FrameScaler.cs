namespace PetDesktop.App.Imaging;

public static class FrameScaler
{
    public static byte[] ScaleBgra(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Frame dimensions must be positive.");
        }

        if (source.Length != checked(sourceWidth * sourceHeight * 4))
        {
            throw new ArgumentException("Source byte length does not match its BGRA dimensions.", nameof(source));
        }

        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            return (byte[])source.Clone();
        }

        var target = new byte[checked(targetWidth * targetHeight * 4)];
        for (var targetY = 0; targetY < targetHeight; targetY++)
        {
            var sourceY = targetY * sourceHeight / targetHeight;
            for (var targetX = 0; targetX < targetWidth; targetX++)
            {
                var sourceX = targetX * sourceWidth / targetWidth;
                var sourceOffset = checked((sourceY * sourceWidth + sourceX) * 4);
                var targetOffset = checked((targetY * targetWidth + targetX) * 4);
                Buffer.BlockCopy(source, sourceOffset, target, targetOffset, 4);
            }
        }

        return target;
    }
}
