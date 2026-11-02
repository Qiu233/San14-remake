namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryCompleteBaseCaptureSpecialNotice(GameState expectedOwner, long batchSequence,
        int requestIndex, CampaignAppRandomState appRandomSnapshot)
    {
        ArgumentNullException.ThrowIfNull(appRandomSnapshot);
        if (!TryReadSourcePresentationHead(expectedOwner, CampaignEffectSource.ArmyBattleDefeat,
                batchSequence, requestIndex, out var owner, out var operation, out var effects,
                out int batchIndex, out var request) ||
            operation is null && _progress?.ArmyProgress is null ||
            owner.BaseCaptureContinuation is not { PersonPrefixProgress:
                { Stage: BaseCapturePersonPrefixStage.AwaitingSpecialCaptureNotice } progress } capture ||
            progress.AnnouncementBatchSequence != batchSequence ||
            request.TalentCommandPresentation is not
                { Operation: TalentCommandPresentationOperation.Announcement, TextAtSource: not null } announcement ||
            !string.Equals(announcement.TextAtSource, progress.AnnouncementTextAtSource, StringComparison.Ordinal))
            return false;
        var completed = owner with
        {
            CampaignAppRandom32 = appRandomSnapshot,
            BaseCaptureContinuation = capture with { PersonPrefixProgress = progress with
                { Stage = BaseCapturePersonPrefixStage.ReadyToMergeCapturedPeople } }
        };
        //Only the real announcement is consumed. The source list append and
        //all Person, Force, territory and retirement writers run on resume.
        CompleteSourcePresentationHead(completed, operation, effects, batchIndex, requestIndex,
            completed.ActiveMonthlyScene, completed.CoalitionCameraPresentation);
        return true;
    }

    private static bool IsBaseCaptureSpecialNoticeOwned(GameState owner, long batchSequence) =>
        owner.BaseCaptureContinuation?.PersonPrefixProgress is { AnnouncementBatchSequence: long sequence } &&
        sequence == batchSequence;
}
