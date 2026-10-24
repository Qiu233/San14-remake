namespace San14.Core;

/// <summary>Submit Fire for an existing ordinary army. Target and fresh
/// intermediate points identify physical Hex receivers. ClickedHexId retains
/// the original press before Object footprint normalization.</summary>
public sealed record SetUnitFireOrder(int ForceId, int UnitId, int ClickedHexId, int TargetHexId,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);

/// <summary>Submit the common target and fresh route in selected page-row order.</summary>
public sealed record SetUnitsFireOrder(int ForceId, IReadOnlyList<int> UnitIds, int ClickedHexId, int TargetHexId,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);

/// <summary>Existing-army Fire packet submission. Target admission reads the
/// current source predicates; ignition and sampling belong to day-stage execution.</summary>
public interface IPlayableArmyFireCommandRules
{
    CommandResult IssueArmiesFire(GameState state, int actorForceId, IReadOnlyList<UnitState> armies,
        int clickedHexId, int targetHexId, IReadOnlyList<int> waypointHexIds);
}
