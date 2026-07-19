using PetDesktop.Core.Wellness;

namespace PetDesktop.Core.Tests;

public sealed class WellnessPromptEvaluatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PromptsAtInitialThresholdThenOnlyAfterCooldown()
    {
        var policy = new WellnessPolicy();
        var state = WellnessPromptEvaluator.Evaluate(policy, new WellnessState(), Start, TimeSpan.Zero).State;

        var first = WellnessPromptEvaluator.Evaluate(policy, state, Start.AddMinutes(60), TimeSpan.Zero);
        var early = WellnessPromptEvaluator.Evaluate(policy, first.State, Start.AddMinutes(89), TimeSpan.Zero);
        var second = WellnessPromptEvaluator.Evaluate(policy, first.State, Start.AddMinutes(90), TimeSpan.Zero);

        Assert.True(first.PromptDue);
        Assert.False(early.PromptDue);
        Assert.True(second.PromptDue);
    }

    [Fact]
    public void BreakResetsSessionBeforeNextActiveSample()
    {
        var policy = new WellnessPolicy();
        var state = new WellnessState(Start, Start.AddMinutes(60));

        var breakResult = WellnessPromptEvaluator.Evaluate(policy, state, Start.AddMinutes(70), TimeSpan.FromMinutes(5));
        var resumed = WellnessPromptEvaluator.Evaluate(policy, breakResult.State, Start.AddMinutes(71), TimeSpan.Zero);

        Assert.False(breakResult.PromptDue);
        Assert.True(breakResult.State.OnBreak);
        Assert.False(resumed.PromptDue);
        Assert.Equal(Start.AddMinutes(71), resumed.State.SessionStartedAt);
    }

    [Fact]
    public void DisabledPolicyNeverPromptsOrMutatesState()
    {
        var state = new WellnessState(Start, Start);
        var result = WellnessPromptEvaluator.Evaluate(new WellnessPolicy(Enabled: false), state, Start.AddHours(2), TimeSpan.Zero);

        Assert.False(result.PromptDue);
        Assert.Equal(state, result.State);
    }
}
