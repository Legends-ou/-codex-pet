namespace PetDesktop.Core.Wellness;

public sealed record WellnessPolicy(bool Enabled = true, int InitialMinutes = 60, int RepeatMinutes = 30, int BreakMinutes = 5)
{
    public WellnessPolicy Normalize() => this with
    {
        InitialMinutes = Math.Clamp(InitialMinutes, 1, 720),
        RepeatMinutes = Math.Clamp(RepeatMinutes, 1, 720),
        BreakMinutes = Math.Clamp(BreakMinutes, 1, 60),
    };
}

public sealed record WellnessState(DateTimeOffset? SessionStartedAt = null, DateTimeOffset? LastPromptAt = null, bool OnBreak = false);

public sealed record WellnessEvaluation(WellnessState State, bool PromptDue);

public static class WellnessPromptEvaluator
{
    public static WellnessEvaluation Evaluate(WellnessPolicy policy, WellnessState state, DateTimeOffset now, TimeSpan idleFor)
    {
        policy = policy.Normalize();
        if (!policy.Enabled)
        {
            return new(state, false);
        }

        if (idleFor >= TimeSpan.FromMinutes(policy.BreakMinutes))
        {
            return new(state with { SessionStartedAt = null, LastPromptAt = null, OnBreak = true }, false);
        }

        if (state.SessionStartedAt is null || state.OnBreak)
        {
            return new(state with { SessionStartedAt = now, LastPromptAt = null, OnBreak = false }, false);
        }

        var dueAt = state.LastPromptAt is { } prompt
            ? prompt.AddMinutes(policy.RepeatMinutes)
            : state.SessionStartedAt.Value.AddMinutes(policy.InitialMinutes);
        if (now < dueAt)
        {
            return new(state, false);
        }

        return new(state with { LastPromptAt = now, OnBreak = false }, true);
    }
}
