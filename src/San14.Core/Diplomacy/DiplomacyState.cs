using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Ordered as the six diplomatic relationship levels in the CE manual.</summary>
public enum DiplomaticRelation { Hostile, Treacherous, Discordant, Neutral, Friendly, Close }
public enum DiplomaticTreatyKind { Alliance, Truce }
public enum DiplomaticMissionKind { Goodwill, Alliance, CancelAlliance, RequestAttack, ReturnCaptives, DemandSurrender, Exchange }

/// <summary>Independent bilateral state. SourceIndexedBytes is deliberately not used.</summary>
public sealed record DiplomaticRelationship(int FirstForceId, int SecondForceId,
    DiplomaticRelation Level, string Origin);
public sealed record DiplomaticTreaty(int FirstForceId, int SecondForceId,
    DiplomaticTreatyKind Kind, int StartedPeriod, int ExpiresPeriod, string Origin);
public sealed record DiplomaticAttackRequest(int Id, int RequestingForceId,
    int RequestedForceId, int TargetCityId, int TargetForceId, int AcceptedPeriod,
    int ExpiresPeriod, string Origin);

/// <summary>Quoted rules remain in the snapshot so pending missions are never repriced.</summary>
public sealed record DiplomaticMission(int Id, int ForceId, int SourceCityId,
    int DistrictId, int ExecutorOfficerId, int TargetForceId, DiplomaticMissionKind Kind,
    int SubmittedPeriod, int ArrivalPeriod, int TreatyPeriods, int RequestPeriods,
    int? TargetCityId, int? TargetCityForceId, int PaidGold, int PaidCommandBooks,
    string PolicyId);

public sealed record DiplomacyState(string Origin,
    Dictionary<long, DiplomaticRelationship> Relationships,
    Dictionary<long, DiplomaticTreaty> Treaties,
    Dictionary<int, DiplomaticMission> Missions,
    Dictionary<int, DiplomaticAttackRequest> AttackRequests,
    int NextMissionId);

public sealed partial record GameState
{
    // Null means unresolved original/legacy diplomacy, rather than verified no treaties.
    public DiplomacyState? Diplomacy { get; init; }
}

public sealed record PrepareDiplomaticOrder(int ForceId, int SourceCityId,
    int ExecutorOfficerId, int TargetForceId, DiplomaticMissionKind Kind,
    int AllianceMonths = 12, int? TargetCityId = null) : GameOrder(ForceId)
{
    public ImmutableArray<int> RequestedCaptiveOfficerIds { get; init; } = [];
    public DiplomacyOffer RequestedOffer { get; init; } = new(0, 0);
    public int GiftGold { get; init; }
    public int GiftSupplies { get; init; }
    public int? GiftItemId { get; init; }
    public int? GiftAreaId { get; init; }
}

public readonly record struct DiplomacyQuote(int CommandBooks, int Gold,
    int TravelPeriods, int TreatyPeriods, int RequestPeriods);
public readonly record struct DiplomacyResolution(bool Accepted, int RelationIncrease,
    ulong RandomState);

/// <summary>Replaceable cost, timing and result rules. The command caller cannot supply them.</summary>
public interface IDiplomacyPolicy
{
    string Id { get; }
    DiplomacyQuote? Quote(GameState state, PrepareDiplomaticOrder order);
    DiplomacyResolution Resolve(GameState state, DiplomaticMission mission);
}

/// <summary>
/// Deterministic first-playable adapter. Costs, travel and success are provisional;
/// only relationship ordering and command relationship thresholds come from the manual.
/// </summary>
public sealed class ProvisionalDiplomacyPolicy : IDiplomacyPolicy
{
    public string Id => "san14:provisional-diplomacy-v1";
    public DiplomacyQuote? Quote(GameState state, PrepareDiplomaticOrder order)
    {
        if (order.Kind is DiplomaticMissionKind.ReturnCaptives or DiplomaticMissionKind.DemandSurrender or DiplomaticMissionKind.Exchange)
            return null;
        if (order.Kind == DiplomaticMissionKind.Alliance &&
            (order.AllianceMonths <= 0 || order.AllianceMonths > int.MaxValue / 3))
            return null;
        return new DiplomacyQuote(1, 0, 1,
            order.Kind == DiplomaticMissionKind.Alliance ? checked(order.AllianceMonths * 3) : 0,
            order.Kind == DiplomaticMissionKind.RequestAttack ? 3 : 0);
    }
    public DiplomacyResolution Resolve(GameState state, DiplomaticMission mission) =>
        new(true, mission.Kind == DiplomaticMissionKind.Goodwill ? 1 : 0, state.RandomState);
}
