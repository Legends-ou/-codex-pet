using PetDesktop.Core.Animation;
using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Tests;

public sealed class AnimationStateMachineTests
{
    [Fact]
    public void DraggingPreemptsReloadMenuWaveLookAndIdle()
    {
        var machine = new AnimationStateMachine(PetLayout.V2);
        machine.SetResourcePhase(ResourceAnimationPhase.Loading);
        machine.SetMenuOpen(true);
        machine.RequestOneShot(PetAction.Waving);
        machine.SetLookSector(4);

        machine.SetDragging(horizontalDelta: -8);

        Assert.Equal(PetAction.RunningLeft, machine.CurrentAction);
        Assert.Null(machine.CurrentLookSector);
    }

    [Theory]
    [InlineData(ResourceAnimationPhase.Loading, PetAction.Running)]
    [InlineData(ResourceAnimationPhase.Validating, PetAction.Review)]
    [InlineData(ResourceAnimationPhase.Failed, PetAction.Failed)]
    public void ResourceStatesPreemptMenuAndOneShot(ResourceAnimationPhase phase, PetAction expected)
    {
        var machine = new AnimationStateMachine(PetLayout.V2);
        machine.SetMenuOpen(true);
        machine.RequestOneShot(PetAction.Jumping);

        machine.SetResourcePhase(phase);

        Assert.Equal(expected, machine.CurrentAction);
    }

    [Fact]
    public void OneShotEndsAndFallsBackToLookThenIdle()
    {
        var machine = new AnimationStateMachine(PetLayout.V2);
        machine.SetLookSector(5);
        machine.RequestOneShot(PetAction.Waving);

        Assert.Equal(PetAction.Waving, machine.CurrentAction);

        machine.Tick(TimeSpan.FromMilliseconds(PetLayout.V2.Actions[PetAction.Waving].DurationsMs.Sum()));

        Assert.Null(machine.CurrentAction);
        Assert.Equal(5, machine.CurrentLookSector);

        machine.SetLookSector(null);

        Assert.Equal(PetAction.Idle, machine.CurrentAction);
    }

    [Fact]
    public void V1NeverUsesLookFrames()
    {
        var machine = new AnimationStateMachine(PetLayout.V1);

        machine.SetLookSector(7);

        Assert.Equal(PetAction.Idle, machine.CurrentAction);
        Assert.Null(machine.CurrentLookSector);
    }

    [Fact]
    public void EachStandardActionIsReachableThroughADeclaredInput()
    {
        var machine = new AnimationStateMachine(PetLayout.V2);

        machine.SetDragging(1);
        Assert.Equal(PetAction.RunningRight, machine.CurrentAction);
        machine.SetDragging(-1);
        Assert.Equal(PetAction.RunningLeft, machine.CurrentAction);
        machine.SetDragging(0);

        foreach (var action in new[] { PetAction.Waving, PetAction.Jumping })
        {
            machine.RequestOneShot(action);
            Assert.Equal(action, machine.CurrentAction);
            machine.Tick(TimeSpan.FromMilliseconds(PetLayout.V2.Actions[action].DurationsMs.Sum()));
        }

        machine.SetMenuOpen(true);
        Assert.Equal(PetAction.Waiting, machine.CurrentAction);
        machine.SetMenuOpen(false);
        machine.SetResourcePhase(ResourceAnimationPhase.Loading);
        Assert.Equal(PetAction.Running, machine.CurrentAction);
        machine.SetResourcePhase(ResourceAnimationPhase.Validating);
        Assert.Equal(PetAction.Review, machine.CurrentAction);
        machine.SetResourcePhase(ResourceAnimationPhase.Failed);
        Assert.Equal(PetAction.Failed, machine.CurrentAction);
        machine.SetResourcePhase(ResourceAnimationPhase.None);
        Assert.Equal(PetAction.Idle, machine.CurrentAction);
    }

    [Fact]
    public void V2ExposesEveryLookSector()
    {
        var machine = new AnimationStateMachine(PetLayout.V2);

        for (var sector = 0; sector < 16; sector++)
        {
            machine.SetLookSector(sector);
            Assert.Null(machine.CurrentAction);
            Assert.Equal(sector, machine.CurrentLookSector);
        }
    }

    [Fact]
    public void IdleForThirtyToNinetySecondsRequestsOnlyLightweightOneShot()
    {
        var machine = new AnimationStateMachine(PetLayout.V2, new FixedRandom(0));

        machine.Tick(TimeSpan.FromSeconds(30));

        Assert.Equal(PetAction.Waving, machine.CurrentAction);
    }

    [Fact]
    public void FailedResourceAnimationPlaysOnceThenReturnsToIdle()
    {
        var machine = new AnimationStateMachine(PetLayout.V2);
        machine.SetResourcePhase(ResourceAnimationPhase.Failed);

        machine.Tick(TimeSpan.FromMilliseconds(PetLayout.V2.Actions[PetAction.Failed].DurationsMs.Sum()));

        Assert.Equal(PetAction.Idle, machine.CurrentAction);
    }

    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int minValue, int maxValue) => value;
    }
}
