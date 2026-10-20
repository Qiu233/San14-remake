namespace San14.Core;

public enum CombatParticipantKind { Army, City, Gate, FieldObject, Area, Force }
public enum CombatSpeechEvent
{
    GarrisonUnderPressure, GarrisonFormationContact, GarrisonContact,
    UnexpectedContact, EngageChargingArmy, EngageTarget, ReplyToEngagement, UnresolvedGarrisonContact,
    ArmyOrderFinished, FalseReportInitiatorSucceeded, FalseReportTargetDeceived,
    FalseReportInitiatorFailed, FalseReportTargetResisted,
    ArmyLowTroops, ArmyLowMorale, ArmyTransporting, ArmyRetreating,
    ArmyMarchingToBase, ArmyAttackingBase, ArmyAttackingArmy,
    ArmyOccupyingArea, ArmyBuildingFacility, ArmyRemovingFacility,
    ArmyPreparingFire, ArmyMarching, ArmyNearbyAlly, ArmyCuttingSupplyLines,
    ArmyHoldingPosition, ArmyDemolitionCompleted, ArmyFacilityCompleted, ArmyFacilityConstructionStarted,
    TrapPitfall, TrapRockfall, TrapCountered, ConfusionApplied, ImmobilizationApplied
}
public sealed record GarrisonContactChoice(byte? DamageTier, int Troops, bool FormationContact);

/// <summary>Force ownership is captured when the pair is accepted. Later
/// changes of owner do not rewrite the most recently generated engagement.</summary>
public sealed record CombatEngagementEndpoint(CombatParticipantKind Kind, int ForceTableIndex);
public sealed record CombatEngagementPair(CombatEngagementEndpoint Actor, CombatEngagementEndpoint Target);

/// <summary>An ordered request awaiting the application's actual random
/// admission and shared speech queue. Speaker/target IDs are campaign unit or
/// location IDs, not source receiver slots. It is neither accepted nor played.</summary>
public sealed record CombatSpeechRequest(CombatParticipantKind SpeakerKind, int SpeakerId,
    int OfficerId, CombatSpeechEvent Event, int HexId, CombatParticipantKind? TargetKind,
    int? TargetId, int? RelatedOfficerId, int? AdmissionForce, byte? AdmissionColor)
{
    public GarrisonContactChoice? GarrisonChoice { get; init; }
    public int? TrapDefinitionId { get; init; }
}

/// <summary>The actual refresh generation and frozen engagement ownership.
/// Generation zero may own the fresh constructor's empty collection or retain
/// unknown knowledge from an older save; it never owns generated pairs.</summary>
public sealed record NormalCombatRefreshState(long Generation, CombatEngagementPair[]? Engagements)
{
    public NormalCombatRefreshState Copy() => this with { Engagements = Engagements?.ToArray() };
}

public sealed partial record GameState
{
    public NormalCombatRefreshState? NormalCombatRefresh { get; init; }
}

internal static class NormalCombatRefreshValidation
{
    internal static void Validate(NormalCombatRefreshState? state)
    {
        if (state is null) return;
        if (state.Generation < 0 || state.Generation == 0 && state.Engagements is { Length: > 0 } || state.Generation > 0 && state.Engagements is null)
            throw new InvalidDataException("Normal combat refresh ownership is incomplete.");
        foreach (var pair in state.Engagements ?? [])
            if (pair?.Actor is not { } actor || pair.Target is not { } target ||
                !Enum.IsDefined(actor.Kind) || !Enum.IsDefined(target.Kind) ||
                actor.ForceTableIndex < 0 || target.ForceTableIndex < 0)
                throw new InvalidDataException("Saved combat engagement is invalid.");
    }
}
