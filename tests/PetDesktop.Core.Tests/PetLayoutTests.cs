using System.Collections;
using System.Reflection;
using System.Text.Json;
using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Tests;

public sealed class PetLayoutTests
{
    private static readonly IReadOnlyDictionary<PetAction, ExpectedRow> ExpectedRows =
        new Dictionary<PetAction, ExpectedRow>
        {
            [PetAction.Idle] = new(0, "idle", [280, 110, 110, 140, 140, 320]),
            [PetAction.RunningRight] = new(1, "running-right", [120, 120, 120, 120, 120, 120, 120, 220]),
            [PetAction.RunningLeft] = new(2, "running-left", [120, 120, 120, 120, 120, 120, 120, 220]),
            [PetAction.Waving] = new(3, "waving", [140, 140, 140, 280]),
            [PetAction.Jumping] = new(4, "jumping", [140, 140, 140, 140, 280]),
            [PetAction.Failed] = new(5, "failed", [140, 140, 140, 140, 140, 140, 140, 240]),
            [PetAction.Waiting] = new(6, "waiting", [150, 150, 150, 150, 150, 260]),
            [PetAction.Running] = new(7, "running", [120, 120, 120, 120, 120, 220]),
            [PetAction.Review] = new(8, "review", [150, 150, 150, 150, 150, 280]),
        };

    [Fact]
    public void StandardActionsExposeExactlyTheCanonicalUniqueKeys()
    {
        var actions = PetActions.All;
        var keys = actions.Select(PetActions.GetKey).ToArray();

        Assert.Equal(Enum.GetValues<PetAction>(), actions);
        Assert.Equal(ExpectedRows.Keys.Order(), actions.Order());
        Assert.Equal(ExpectedRows.Values.Select(row => row.Key).Order(), keys.Order());
        Assert.Equal(actions.Order(), PetLayout.V1.Actions.Keys.Order());
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, keys.Length);
    }

    [Fact]
    public void GetKeyRejectsUnknownEnumValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PetActions.GetKey((PetAction)999));
    }

    [Fact]
    public void StandardRowsMatchEveryCanonicalFrameDuration()
    {
        var actions = PetLayout.V1.Actions;

        Assert.Equal(9, actions.Count);
        foreach (var (action, expected) in ExpectedRows)
        {
            var row = actions[action];
            Assert.Equal(action, row.Action);
            Assert.Equal(expected.Row, row.Row);
            Assert.Equal(expected.Durations.Length, row.UsedFrames);
            Assert.Equal(expected.Durations, row.DurationsMs);
        }
    }

    [Fact]
    public void V1AndV2ShareTheStandardActionTableAndOnlyDifferInVersionRowsAndLookDirections()
    {
        Assert.Equal(1, PetLayout.V1.Version);
        Assert.Equal(2, PetLayout.V2.Version);
        Assert.Equal(8, PetLayout.V1.Columns);
        Assert.Equal(8, PetLayout.V2.Columns);
        Assert.Equal(9, PetLayout.V1.Rows);
        Assert.Equal(11, PetLayout.V2.Rows);
        Assert.Equal(192, PetLayout.V1.CellWidth);
        Assert.Equal(192, PetLayout.V2.CellWidth);
        Assert.Equal(208, PetLayout.V1.CellHeight);
        Assert.Equal(208, PetLayout.V2.CellHeight);
        Assert.False(PetLayout.V1.HasLookDirections);
        Assert.True(PetLayout.V2.HasLookDirections);
        Assert.Same(PetLayout.V1.Actions, PetLayout.V2.Actions);
    }

    [Fact]
    public void PublicActionAndDurationCollectionsCannotBeModifiedByCallers()
    {
        var standardActions = Assert.IsAssignableFrom<IList>(PetActions.All);
        var actions = Assert.IsAssignableFrom<IDictionary>(PetLayout.V1.Actions);
        var durations = Assert.IsAssignableFrom<IList>(PetLayout.V1.Actions[PetAction.Idle].DurationsMs);

        Assert.Throws<NotSupportedException>(() => standardActions.Add(PetAction.Idle));
        Assert.Throws<NotSupportedException>(() => actions.Add(PetAction.Review, PetLayout.V1.Actions[PetAction.Idle]));
        Assert.Throws<NotSupportedException>(() => durations[0] = 1);
    }

    [Fact]
    public void AnimationRowRejectsNullDurations()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AnimationRow(PetAction.Idle, 0, 1, null!));
    }

    [Theory]
    [InlineData(-1, 1, new[] { 100 })]
    [InlineData(0, 0, new int[] { })]
    [InlineData(0, 9, new[] { 100, 100, 100, 100, 100, 100, 100, 100, 100 })]
    [InlineData(0, 2, new[] { 100 })]
    [InlineData(0, 1, new[] { 0 })]
    [InlineData(0, 1, new[] { -1 })]
    public void AnimationRowRejectsInvalidInvariants(int row, int usedFrames, int[] durations)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new AnimationRow(PetAction.Idle, row, usedFrames, durations));
    }

    [Fact]
    public void AnimationRowCopiesCallerOwnedDurations()
    {
        int[] source = [100];
        var row = new AnimationRow(PetAction.Idle, 0, 1, source);

        source[0] = 999;

        Assert.Equal(100, row.DurationsMs[0]);
    }

    [Fact]
    public void PetManifestUsesCodexJsonPropertyNames()
    {
        var manifest = new PetManifest("pet-id", "Pet Name", null, 2, "spritesheet.webp");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
        var root = json.RootElement;
        Assert.Equal("pet-id", root.GetProperty("id").GetString());
        Assert.Equal("Pet Name", root.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("description").ValueKind);
        Assert.Equal(2, root.GetProperty("spriteVersionNumber").GetInt32());
        Assert.Equal("spritesheet.webp", root.GetProperty("spritesheetPath").GetString());
        Assert.Equal(5, root.EnumerateObject().Count());
    }

    [Fact]
    public void PetManifestDisplayNameIsNullableWhenJsonPropertyIsMissing()
    {
        const string json = """
            {
              "id": "pet-id",
              "spriteVersionNumber": 2,
              "spritesheetPath": "spritesheet.webp"
            }
            """;

        var manifest = JsonSerializer.Deserialize<PetManifest>(json);
        var displayNameProperty = typeof(PetManifest).GetProperty(nameof(PetManifest.DisplayName));
        var nullability = new NullabilityInfoContext().Create(displayNameProperty!);

        Assert.NotNull(manifest);
        Assert.Null(manifest.DisplayName);
        Assert.Equal(NullabilityState.Nullable, nullability.ReadState);
    }

    private sealed record ExpectedRow(int Row, string Key, int[] Durations);
}
