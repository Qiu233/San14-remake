using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    //Ordinary source coefficients are independent of trap class4. These
    //normal immutable rules were extracted offline; saved parameter overrides
    //retain their own keys rather than aliasing the trap settings.
    private const string FacilityObjectRelationCeiling = "facility-object:relation-ceiling";
    private const string FacilityObjectRelationAdjustment = "facility-object:relation-adjustment";

    private int OrdinaryFacilityRelationParameter(string key)
    {
        var bank=State.NativeArmySimulation?.Parameters??throw Missing("ordinary_relation_parameters");
        if(bank.Float32Values.ContainsKey(key))throw Missing("ordinary_relation_parameter_type");
        if(bank.Int32Values.TryGetValue(key,out int value))return value;
        return key switch{FacilityObjectRelationCeiling=>15,FacilityObjectRelationAdjustment=>-5,
            _=>Read(State,key)};
    }

    private int FacilitySourcePanelDamage(ObjectReference facility, int basisDamage)
    {
        //22C250 reads activation/suppression and the current panel signed36.
        //It has no additional native Force-validity gate.
        int raw = Need(Current.ReadObjectForceSlot13(facility), "damage_panel_source_Force13");
        var force = Current.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw Missing("damage_panel_source_Force_pointer");
        int panel = Need(Current.ReadBaseForceModifier(force, 61), "damage_source_Force_panel61");
        return unchecked(panel * basisDamage) / 100;
    }

    private int FacilityTargetPersonDamage(ArmyUnitReference army, int basisDamage)
    {
        if (!Need(Current.ReadArmyUnitNativeValidity(army), "damage_person112_Army_validity")) return 0;
        int person = FacilityTargetCommander(army);
        if (!Need(TacticRemakeCurrentTraits.CommanderHasEffect(Current, person, 112),
                "damage_target_Person_has112")) return 0;
        //040080 is called again after2C5830. PersonContribution supplies
        //2B3170's own native validity and nine selected personality sum;
        //neither Army36 nor retained activation1BC participates.
        person = FacilityTargetCommander(army);
        int contribution = Need(TacticRemakeCurrentTraits.PersonContribution(Current, person, 112),
            "damage_target_Person_sum112");
        return unchecked(contribution * basisDamage) / 100;
    }

    private int FacilityTargetCommander(ArmyUnitReference army)
    {
        int raw = Need(Current.ReadArmyUnitCommanderId(army), "damage_person112_Commander12");
        return (raw <= 6000 ? Current.ReadPersonLookupReference(raw) : Current.ReadTransferPersonTableReference(0))
            ?.PhysicalSlot ?? throw Missing("damage_person112_Commander_pointer");
    }
}
