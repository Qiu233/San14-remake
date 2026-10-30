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
        if (pending.PendingRulerSuccession is { } succession)
        {
            if (order is not ConfirmRulerSuccessionOrder selected || selected.ForceId != succession.ForceId ||
                !succession.CandidateOfficerIds.Contains(selected.OfficerId))
                return CommandResult.Reject("The campaign is awaiting its ruler successor.", "command.pending_succession");
            var rulerResult = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
            if (rulerResult.Accepted) _normalPeriod = new(rulerResult.State!);
            return rulerResult;
        }
        if (pending.PendingCapturedRulerTreatment is { } treatment)
        {
            if (order is not ConfirmCapturedRulerTreatmentOrder selected || selected.ForceId != treatment.CapturingForceId ||
                selected.OfficerId != treatment.OfficerId)
                return CommandResult.Reject("The campaign is awaiting its captive ruler decision.", "command.pending_ruler_treatment");
            var capturedResult = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
            if (capturedResult.Accepted) _normalPeriod = new(capturedResult.State!);
            return capturedResult;
        }
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
        if (cursor is { StageIndex: 0, AwaitingIntelligenceTaskId: not null })
            return CommandResult.Reject("The campaign is awaiting its actual intelligence narration or continuation.",
                "command.pending_intelligence_narration");
        if (cursor is { StageIndex: 0, AwaitingCaravanTaskId: int caravanId })
        {
            if (order is not CompleteForeignTradeReportOrder report || report.TaskId != caravanId ||
                pending.Candidate.PersonnelCommands?.Caravans?.GetValueOrDefault(caravanId) is not { PendingReport: not null })
                return CommandResult.Reject("The campaign is awaiting this overseas caravan report.", "command.pending_caravan_report");
            var source = appRandomSnapshot is null ? pending.Candidate : pending.Candidate with { CampaignAppRandom32 = appRandomSnapshot };
            var completed = GameEngine.ApplyCandidate(source, order, rules, context, authority);
            if (completed.Accepted) _normalPeriod = new(completed.State!);
            return completed;
        }
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
        if (pending.Candidate.AutomaticEmperorCapture?.PendingDecision is not null || AutomaticEmperorCaptureLifecycle.IsPresentationPending(pending.Candidate))
            throw new InvalidOperationException("The emperor capture requires its actual choice or presentation receipts.");
        if (pending.PendingRulerSuccession is not null || pending.PendingCapturedRulerTreatment is not null)
            throw new InvalidOperationException("The ruler decision requires a player response.");
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
        if (cursor is { StageIndex: 0, AwaitingIntelligenceTaskId: not null })
        {
            if (!pending.RequiresAutomaticIntelligenceContinuation)
                throw new InvalidOperationException("The intelligence narration requires its actual completion.");
            return;
        }
        if (cursor is { StageIndex: 0, AwaitingStrategicTaskId: not null }) return;
        if (cursor is { StageIndex: 0, AwaitingCaravanTaskId: not null })
        {
            if (!pending.RequiresAutomaticCaravanContinuation)
                throw new InvalidOperationException("The overseas caravan report requires its actual completion.");
            return;
        }
        if (cursor.StageIndex == 6)
        {
            if (!pending.RequiresAutomaticTacticPresentationContinuation && !pending.RequiresAutomaticManualTacticContinuation)
                throw new InvalidOperationException("Ordinary tactic effects still require their actual presentation lifecycle.");
            return;
        }
        if (pending.Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(cursor.AwaitingDiplomacyTaskId)
            is { AwaitingNegotiation: true, NativeKind: not 6 })
            throw new InvalidOperationException("The diplomatic negotiation requires a player decision.");
    }
}
