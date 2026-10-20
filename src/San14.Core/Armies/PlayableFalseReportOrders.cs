namespace San14.Core;

public sealed record IssueArmyFalseReportOrder(int ForceId, int UnitId, int TargetUnitId, int DestinationHexId) : GameOrder(ForceId);
public interface IPlayableArmyFalseReportCommandRules
{
    CommandResult IssueFalseReport(GameState state, UnitState actor, UnitState target, int destinationHexId);
}
internal sealed class PlayableFalseReportOrders(IOrderRules next, IPlayableArmyFalseReportCommandRules? source) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not IssueArmyFalseReportOrder request) return next.Apply(state, force, order, context);
        if (!PlayableUnitControl.TryResolve(state, force, request.UnitId, context, out var actor, out var denied)) return denied!;
        if (!state.Units.TryGetValue(request.TargetUnitId, out var target) || request.DestinationHexId < 0 ||
            request.DestinationHexId >= (long)state.MapWidth * state.MapHeight)
            return CommandResult.Reject("The false-report target or destination does not exist.", "army.false_report_target");
        return source?.IssueFalseReport(state, actor!, target, request.DestinationHexId) ??
            CommandResult.RuleUnavailable("rules.army_false_report", "Current false-report command rules are unavailable.");
    }
}
