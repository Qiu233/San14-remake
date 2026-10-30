using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal DamagePresentationSnapshot CaptureArmyDamagePresentationCurrent(GameState state,
        SourceWorldContext current,ArmyUnitReference army,bool gross)
    {
        int slot=army.PhysicalSlot??throw DamagePresentationMissing("Army_pointer");
        var life=state.ReadArmyLifecycle()??throw DamagePresentationMissing("Army_lifecycle");
        var receiver=life.Slots[slot].ReceiverId;
        int hex=current.ReadArmyUnitRawHex2A(army)??throw DamagePresentationMissing("Army_Hex");
        //43D8E0 and43E740 supply byte0 to1A3F60. Trap's Army4D
        //is a different caller and must not replace this actual argument.
        return new(receiver,null,hex,gross?(sbyte)0:null);
    }

    internal DamagePresentationSnapshot CaptureBaseDamagePresentationCurrent(GameState state,
        SourceWorldContext current,BaseReference target)
    {
        int link=current.ReadBaseObjectLink(target)??throw DamagePresentationMissing("Base_Object4E");
        var directory=current.CurrentMapObjectDirectory??throw DamagePresentationMissing("Base_receiver_directory");
        return new(null,directory.ReceiverAtPhysical(link),current.ReadBaseCurrentHexIndex(target),0);
    }

    private static InvalidDataException DamagePresentationMissing(string field)=>new("damage.presentation."+field);
}
