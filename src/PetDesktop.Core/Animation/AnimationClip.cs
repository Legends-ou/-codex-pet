using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Animation;

public sealed class AnimationClip
{
    private AnimationClip(PetAction? action, int? lookSector, IReadOnlyList<int> frameDurationsMs, bool loops)
    {
        Action = action;
        LookSector = lookSector;
        FrameDurationsMs = Array.AsReadOnly(frameDurationsMs.ToArray());
        Loops = loops;
    }

    public PetAction? Action { get; }

    public int? LookSector { get; }

    public IReadOnlyList<int> FrameDurationsMs { get; }

    public bool Loops { get; }

    public static AnimationClip Standard(PetLayout layout, PetAction action, bool loops)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!layout.Actions.TryGetValue(action, out var row))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown standard pet action.");
        }

        return new AnimationClip(action, null, row.DurationsMs, loops);
    }

    public static AnimationClip Look(int sector)
    {
        if (sector is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(sector), sector, "Look sector must be from 0 through 15.");
        }

        return new AnimationClip(null, sector, [], loops: true);
    }
}
