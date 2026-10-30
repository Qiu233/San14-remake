namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>The actual Intelligence narration consumer completes only its
    /// waiting source head. Its numerical tail remains with the normal period.</summary>
    public bool TryCompleteIntelligenceNarration(GameState expectedOwner, long batchSequence,
        int requestIndex, CampaignAppRandomState appRandomSnapshot)
    {
        ArgumentNullException.ThrowIfNull(appRandomSnapshot);
        if (!TryReadSourcePresentationHead(expectedOwner, CampaignEffectSource.IntelligenceCommands,
                batchSequence, requestIndex, out var owner, out var operation, out var effects,
                out int batchIndex, out var request) || operation is null ||
            !TryReadIntelligenceNarrationWait(owner, batchSequence, out int taskId, out var task, out var wait) ||
            wait.Phase != PersonnelIntelligenceNarrationPhase.AwaitingNarration ||
            request.TalentCommandPresentation is not
                { Operation: TalentCommandPresentationOperation.OfficerText or
                    TalentCommandPresentationOperation.ConditionalOfficerText, Speaker: not null, TextAtSource: not null } message ||
            !string.Equals(message.TextAtSource, wait.MainTextAtSource, StringComparison.Ordinal) ||
            wait.Branch == PersonnelIntelligenceNarrationBranch.AwayTargetNotice &&
                (message.Operation != TalentCommandPresentationOperation.OfficerText || message.OfficerId != 5012))
            return false;

        var commands = owner.PersonnelCommands!;
        var completed = owner with
        {
            CampaignAppRandom32 = appRandomSnapshot,
            PersonnelCommands = commands with
            {
                Intelligences = new(commands.Intelligences!)
                {
                    [taskId] = task with
                    {
                        NarrationProgress = task.NarrationProgress! with
                        { Pending = wait with { Phase = PersonnelIntelligenceNarrationPhase.ReadyToResume } }
                    }
                }
            }
        };
        //The source speech and phase move together. No sampling, budget,
        //health, report, return or retirement writer runs in this receipt.
        CompleteSourcePresentationHead(completed, operation, effects, batchIndex, requestIndex,
            completed.ActiveMonthlyScene, completed.CoalitionCameraPresentation);
        return true;
    }

    private static bool TryReadIntelligenceNarrationWait(GameState owner, long batchSequence,
        out int taskId, out PersonnelIntelligenceTask task, out PersonnelIntelligenceNarrationPending wait)
    {
        taskId = -1; task = null!; wait = null!;
        if (owner.NativePeriodContinuation?.Cursor is not
                { StageIndex: 0, AwaitingIntelligenceTaskId: int id, AwaitingCaravanTaskId: null,
                    AwaitingStrategicTaskId: null, AwaitingDiplomacyTaskId: -1 } cursor ||
            owner.PersonnelCommands?.Intelligences?.GetValueOrDefault(id) is not
                { Started: true, IsRetiring: false, IsAssociated: true, NarrationProgress.Pending: { } pending } found ||
            found.DispatchSlot != cursor.PersonnelAfterSlot || pending.SourceDate != cursor.Date ||
            pending.NarrationBatchSequence != batchSequence)
            return false;
        taskId = id; task = found; wait = pending;
        return true;
    }

    private static bool IsIntelligenceNarrationOwnedByWait(GameState owner, long batchSequence)
    {
        if (owner.NativePeriodContinuation?.Cursor?.AwaitingIntelligenceTaskId is not int taskId) return false;
        var wait = owner.PersonnelCommands?.Intelligences?.GetValueOrDefault(taskId)?.NarrationProgress?.Pending;
        //An active cursor with missing progress is invalid, never a reason
        //to let the generic presentation route erase its pending narration.
        return wait is null || wait.NarrationBatchSequence == batchSequence;
    }

    //SE6 precedes the Serif call even when that call is suppressed and creates
    //no wait. An earlier no-wait task's sound can also precede another wait.
    //The shared source-head check retains that FIFO; sound never releases a tail.
    private static bool IsIntelligenceNarrationSound(GameState owner, long batchSequence,
        TalentCommandPresentationRequest? message)
    {
        if (message is not { Operation: TalentCommandPresentationOperation.SoundCue, SoundId: 6 }) return false;
        var wait = owner.NativePeriodContinuation?.Cursor?.AwaitingIntelligenceTaskId is int taskId
            ? owner.PersonnelCommands?.Intelligences?.GetValueOrDefault(taskId)?.NarrationProgress?.Pending : null;
        if (wait is null || wait.NarrationBatchSequence != batchSequence) return true;
        return TryReadIntelligenceNarrationWait(owner, batchSequence, out _, out _, out _) &&
            wait is { Branch: PersonnelIntelligenceNarrationBranch.AwayTargetNotice,
                Phase: PersonnelIntelligenceNarrationPhase.AwaitingNarration };
    }
}
