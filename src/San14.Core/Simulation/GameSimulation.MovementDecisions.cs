namespace San14.Core;

public sealed partial class GameSimulation
{
    private CommandResult ConfirmArmyProgressDecision(CampaignArmyProgressOperation pending, GameOrder order,
        IOrderRules rules, CommandContext context, IOrderAuthority authority,
        CampaignAppRandomState? appRandomSnapshot)
    {
        if (pending.Candidate.PersonDeathContinuation?.PendingSuccession is { } succession)
        {
            if (order is not ConfirmRulerSuccessionOrder selected || selected.ForceId != succession.ForceId ||
                !succession.CandidateOfficerIds.Contains(selected.OfficerId))
                return CommandResult.Reject("Movement is awaiting the actual ruler successor.", "command.pending_succession");
        }
        else if ((pending.Candidate.ArmyDefeatContinuation?.PendingCapturedRulerTreatment ??
            pending.Candidate.BaseCaptureContinuation?.PendingCapturedRulerTreatment) is { } treatment)
        {
            if (order is not ConfirmCapturedRulerTreatmentOrder selected || selected.ForceId != treatment.CapturingForceId ||
                selected.OfficerId != treatment.OfficerId)
                return CommandResult.Reject("The Army operation is awaiting its captive ruler decision.", "command.pending_ruler_treatment");
        }
        else if (pending.Candidate.ArmyReturnCaptiveContinuation is null || order is not
            (ConfirmArmyReturnCaptiveBatchOrder or ConfirmArmyReturnCaptiveExecutionOrder or
                CompleteArmyReturnCaptivePresentationOrder))
            return CommandResult.Reject("Movement is awaiting its captive return reply.", "command.pending_movement_reply");

        // The regular command boundary checks the exact batch, force, round,
        // selected people and consumed presentation. The reply never runs an
        // earlier movement actor or changes the committed session world.
        var result = GameEngine.ApplyCandidate(appRandomSnapshot is null ? pending.Candidate :
            pending.Candidate with { CampaignAppRandom32 = appRandomSnapshot }, order, rules,
            context with { ArmyProgressMoment = pending.Context.Moment }, authority);
        if (result.Accepted) _progress = new(null, null, pending with { Candidate = result.State! });
        return result;
    }
}
