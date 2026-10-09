using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>A known health condition identity. Absence on an officer means
/// unresolved knowledge; identities outside the conventional injury levels
/// remain available to independent rules and future content.</summary>
public sealed record OfficerHealth
{
    [JsonConstructor]
    public OfficerHealth(int stateId)
    {
        if (stateId < 0) throw new InvalidDataException("Officer health identity cannot be negative.");
        StateId = stateId;
    }

    public int StateId { get; }
    public static OfficerHealth Healthy { get; } = new(0);
}

public enum HealthAbilityModifier { Healthy, InjuryLevel1, InjuryLevel2, InjuryLevel3, Zero }

/// <summary>Conventional health behavior, separate from source record layout
/// and from the provider of configurable ability percentages.</summary>
public static class OfficerHealthRules
{
    /// <summary>The return health flag treats every known nonzero identity as
    /// injured, including opaque historical identities without medical labels.</summary>
    public static bool IsInjured(OfficerHealth health) => health.StateId != 0;

    public static HealthAbilityModifier AbilityModifier(OfficerHealth health) => health.StateId switch
    {
        0 => HealthAbilityModifier.Healthy,
        1 => HealthAbilityModifier.InjuryLevel1,
        2 => HealthAbilityModifier.InjuryLevel2,
        3 => HealthAbilityModifier.InjuryLevel3,
        _ => HealthAbilityModifier.Zero
    };

    public static bool SupportsOrdinaryBattleInjury(OfficerHealth health) => health.StateId is 0 or 1;

    public static OfficerHealth WorsenOrdinaryBattleInjury(OfficerHealth health) => health.StateId switch
    {
        0 => new(1),
        1 => new(2),
        _ => throw new InvalidOperationException("This health condition requires a separate severe-injury rule.")
    };
}

public sealed partial record OfficerState
{
    public OfficerHealth? Health { get; init; }
}
