namespace San14.Core;

public sealed partial class GameSimulation
{
    private CommandResult ConfirmManualTacticDecision(ManualTacticProgressOperation pending, GameOrder order,
        IOrderRules rules, CommandContext context, IOrderAuthority authority)
    {
        if (pending.PendingRulerSuccession is { } succession)
        {
            if (order is not ConfirmRulerSuccessionOrder selected || selected.ForceId != succession.ForceId ||
                !succession.CandidateOfficerIds.Contains(selected.OfficerId))
                return CommandResult.Reject("The campaign is awaiting its ruler successor.", "command.pending_succession");
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
        var result = GameEngine.ApplyCandidate(pending.Candidate, order, rules, context, authority);
        if (result.Accepted)
        {
            var candidate = result.State!;
            bool waiting = candidate.ArmyDefeatContinuation?.PendingCapturedRulerTreatment is not null ||
                candidate.BaseCaptureContinuation?.PendingCapturedRulerTreatment is not null ||
                candidate.PersonDeathContinuation?.PendingSuccession is not null;
            _progress = new(null, pending with { Candidate = candidate, Presentation = null, ContinueAfterPresentation = !waiting });
        }
        return result;
    }
}
