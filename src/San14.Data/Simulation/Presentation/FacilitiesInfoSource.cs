using San14.Core;

namespace San14.Data;

/// <summary>Current Object fields used by its ordinary information layout.
/// The renderer separately owns the accepted SourceUnit position and lifetime.</summary>
public sealed record FacilityMapInfo(int ReceiverId,int DefinitionKind,int DefinitionCategory,
    int HexId,string Name,int? ForceColor49,int Durability,int MaximumDurability,bool Activated,
    ArmySimpleOutlineSelection? Outline);

public sealed record FacilityMapIconNames(string Fill,string Outline);

/// <summary>The source Object type-to-common-sprite mapping. These resource
/// names retain the original spellings, including the tpap and outlinei names.</summary>
public static class FacilitiesInfoSource
{
    public static FacilityMapIconNames IconNames(int kind,bool useOutline)
    {
        string prefix=kind switch
        {
            3=>"000_cmn_icon_facility_00jin",4=>"000_cmn_icon_facility_01toride",
            5=>"000_cmn_icon_facility_02yumiyagura",6=>"000_cmn_icon_facility_03touseki",
            7=>"000_cmn_icon_facility_04gungaku",8=>"000_cmn_icon_facility_05sekihei",
            11=>"000_cmn_icon_tpap_00hiwana",12=>"000_cmn_icon_tpap_01mouka",
            13=>"000_cmn_icon_tpap_02kasen",14=>"000_cmn_icon_trap_03ana",
            15 or 17=>"",9 or 10 or 16=>throw new InvalidDataException("facility.info.zero_icon_enum"),
            _=>"000_cmn_icon_facility_00jin"
        };
        if(kind is 15 or 17)
        {
            string name=kind==15?"doyagura":"rakuseki";
            return new("000_cmn_icon_bt_"+(kind==15?"12":"13")+name,
                useOutline?"000_cmn_icon_facility_"+name+"_fuchi":"000_cmn_icon_trap_"+name+"_base");
        }
        if(kind is <3 or >17||kind is 1 or 2)return new(prefix,prefix);
        return new(prefix,prefix+(useOutline?(kind==6?"_outlinei":"_outline"):"_base"));
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<FacilityMapInfo> QueryFacilityMapInfo(GameState world,int receiverId)
    {
        try
        {
            var current=FacilitySceneCurrent(world);
            var directory=current.CurrentMapObjectDirectory
                ?? throw new InvalidDataException("facility.info.Object_directory");
            var obj=new ObjectReference(directory.PhysicalOf(new(receiverId)));
            if(!InfoNeed(current.ReadObjectNativeValidity(obj),"Object_validity"))
                return new(null,null); // The native notification is an actual no-op.
            var fields=current.ReadPhysicalObject(obj.PhysicalSlot!.Value)
                ?? throw new InvalidDataException("facility.info.Object_fields");
            var definition=FacilityConstructionLegality.DefinitionOf(current,obj);
            bool valid=InfoNeed(current.ReadObjectDefinitionValidity(definition),"definition_validity");
            int category=InfoNeed(current.ReadObjectDefinitionRaw2C(definition),"definition_category");
            int? color=null;ArmySimpleOutlineSelection? outline=null;
            if(category!=3)
            {
                byte rawForce=InfoNeed(current.ReadObjectForceSlot13(obj),"Object_Force13");
                var force=current.ReadForceTableReference(rawForce<52?rawForce:0)
                    ?? throw new InvalidDataException("facility.info.Force_pointer");
                color=InfoNeed(current.ReadForceReportColor(force),"Force_Color49");
                var selected=ArmySimpleOutlineSource.ReadConfiguredSceneryMap(world,current,
                    ForcePrimaryDistrictSource.Resolve(current,force));
                outline=selected.Selection??throw new InvalidDataException(selected.Missing??"facility.info.current_outline");
            }
            int capacity=valid?InfoNeed(FacilityRepairCapacity.ReadCapacity(world,current,obj,_baseCapacityInputs),
                "current_maximum_durability"):9999;
            return new(new(receiverId,fields.RawField12,category,
                InfoNeed(current.ReadObjectHexIndex(obj),"Object_Hex"),
                ReadFacilityArrivalObjectName(world,current,obj),color,
                InfoNeed(current.ReadObjectRawField14(obj),"Object_durability"),capacity,
                (InfoNeed(current.ReadMapObjectFields16(obj),"Object_activation")&1)!=0,outline),null);
        }
        catch(InvalidDataException error) { return new(null,error.Message); }

        static T InfoNeed<T>(T? value,string field) where T:struct => value
            ?? throw new InvalidDataException("facility.info."+field);
    }
}
