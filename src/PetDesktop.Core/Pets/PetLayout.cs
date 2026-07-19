using System.Collections.ObjectModel;

namespace PetDesktop.Core.Pets;

public sealed class AnimationRow
{
    public AnimationRow(PetAction action, int row, int usedFrames, IReadOnlyList<int> durationsMs)
    {
        ArgumentNullException.ThrowIfNull(durationsMs);

        if (!PetActions.All.Contains(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown standard pet action.");
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, "Animation row must be non-negative.");
        }

        if (usedFrames is < 1 or > PetLayout.CanonicalColumns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedFrames),
                usedFrames,
                $"Used frame count must be between 1 and {PetLayout.CanonicalColumns}.");
        }

        if (durationsMs.Count != usedFrames)
        {
            throw new ArgumentException(
                "Animation duration count must equal the used frame count.",
                nameof(durationsMs));
        }

        if (durationsMs.Any(duration => duration <= 0))
        {
            throw new ArgumentException("Every animation duration must be positive.", nameof(durationsMs));
        }

        Action = action;
        Row = row;
        UsedFrames = usedFrames;
        DurationsMs = Array.AsReadOnly(durationsMs.ToArray());
    }

    public PetAction Action { get; }

    public int Row { get; }

    public int UsedFrames { get; }

    public IReadOnlyList<int> DurationsMs { get; }
}

public sealed class PetLayout
{
    public const int CanonicalColumns = 8;
    public const int CanonicalCellWidth = 192;
    public const int CanonicalCellHeight = 208;

    private static readonly IReadOnlyDictionary<PetAction, AnimationRow> StandardActions =
        new ReadOnlyDictionary<PetAction, AnimationRow>(
            new Dictionary<PetAction, AnimationRow>
            {
                [PetAction.Idle] = CreateRow(PetAction.Idle, 0, [280, 110, 110, 140, 140, 320]),
                [PetAction.RunningRight] = CreateRow(PetAction.RunningRight, 1, [120, 120, 120, 120, 120, 120, 120, 220]),
                [PetAction.RunningLeft] = CreateRow(PetAction.RunningLeft, 2, [120, 120, 120, 120, 120, 120, 120, 220]),
                [PetAction.Waving] = CreateRow(PetAction.Waving, 3, [140, 140, 140, 280]),
                [PetAction.Jumping] = CreateRow(PetAction.Jumping, 4, [140, 140, 140, 140, 280]),
                [PetAction.Failed] = CreateRow(PetAction.Failed, 5, [140, 140, 140, 140, 140, 140, 140, 240]),
                [PetAction.Waiting] = CreateRow(PetAction.Waiting, 6, [150, 150, 150, 150, 150, 260]),
                [PetAction.Running] = CreateRow(PetAction.Running, 7, [120, 120, 120, 120, 120, 220]),
                [PetAction.Review] = CreateRow(PetAction.Review, 8, [150, 150, 150, 150, 150, 280]),
            });

    private PetLayout(int version, int rows, bool hasLookDirections)
    {
        Version = version;
        Columns = CanonicalColumns;
        Rows = rows;
        CellWidth = CanonicalCellWidth;
        CellHeight = CanonicalCellHeight;
        Actions = StandardActions;
        HasLookDirections = hasLookDirections;
    }

    public static PetLayout V1 { get; } = new(1, 9, false);

    public static PetLayout V2 { get; } = new(2, 11, true);

    public int Version { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int CellWidth { get; }

    public int CellHeight { get; }

    public int AtlasWidth => Columns * CellWidth;

    public int AtlasHeight => Rows * CellHeight;

    public IReadOnlyDictionary<PetAction, AnimationRow> Actions { get; }

    public bool HasLookDirections { get; }

    private static AnimationRow CreateRow(PetAction action, int row, int[] durations) =>
        new(action, row, durations.Length, durations);
}
