namespace San14.Core;

/// <summary>Current, independently known duel inputs. Null is unknown, never
/// a constructor default. Immutable copies preserve both channels.</summary>
public sealed record OfficerDuelCondition(short? QualificationAdjustment, byte? Readiness);

public sealed partial record OfficerState
{
    public OfficerDuelCondition? DuelCondition { get; init; }
}
