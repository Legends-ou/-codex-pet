using System.Collections;
using PetDesktop.App.Imaging;
using PetDesktop.Core.Pets;

namespace PetDesktop.App.Tests;

public sealed class SkiaSpriteDecoderTests
{
    [Fact]
    public async Task DecodeAsyncInfersV2FromCanonicalAtlasWhenManifestOmitsTheVersion()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);

        var asset = await SkiaSpriteDecoder.DecodeAsync(
            pet.SpritesheetPath,
            declaredSpriteVersionNumber: null,
            CancellationToken.None);

        Assert.Equal(2, asset.Version);
        Assert.Equal(16, asset.LookFrames.Count);
    }

    [Theory]
    [InlineData(1, 57, 0)]
    [InlineData(2, 73, 16)]
    public async Task DecodeAsyncExtractsOnlyCanonicalFrames(int version, int totalFrames, int lookFrames)
    {
        var layout = version == 1 ? PetLayout.V1 : PetLayout.V2;
        using var pet = TestPetFactory.Create(layout);

        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, layout, CancellationToken.None);

        Assert.Equal(layout.Version, asset.Version);
        Assert.Equal(layout.AtlasWidth, asset.AtlasWidth);
        Assert.Equal(layout.AtlasHeight, asset.AtlasHeight);
        Assert.Equal(totalFrames, asset.Frames.Count);
        Assert.Equal(lookFrames, asset.LookFrames.Count);
        Assert.Equal(PetActions.All.Order(), asset.ActionFrames.Keys.Order());

        foreach (var action in PetActions.All)
        {
            var expected = layout.Actions[action];
            var actual = asset.ActionFrames[action];
            Assert.Equal(expected.UsedFrames, actual.Count);
            Assert.Equal(expected.DurationsMs, actual.Select(frame => frame.DurationMs!.Value));
            Assert.Equal(Enumerable.Range(0, expected.UsedFrames), actual.Select(frame => frame.FrameIndex));
            Assert.All(actual, frame =>
            {
                Assert.Equal(action, frame.Action);
                Assert.Null(frame.LookSector);
            });
        }
    }

    [Fact]
    public async Task DecodeAsyncMapsV2LookSectorsInRowMajorOrder()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);

        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V2, CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, 16), asset.LookFrames.Select(frame => frame.LookSector!.Value));
        Assert.All(asset.LookFrames, frame =>
        {
            Assert.Null(frame.Action);
            Assert.Null(frame.DurationMs);
            Assert.Equal(0, frame.FrameIndex);
        });
    }

    [Fact]
    public async Task DecodeAsyncPreservesEveryCanonicalCellMarker()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);

        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V2, CancellationToken.None);

        foreach (var action in PetActions.All)
        {
            var expectedRow = PetLayout.V2.Actions[action].Row;
            foreach (var frame in asset.ActionFrames[action])
            {
                AssertCenterMarker(frame, TestPetFactory.MarkerColor(expectedRow, frame.FrameIndex));
            }
        }

        (int Row, int Column)[] expectedLookCells =
        [
            (9, 0), (9, 1), (9, 2), (9, 3), (9, 4), (9, 5), (9, 6), (9, 7),
            (10, 0), (10, 1), (10, 2), (10, 3), (10, 4), (10, 5), (10, 6), (10, 7),
        ];

        for (var sector = 0; sector < expectedLookCells.Length; sector++)
        {
            var expectedCell = expectedLookCells[sector];
            AssertCenterMarker(
                asset.LookFrames[sector],
                TestPetFactory.MarkerColor(expectedCell.Row, expectedCell.Column));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DecodeAsyncCopiesBgraPixelsAndMatchingAlphaMask(int version)
    {
        var layout = version == 1 ? PetLayout.V1 : PetLayout.V2;
        using var pet = TestPetFactory.Create(layout);

        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, layout, CancellationToken.None);

        foreach (var frame in asset.Frames)
        {
            Assert.Equal(PetLayout.CanonicalCellWidth, frame.Width);
            Assert.Equal(PetLayout.CanonicalCellHeight, frame.Height);
            var pixels = frame.CopyPixelBytes();
            Assert.Equal(checked(frame.Width * frame.Height * 4), pixels.Length);
            Assert.Equal(frame.Width, frame.HitTestMask.Width);
            Assert.Equal(frame.Height, frame.HitTestMask.Height);

            foreach (var point in SamplePoints(frame.Width, frame.Height))
            {
                var alpha = pixels[checked(((point.Y * frame.Width) + point.X) * 4 + 3)];
                Assert.Equal(alpha >= AlphaHitTestMask.DefaultThreshold, frame.HitTestMask.IsHit(point.X, point.Y));
            }

            Assert.False(frame.HitTestMask.IsHit(0, 0));
            Assert.True(frame.HitTestMask.IsHit(frame.Width / 2, frame.Height / 2));
        }
    }

    [Fact]
    public async Task DecodeAsyncDoesNotPublishUnusedTransparentCells()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);

        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V2, CancellationToken.None);

        Assert.Equal(57, asset.ActionFrames.Values.Sum(frames => frames.Count));
        Assert.All(
            asset.ActionFrames,
            pair => Assert.All(pair.Value, frame => Assert.InRange(frame.FrameIndex, 0, PetLayout.V2.Actions[pair.Key].UsedFrames - 1)));
    }

    [Fact]
    public async Task DecodeAsyncRejectsLayoutMismatchAndWrongDimensions()
    {
        using var v1 = TestPetFactory.Create(PetLayout.V1);
        using var wrongSize = TestPetFactory.CreateWithDimensions(64, 64);

        var mismatch = await Assert.ThrowsAsync<PetFormatException>(
            () => SkiaSpriteDecoder.DecodeAsync(v1.SpritesheetPath, PetLayout.V2, CancellationToken.None));
        var dimensions = await Assert.ThrowsAsync<PetFormatException>(
            () => SkiaSpriteDecoder.DecodeAsync(wrongSize.SpritesheetPath, PetLayout.V1, CancellationToken.None));

        Assert.Contains("version 2", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("64x64", dimensions.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecodeAsyncRejectsMissingAndCorruptFiles()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.webp");
        var corrupt = Path.GetTempFileName();
        await File.WriteAllBytesAsync(corrupt, [1, 2, 3, 4, 5]);

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => SkiaSpriteDecoder.DecodeAsync(missing, PetLayout.V1, CancellationToken.None));
            var error = await Assert.ThrowsAsync<InvalidDataException>(
                () => SkiaSpriteDecoder.DecodeAsync(corrupt, PetLayout.V1, CancellationToken.None));
            Assert.Contains("decode", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(corrupt);
        }
    }

    [Fact]
    public async Task DecodeAsyncAcceptsCanonicalPngEvenWhenTheFileExtensionIsWebp()
    {
        using var pet = TestPetFactory.CreatePngDisguisedAsWebp(PetLayout.V2);

        var asset = await SkiaSpriteDecoder.DecodeAsync(
            pet.SpritesheetPath,
            PetLayout.V2,
            CancellationToken.None);

        Assert.Equal(PetLayout.V2.AtlasWidth, asset.AtlasWidth);
        Assert.Equal(PetLayout.V2.AtlasHeight, asset.AtlasHeight);
    }

    [Fact]
    public async Task DecodeAsyncPropagatesPreCanceledToken()
    {
        using var pet = TestPetFactory.Create(PetLayout.V1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V1, cancellation.Token));
    }

    [Fact]
    public async Task DecodedAssetCollectionsAndPixelsCannotMutatePublishedState()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);
        var asset = await SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V2, CancellationToken.None);
        var frame = asset.ActionFrames[PetAction.Idle][0];
        var expectedFirstByte = frame.CopyPixelBytes()[0];

        var mutableDictionaryView = Assert.IsAssignableFrom<IDictionary>(asset.ActionFrames);
        var mutableActionView = Assert.IsAssignableFrom<IList>(asset.ActionFrames[PetAction.Idle]);
        var mutableLookView = Assert.IsAssignableFrom<IList>(asset.LookFrames);
        Assert.Throws<NotSupportedException>(() => mutableDictionaryView.Clear());
        Assert.Throws<NotSupportedException>(() => mutableActionView.Clear());
        Assert.Throws<NotSupportedException>(() => mutableLookView.Clear());

        var exposed = frame.CopyPixelBytes();
        exposed[0] ^= byte.MaxValue;

        Assert.Equal(expectedFirstByte, frame.CopyPixelBytes()[0]);
    }

    [Fact]
    public async Task DecodeAsyncParallelCallsDoNotShareMutableState()
    {
        using var pet = TestPetFactory.Create(PetLayout.V2);

        var assets = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(
                _ => SkiaSpriteDecoder.DecodeAsync(pet.SpritesheetPath, PetLayout.V2, CancellationToken.None)));

        Assert.All(assets.Skip(1), asset => Assert.NotSame(assets[0], asset));
        Assert.All(assets.Skip(1), asset => Assert.NotSame(assets[0].Frames[0], asset.Frames[0]));

        var original = assets[1].Frames[0].CopyPixelBytes()[0];
        var copy = assets[0].Frames[0].CopyPixelBytes();
        copy[0] ^= byte.MaxValue;
        Assert.Equal(original, assets[1].Frames[0].CopyPixelBytes()[0]);
        Assert.NotEqual(copy[0], assets[0].Frames[0].CopyPixelBytes()[0]);
    }

    private static IEnumerable<(int X, int Y)> SamplePoints(int width, int height)
    {
        yield return (0, 0);
        yield return (width / 2, height / 2);
        yield return (width - 1, height - 1);
        yield return (12, 12);
        yield return (11, 11);
    }

    private static void AssertCenterMarker(DecodedFrame frame, SkiaSharp.SKColor expected)
    {
        var pixels = frame.CopyPixelBytes();
        var centerOffset = checked((((frame.Height / 2) * frame.Width) + (frame.Width / 2)) * 4);
        Assert.Equal(expected.Blue, pixels[centerOffset]);
        Assert.Equal(expected.Green, pixels[centerOffset + 1]);
        Assert.Equal(expected.Red, pixels[centerOffset + 2]);
        Assert.Equal(expected.Alpha, pixels[centerOffset + 3]);
    }
}
