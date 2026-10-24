namespace San14.Core;

public sealed partial class GameSimulation
{
    private NormalPeriodOperation? _normalPeriod;
    /// <summary>Borrowed read-only candidate for the actual negotiation UI.</summary>
    public NormalPeriodOperation? PendingNormalPeriod => _normalPeriod;

    public void RestoreNormalPeriod(NormalPeriodOperation? pending)
    {
        RequireNoProgress();
        pending?.Validate(State);
        _normalPeriod = pending?.Copy();
    }

    private CommandResult ConfirmNormalPeriod(GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority, CampaignAppRandomState? appRandomSnapshot)
    {
        var pending = _normalPeriod!;
        var cursor = pending.Candidate.NativePeriodContinuation?.Cursor
            ?? throw new InvalidOperationException("The normal period has no waiting cursor.");
        if (order is not ConfirmDiplomaticNegotiationOrder confirm || confirm.TaskId != cursor.AwaitingDiplomacyTaskId)
            return CommandResult.Reject("The campaign is awaiting this diplomatic negotiation.", "command.pending_negotiation");
        if (pending.RequiresAutomaticDiplomacyContinuation)
            return CommandResult.Reject("The issued alliance break continues without a negotiation decision.", "command.unilateral_diplomacy");
        if (pending.Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(confirm.TaskId)
            is not { AwaitingNegotiation: true, NativeKind: not 6 })
            return CommandResult.Reject("This negotiation has already been decided.", "command.negotiation_decided");
        //The waiting candidate, not the still-visible committed world, owns
        //this period's App stream. This decision UI consumes no App random.
        var result = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
        if (result.Accepted) _normalPeriod = new(result.State!);
        return result;
    }

    private static void RequireNormalPeriodConfirmed(NormalPeriodOperation pending)
    {
        var cursor = pending.Candidate.NativePeriodContinuation?.Cursor
            ?? throw new InvalidOperationException("The normal period has no continuation cursor.");
        if (pending.Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(cursor.AwaitingDiplomacyTaskId)
            is { AwaitingNegotiation: true, NativeKind: not 6 })
            throw new InvalidOperationException("The diplomatic negotiation requires a player decision.");
    }
}
