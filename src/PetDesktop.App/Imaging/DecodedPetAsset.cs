using System.Collections.ObjectModel;
using PetDesktop.Core.Pets;

namespace PetDesktop.App.Imaging;

public sealed class DecodedFrame
{
    private readonly byte[] _pixels;

    private DecodedFrame(
        PetAction? action,
        int? lookSector,
        int frameIndex,
        int? durationMs,
        int width,
        int height,
        byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if ((action is null) == (lookSector is null))
        {
            throw new ArgumentException("A decoded frame must be either a standard action frame or a look frame.");
        }

        if (action is not null && (durationMs is null or <= 0 || frameIndex < 0))
        {
            throw new ArgumentException("A standard action frame requires a non-negative index and positive duration.");
        }

        if (lookSector is not null && (lookSector is < 0 or > 15 || durationMs is not null || frameIndex != 0))
        {
            throw new ArgumentException("A look frame requires sector 0 through 15, no duration, and frame index zero.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Decoded frame width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Decoded frame height must be positive.");
        }

        var pixelCount = checked(width * height);
        var expectedLength = checked(pixelCount * 4);
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException("Pixel data length must equal width multiplied by height multiplied by four.", nameof(pixels));
        }

        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            var offset = checked(pixelIndex * 4);
            var pixelAlpha = pixels[offset + 3];
            if (pixels[offset] > pixelAlpha || pixels[offset + 1] > pixelAlpha || pixels[offset + 2] > pixelAlpha)
            {
                throw new ArgumentException(
                    "Pixel data must contain premultiplied BGRA channels (blue, green, and red must not exceed alpha).",
                    nameof(pixels));
            }
        }

        Action = action;
        LookSector = lookSector;
        FrameIndex = frameIndex;
        DurationMs = durationMs;
        Width = width;
        Height = height;
        _pixels = (byte[])pixels.Clone();

        var alpha = new byte[pixelCount];
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            alpha[pixelIndex] = _pixels[checked((pixelIndex * 4) + 3)];
        }

        HitTestMask = new AlphaHitTestMask(width, height, alpha);
    }

    public PetAction? Action { get; }

    public int? LookSector { get; }

    public int FrameIndex { get; }

    public int? DurationMs { get; }

    public int Width { get; }

    public int Height { get; }

    public int PixelByteLength => _pixels.Length;

    public AlphaHitTestMask HitTestMask { get; }

    public byte[] CopyPixelBytes() => (byte[])_pixels.Clone();

    internal static DecodedFrame Standard(
        PetAction action,
        int frameIndex,
        int durationMs,
        int width,
        int height,
        byte[] pixels) =>
        new(action, null, frameIndex, durationMs, width, height, pixels);

    internal static DecodedFrame Look(int sector, int width, int height, byte[] pixels) =>
        new(null, sector, 0, null, width, height, pixels);
}

public sealed class DecodedPetAsset
{
    internal DecodedPetAsset(
        int version,
        int atlasWidth,
        int atlasHeight,
        IReadOnlyDictionary<PetAction, IReadOnlyList<DecodedFrame>> actionFrames,
        IReadOnlyList<DecodedFrame> lookFrames)
    {
        ArgumentNullException.ThrowIfNull(actionFrames);
        ArgumentNullException.ThrowIfNull(lookFrames);

        var layout = PetFormatValidator.Resolve(version, atlasWidth, atlasHeight);
        if (actionFrames.Count != PetActions.All.Count ||
            !actionFrames.Keys.ToHashSet().SetEquals(PetActions.All))
        {
            throw new ArgumentException(
                "Decoded asset action keys must be exactly the nine standard pet actions.",
                nameof(actionFrames));
        }

        var copiedActions = new Dictionary<PetAction, IReadOnlyList<DecodedFrame>>();
        foreach (var action in PetActions.All)
        {
            if (!actionFrames.TryGetValue(action, out var sourceFrames) || sourceFrames is null)
            {
                throw new ArgumentException($"Decoded asset is missing standard action {action}.", nameof(actionFrames));
            }

            var frames = sourceFrames.ToArray();
            ValidateActionFrames(action, layout.Actions[action], frames);
            copiedActions.Add(action, Array.AsReadOnly(frames));
        }

        var lookFrameArray = lookFrames.ToArray();
        ValidateLookFrames(layout, lookFrameArray);
        var copiedLookFrames = Array.AsReadOnly(lookFrameArray);
        var allFrames = PetActions.All
            .SelectMany(action => copiedActions[action])
            .Concat(copiedLookFrames)
            .ToArray();
        var expectedFrameCount = layout.Actions.Values.Sum(row => row.UsedFrames) +
            (layout.HasLookDirections ? 16 : 0);
        if (allFrames.Length != expectedFrameCount)
        {
            throw new ArgumentException(
                $"Decoded asset must contain exactly {expectedFrameCount} canonical frames.",
                nameof(actionFrames));
        }

        Version = version;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
        ActionFrames = new ReadOnlyDictionary<PetAction, IReadOnlyList<DecodedFrame>>(copiedActions);
        LookFrames = copiedLookFrames;
        Frames = Array.AsReadOnly(allFrames);
    }

    public int Version { get; }

    public int AtlasWidth { get; }

    public int AtlasHeight { get; }

    public IReadOnlyDictionary<PetAction, IReadOnlyList<DecodedFrame>> ActionFrames { get; }

    public IReadOnlyList<DecodedFrame> LookFrames { get; }

    public IReadOnlyList<DecodedFrame> Frames { get; }

    private static void ValidateActionFrames(
        PetAction action,
        AnimationRow animationRow,
        DecodedFrame[] frames)
    {
        if (frames.Length != animationRow.UsedFrames)
        {
            throw new ArgumentException(
                $"Decoded action {action} must contain exactly {animationRow.UsedFrames} frames.",
                nameof(frames));
        }

        for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            var frame = frames[frameIndex];
            if (frame is null ||
                frame.Action != action ||
                frame.LookSector is not null ||
                frame.FrameIndex != frameIndex ||
                frame.DurationMs != animationRow.DurationsMs[frameIndex] ||
                frame.Width != PetLayout.CanonicalCellWidth ||
                frame.Height != PetLayout.CanonicalCellHeight)
            {
                throw new ArgumentException(
                    $"Decoded action {action} frame {frameIndex} does not match the canonical action contract.",
                    nameof(frames));
            }
        }
    }

    private static void ValidateLookFrames(PetLayout layout, DecodedFrame[] frames)
    {
        var expectedCount = layout.HasLookDirections ? 16 : 0;
        if (frames.Length != expectedCount)
        {
            throw new ArgumentException(
                $"Decoded layout version {layout.Version} must contain exactly {expectedCount} look frames.",
                nameof(frames));
        }

        for (var sector = 0; sector < frames.Length; sector++)
        {
            var frame = frames[sector];
            if (frame is null ||
                frame.Action is not null ||
                frame.LookSector != sector ||
                frame.FrameIndex != 0 ||
                frame.DurationMs is not null ||
                frame.Width != PetLayout.CanonicalCellWidth ||
                frame.Height != PetLayout.CanonicalCellHeight)
            {
                throw new ArgumentException(
                    $"Decoded look frame {sector} does not match the canonical look contract.",
                    nameof(frames));
            }
        }
    }
}
