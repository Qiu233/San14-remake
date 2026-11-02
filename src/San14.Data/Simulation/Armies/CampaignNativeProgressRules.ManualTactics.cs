using San14.Core;
namespace San14.Data;

public sealed partial class CampaignNativeProgressRules
{
    private sealed class UnknownManualPresentation : IManualTacticPresentationAdmission
    {
        public static readonly UnknownManualPresentation Instance = new();
        public bool? ApplicationPresent => null;
        public int? ApplicationUserIndex => null;
        public bool? ReadUniqueDirectionAdmission(GameState state, UnitState actor, int tacticId) => null;
    }
    private sealed record ManualStageResult(GameState State, ManualTacticStagePhase Phase, TacticPresentationRequest? Presentation);
    private ManualStageResult AdvanceManualStage(GameState input, NativeArmyProgressStageMoment moment,
        ManualTacticStagePhase phase, long presentationFloor)
    {
        var owner = input.ManualTacticIntervention ?? throw Missing("manual_tactic_manager");
        GameState state = input;
        if (state.ManualTacticExecutionContinuation is { } execution)
        {
            var resumed = campaign.TryExecuteManualTacticIntervention(state, moment, execution.Request,
                manualPresentation ?? UnknownManualPresentation.Instance);
            state = resumed.State ?? throw Missing(resumed.UnresolvedOwner ?? "manual_execution_resume");
            if (state.ManualTacticExecutionContinuation is not null) return new(state, phase, null);
            owner = state.ManualTacticIntervention ?? throw Missing("manual_resume_manager");
            phase = ManualTacticStagePhase.AfterIntervention;
        }
        if (phase == ManualTacticStagePhase.EnterStage)
        {
            if (owner.EffectBusy)
            {
                if (owner.Effects.Length != 0) return new(state, phase, owner.Effects[0]);
                // The original call clears busy and returns. It does not also
                // consume a still-pending intervention on this same visit.
                owner = owner with { EffectBusy = false };
                state = state with { ManualTacticIntervention = owner };
            }
            else if (owner.Pending is { } request)
            {
                owner = owner with { LastPresentationSequence = Math.Max(owner.LastPresentationSequence, presentationFloor) };
                state = state with { ManualTacticIntervention = owner };
                var result = campaign.TryExecuteManualTacticIntervention(state, moment, request,
                    manualPresentation ?? UnknownManualPresentation.Instance);
                state = result.State ?? throw Missing(result.UnresolvedOwner ?? "manual_tactic_execution");
                owner = state.ManualTacticIntervention ?? throw Missing("manual_tactic_execution_owner");
            }
            if (state.ManualTacticExecutionContinuation is not null) return new(state, phase, null);
            phase = ManualTacticStagePhase.AfterIntervention;
        }
        if (owner.EffectBusy)
        {
            if (owner.Effects.Length != 0) return new(state, phase, owner.Effects[0]);
            // A receipt returns to the original case6 manager call. Its busy
            // branch observes the now-empty effect list, clears busy and
            // returns without invoking the commanded numerical consumer again.
            owner = owner with { EffectBusy = false };
            state = state with { ManualTacticIntervention = owner };
        }
        return new(state, phase, owner.Directions.Length != 0 ? owner.Directions[0] : null);
    }

    public CampaignProgressTransition ResumeManualTactic(ManualTacticProgressOperation pending, TacticPresentationReceipt receipt)
    {
        //Ordinary effects retire through their exact manager-head lifecycle.
        //That actual retirement changes the pending operation to retryable;
        //a legacy receipt must not remove a newly created or still-live node.
        if (receipt.Kind == TacticPresentationKind.OrdinaryEffects)
            throw Missing("ordinary_tactic_actual_retirement_required");
        if (pending.ContinueAfterPresentation || pending.Revision != Revision || pending.Presentation is not { } shown ||
            shown.Sequence != receipt.Sequence || shown.Kind != receipt.Kind)
            throw Missing("manual_tactic_receipt");
        var owner = pending.Candidate.ManualTacticIntervention ?? throw Missing("manual_tactic_receipt_owner");
        var accepted = pending with { Candidate = pending.Candidate with { ManualTacticIntervention = owner.Acknowledge(receipt) },
            Presentation = null, ContinueAfterPresentation = true };
        return ContinueManualTactic(accepted);
    }
    public CampaignProgressTransition RetryManualTactic(ManualTacticProgressOperation pending)
    {
        if ((!pending.ContinueAfterPresentation && !pending.RequiresAutomaticManualTacticContinuation) ||
            pending.Revision != Revision) throw Missing("manual_tactic_retry_boundary");
        return ContinueManualTactic(pending);
    }
    private CampaignProgressTransition ContinueManualTactic(ManualTacticProgressOperation pending)
    {
        var accepted = pending;
        try
        {
            stories.BeginOperation(accepted.ServiceChanges);
            var stage = AdvanceManualStage(accepted.Candidate, accepted.Context.Moment, accepted.StagePhase, accepted.LastStorySequence);
            accepted = accepted with { Candidate = stage.State, StagePhase = stage.Phase,
                LastStorySequence = Math.Max(accepted.LastStorySequence, stage.State.ManualTacticIntervention!.LastPresentationSequence) };
            if (stage.State.ManualTacticExecutionContinuation is not null)
                return AwaitingManual(accepted with { Presentation = null, ContinueAfterPresentation = false });
            if (stage.Presentation is not null)
                return AwaitingManual(accepted with { Presentation = new(stage.Presentation.Sequence, stage.Presentation.Kind), ContinueAfterPresentation = false });
            return Drive(accepted.Candidate, accepted.Request, accepted.BlockIndex, accepted.Calendar,
                accepted.ServiceChanges, accepted.LastStorySequence, null, accepted);
        }
        catch (InvalidDataException error)
        {
            return new(CampaignProgressOutcome.Unresolved, null, null, error.Message)
            { ManualTactic = accepted with { ServiceChanges = stories.PreserveClockReads(accepted.ServiceChanges) } };
        }
    }
    private static CampaignProgressTransition AwaitingManual(ManualTacticProgressOperation pending) =>
        new(CampaignProgressOutcome.AwaitingManualTactic, null, null, null) { ManualTactic = pending };
}
