using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IFireAnnouncementRules
{
    private Func<ArmyReceiverId,ArmyFireViewSnapshot?>? armyFireViews;

    public void ConfigureArmyFireViews(Func<ArmyReceiverId,ArmyFireViewSnapshot?>? views)=>armyFireViews=views;

    internal GameState AppendArmyFireCastCurrent(GameState state,SourceWorldContext current,
        ArmyUnitReference army,int hex,long revision)
    {
        var (receiver,id)=ArmyFireReceiverCurrent(state,army);
        var view=armyFireViews?.Invoke(receiver)??throw ArmyFirePresentationMissing("source_view_owner");
        //2E41F0 attempts86 only when the Army's actual view exists. A
        //known absent view consumes no created-effect or playback receipt.
        if(!view.Present)return state;
        int officer=current.ReadArmyUnitCommanderId(army)??throw ArmyFirePresentationMissing("Commander12");
        var request=new CampaignEffectRequest(null,null){ArmyFireCast=new(id,officer,hex)
            {SourceSnapshot=new(receiver,view)}};
        return state with {CampaignEffectRequests=(state.CampaignEffectRequests??throw ArmyFirePresentationMissing("FIFO"))
            .Append(CampaignEffectSource.ArmyFirePresentation,revision,[request])};
    }

    private GameState AppendArmyFireAnnouncementCurrent(GameState state,SourceWorldContext current,
        ArmyUnitReference army,int officer,int? trait,bool fromCommand,NativeMapSeasonDate sourceDate,
        long revision,List<CampaignEffectRequest>? sink)
    {
        var (receiver,id)=ArmyFireReceiverCurrent(state,army);
        if(current.ReadOwnedPersonCommandManager() is null)
        {
            if(state.PersonnelCommands is null)throw ArmyFirePresentationMissing("Person_manager");
            PersonReturnCommands.BindCurrent(state,current);
        }
        int person=current.ReadPersonLookupReference(officer<=6000?officer:0)?.PhysicalSlot
            ??throw ArmyFirePresentationMissing("Person_pointer");
        int at=PersonReturnCommands.ResolvePersonPosition(state,current,person,sourceDate).PhysicalSlot??48400;
        var request=new CampaignEffectRequest(null,null){ArmyFireAnnouncement=new(id,officer,trait)
            {FromFireCommand=fromCommand,SourceSnapshot=new(receiver,at)}};
        state=state with {PersonnelCommands=current.ReadOwnedPersonCommandManager()
            ??throw ArmyFirePresentationMissing("Person_publication")};
        if(sink is not null){sink.Add(request);return state;}
        return state with {CampaignEffectRequests=(state.CampaignEffectRequests??throw ArmyFirePresentationMissing("FIFO"))
            .Append(CampaignEffectSource.ArmyFirePresentation,revision,[request])};
    }

    public FireAnnouncementPreparationResult PrepareFireAnnouncement(GameState state,ArmyFireAnnouncement request)
    {
        try
        {
            var snapshot=request.SourceSnapshot??throw ArmyFirePresentationMissing("legacy_source_snapshot");
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            //The retained source operands go to the one ordinary speech
            //queue. Admission can legitimately reject without creating text.
            int action=request.PersonalityId is null?6:28;
            return new(QueueArmyMapSpeechCurrent(state,current,request.UnitId,request.OfficerId,
                action,snapshot.SpeakerHexId,contextTraitId:request.PersonalityId,
                fireCommandOrigin:action==6?request.FromFireCommand:null),true);
        }
        catch(InvalidDataException error){return new(state,false,error.Message);}
    }

    private static (ArmyReceiverId Receiver,int UnitId) ArmyFireReceiverCurrent(GameState state,ArmyUnitReference army)
    {
        int physical=army.PhysicalSlot??throw ArmyFirePresentationMissing("Army_pointer");
        var life=state.ReadArmyLifecycle()??throw ArmyFirePresentationMissing("Army_lifecycle");
        var id=life.Slots[physical].ReceiverId;
        var receiver=life.Receivers.Find(id)??throw ArmyFirePresentationMissing("Army_receiver");
        return(id,receiver.ActiveUnitId??receiver.PendingImage?.Id??throw ArmyFirePresentationMissing("Army_identity"));
    }
    private static InvalidDataException ArmyFirePresentationMissing(string field)=>new("Army-fire.presentation."+field);
}
