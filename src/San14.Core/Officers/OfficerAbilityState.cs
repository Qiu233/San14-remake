namespace San14.Core;

/// <summary>Current ability limits and observed doctrine knowledge. Each channel
/// can be unknown independently; resource record identities and encodings are
/// resolved by the data layer.</summary>
public sealed record OfficerAbilityState
{
    public int? OrdinaryLimit { get; }
    public int? CharmLimit { get; }
    public int? ObservedDoctrineId { get; }

    public OfficerAbilityState(int? ordinaryLimit, int? charmLimit, int? observedDoctrineId)
    {
        if (ordinaryLimit < 0 || charmLimit < 0 || observedDoctrineId < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinaryLimit), "Ability limits and doctrine identities must be nonnegative.");
        OrdinaryLimit = ordinaryLimit;
        CharmLimit = charmLimit;
        ObservedDoctrineId = observedDoctrineId;
    }
}

public static class OfficerDoctrineRules
{
    // An observed zero proves no doctrine. A nonzero serialized observation
    // does not establish its relationship to the declared doctrine catalog.
    public static int? ReadDoctrineId(int? declaredDoctrineId, OfficerAbilityState? abilities) =>
        declaredDoctrineId ?? (abilities?.ObservedDoctrineId == 0 ? 0 : null);
}

public sealed partial record OfficerState
{
    public OfficerAbilityState? AbilityState { get; init; }
}
