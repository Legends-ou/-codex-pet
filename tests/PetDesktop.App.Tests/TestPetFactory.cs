using PetDesktop.Core.Pets;
using SkiaSharp;

namespace PetDesktop.App.Tests;

internal sealed class TestPetFile : IDisposable
{
    public TestPetFile(string directoryPath, string spritesheetPath)
    {
        DirectoryPath = directoryPath;
        SpritesheetPath = spritesheetPath;
    }

    public string DirectoryPath { get; }

    public string SpritesheetPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

internal static class TestPetFactory
{
    public static TestPetFile Create(PetLayout layout) =>
        Create(layout.AtlasWidth, layout.AtlasHeight, layout, SKEncodedImageFormat.Webp);

    public static TestPetFile CreatePngDisguisedAsWebp(PetLayout layout) =>
        Create(layout.AtlasWidth, layout.AtlasHeight, layout, SKEncodedImageFormat.Png);

    public static TestPetFile CreateWithDimensions(int width, int height) =>
        Create(width, height, layout: null, SKEncodedImageFormat.Webp);

    internal static SKColor MarkerColor(int row, int column) =>
        new(
            red: (byte)(32 + (row * 17)),
            green: (byte)(48 + (column * 19)),
            blue: (byte)(64 + ((row + column) * 7)),
            alpha: 255);

    private static TestPetFile Create(
        int width,
        int height,
        PetLayout? layout,
        SKEncodedImageFormat encodedFormat)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PetDesktop.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "spritesheet.webp");

        try
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);
            bitmap.Erase(SKColors.Transparent);

            if (layout is not null)
            {
                PaintCanonicalCells(bitmap, layout);
            }

            using var pixmap = bitmap.PeekPixels();
            using var data = Encode(pixmap, encodedFormat)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the test WebP atlas.");
            using var stream = File.Create(path);
            data.SaveTo(stream);
            return new TestPetFile(directory, path);
        }
        catch
        {
            Directory.Delete(directory, recursive: true);
            throw;
        }
    }

    private static SKData? Encode(SKPixmap pixmap, SKEncodedImageFormat encodedFormat) =>
        encodedFormat == SKEncodedImageFormat.Webp
            ? pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))
            : pixmap.Encode(encodedFormat, 100);

    private static void PaintCanonicalCells(SKBitmap bitmap, PetLayout layout)
    {
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = false };

        foreach (var row in layout.Actions.Values)
        {
            for (var frameIndex = 0; frameIndex < row.UsedFrames; frameIndex++)
            {
                paint.Color = MarkerColor(row.Row, frameIndex);
                PaintCell(canvas, paint, row.Row, frameIndex);
            }
        }

        if (layout.HasLookDirections)
        {
            for (var sector = 0; sector < 16; sector++)
            {
                var row = 9 + (sector / layout.Columns);
                var column = sector % layout.Columns;
                paint.Color = MarkerColor(row, column);
                PaintCell(canvas, paint, row, column);
            }
        }
    }

    private static void PaintCell(SKCanvas canvas, SKPaint paint, int row, int column)
    {
        const int inset = 12;
        canvas.DrawRect(
            (column * PetLayout.CanonicalCellWidth) + inset,
            (row * PetLayout.CanonicalCellHeight) + inset,
            PetLayout.CanonicalCellWidth - (inset * 2),
            PetLayout.CanonicalCellHeight - (inset * 2),
            paint);
    }

}
