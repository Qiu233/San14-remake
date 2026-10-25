namespace San14.Core;

public enum MonthlySceneFamily { FinalOrder, PersonDeath }

/// <summary>A monthly scene genuinely installed by the presentation host.</summary>
public sealed record MonthlySceneState(MonthlySceneFamily Family, int EventKind);

public sealed partial record GameState
{
    public MonthlySceneState? ActiveMonthlyScene { get; init; }
}
