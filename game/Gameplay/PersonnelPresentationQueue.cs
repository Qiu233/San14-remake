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
                CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or CampaignEffectSource.ArmyBattleDefeat;
            if(batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.DiplomacyCommands or
                CampaignEffectSource.OfficerGrowthNotifications or CampaignEffectSource.OfficerOfficeCommands or
                CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance or
                CampaignEffectSource.DoctrineResearchCommands or CampaignEffectSource.StrategicCommands or
                CampaignEffectSource.ArmyBattleDefeat) && !traitSource)continue;
            for(int index=0;index<batch.Requests.Length;index++)
            {
                var request=batch.Requests[index];
                //Monthly controller and map operations keep their source order.
                //The host must handle the head before showing a later line.
                if(traitSource && request.ArmyTraitViewRefresh is not null ||
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
        entry.Request.TalentCommandPresentation ?? (entry.Request.MonthlyMaintenance switch
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
