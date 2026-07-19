using PetDesktop.App.Imaging;
using PetDesktop.Core.Pets;

namespace PetDesktop.App.Tests;

public sealed class DecodedPetAssetTests
{
    [Theory]
    [InlineData(0, 208)]
    [InlineData(-1, 208)]
    [InlineData(192, 0)]
    [InlineData(192, -1)]
    public void DecodedFrameRejectsNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DecodedFrame.Standard(PetAction.Idle, 0, 280, width, height, []));
    }

    [Fact]
    public void DecodedFrameRejectsNonPremultipliedBgraPixels()
    {
        byte[] pixels = [1, 0, 0, 0];

        var error = Assert.Throws<ArgumentException>(
            () => DecodedFrame.Standard(PetAction.Idle, 0, 280, 1, 1, pixels));

        Assert.Contains("premultiplied", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodedFrameExposesLengthAndExplicitDefensiveCopy()
    {
        var source = new byte[4];
        var frame = DecodedFrame.Standard(PetAction.Idle, 0, 280, 1, 1, source);

        source[3] = 255;
        var firstCopy = frame.CopyPixelBytes();
        firstCopy[3] = 255;

        Assert.Equal(4, frame.PixelByteLength);
        Assert.Equal(0, frame.CopyPixelBytes()[3]);
    }

    [Theory]
    [InlineData(3, 1536, 2288)]
    [InlineData(1, 1536, 2288)]
    [InlineData(2, 1536, 1872)]
    public void DecodedAssetRejectsNonCanonicalVersionAndAtlasDimensions(int version, int width, int height)
    {
        var valid = CreateCanonical(PetLayout.V2);

        Assert.Throws<PetFormatException>(
            () => new DecodedPetAsset(version, width, height, valid.Actions, valid.LookFrames));
    }

    [Fact]
    public void DecodedAssetRequiresExactlyTheNineStandardActionKeys()
    {
        var valid = CreateCanonical(PetLayout.V2);
        var missing = valid.Actions
            .Where(pair => pair.Key != PetAction.Idle)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var extra = valid.Actions.ToDictionary(pair => pair.Key, pair => pair.Value);
        extra.Add((PetAction)999, valid.Actions[PetAction.Idle]);

        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, missing, valid.LookFrames));
        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, extra, valid.LookFrames));
    }

    [Fact]
    public void DecodedAssetValidatesEveryStandardActionFrameContract()
    {
        var valid = CreateCanonical(PetLayout.V2);

        AssertInvalidActionFrames(valid, PetAction.Idle, frames => frames.Take(frames.Count - 1).ToArray());
        AssertInvalidActionFrames(valid, PetAction.Idle, frames => Replace(frames, 0, null!));
        AssertInvalidActionFrames(
            valid,
            PetAction.Idle,
            frames => Replace(frames, 0, StandardFrame(PetAction.Waving, 0, 280)));
        AssertInvalidActionFrames(
            valid,
            PetAction.Idle,
            frames => Replace(frames, 0, StandardFrame(PetAction.Idle, 1, 280)));
        AssertInvalidActionFrames(
            valid,
            PetAction.Idle,
            frames => Replace(frames, 0, StandardFrame(PetAction.Idle, 0, 281)));
        AssertInvalidActionFrames(
            valid,
            PetAction.Idle,
            frames => Replace(frames, 0, StandardFrame(PetAction.Idle, 0, 280, width: 1, height: 1)));
    }

    [Fact]
    public void DecodedAssetValidatesVersionSpecificLookFrameContract()
    {
        var v1 = CreateCanonical(PetLayout.V1);
        var v2 = CreateCanonical(PetLayout.V2);

        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(1, 1536, 1872, v1.Actions, [LookFrame(0)]));
        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, v2.Actions, v2.LookFrames.Take(15).ToArray()));
        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, v2.Actions, Replace(v2.LookFrames, 1, LookFrame(0))));
        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, v2.Actions, Replace(v2.LookFrames, 0, StandardFrame(PetAction.Idle, 0, 280))));
        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, v2.Actions, Replace(v2.LookFrames, 0, LookFrame(0, width: 1, height: 1))));
    }

    private static void AssertInvalidActionFrames(
        CanonicalAsset valid,
        PetAction action,
        Func<IReadOnlyList<DecodedFrame>, IReadOnlyList<DecodedFrame>> mutate)
    {
        var actions = valid.Actions.ToDictionary(pair => pair.Key, pair => pair.Value);
        actions[action] = mutate(actions[action]);

        Assert.Throws<ArgumentException>(
            () => new DecodedPetAsset(2, 1536, 2288, actions, valid.LookFrames));
    }

    private static CanonicalAsset CreateCanonical(PetLayout layout)
    {
        var actions = PetActions.All.ToDictionary(
            action => action,
            action => (IReadOnlyList<DecodedFrame>)layout.Actions[action].DurationsMs
                .Select((duration, index) => StandardFrame(action, index, duration))
                .ToArray());
        IReadOnlyList<DecodedFrame> lookFrames = layout.HasLookDirections
            ? Enumerable.Range(0, 16).Select(sector => LookFrame(sector)).ToArray()
            : [];
        return new CanonicalAsset(actions, lookFrames);
    }

    private static DecodedFrame StandardFrame(
        PetAction action,
        int frameIndex,
        int duration,
        int width = PetLayout.CanonicalCellWidth,
        int height = PetLayout.CanonicalCellHeight) =>
        DecodedFrame.Standard(action, frameIndex, duration, width, height, new byte[checked(width * height * 4)]);

    private static DecodedFrame LookFrame(
        int sector,
        int width = PetLayout.CanonicalCellWidth,
        int height = PetLayout.CanonicalCellHeight) =>
        DecodedFrame.Look(sector, width, height, new byte[checked(width * height * 4)]);

    private static DecodedFrame[] Replace(
        IReadOnlyList<DecodedFrame> frames,
        int index,
        DecodedFrame replacement)
    {
        var copy = frames.ToArray();
        copy[index] = replacement;
        return copy;
    }

    private sealed record CanonicalAsset(
        IReadOnlyDictionary<PetAction, IReadOnlyList<DecodedFrame>> Actions,
        IReadOnlyList<DecodedFrame> LookFrames);
}
