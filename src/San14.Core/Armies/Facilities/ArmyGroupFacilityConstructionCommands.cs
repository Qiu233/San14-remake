namespace San14.Core;

/// <summary>One selected deployed army and its own ordered intermediate destinations.</summary>
public sealed record ArmyFacilityConstructionSelection(int UnitId,
    IReadOnlyList<int> WaypointHexIds);

/// <summary>Replace construction orders for an ordered selection in one transaction.
/// The optional final site belongs to continuous obstacle construction.</summary>
public sealed record IssueArmiesFacilityConstructionOrder(int ForceId,
    IReadOnlyList<ArmyFacilityConstructionSelection> Armies,
    int InitialHexId, int? FinalHexId, string DefinitionKey) : GameOrder(ForceId);

/// <summary>Qualify the selected people together, then submit all army rows in
/// order. Rejection publishes no prefix; arrival retains its own qualification
/// and payment rules.</summary>
public interface IPlayableArmyGroupConstructionCommandRules
{
    CommandResult IssueArmyGroupFacilityConstruction(GameState state, ForceState force,
        IssueArmiesFacilityConstructionOrder order, CommandContext context);
}
