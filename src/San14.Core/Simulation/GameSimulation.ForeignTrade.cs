namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>The source report's exact candidate receives its real App stream
    /// and completion command. The visible committed world is not substituted.</summary>
    public CommandResult CompleteForeignTradeReport(GameState expectedOwner, CompleteForeignTradeReportOrder order,
        IOrderRules rules, CommandContext context, IOrderAuthority authority, CampaignAppRandomState? appRandomSnapshot = null)
    {
        var owner = _normalPeriod?.Candidate ?? State;
        if (!ReferenceEquals(owner, expectedOwner) || owner.PersonnelCommands?.Caravans?.GetValueOrDefault(order.TaskId)
            is not { PendingReport: not null })
            return CommandResult.Reject("The overseas caravan report owner has changed.", "foreign-trade.stale_report");
        return Apply(order, rules, context, authority, appRandomSnapshot);
    }
}
