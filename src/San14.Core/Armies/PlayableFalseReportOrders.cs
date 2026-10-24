namespace San14.Core;

public sealed record IssueArmyFalseReportOrder(int ForceId, int UnitId, int TargetUnitId, int DestinationHexId) : GameOrder(ForceId);
/// <summary>One selected-row-order submission with one shared destination and fresh five-point route.</summary>
public sealed record IssueArmiesFalseReportOrder(int ForceId, IReadOnlyList<int> UnitIds, int TargetUnitId,
    int DestinationHexId, IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public interface IPlayableArmyFalseReportCommandRules
{
    CommandResult IssueFalseReport(GameState state, UnitState actor, UnitState target, int destinationHexId);
    CommandResult IssueFalseReports(GameState state, IReadOnlyList<UnitState> actors, UnitState target,
        int destinationHexId, IReadOnlyList<int> waypointHexIds);
}
internal sealed class PlayableFalseReportOrders(IOrderRules next, IPlayableArmyFalseReportCommandRules? source) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        IReadOnlyList<int> ids; IReadOnlyList<int> waypoints; int targetId, destination;
        if (order is IssueArmyFalseReportOrder one)
        { ids = [one.UnitId]; waypoints = []; targetId = one.TargetUnitId; destination = one.DestinationHexId; }
        else if (order is IssueArmiesFalseReportOrder many)
        { ids = many.UnitIds; waypoints = many.WaypointHexIds; targetId = many.TargetUnitId; destination = many.DestinationHexId; }
        else return next.Apply(state, force, order, context);
        long size = (long)state.MapWidth * state.MapHeight;
        if (ids is null || ids.Count is < 1 or > 30 || ids.Distinct().Count() != ids.Count ||
            waypoints is null || waypoints.Count > PlayableWaypointMarch.MaximumWaypoints ||
            waypoints.Any(hex => hex < 0 || hex >= size) || destination < 0 || destination >= size ||
            ids.Contains(targetId) || !state.Units.TryGetValue(targetId, out var target))
            return CommandResult.Reject("Select distinct ordinary armies, a target and up to five intermediate points.", "army.false_report_selection");
        var actors = new List<UnitState>(ids.Count);
        foreach (int id in ids)
        {
            if (!PlayableUnitControl.TryResolve(state, force, id, context, out var actor, out var denied)) return denied!;
            if (actor!.NativeArmyOrigin is null || actor.NativeTacticRuntime?.Status10 != 1 ||
                actor.Mission is null or { Kind: UnitMissionKind.Transport })
                return CommandResult.Reject("Only current ordinary armies may receive false-report orders.", "army.false_report_actor");
            actors.Add(actor);
        }
        //No provider invocation occurs until all actors have passed the common ownership boundary.
        //The provider owns one writable current and returns one atomic result for the whole selection.
        return source?.IssueFalseReports(state, actors, target, destination, waypoints) ??
            CommandResult.RuleUnavailable("rules.army_false_report", "Current false-report command rules are unavailable.");
    }
}
