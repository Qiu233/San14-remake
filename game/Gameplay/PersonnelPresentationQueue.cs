using San14.Core;

namespace San14.Client.Gameplay;

internal sealed record PersonnelPresentationEntry(long BatchSequence,int RequestIndex,CampaignEffectRequest Request,
    CampaignEffectSource Source);

internal static class PersonnelPresentationQueue
{
    internal static PersonnelPresentationEntry? Next(GameState world)
    {
        foreach(var batch in world.CampaignEffectRequests?.PendingBatches??[])
        {
            //Trait view updates are actual map commands, including for an Army
            //which has since retired. They share the source FIFO with narration.
            bool traitSource = batch.Source is CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or
                CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or
                CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or CampaignEffectSource.ArmyBattleDefeat or
                CampaignEffectSource.ArmyMovement or CampaignEffectSource.ArmyFacilityConstruction or CampaignEffectSource.ArmyFacilityRepair;
            if(batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.DiplomacyCommands or CampaignEffectSource.ForeignTradeCommands or CampaignEffectSource.SceneryInvestmentCommands or CampaignEffectSource.IntelligenceCommands or
                CampaignEffectSource.OfficerGrowthNotifications or CampaignEffectSource.OfficerOfficeCommands or
                CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance or
                CampaignEffectSource.DoctrineResearchCommands or CampaignEffectSource.StrategicCommands or CampaignEffectSource.ArmyFirePresentation or CampaignEffectSource.ArmyCommandMaintenance or CampaignEffectSource.MapObjectRetirement or
                CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.PersonDeath or CampaignEffectSource.ProposalCommands) && !traitSource && !MapObjectPresentationSources.HasSource(batch.Source))continue;
            for(int index=0;index<batch.Requests.Length;index++)
            {
                var request=batch.Requests[index];
                //Monthly controller and map operations keep their source order.
                //The host must handle the head before showing a later line.
                if(MapObjectPresentationSources.Supports(batch.Source, request.MapObjectPresentation) ||
                    batch.Source == CampaignEffectSource.ProposalCommands && request.HistoryNotification is null || request.MapObjectPresentation is { BaseCapture: not null,
                    Event: MapObjectPresentationEvent.BaseOwnershipLostVisual or MapObjectPresentationEvent.BaseOwnershipLostSound } ||
                    request.PersonDeathReport is not null || request.MapMusicIntent is not null ||
                    batch.Source == CampaignEffectSource.ArmyBattleDefeat && request.DuelCaptureAnnouncement is not null ||
                    request.ApplicationExit is not null || request.ForceCoalitionVisual is not null ||
                    traitSource && request.ArmyTraitViewRefresh is not null ||
                    batch.Source == CampaignEffectSource.ArmyFirePresentation && request.ArmyFireCast is not null ||
                    batch.Source is CampaignEffectSource.ArmyFirePresentation or CampaignEffectSource.NormalCombatApplication or
                        CampaignEffectSource.ArmyCommandMaintenance or CampaignEffectSource.FacilityTraps && request.ArmyFireAnnouncement is not null ||
                    batch.Source == CampaignEffectSource.FireDamage && request.FireDamagePresentation is not null ||
                    batch.Source == CampaignEffectSource.TerrainDamage && request.TerrainDamagePresentation is not null ||
                    batch.Source == CampaignEffectSource.ArmyMovement && request.ArmyDeparture is not null ||
                    batch.Source == CampaignEffectSource.ArmyCommandMaintenance &&
                        request.Speech?.Event is CombatSpeechEvent.ArmyOrderFinished or CombatSpeechEvent.ArmyHoldingPosition or
                            CombatSpeechEvent.ArmyDestinationReachedHolding or CombatSpeechEvent.ArmyAreaOccupationCompleted ||
                    batch.Source == CampaignEffectSource.ArmyMovement &&
                        request.Speech is { Event: CombatSpeechEvent.ArmyEnteredBase, BaseArrival: not null } ||
                    batch.Source == CampaignEffectSource.ArmyFacilityConstruction && request.FacilityConstructionPresentation is not null ||
                    batch.Source == CampaignEffectSource.ArmyFacilityRepair && request.FacilityRepairPresentation is not null ||
                    batch.Source is CampaignEffectSource.FacilityEffects or CampaignEffectSource.FacilityTraps &&
                        (request.FacilityAuraReset is not null || request.MilitaryMusicPulse is not null ||
                         request.FacilityProjectile is not null || request.FacilityStatisticPresentation is not null ||
                         request.FacilitySupportAura is not null || request.ArmyTrapPresentation is not null ||
                         request.FacilityTrapPresentation is not null || request.ArmyFacilityDamagePresentation is not null) ||
                    batch.Source == CampaignEffectSource.ArmyFacilityConstruction &&
                        request.Speech?.Event == CombatSpeechEvent.ArmyFacilityConstructionStarted ||
                    batch.Source == CampaignEffectSource.ArmyFacilityRepair &&
                        request.Speech?.Event == CombatSpeechEvent.ArmyFacilityCompleted ||
                    batch.Source is CampaignEffectSource.FacilityEffects or CampaignEffectSource.FacilityTraps &&
                        request.Speech?.Event is CombatSpeechEvent.ArmyUnderFacilityAttack or CombatSpeechEvent.TrapPitfall or
                            CombatSpeechEvent.TrapRockfall or CombatSpeechEvent.TrapCountered or CombatSpeechEvent.ConfusionApplied or
                            CombatSpeechEvent.ImmobilizationApplied ||
                    request.BaseAbnormalFeedback is not null ||
                    request.MonthlyMaintenance is not null||request.NormalTurnFoodWarningPresentation is not null||
                    request.NormalTurnForceResourceNotice is not null||request.TalentSceneryReward?.DiscoveryTextAtSource is not null||
                    request.OfficerGrowthFeedback?.Operation is OfficerGrowthFeedbackOperation.SoundCue or OfficerGrowthFeedbackOperation.Narration||
                    request.DiplomacyArrivalAudio is not null||request.TalentCommandPresentation?.Operation is
                    TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or
                    TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText)
                    return new(batch.Sequence,index,request,batch.Source);
            }
        }
        return null;
    }

    internal static TalentCommandPresentationRequest? Message(PersonnelPresentationEntry entry) =>
        entry.Request.TalentCommandPresentation ?? (entry.Request.PersonDeathReport switch
        {
            { Operation: PersonDeathReportOperation.SpeakerText } text =>
                new TalentCommandPresentationRequest(TalentCommandPresentationOperation.OfficerText, null, text.SpeakerId, null, null, text.TextAtSource)
                    { Speaker = text.Speaker, Voice = text.Voice },
            { Operation: PersonDeathReportOperation.NarrationText } text =>
                new TalentCommandPresentationRequest(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.TextAtSource),
            _ => null
        }) ?? (entry.Request.MonthlyMaintenance switch
        {
            { Operation: MonthlyMaintenanceOperation.ControllerQueueEvent, EventKind: 6 or 8 } sound =>
                new(TalentCommandPresentationOperation.SoundCue, sound.EventKind, null, null, null, null),
            { Operation: MonthlyMaintenanceOperation.NarrationText } text =>
                new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.TextAtSource),
            { Operation: MonthlyMaintenanceOperation.ConditionalSpeakerText } text =>
                new(TalentCommandPresentationOperation.OfficerText, null, text.SpeakerId, null, null, text.TextAtSource),
            _ => null
        });
}
