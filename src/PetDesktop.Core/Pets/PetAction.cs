using System.Collections.ObjectModel;

namespace PetDesktop.Core.Pets;

public enum PetAction
{
    Idle,
    RunningRight,
    RunningLeft,
    Waving,
    Jumping,
    Failed,
    Waiting,
    Running,
    Review,
}

public static class PetActions
{
    private static readonly ReadOnlyCollection<PetAction> StandardActions = Array.AsReadOnly(
    [
        PetAction.Idle,
        PetAction.RunningRight,
        PetAction.RunningLeft,
        PetAction.Waving,
        PetAction.Jumping,
        PetAction.Failed,
        PetAction.Waiting,
        PetAction.Running,
        PetAction.Review,
    ]);

    public static IReadOnlyList<PetAction> All => StandardActions;

    public static string GetKey(PetAction action) => action switch
    {
        PetAction.Idle => "idle",
        PetAction.RunningRight => "running-right",
        PetAction.RunningLeft => "running-left",
        PetAction.Waving => "waving",
        PetAction.Jumping => "jumping",
        PetAction.Failed => "failed",
        PetAction.Waiting => "waiting",
        PetAction.Running => "running",
        PetAction.Review => "review",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown standard pet action."),
    };
}
