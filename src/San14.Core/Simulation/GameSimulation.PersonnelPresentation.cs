namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Records consumption by the real personnel presentation host.
    /// A receipt belongs to the exact world from which its request was read.</summary>
    public bool TryCompletePersonnelPresentation(GameState expectedOwner, long batchSequence, int requestIndex)
    {
        // Ordinary periods and restored legacy operations own the same real
        // source outbox. Every request retains its existing admission below.
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) || owner.CampaignEffectRequests is not { } effects)
            return false;
        int batchIndex = Array.FindIndex(effects.PendingBatches, batch => batch.Sequence == batchSequence);
        if (batchIndex < 0) return false;
        var batch = effects.PendingBatches[batchIndex];
        if ((uint)requestIndex >= (uint)batch.Requests.Length) return false;
        var request = batch.Requests[requestIndex];
        //A real Intelligence wait consumes narration only through its typed
        //receipt, which also preserves the current App stream and resume phase.
        if (batch.Source == CampaignEffectSource.IntelligenceCommands &&
            request.TalentCommandPresentation is { Operation: TalentCommandPresentationOperation.OfficerText or
                TalentCommandPresentationOperation.ConditionalOfficerText } &&
            IsIntelligenceNarrationOwnedByWait(owner, batchSequence)) return false;
        if (batch.Source == CampaignEffectSource.ArmyBattleDefeat &&
            request.TalentCommandPresentation is { Operation: TalentCommandPresentationOperation.Announcement } &&
            IsBaseCaptureSpecialNoticeOwned(owner, batchSequence)) return false;
        if ((BaseCapturePersonPrefixProgress.IsPresentationPending(owner) ||
                HiredArmyCaptureProgress.IsPresentationPending(owner)) &&
            batch.Source == CampaignEffectSource.ArmyBattleDefeat &&
            !TryReadSourcePresentationHead(expectedOwner, batch.Source, batchSequence, requestIndex,
                out _, out _, out _, out _, out _)) return false;
        if (IsBaseCaptureRequest(request) && !IsPresentationHead(effects, batchIndex, requestIndex, IsBaseCaptureRequest)) return false;
        if (request.PersonDeathReport is not null &&
            !IsPresentationHead(effects, batchIndex, requestIndex, static item => item.PersonDeathReport is not null)) return false;
        if (batch.Source is CampaignEffectSource.ForeignTradeCommands or CampaignEffectSource.SceneryInvestmentCommands or CampaignEffectSource.IntelligenceCommands or CampaignEffectSource.DiplomacyCommands &&
            !TryReadSourcePresentationHead(expectedOwner, batch.Source, batchSequence, requestIndex,
                out _, out _, out _, out _, out _)) return false;
        //Occupation shares ArmyMovement with departure/trait work. Its
        //actual map receipt cannot jump any earlier same-source request.
        if (batch.Source == CampaignEffectSource.ArmyMovement &&
            request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.TerritoryOccupation } &&
            !TryReadSourcePresentationHead(expectedOwner, batch.Source, batchSequence, requestIndex,
                out _, out _, out _, out _, out _)) return false;
        bool canComplete = batch.Source switch
        {
            CampaignEffectSource.ArmyMovement => request.MonthlyMaintenance is
                { Operation: MonthlyMaintenanceOperation.TerritoryOccupation, EventKind: 4 or 5 },
            CampaignEffectSource.SceneryInvestmentCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue, SoundId: 4 } or
                { Operation: TalentCommandPresentationOperation.Announcement },
            CampaignEffectSource.IntelligenceCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.ConditionalOfficerText, Speaker: not null } or
                { Operation: TalentCommandPresentationOperation.OfficerText, Speaker: not null } ||
                IsIntelligenceNarrationSound(owner, batchSequence, request.TalentCommandPresentation),
            CampaignEffectSource.ForeignTradeCommands => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue, SoundId: 4 or 5 or 6 or 7 or 8 } or
                { Operation: TalentCommandPresentationOperation.Announcement } or
                { Operation: TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText, Speaker: not null },
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
            CampaignEffectSource.ArmyBattleDefeat => request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue, SoundId: 4 or 5 or 6 or 7 or 8 or 23 or 24 } or
                { Operation: TalentCommandPresentationOperation.Announcement } or
                { Operation: TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText, Speaker: not null } ||
                request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.NarrationText } or
                    { Operation: MonthlyMaintenanceOperation.TerritoryOccupation, EventKind: 4 or 5 } ||
                request.MapObjectPresentation is { BaseCapture: not null,
                    Event: MapObjectPresentationEvent.BaseOwnershipLostVisual or MapObjectPresentationEvent.BaseOwnershipLostSound },
            CampaignEffectSource.OfficerGrowthNotifications => request.OfficerGrowthFeedback is
                { Operation: OfficerGrowthFeedbackOperation.SoundCue or OfficerGrowthFeedbackOperation.Narration },
            CampaignEffectSource.PersonDeath => request.PersonDeathReport is
                { Operation: PersonDeathReportOperation.EventEnter, EventKind: 4 or 5 or 6 } or
                { Operation: PersonDeathReportOperation.EventExit or PersonDeathReportOperation.SpeakerText or PersonDeathReportOperation.NarrationText } or
                { Operation: PersonDeathReportOperation.ControllerQueueEvent, EventKind: 22 },
            CampaignEffectSource.MonthlyMaintenance => request.MapObjectPresentation is { BaseCapture: not null,
                    Event: MapObjectPresentationEvent.BaseOwnershipLostVisual or MapObjectPresentationEvent.BaseOwnershipLostSound } ||
                request.MonthlyMaintenance is
                { Operation: MonthlyMaintenanceOperation.NarrationText or MonthlyMaintenanceOperation.ConditionalSpeakerText or
                    MonthlyMaintenanceOperation.DisasterSceneEnter or MonthlyMaintenanceOperation.DisasterSceneExit } ||
                request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.ResetPersonnelGroupMoveDestinations } ||
                request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.TerritoryOccupation, EventKind: 4 or 5 } ||
                request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.FinalOrderSceneEnter, EventKind: 10 or 26 or 31 } ||
                request.MonthlyMaintenance is { Operation: MonthlyMaintenanceOperation.FinalOrderSceneExit } ||
                request.NormalTurnFoodWarningPresentation is not null ||
                request.NormalTurnForceResourceNotice is { TextAtSource: not null } ||
                request.MonthlyMaintenance is
                { Operation: MonthlyMaintenanceOperation.ControllerQueueEvent, EventKind: 6 or 8 } ||
                request.TalentCommandPresentation is
                { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText },
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
        var activeDisaster = request.MonthlyMaintenance?.Operation switch
        {
            MonthlyMaintenanceOperation.DisasterSceneEnter => request.MonthlyMaintenance.EventKind is 14 or 15 or 16
                ? new DisasterSceneState(request.MonthlyMaintenance.EventKind.Value) : owner.ActiveDisasterScene,
            MonthlyMaintenanceOperation.DisasterSceneExit => null,
            _ => owner.ActiveDisasterScene
        };
        var activeMonthly = request.PersonDeathReport?.Operation switch
        {
            PersonDeathReportOperation.EventEnter =>
                new MonthlySceneState(MonthlySceneFamily.PersonDeath, request.PersonDeathReport.EventKind!.Value),
            PersonDeathReportOperation.EventExit => null,
            _ => owner.ActiveMonthlyScene
        };
        var completedOwner = owner with
        {
            CampaignEffectRequests = effects with { PendingBatches = batches },
            ActiveDisasterScene = activeDisaster,
            ActiveMonthlyScene = request.MonthlyMaintenance?.Operation switch
            {
                MonthlyMaintenanceOperation.FinalOrderSceneEnter =>
                    new MonthlySceneState(MonthlySceneFamily.FinalOrder, request.MonthlyMaintenance.EventKind!.Value),
                MonthlyMaintenanceOperation.FinalOrderSceneExit => null,
                _ => activeMonthly
            }
        };
        PublishSourcePresentationOwner(completedOwner, pending);
        return true;
    }

    private static bool IsBaseCaptureRequest(CampaignEffectRequest request) => request.MapObjectPresentation is
        { Event: MapObjectPresentationEvent.BaseOwnershipLostVisual or MapObjectPresentationEvent.BaseOwnershipLostSound };

    private static bool IsPresentationHead(CampaignEffectRequests effects, int expectedBatch, int expectedRequest,
        Func<CampaignEffectRequest, bool> matches)
    {
        for (int batch = 0; batch <= expectedBatch; batch++)
            for (int request = 0; request < effects.PendingBatches[batch].Requests.Length; request++)
                if (matches(effects.PendingBatches[batch].Requests[request]))
                    return batch == expectedBatch && request == expectedRequest;
        return false;
    }
}
