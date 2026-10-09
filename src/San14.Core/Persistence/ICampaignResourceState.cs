using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Immutable compatibility knowledge owned by the resource adapter.
/// Simulation snapshots may share it; ordinary game rules do not inspect it.</summary>
public interface ICampaignResourceState { }

/// <summary>Serialization boundary for resource knowledge which Core does not interpret.</summary>
public interface ICampaignResourceStateCodec
{
    JsonObject Encode(ICampaignResourceState state);
    ICampaignResourceState Decode(JsonObject document);
    ICampaignResourceState? MigrateLocations(ICampaignResourceState? state,
        IReadOnlyList<LegacyOfficerLocationKnowledge> locations);
    ICampaignResourceState? MigrateAftermath(ICampaignResourceState? state,
        IReadOnlyList<LegacyOfficerAftermathKnowledge> aftermath);
    ICampaignResourceState? MigrateArmyTargets(ICampaignResourceState? state,
        IReadOnlyList<LegacyArmyTargetKnowledge> targets);
    ICampaignResourceState? MigrateArmyOverrides(ICampaignResourceState? state,
        IReadOnlyList<LegacyArmyOverrideKnowledge> targets);
}

/// <summary>Only the historical save migration exposes these legacy source codes.</summary>
public sealed record LegacyOfficerLocationKnowledge(int OfficerId, int? SecondLocationCode, int? DistrictCode);

/// <summary>Historical encodings passed once to the resource compatibility boundary.</summary>
public sealed record LegacyOfficerAftermathKnowledge(int OfficerId, int? UnmappedResentmentTarget,
    int? PreviousForceCode, int? CaptureResetCode);
public sealed record LegacyArmyTargetKnowledge(ArmyReceiverId ReceiverId, int? KindCode, int? IdentityCode);
public sealed record LegacyArmyOverrideKnowledge(ArmyReceiverId? ReceiverId, int? UnitId, int? KindCode, int? IdentityCode);

public sealed partial record GameState
{
    [JsonIgnore]
    public ICampaignResourceState? ResourceState { get; init; }
}
