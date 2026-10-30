using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private PersonnelIntelligenceAdvanceResult BeginIntelligenceAwayNarrationCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date)
    {
        var outcome = CaptureIntelligenceAwayOutcomeCurrent(state, current, task, actor);
        var own = new ForceReference(outcome.OwnForcePhysicalSlot);
        var target = new ForceReference(outcome.TargetForcePhysicalSlot);
        if (TalentForceIsPlayerCurrent(state, current, own))
        {
            int actorId = IntelligenceNeed(current.ReadPersonIdentity(actor), "narration_actor_identity");
            int ownRuler = IntelligenceNeed(current.ReadPersonIdentity(outcome.OwnRulerPhysicalSlot), "narration_own_ruler_identity");
            int targetRuler = IntelligenceNeed(current.ReadPersonIdentity(outcome.TargetRulerPhysicalSlot), "narration_target_ruler_identity");
            int root = outcome.Succeeded ? 0x3439 : outcome.Injured ? 0x3443 : 0x343E;
            var main = FormatIntelligenceNarrationCurrent(state, current, root,
                new(Person0: actorId, Person1: ownRuler, Person2: targetRuler));
            state = main.State;
            var effects = new List<CampaignEffectRequest>();
            if (outcome.Succeeded)
            {
                var extra = FormatIntelligenceNarrationCurrent(state, current, 0x3448,
                    new(Person0: actorId, Person1: targetRuler, Integers: [outcome.Gain]));
                state = WriteIntelligenceNarrationHistoryCurrent(extra.State, current, actorId, main.Text, extra.Text,
                    outcome.ActorDistrictPhysicalSlot, ReadIntelligenceJourneyHexCurrent(extra.State, current, task, date), true, true, effects);
            }
            if (IntelligenceNarrationAllowedCurrent(state, current))
            {
                effects.Add(CreateSingleOfficerPresentation(current, state, actorId, main,
                    TalentCommandPresentationOperation.ConditionalOfficerText, "intelligence.narration"));
                return new(InstallIntelligenceNarrationCurrent(state, current, task, actor, date,
                    outcome.Succeeded ? PersonnelIntelligenceNarrationBranch.AwaySuccess : PersonnelIntelligenceNarrationBranch.AwayFailure,
                    main.Text, outcome, effects), false);
            }
            state = AppendIntelligenceEffectsCurrent(state, effects);
            if (!outcome.Succeeded)
                state = CompleteIntelligenceFailureHistoryCurrent(state, current, task, actor, date, outcome, main.Text);
        }
        else if (outcome.Succeeded && TalentForceIsPlayerCurrent(state, current, target))
        {
            int actorId = IntelligenceNeed(current.ReadPersonIdentity(actor), "target_notice_actor_identity");
            int ownRuler = IntelligenceNeed(current.ReadPersonIdentity(outcome.OwnRulerPhysicalSlot), "target_notice_own_ruler_identity");
            int targetRuler = IntelligenceNeed(current.ReadPersonIdentity(outcome.TargetRulerPhysicalSlot), "target_notice_ruler_identity");
            var main = FormatIntelligenceNarrationCurrent(state, current, 0x344B,
                new(Person0: 5012, Person1: targetRuler, Person2: ownRuler));
            var extra = FormatIntelligenceNarrationCurrent(main.State, current, 0x344C,
                new(Person0: targetRuler, Person1: actorId, Integers: [outcome.Gain]));
            var district = ForcePrimaryDistrictSource.Resolve(current, target);
            var effects = new List<CampaignEffectRequest>();
            state = WriteIntelligenceNarrationHistoryCurrent(extra.State, current, 5012, main.Text, extra.Text,
                IntelligenceNeed(district.PhysicalSlot, "target_notice_District"),
                ReadIntelligenceJourneyHexCurrent(extra.State, current, task, date), false, true, effects);
            //The real ordinary cue precedes Serif, including when its own
            //entry is suppressed. It never stands in for the Serif receipt.
            effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.SoundCue, 6, null, null, null, null) });
            if (IntelligenceNarrationAllowedCurrent(state, current))
            {
                effects.Add(CreateSingleOfficerPresentation(current, state, 5012, main,
                    TalentCommandPresentationOperation.OfficerText, "intelligence.target_serif"));
                return new(InstallIntelligenceNarrationCurrent(state, current, task, actor, date,
                    PersonnelIntelligenceNarrationBranch.AwayTargetNotice, main.Text, outcome, effects), false);
            }
            state = AppendIntelligenceEffectsCurrent(state, effects);
        }
        return FinishIntelligenceAwayCurrent(state, current, task, actor, date, outcome);
    }

    private PersonnelIntelligenceAdvanceResult BeginIntelligenceInspectNarrationCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date)
    {
        //1DDA90's transient80 has reached1. It is not SavedResult/5C.
        var force = current.ReadPersonForce(actor) ?? throw IntelligenceMissing("inspect_narration_Force");
        if (TalentForceIsPlayerCurrent(state, current, force))
        {
            int actorId = IntelligenceNeed(current.ReadPersonIdentity(actor), "inspect_narration_actor");
            var city = current.ReadCityTableReference((uint)task.TargetId < 52 ? task.TargetId : 0)
                ?? throw IntelligenceMissing("inspect_narration_City");
            ushort location = IntelligenceNeed(current.ReadBaseObjectLink(city), "inspect_narration_Base4E");
            var main = FormatIntelligenceNarrationCurrent(state, current, 0x34C7, new(Person0: actorId, Base0: location));
            var extra = FormatIntelligenceNarrationCurrent(main.State, current, 0x34CC, new(Person0: actorId, Base0: location));
            var effects = new List<CampaignEffectRequest>();
            state = WriteIntelligenceNarrationHistoryCurrent(extra.State, current, actorId, main.Text, extra.Text,
                IntelligenceActorDistrict(current, actor), ReadIntelligenceStartHexCurrent(current, task), false, true, effects);
            if (IntelligenceNarrationAllowedCurrent(state, current))
            {
                effects.Add(CreateSingleOfficerPresentation(current, state, actorId, main,
                    TalentCommandPresentationOperation.ConditionalOfficerText, "intelligence.inspect_narration"));
                return new(InstallIntelligenceNarrationCurrent(state, current, task, actor, date,
                    PersonnelIntelligenceNarrationBranch.InspectSuccess, main.Text, null, effects), false);
            }
            state = AppendIntelligenceEffectsCurrent(state, effects);
        }
        return FinishIntelligenceInspectCurrent(state, current, task, actor, date);
    }

    private GameState BeginIntelligenceCancellationNarrationCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date)
    {
        if (!IsIntelligenceCancellationPlayerCurrent(state, current, task) ||
            (IntelligenceNeed(current.ReadWorldTransferServiceFlags(), "cancel_world16A8") & 0x100) != 0) return state;
        int actorId = IntelligenceNeed(current.ReadPersonIdentity(actor), "cancel_narration_actor");
        HistoryMessageOperands operands, supplemental;
        int root, extraRoot, district, hex;
        if (task.Kind == PersonnelIntelligenceKind.Inspect)
        {
            var city = current.ReadCityTableReference((uint)task.TargetId < 52 ? task.TargetId : 0) ?? throw IntelligenceMissing("cancel_narration_City");
            operands = supplemental = new(Person0: actorId, Base0: IntelligenceNeed(current.ReadBaseObjectLink(city), "cancel_narration_Base4E"));
            root = 0x3461; extraRoot = 0x3468;
            district = IntelligenceActorDistrict(current, actor); hex = ReadIntelligenceStartHexCurrent(current, task);
        }
        else
        {
            var own = current.ReadForceTableReference((uint)task.ForceId < 52 ? task.ForceId : 0) ?? throw IntelligenceMissing("cancel_narration_Force");
            var target = current.ReadForceTableReference((uint)task.TargetId < 52 ? task.TargetId : 0) ?? throw IntelligenceMissing("cancel_narration_target");
            int ruler = TalentRulerCurrent(current, target), targetId = IntelligenceNeed(current.ReadForcePhysicalSlot(target), "cancel_narration_target_identity");
            operands = new(Person0: actorId, Person1: TalentRulerCurrent(current, own), Person2: ruler, Force0: targetId);
            supplemental = new(Person0: ruler, Force0: targetId);
            root = 0x3453; extraRoot = 0x3466; district = task.SourceDistrictId; hex = 0;
        }
        var main = FormatIntelligenceNarrationCurrent(state, current, root, operands);
        var extra = FormatIntelligenceNarrationCurrent(main.State, current, extraRoot, supplemental);
        var effects = new List<CampaignEffectRequest>();
        state = WriteIntelligenceNarrationHistoryCurrent(extra.State, current, actorId, main.Text, extra.Text, district, hex, false, false, effects);
        if (IntelligenceNarrationAllowedCurrent(state, current))
        {
            effects.Add(CreateSingleOfficerPresentation(current, state, actorId, main,
                TalentCommandPresentationOperation.ConditionalOfficerText, "intelligence.cancel_narration"));
            return InstallIntelligenceNarrationCurrent(state, current, task, actor, date,
                task.Kind == PersonnelIntelligenceKind.Away ? PersonnelIntelligenceNarrationBranch.CancelAway : PersonnelIntelligenceNarrationBranch.CancelInspect,
                main.Text, null, effects);
        }
        return AppendIntelligenceEffectsCurrent(state, effects);
    }

    private PersonnelIntelligenceAdvanceResult ResumeIntelligenceNarrationCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task, NativeMapSeasonDate date)
    {
        var pending = task.NarrationProgress?.Pending ?? throw IntelligenceMissing("narration_resume_owner");
        if (pending.SourceDate != date || task.IsRetiring || !task.IsAssociated)
            throw IntelligenceMissing("narration_resume_lifecycle");
        if (pending.Phase == PersonnelIntelligenceNarrationPhase.AwaitingNarration) return new(state, false);
        if (pending.Phase != PersonnelIntelligenceNarrationPhase.ReadyToResume) throw IntelligenceMissing("narration_resume_phase");
        //The typed receipt already installed the live host App32. Never install
        //a preparation-time stream here or choose a new actor/ruler/outcome.
        state = StoreIntelligenceTaskCurrent(state, current, task with { NarrationProgress = new(null) });
        int actor = pending.ActorPhysicalSlot;
        switch (pending.Branch)
        {
            case PersonnelIntelligenceNarrationBranch.CancelAway:
            case PersonnelIntelligenceNarrationBranch.CancelInspect:
                return FinishIntelligenceReturnCurrent(state, current, task, date);
            case PersonnelIntelligenceNarrationBranch.InspectSuccess:
                return FinishIntelligenceInspectCurrent(state, current, task, actor, date);
            case PersonnelIntelligenceNarrationBranch.AwayFailure:
                state = CompleteIntelligenceFailureHistoryCurrent(state, current, task, actor, date,
                    pending.AwayOutcome ?? throw IntelligenceMissing("narration_failure_outcome"), pending.MainTextAtSource);
                break;
            case PersonnelIntelligenceNarrationBranch.AwaySuccess:
            case PersonnelIntelligenceNarrationBranch.AwayTargetNotice:
                break;
            default: throw IntelligenceMissing("narration_resume_branch");
        }
        return FinishIntelligenceAwayCurrent(state, current, task, actor, date,
            pending.AwayOutcome ?? throw IntelligenceMissing("narration_away_outcome"));
    }

    private GameState CompleteIntelligenceFailureHistoryCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date,
        PersonnelIntelligenceAwayNarrationOutcome outcome, string main)
    {
        int actorId = IntelligenceNeed(current.ReadPersonIdentity(actor), "failure_resume_actor_identity");
        int targetRuler = IntelligenceNeed(current.ReadPersonIdentity(outcome.TargetRulerPhysicalSlot), "failure_resume_target_ruler_identity");
        var extra = FormatIntelligenceNarrationCurrent(state, current, outcome.Injured ? 0x344A : 0x3449,
            new(Person0: actorId, Person1: targetRuler, Integers: [outcome.Loss]));
        var effects = new List<CampaignEffectRequest>();
        state = WriteIntelligenceNarrationHistoryCurrent(extra.State, current, actorId, main, extra.Text,
            outcome.ActorDistrictPhysicalSlot, ReadIntelligenceJourneyHexCurrent(extra.State, current, task, date), false, true, effects);
        return AppendIntelligenceEffectsCurrent(state, effects);
    }

    private PersonnelIntelligenceAdvanceResult FinishIntelligenceAwayCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date, PersonnelIntelligenceAwayNarrationOutcome outcome)
    {
        state = ApplyIntelligenceAwayBodyCurrent(state, current, actor, outcome);
        state = ApplyIntelligenceJourneyRewardsCurrent(state, current, actor, 21, outcome.Succeeded ? 1 : 2);
        if (!outcome.Succeeded && (IntelligenceNeed(current.ReadPersonOldForceSlot(actor), "journey_post_raw_Force") != task.ForceId ||
            IntelligenceNeed(current.ReadPersonStatus(actor), "journey_post_status") is not (>= 1 and <= 4))) return new(state, true);
        return FinishIntelligenceReturnCurrent(state, current, task, date);
    }

    private PersonnelIntelligenceAdvanceResult FinishIntelligenceInspectCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, int actor, NativeMapSeasonDate date)
    {
        state = ApplyTalentPendingMeritCurrent(state, current, actor, unchecked(IntelligenceResultParameter(state, "inspect-merit", 60) * 10));
        state = ApplyIntelligenceJourneyRewardsCurrent(state, current, actor, 25, 1);
        return FinishIntelligenceReturnCurrent(state, current, task, date);
    }

    private static PersonnelIntelligenceAdvanceResult FinishIntelligenceReturnCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, NativeMapSeasonDate date)
        => new(PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(state, current, task.Id,
            ReadIntelligenceReturnMembersCurrent(current, task), date, task.ReportToPlayer), true);

    private bool IntelligenceNarrationAllowedCurrent(GameState state, SourceWorldContext current)
    {
        if (IntelligenceNeed(ReadApplicationReturnToTitlePending(), "narration_return_title")) return false;
        var blocker = current.ReadReportBlocker() ?? (state.CampaignEventEligibility?.HistoryRecordingSuppressed is bool suppressed
            ? new ReportBlockerState(true, suppressed ? 1 : 0) : null) ?? throw IntelligenceMissing("narration_event_manager1B0");
        return !blocker.Present || IntelligenceNeed(blocker.RawField1B0, "narration_event_manager1B0_value") == 0;
    }

    private static PersonnelIntelligenceTask ReadIntelligenceNarrationTaskCurrent(SourceWorldContext current, int id)
        => current.ReadOwnedPersonCommandManager()?.Intelligences?.GetValueOrDefault(id) ?? throw IntelligenceMissing("narration_task_owner");

    private GameState InstallIntelligenceNarrationCurrent(GameState state, SourceWorldContext current, PersonnelIntelligenceTask task,
        int actor, NativeMapSeasonDate date, PersonnelIntelligenceNarrationBranch branch, string main,
        PersonnelIntelligenceAwayNarrationOutcome? outcome, List<CampaignEffectRequest> effects)
    {
        if (state.NativePeriodContinuation is null) throw IntelligenceMissing("narration_normal_period_host_unavailable");
        var actual = ReadIntelligenceNarrationTaskCurrent(current, task.Id);
        if (actual.NarrationProgress is null || actual.NarrationProgress.Pending is not null)
            throw IntelligenceMissing("narration_source_time_owner");
        state = AppendIntelligenceEffectsCurrent(state, effects);
        long sequence = (state.CampaignEffectRequests ?? throw IntelligenceMissing("narration_outbox")).LastBatchSequence;
        return StoreIntelligenceTaskCurrent(state, current, actual with { NarrationProgress = new(
            new(branch, PersonnelIntelligenceNarrationPhase.AwaitingNarration, date, actor, sequence, main) { AwayOutcome = outcome }) });
    }

    private HistoryTextResult FormatIntelligenceNarrationCurrent(GameState state, SourceWorldContext current, int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.IntelligenceCommands, null, _personnelNarrationVoice);

    private GameState WriteIntelligenceNarrationHistoryCurrent(GameState state, SourceWorldContext current, int speaker,
        string main, string extra, int district, int hex, bool succeeded, bool result, List<CampaignEffectRequest> effects)
        => WriteHistory(state, current, new(6, district, hex,
            (ReadArmyDestructionSampler(state, current) ?? throw IntelligenceMissing("narration_report_day450")).Day450,
            extra, main, speaker, MarkBit0: 1, MarkBit4: result ? 1 : 0, MarkBit5: succeeded ? 1 : 0), effects);

    private static GameState AppendIntelligenceEffectsCurrent(GameState state, List<CampaignEffectRequest> effects)
        => effects.Count == 0 ? state : state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw IntelligenceMissing("narration_outbox")).Append(CampaignEffectSource.IntelligenceCommands,
                state.NativeMapSeason?.AdvancedCalendarSteps ?? throw IntelligenceMissing("narration_revision"), effects) };

    private static int ReadIntelligenceJourneyHexCurrent(GameState state, SourceWorldContext current, PersonnelIntelligenceTask task, NativeMapSeasonDate date)
        => IntelligenceNeed(TalentTravelSource.ResolvePosition(state, current, task.StartHexId, task.EndHexId,
            task.SourceDistrictId, task.StartDate, task.DueDate, date).PhysicalSlot, "narration_journey_Hex");

    private static int ReadIntelligenceRulerReceiverCurrent(SourceWorldContext current, ForceReference force)
    {
        ushort raw = IntelligenceNeed(current.ReadForceRecord(IntelligenceNeed(force.PhysicalSlot, "narration_ruler_Force"))?.RawRuler10, "narration_ruler10");
        return IntelligenceNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "narration_ruler_receiver");
    }
}
