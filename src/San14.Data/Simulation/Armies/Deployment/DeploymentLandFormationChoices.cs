namespace San14.Data;

/// <summary>A source-published selectable row, not a disabled table entry.</summary>
public sealed record DeploymentLandFormationChoice(int FormationId, string Name, byte RawKind,
    ArmyMapIconSelection Icon, bool Eligible);
public sealed record DeploymentLandFormationChoices(IReadOnlyList<DeploymentLandFormationChoice>? Choices,
    string? UnresolvedInput)
{
    public bool Available => Choices is not null && UnresolvedInput is null;
    // Only the five registered radio consumers request these current fields.
    // An unknown balloon never removes an otherwise eligible choice.
    public System.Collections.Immutable.ImmutableDictionary<int, AreaAdministrationQuery<DeploymentFormationBalloon>> Balloons
        { get; init; } = System.Collections.Immutable.ImmutableDictionary<int, AreaAdministrationQuery<DeploymentFormationBalloon>>.Empty;
}

/// <summary>The ordinary formation popup's current fields, independent of
/// title-gallery unlocks, selected-state styling and draft admission.</summary>
public sealed record DeploymentFormationBalloon(int FormationId, string Name, string Description,
    byte RawKind, string CategoryCaption, string RangeCaption, string RangeValue, bool HighlightRange);

internal static class DeploymentFormationEligibility
{
    //232480: unsigned formation bound, current capability bit, then the
    //unsigned Person.word10 minus4101 range test for formation14 only.
    internal static bool Allows(uint currentMask, int formation, int currentPersonId)
        => (uint)formation < 21 && (currentMask & (1u << formation)) != 0 &&
            (formation != 14 || unchecked((uint)(currentPersonId - 4101)) <= 99);
}
