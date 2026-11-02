namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Receipts only the exact source-head cast accepted by the real
    /// host, including its source-known absent view. Speech owns preparation.</summary>
    public bool TryCompleteFirePresentation(GameState expectedOwner,CampaignEffectSource source,
        long batchSequence,int requestIndex,CampaignEffectRequest expectedRequest)
    {
        if(source!=CampaignEffectSource.ArmyFirePresentation||
            !TryReadSourcePresentationHead(expectedOwner,source,batchSequence,requestIndex,
                out var owner,out var pending,out var effects,out int batch,out var request)||
            !ReferenceEquals(request,expectedRequest)||request.ArmyFireCast?.SourceSnapshot is null)return false;
        CompleteSourcePresentationHead(owner,pending,effects,batch,requestIndex,
            owner.ActiveMonthlyScene,owner.CoalitionCameraPresentation);
        return true;
    }

    /// <summary>Source admission and insertion into the real shared Army speech
    /// queue complete atomically with removing this exact pending announcement.</summary>
    public bool TryPrepareFireAnnouncement(GameState expectedOwner,CampaignEffectSource source,
        long batchSequence,int requestIndex,CampaignEffectRequest expectedRequest,
        IFireAnnouncementRules rules,out FireAnnouncementPreparationResult? result,
        CampaignAppRandomState? appRandomSnapshot=null)
    {
        result=null;
        if(!FirePresentationValidation.AnnouncementSource(source)||
            !TryReadSourcePresentationHead(expectedOwner,source,batchSequence,requestIndex,
                out var owner,out var pending,out var effects,out int batch,out var request)||
            !ReferenceEquals(request,expectedRequest)||request.ArmyFireAnnouncement is not {SourceSnapshot:not null} announcement)return false;
        ArgumentNullException.ThrowIfNull(rules);
        var working=pending is null&&appRandomSnapshot is not null
            ?owner with {CampaignAppRandom32=appRandomSnapshot}:owner;
        result=rules.PrepareFireAnnouncement(working,announcement);
        if(!result.Handled||result.UnresolvedOwner is not null)return false;
        CompleteSourcePresentationHead(result.State,pending,effects,batch,requestIndex,
            result.State.ActiveMonthlyScene,result.State.CoalitionCameraPresentation);
        result=result with {State=SourcePresentationOwner};
        return true;
    }

    public bool TryCompleteDamagePresentation(GameState expectedOwner,CampaignEffectSource source,
        long batchSequence,int requestIndex,CampaignEffectRequest expectedRequest)
    {
        if(source is not (CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage)||
            !TryReadSourcePresentationHead(expectedOwner,source,batchSequence,requestIndex,
                out var owner,out var pending,out var effects,out int batch,out var request)||
            !ReferenceEquals(request,expectedRequest))return false;
        bool known=source==CampaignEffectSource.FireDamage
            ?request.FireDamagePresentation?.SourceSnapshot is not null
            :request.TerrainDamagePresentation?.SourceSnapshot is not null;
        if(!known)return false;
        CompleteSourcePresentationHead(owner,pending,effects,batch,requestIndex,
            owner.ActiveMonthlyScene,owner.CoalitionCameraPresentation);
        return true;
    }
}
