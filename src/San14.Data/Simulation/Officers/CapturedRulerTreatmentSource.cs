using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, CapturedRulerTreatmentRequest Request) PrepareCapturedRulerTreatmentCurrent(
        GameState state, SourceWorldContext current, RulerCaptureDecisionReceipt receipt)
    {
        bool forbidden = OfficerBehaviorRules.MustReleaseAfterAiCapture(current.ReadPersonBehavior(receipt.PersonPhysical))
            ?? throw RulerTreatmentMissing("Person15E_execution_gate");
        var text = FormatNormalTurnReport(state, current, 0x2C50, new());
        return (text.State, new(receipt.PersonId, receipt.WinnerForcePhysical, receipt.WinnerRulerId,
            !forbidden, text.Text));
    }

    internal CommandResult ConfirmCapturedRulerTreatment(GameState state,
        ConfirmCapturedRulerTreatmentOrder order, CommandContext context)
    {
        var continuation = state.ArmyDefeatContinuation;
        var occupation = state.BaseCaptureContinuation;
        var request = continuation?.PendingCapturedRulerTreatment ?? occupation?.PendingCapturedRulerTreatment;
        if (request is null ||
            request.OfficerId != order.OfficerId || request.CapturingForceId != order.ForceId ||
            !Enum.IsDefined(order.Disposition) || order.Disposition == CapturedRulerDisposition.Execute && !request.CanExecute ||
            state.PersonDeathContinuation is not null)
            return CommandResult.Reject("This captive ruler is not awaiting this decision.", "ruler.treatment_pending");
        try
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw RulerTreatmentMissing("lifecycle");
            if (continuation is not null)
            {
                if (lifecycle.Slots[continuation.ArmyPhysicalSlot].ReceiverId != continuation.ReceiverId)
                    return CommandResult.Reject("This defeated army receiver has changed.", "ruler.treatment_receiver");
            }
            else if (occupation is not null)
            {
                var operation = occupation.Operation;
                var receiver = lifecycle.Receivers.Find(lifecycle.Slots[operation.WinnerArmyPhysicalSlot].ReceiverId);
                if (lifecycle.Slots[operation.WinnerArmyPhysicalSlot].ReceiverId != operation.WinnerReceiverId ||
                    receiver?.ActiveUnitId != operation.Request.WinnerUnitId)
                    return CommandResult.Reject("This occupying army receiver has changed.", "ruler.treatment_receiver");
            }
            else throw RulerTreatmentMissing("decision_operation");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            int player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)?.PhysicalSlot
                ?? throw RulerTreatmentMissing("player_Force");
            if (player != request.CapturingForceId || context.DelegatedDistrictId is not null)
                return CommandResult.Reject("Only the capturing player can decide this ruler's treatment.", "ruler.treatment_authority");
            int person = current.ReadPersonLookupReference(request.OfficerId)?.PhysicalSlot
                ?? throw RulerTreatmentMissing("Person_pointer");
            if (current.ReadPersonNativeValidity(person) != true || current.ReadPersonStatus(person) != 1)
                return CommandResult.Reject("This ruler is no longer awaiting treatment.", "ruler.treatment_person");
            bool forbidden = OfficerBehaviorRules.MustReleaseAfterAiCapture(current.ReadPersonBehavior(person))
                ?? throw RulerTreatmentMissing("Person15E_execution_gate");
            if (order.Disposition == CapturedRulerDisposition.Execute && forbidden)
                return CommandResult.Reject("This ruler cannot be executed.", "ruler.treatment_execute_forbidden");
            int rulerPhysical = current.ReadPersonLookupReference(request.CapturingRulerId)?.PhysicalSlot
                ?? throw RulerTreatmentMissing("capturing_ruler_pointer");
            //An occupying Base can contain a ruler of another Force. The
            //person's actual pre-decision Force owns release/execution effects.
            int priorForce = continuation?.VictimForceId ?? current.ReadPersonForce(person)?.PhysicalSlot
                ?? throw RulerTreatmentMissing("captured_ruler_original_Force");
            var decision = new RulerCaptureDecisionResult(order.Disposition == CapturedRulerDisposition.Execute
                ? RulerCaptureDecision.ExecuteRequired : RulerCaptureDecision.Release,
                new(person, (ushort)request.OfficerId, request.CapturingForceId, priorForce,
                    rulerPhysical, (ushort)request.CapturingRulerId, null, null, null, 0, "player_806D00"), null);
            var date = state.NativePeriodContinuation?.Cursor?.Date;
            if (date is null)
            {
                //The session stamps the actual saved legacy owner moment on
                //this formal reply. Neither a UI-selected date nor the block's
                //unchanged starting season can replace that reached stage.
                var moment = context.ArmyProgressMoment;
                int index = continuation?.SettlementIndex ?? occupation!.SettlementIndex;
                if (moment is not { Boundary: NativeArmyProgressStageBoundary.StageEntry } ||
                    !(moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Combat && index == -1 &&
                        state.NormalCombatImmediateContinuation is not null ||
                      moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.DuelOpportunity && index == -1 &&
                        state.DuelAftermathContinuation is not null ||
                      moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.ManualTacticIntervention && index == -1 &&
                        state.ManualTacticExecutionContinuation is not null ||
                      moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Defeat && index >= 0))
                    throw RulerTreatmentMissing("actual_capture_source_date");
                date = moment.PreWriterThirdDate;
            }
            var effects = new List<CampaignEffectRequest>();
            var working = continuation is not null
                ? state with { ArmyDefeatContinuation = continuation with
                    { PendingCapturedRulerTreatment = null, ChosenDisposition = order.Disposition } }
                : state with { BaseCaptureContinuation = occupation! with
                    { PendingCapturedRulerTreatment = null, ChosenDisposition = order.Disposition } };
            if (order.Disposition == CapturedRulerDisposition.Execute)
                working = ApplyRulerCaptureExecutionCurrent(working, current, world.Pool, decision, date, effects);
            else
            {
                var released = ApplyRulerCaptureReleaseCurrent(working, current, world.Pool, decision, date, effects);
                working = released.State ?? throw RulerTreatmentMissing(released.UnresolvedOwner ?? "release");
            }
            working = OwnedArmyDestructionStage.PublishRulerTreatmentCurrent(working, current, world.Pool);
            if (effects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw RulerTreatmentMissing("effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeClock?.UpdateOrdinal ?? throw RulerTreatmentMissing("source_revision"), effects) };
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static InvalidDataException RulerTreatmentMissing(string owner) => new("ruler.treatment." + owner);
}
