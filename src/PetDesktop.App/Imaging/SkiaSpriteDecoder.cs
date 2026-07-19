using System.IO;
using System.Runtime.InteropServices;
using PetDesktop.Core.Pets;
using SkiaSharp;

namespace PetDesktop.App.Imaging;

public static class SkiaSpriteDecoder
{
    public static Task<DecodedPetAsset> DecodeAsync(
        string path,
        PetLayout layout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(layout);
        return DecodeAsync(path, layout.Version, cancellationToken);
    }

    public static Task<DecodedPetAsset> DecodeAsync(
        string path,
        int? declaredSpriteVersionNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Decode(path, declaredSpriteVersionNumber, cancellationToken), cancellationToken);
    }

    private static DecodedPetAsset Decode(
        string path,
        int? declaredSpriteVersionNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The Codex pet spritesheet was not found.", path);
        }

        using var data = SKData.Create(path)
            ?? throw new InvalidDataException($"Could not read Codex pet spritesheet '{path}'.");
        cancellationToken.ThrowIfCancellationRequested();

        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"SkiaSharp could not decode Codex pet spritesheet '{path}'.");

        if (codec.EncodedFormat is not (SKEncodedImageFormat.Webp or SKEncodedImageFormat.Png))
        {
            throw new InvalidDataException(
                $"Codex pet spritesheet '{path}' must be encoded as static PNG or WebP, but SkiaSharp detected {codec.EncodedFormat}.");
        }

        // Skia reports zero for some static lossless WebP files and one for others.
        // Any value above one is unambiguously an animated atlas and is rejected.
        if (codec.FrameCount > 1)
        {
            throw new InvalidDataException(
                $"Codex pet spritesheet '{path}' must be static, but the codec reported {codec.FrameCount} frames.");
        }

        var sourceInfo = codec.Info;
        var layout = PetFormatValidator.Resolve(declaredSpriteVersionNumber, sourceInfo.Width, sourceInfo.Height);

        var targetInfo = new SKImageInfo(
            sourceInfo.Width,
            sourceInfo.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = new SKBitmap(targetInfo);
        var pixelsAddress = bitmap.GetPixels();
        if (pixelsAddress == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"SkiaSharp could not allocate {targetInfo.Width}x{targetInfo.Height} pixels for '{path}'.");
        }

        var result = codec.GetPixels(
            targetInfo,
            pixelsAddress,
            bitmap.RowBytes,
            SKCodecOptions.Default);
        if (result != SKCodecResult.Success)
        {
            throw new InvalidDataException(
                $"SkiaSharp could not completely decode Codex pet spritesheet '{path}': {result}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var actions = new Dictionary<PetAction, IReadOnlyList<DecodedFrame>>();
        foreach (var action in PetActions.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var animationRow = layout.Actions[action];
            var frames = new DecodedFrame[animationRow.UsedFrames];
            for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pixels = CopyCell(
                    pixelsAddress,
                    bitmap.RowBytes,
                    animationRow.Row,
                    frameIndex,
                    layout.CellWidth,
                    layout.CellHeight);
                frames[frameIndex] = DecodedFrame.Standard(
                    action,
                    frameIndex,
                    animationRow.DurationsMs[frameIndex],
                    layout.CellWidth,
                    layout.CellHeight,
                    pixels);
            }

            actions.Add(action, frames);
        }

        var lookFrames = layout.HasLookDirections
            ? ExtractLookFrames(pixelsAddress, bitmap.RowBytes, layout, cancellationToken)
            : [];

        cancellationToken.ThrowIfCancellationRequested();
        return new DecodedPetAsset(
            layout.Version,
            sourceInfo.Width,
            sourceInfo.Height,
            actions,
            lookFrames);
    }

    private static DecodedFrame[] ExtractLookFrames(
        IntPtr pixelsAddress,
        int sourceRowBytes,
        PetLayout layout,
        CancellationToken cancellationToken)
    {
        var frames = new DecodedFrame[16];
        for (var sector = 0; sector < frames.Length; sector++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = 9 + (sector / layout.Columns);
            var column = sector % layout.Columns;
            var pixels = CopyCell(
                pixelsAddress,
                sourceRowBytes,
                row,
                column,
                layout.CellWidth,
                layout.CellHeight);
            frames[sector] = DecodedFrame.Look(sector, layout.CellWidth, layout.CellHeight, pixels);
        }

        return frames;
    }

    private static byte[] CopyCell(
        IntPtr sourcePixels,
        int sourceRowBytes,
        int cellRow,
        int cellColumn,
        int cellWidth,
        int cellHeight)
    {
        var destinationRowBytes = checked(cellWidth * 4);
        var destination = new byte[checked(destinationRowBytes * cellHeight)];
        var cellTop = checked(cellRow * cellHeight);
        var cellLeftBytes = checked(cellColumn * destinationRowBytes);

        for (var row = 0; row < cellHeight; row++)
        {
            var sourceOffset = checked((checked(cellTop + row) * sourceRowBytes) + cellLeftBytes);
            var destinationOffset = checked(row * destinationRowBytes);
            Marshal.Copy(
                IntPtr.Add(sourcePixels, sourceOffset),
                destination,
                destinationOffset,
                destinationRowBytes);
        }

        return destination;
    }
}
