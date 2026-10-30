using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    private (GameState State, bool AwaitingAnnouncement) BeginHiredCaptureAnnouncementCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        ForcedDuelCaptureAnnouncement announcement, Func<ArmyDefeatContinuation> checkpoint,
        List<CampaignEffectRequest> feedback, bool allowPause)
    {
        if (announcement is not { MessageRoot: 0x34AA, SoundId: 6 or 8, TextAtSource: not null, Speaker: null })
            throw Missing("hired_capture_special_announcement");
        if (!package.HiredCaptureNarrationAllowedCurrent(state, current))
        {
            //405E40 precedes1F0BC0 even when no dialogue is admitted. This
            //ordinary cue has its real consumer, but does not invent a wait.
            feedback.Add(new(null, null) { TalentCommandPresentation = new(
                TalentCommandPresentationOperation.SoundCue, announcement.SoundId, null, null, null, null) });
            return (state, false);
        }
        if (!allowPause || state.NativePeriodContinuation is null)
            throw Missing("hired_capture_normal_period_host");
        var saved = checkpoint();
        if (saved.ResumePoint != ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement ||
            saved.SpecialCommanderReclaim || saved.ReclaimDefeatedCommander != false)
            throw Missing("hired_capture_checkpoint");
        //Publish all completed numerical/current writes before the only new
        //wait. The caller still owns its live pool and full settlement queue.
        state = PublishRulerTreatmentCurrent(state, current, pool);
        var outbox = state.CampaignEffectRequests ?? throw Missing("hired_capture_outbox");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("hired_capture_revision");
        if (feedback.Count != 0)
        {
            outbox = outbox.Append(CampaignEffectSource.ArmyBattleDefeat, revision, feedback);
            feedback.Clear();
        }
        outbox = outbox.Append(CampaignEffectSource.ArmyBattleDefeat, revision,
            [new(null, null) { DuelCaptureAnnouncement = new(announcement.OfficerId,
                announcement.OfficerNameAtSource, announcement.MessageRoot, announcement.SoundId)
                { TextAtSource = announcement.TextAtSource, Speaker = null } }]);
        return (state with { CampaignEffectRequests = outbox, ArmyDefeatContinuation = saved with
            { HiredCapture = new(HiredArmyCapturePhase.AwaitingAnnouncement, outbox.LastBatchSequence) } }, true);
    }

    private (GameState State, OwnedArmyDestructionReceipt? Receipt) ResumeHiredCaptureAnnouncementCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        ArmyDefeatContinuation checkpoint, NativeMapSeasonDate? date, List<CampaignEffectRequest> feedback)
    {
        if (checkpoint.HiredCapture is not { Phase: HiredArmyCapturePhase.ReadyToRelease } ||
            checkpoint.ChosenDisposition is not null || checkpoint.PendingCapturedRulerTreatment is not null ||
            checkpoint.SpecialCommanderReclaim || checkpoint.ReclaimDefeatedCommander != false)
            throw Missing("hired_capture_actual_announcement_pending");
        int person = current.ReadPersonLookupReference(checkpoint.CommanderOfficerId)?.PhysicalSlot
            ?? throw Missing("hired_capture_resume_Person");
        TacticSourceSamplerWorld? sampler = checkpoint.SampleDay is int day && checkpoint.SampleSalt is int salt
            ? new(day, salt) : null;
        state = state with { ArmyDefeatContinuation = null };
        state = package.ApplyCapturedSpecialPersonReleaseCurrent(state, current, pool,
            person, date, sampler, feedback);
        //The original capture returned1. Do not rerun injury/resources,
        //loyalty/morale, the capture sample or ordinary Person Return.
        return CompleteDestructionTail(state, current, pool, checkpoint, date, feedback,
            projectOwnedRoles: true, sourceEffects:
                ["25B75x.resumed_2E0FC0_special_265250_pending_hire_release"]);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal bool HiredCaptureNarrationAllowedCurrent(GameState state, SourceWorldContext current)
    {
        if (HiredCaptureNeed(ReadApplicationReturnToTitlePending(), "return_title")) return false;
        var blocker = current.ReadReportBlocker() ?? (state.CampaignEventEligibility?.HistoryRecordingSuppressed is bool suppressed
            ? new ReportBlockerState(true, suppressed ? 1 : 0) : null) ?? throw HiredCaptureMissing("event_manager1B0");
        return !blocker.Present || HiredCaptureNeed(blocker.RawField1B0, "event_manager1B0_value") == 0;
    }
    private static T HiredCaptureNeed<T>(T? value, string field) where T : struct => value ?? throw HiredCaptureMissing(field);
    private static InvalidDataException HiredCaptureMissing(string field) => new("capture.hired-army." + field);
}
