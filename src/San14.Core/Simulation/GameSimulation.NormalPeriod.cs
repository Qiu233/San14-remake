namespace San14.Core;

public sealed partial class GameSimulation
{
    private NormalPeriodOperation? _normalPeriod;
    /// <summary>Borrowed read-only candidate for the actual player decision UI.</summary>
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
        if (pending.Candidate.NativeNormalTurnContinuation is { } turn)
        {
            if (turn.PendingCoalitionInvitation is { } invitation)
            {
                if (order is not ConfirmCoalitionInvitationOrder coalition || coalition.ForceId != invitation.PlayerForceId ||
                    coalition.TargetForceId != invitation.TargetForceId)
                    return CommandResult.Reject("The campaign is awaiting this coalition invitation.", "command.pending_coalition_invitation");
                var answered = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
                if (answered.Accepted) _normalPeriod = new(answered.State!);
                return answered;
            }
            if (turn.PendingSelfRecommendation is not { } offer ||
                order is not ConfirmSelfRecommendationOrder recommendation ||
                recommendation.OfficerId != offer.OfficerId || recommendation.ForceId != offer.ForceId)
                return CommandResult.Reject("The campaign is awaiting this officer recommendation.", "command.pending_recommendation");
            var decided = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
            if (decided.Accepted) _normalPeriod = new(decided.State!);
            return decided;
        }
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
        if (pending.Candidate.NativeNormalTurnContinuation is { } turn)
        {
            if (turn.PendingSelfRecommendation is not null || turn.PendingCoalitionInvitation is not null)
                throw new InvalidOperationException("The normal Turn requires a player decision.");
            if (pending.PendingStory is not null)
                throw new InvalidOperationException("The normal Turn story still requires its actual presentation receipt.");
            return;
        }
        var cursor = pending.Candidate.NativePeriodContinuation?.Cursor
            ?? throw new InvalidOperationException("The normal period has no continuation cursor.");
        if (pending.Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(cursor.AwaitingDiplomacyTaskId)
            is { AwaitingNegotiation: true, NativeKind: not 6 })
            throw new InvalidOperationException("The diplomatic negotiation requires a player decision.");
    }
}
