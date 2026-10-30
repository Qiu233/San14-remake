using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IFacilityArmySpeechRules
{
    /// <summary>Adopts the actual1B4AB0 packet into the shared1B4720 queue.
    /// Handled includes the real capacity/speaker/priority rejection branches;
    /// it does not assert that a balloon or voice has started.</summary>
    public FacilityArmySpeechPreparationResult PrepareFacilityArmySpeech(GameState state,CombatSpeechRequest request)
    {
        try
        {
            if(request.SpeakerKind!=CombatParticipantKind.Army||
                request.HexId is <0 or >48400||request.OfficerId is <0 or >6000)
                throw MapSpeechMissing("facility_source_packet");
            int action=request.Event switch
            {
                CombatSpeechEvent.TrapPitfall=>7,
                CombatSpeechEvent.TrapRockfall=>8,
                CombatSpeechEvent.ConfusionApplied=>23,
                CombatSpeechEvent.ImmobilizationApplied=>24,
                CombatSpeechEvent.TrapCountered=>32,
                CombatSpeechEvent.ArmyUnderFacilityAttack=>41,
                CombatSpeechEvent.ArmyFacilityConstructionStarted=>44,
                CombatSpeechEvent.ArmyFacilityCompleted=>45,
                _=>throw MapSpeechMissing("facility_source_event")
            };
            MapObjectReceiverId? facility=null;
            int? armySlot=null,definition=null;
            if(action is 44 or 45)
            {
                if(request is not {TargetKind:CombatParticipantKind.FieldObject,TargetId:int receiver,
                    FacilityArmySlot:int slot}||receiver<1||slot is <0 or >500)
                    throw MapSpeechMissing("facility_source_packet");
                facility=new(receiver);armySlot=slot;
            }
            else if(action==32)
            {
                definition=request.TrapDefinitionId;
                if(definition is null or <0 or >30)throw MapSpeechMissing("trap_countered_definition");
            }
            else if(action==41)
            {
                definition=request.FacilityDefinitionId;
                if(definition is not (5 or 6 or 15))throw MapSpeechMissing("facility_attack_definition");
            }
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            return new(QueueArmyMapSpeechCurrent(state,current,request.SpeakerId,request.OfficerId,action,
                request.HexId,facilityReceiver:facility,facilityArmySlot:armySlot,facilityDefinitionId:definition),true);
        }
        catch(InvalidDataException error) {return new(state,false,error.Message);}
    }

    //1B16D0 reads Object packet18 at actual Start. A known invalid Object
    //returns the empty line before formatting or selecting its voice.
    private HistoryMessageOperands? ReadFacilityArmySpeechOperands(GameState state,SourceWorldContext current,
        ArmyMapSpeechEntry entry)
    {
        var receiver=entry.ContextFacilityReceiverId??throw MapSpeechMissing("facility_Object_operand");
        int slot=entry.ContextFacilityArmySlot??throw MapSpeechMissing("facility_Army_operand");
        var directory=current.CurrentMapObjectDirectory??throw MapSpeechMissing("facility_Object_directory");
        var obj=new ObjectReference(directory.PhysicalOf(receiver));
        if(!MapSpeechNeed(current.ReadObjectNativeValidity(obj),"facility_Object_validity"))return null;
        return new(Army0:new(slot),Person0:entry.OfficerId,String0:ReadFacilityArrivalObjectName(state,current,obj));
    }
}
