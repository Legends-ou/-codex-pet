namespace PetDesktop.App.Imaging;

public sealed class AlphaHitTestMask
{
    public const byte DefaultThreshold = 16;

    private readonly byte[] _alpha;

    public AlphaHitTestMask(
        int width,
        int height,
        byte[] alpha,
        byte threshold = DefaultThreshold)
    {
        ArgumentNullException.ThrowIfNull(alpha);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        if (threshold == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "The hit-test threshold must keep fully transparent pixels outside the hit region.");
        }

        var area = (long)width * height;
        if (area > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "The mask dimensions exceed the maximum supported array length.");
        }

        if (alpha.LongLength != area)
        {
            throw new ArgumentException(
                "Alpha data length must equal width multiplied by height.",
                nameof(alpha));
        }

        Width = width;
        Height = height;
        Threshold = threshold;
        _alpha = (byte[])alpha.Clone();
    }

    public int Width { get; }

    public int Height { get; }

    public byte Threshold { get; }

    public bool IsHit(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return false;
        }

        return _alpha[(y * Width) + x] >= Threshold;
    }
}
