using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyCommandSpeechRules
{
    /// <summary>Adopts the four ordinary command packets into the one shared
    /// map-speech manager. A known admission refusal is handled; this is not a
    /// receipt that a balloon or voice has already started.</summary>
    public ArmyCommandSpeechPreparationResult PrepareArmyCommandSpeech(GameState state,CombatSpeechRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(request);
        try
        {
            if(request.SpeakerKind!=CombatParticipantKind.Army||request.SpeakerId<=0||
                request.OfficerId is <0 or >6000||request.HexId is <0 or >48400)
                throw MapSpeechMissing("command_source_packet");
            int action=request.Event switch
            {
                CombatSpeechEvent.ArmyOrderFinished=>60,
                CombatSpeechEvent.ArmyHoldingPosition=>61,
                CombatSpeechEvent.ArmyDestinationReachedHolding=>47,
                CombatSpeechEvent.ArmyEnteredBase=>46,
                _=>throw MapSpeechMissing("command_source_event")
            };
            int hex=request.HexId;int? location=null;
            if(action==46)
            {
                var arrival=request.BaseArrival??throw MapSpeechMissing("command_arrival_context");
                if(arrival.GoalHexId is <0 or >=48400||arrival.LocationId is <0 or >61)
                    throw MapSpeechMissing("command_arrival_packet");
                hex=arrival.GoalHexId;location=arrival.LocationId;
            }
            else if(request.BaseArrival is not null)throw MapSpeechMissing("command_unexpected_arrival_context");
            //1B4AB0 already checked the source Army at the producer. Do not
            //repeat that gate against an Army retired by a later native stage.
            return new(QueueArmyMapSpeechCurrent(state,null,request.SpeakerId,request.OfficerId,
                action,hex,contextArgument:location,commandOwnedLayout:request.CommandOwnedLayout),true);
        }
        catch(InvalidDataException error){return new(state,false,error.Message);}
    }

    //1AF720 runs in the source operation, before later stages can change the
    //Person. A present snapshot with no image records its known-invalid branch.
    private ArmyCommandSpeechLayoutSnapshot CaptureArmyCommandSpeechLayoutCurrent(GameState state,
        SourceWorldContext current,int officerId)
    {
        var image=CaptureBaseMapSpeechSpeakerCurrent(state,current,officerId);
        return new(image?.AppearanceIndex,image?.ForceColor);
    }
}
