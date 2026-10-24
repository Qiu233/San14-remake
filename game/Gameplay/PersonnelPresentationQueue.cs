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
            if(batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.DiplomacyCommands or
                CampaignEffectSource.OfficerGrowthNotifications or CampaignEffectSource.OfficerOfficeCommands or
                CampaignEffectSource.PersonnelAdministrationCommands))continue;
            for(int index=0;index<batch.Requests.Length;index++)
            {
                var request=batch.Requests[index];
                if(request.TalentSceneryReward?.DiscoveryTextAtSource is not null||
                    request.OfficerGrowthFeedback?.Operation is OfficerGrowthFeedbackOperation.SoundCue or OfficerGrowthFeedbackOperation.Narration||
                    request.DiplomacyArrivalAudio is not null||request.TalentCommandPresentation?.Operation is
                    TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or
                    TalentCommandPresentationOperation.OfficerText)
                    return new(batch.Sequence,index,request,batch.Source);
            }
        }
        return null;
    }
}
