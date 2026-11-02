namespace San14.Core;

public sealed partial class GameSimulation
{
    private CommandResult ConfirmManualTacticDecision(ManualTacticProgressOperation pending, GameOrder order,
        IOrderRules rules, CommandContext context, IOrderAuthority authority, CampaignAppRandomState? appRandomSnapshot)
    {
        if (pending.PendingRulerSuccession is { } succession)
        {
            if (order is not ConfirmRulerSuccessionOrder selected || selected.ForceId != succession.ForceId ||
                !succession.CandidateOfficerIds.Contains(selected.OfficerId))
                return CommandResult.Reject("The campaign is awaiting its ruler successor.", "command.pending_succession");
        }
        else if (pending.PendingArmyReturnCaptives is not null)
        {
            if (order is not (ConfirmArmyReturnCaptiveBatchOrder or ConfirmArmyReturnCaptiveExecutionOrder or
                CompleteArmyReturnCaptivePresentationOrder))
                return CommandResult.Reject("The manual tactic is awaiting captive treatment.", "command.pending_captive_batch");
        }
        else if (pending.PendingCapturedRulerTreatment is { } treatment)
        {
            if (order is not ConfirmCapturedRulerTreatmentOrder selected || selected.ForceId != treatment.CapturingForceId ||
                selected.OfficerId != treatment.OfficerId)
                return CommandResult.Reject("The campaign is awaiting its captive ruler decision.", "command.pending_ruler_treatment");
        }
        else return CommandResult.Reject("The manual tactic is ready to continue.", "command.manual_tactic_already_decided");

        //This private candidate owns the original execution and App stream.
        //The visible committed world is never used to re-run its prefix.
        var result = GameEngine.ApplyCandidate(appRandomSnapshot is null ? pending.Candidate :
            pending.Candidate with { CampaignAppRandom32 = appRandomSnapshot }, order, rules,
            context with { ArmyProgressMoment = pending.Context.Moment }, authority);
        if (result.Accepted)
        {
            var candidate = result.State!;
            var reached = pending with { Candidate = candidate, Presentation = null };
            bool waiting = reached.PendingCapturedRulerTreatment is not null ||
                reached.PendingRulerSuccession is not null || reached.IsAwaitingArmyReturnCaptiveReply;
            _progress = new(null, reached with { ContinueAfterPresentation = !waiting });
        }
        return result;
    }
}
