namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Records consumption by the real personnel presentation host.
    /// A receipt belongs to the exact world from which its request was read.</summary>
    public bool TryCompletePersonnelPresentation(GameState expectedOwner, long batchSequence, int requestIndex)
    {
        //Other continuation kinds own their own presentation receipt protocols.
        if (_progress is not null) return false;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (!ReferenceEquals(owner, expectedOwner) || owner.CampaignEffectRequests is not { } effects)
            return false;
        int batchIndex = Array.FindIndex(effects.PendingBatches, batch => batch.Sequence == batchSequence);
        if (batchIndex < 0) return false;
        var batch = effects.PendingBatches[batchIndex];
        if ((uint)requestIndex >= (uint)batch.Requests.Length) return false;
        var request = batch.Requests[requestIndex];
        bool canComplete = batch.Source switch
        {
            CampaignEffectSource.TalentCommands => request.TalentCommandPresentation is not null ||
                request.TalentSceneryReward is not null,
            CampaignEffectSource.DiplomacyCommands => request.TalentCommandPresentation is not null ||
                request.DiplomacyArrivalAudio is not null,
            CampaignEffectSource.PersonnelAdministrationCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.OfficerText },
            CampaignEffectSource.OfficerOfficeCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement },
            CampaignEffectSource.DoctrineResearchCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.OfficerText },
            CampaignEffectSource.StrategicCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText },
            CampaignEffectSource.OfficerGrowthNotifications => request.OfficerGrowthFeedback is
                { Operation: OfficerGrowthFeedbackOperation.SoundCue or OfficerGrowthFeedbackOperation.Narration },
            CampaignEffectSource.MonthlyMaintenance => request.MonthlyMaintenance is
                { Operation: MonthlyMaintenanceOperation.NarrationText or MonthlyMaintenanceOperation.ConditionalSpeakerText } ||
                request.MonthlyMaintenance is
                { Operation: MonthlyMaintenanceOperation.ControllerQueueEvent, EventKind: 6 or 8 } ||
                request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.OfficerText },
            _ => false
        };
        // Controller events 6/8 enqueue actual SE audio. History, discovery
        // and monthly map effects retain their separate consumers.
        if (!canComplete) return false;

        CampaignEffectBatch[] batches;
        if (batch.Requests.Length == 1)
        {
            batches = new CampaignEffectBatch[effects.PendingBatches.Length - 1];
            Array.Copy(effects.PendingBatches, 0, batches, 0, batchIndex);
            Array.Copy(effects.PendingBatches, batchIndex + 1, batches, batchIndex,
                effects.PendingBatches.Length - batchIndex - 1);
        }
        else
        {
            var requests = new CampaignEffectRequest[batch.Requests.Length - 1];
            Array.Copy(batch.Requests, 0, requests, 0, requestIndex);
            Array.Copy(batch.Requests, requestIndex + 1, requests, requestIndex,
                batch.Requests.Length - requestIndex - 1);
            batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
            batches[batchIndex] = batch with { Requests = requests };
        }
        var completedOwner = owner with { CampaignEffectRequests = effects with { PendingBatches = batches } };
        if (pending is not null) _normalPeriod = pending with { Candidate = completedOwner };
        else State = completedOwner;
        return true;
    }
}
