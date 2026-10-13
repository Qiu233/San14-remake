namespace San14.Data;

/// <summary>Explicit action ordinal, selected animation resource and caller clock.
/// Mission, troop count and movement distance do not infer an animation or elapsed time.</summary>
public sealed record ArmyAnimationSourceInput(int Category, int ActionIndex,
    uint AnimationFileId, float CurrentTimeSeconds)
{
    public float? PreviousTimeSeconds { get; init; }
}
