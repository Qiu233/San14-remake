using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //0C22C0 query110 ->0AA000: source literal0..4 maps to District17=1..5.
    internal bool ReadOriginalAiConditionDistrictPolicyCurrent(SourceWorldContext current, int district, uint literal)
    {
        if (!ConditionFamilyNeed(current.ReadDistrictNativeValidity(district), "District_validity")) return false;
        return literal <= 4 && ConditionFamilyNeed(current.ReadDistrictRawByte(district, 0x17), "District_policy17") == literal + 1;
    }

    //Query124 ->0ABF50. Definition literals select actual table pointers;
    //the facility kind is compared with that pointer's physical ordinal.
    internal bool ReadOriginalAiConditionHexFacilityCurrent(SourceWorldContext current, OriginalAiHex hex, uint literal)
    {
        int slot = literal switch
        {
            <= 8 => (int)literal + 3,
            9 => 12, 10 => 14, 11 => 13,
            >= 12 and <= 14 => (int)literal + 3,
            _ => -1
        };
        if (slot < 0) return false; //0BA120's known null mapping.
        var definition = current.ReadObjectDefinitionReference(slot) ?? throw ConditionFamilyMissing("facility_definition_pointer");
        if (!ConditionFamilyNeed(current.ReadObjectDefinitionValidity(definition), "facility_definition_validity")) return false;
        var at = current.ReadHexTableReference(hex.Id) ?? throw ConditionFamilyMissing("facility_Hex_pointer");
        int raw = ConditionFamilyNeed(current.ReadHexObjectLink(at), "facility_Hex_Object1A");
        var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw ConditionFamilyMissing("facility_Object_pointer");
        if (!ConditionFamilyNeed(current.ReadObjectNativeValidity(obj), "facility_Object_validity")) return false;
        var fields = current.ReadPhysicalObject(ConditionFamilyNeed(obj.PhysicalSlot, "facility_Object_identity"))
            ?? throw ConditionFamilyMissing("facility_Object_fields");
        return fields.RawField12 == ConditionFamilyNeed(definition.PhysicalSlot, "facility_definition_identity");
    }

    //Query129 ->0ADB00. The source does not insert an Army-validity gate.
    //22B2D0 returns the selected raw byte, while22DE60 returns the literal
    //definition's physical ordinal. Do not resolve the selected byte twice.
    internal bool ReadOriginalAiConditionArmyEffectiveFormationCurrent(GameState state,
        SourceWorldContext current, int unitId, uint literal)
    {
        int slot = literal switch
        {
            >= 1 and <= 5 => (int)literal,
            7 => 6, 8 => 7, 9 => 11, 10 => 12, 11 => 13,
            12 => 8, 13 => 9, 14 => 10, 15 => 19, 16 => 20,
            _ => -1
        };
        if (slot < 0) return false; //0BCC60 returns a known null pointer.
        var definition = current.ReadStaticTableReference(0x77428, slot) ?? throw ConditionFamilyMissing("formation_literal_pointer");
        if (definition.PhysicalSlot is not int expected) return false;
        var unit = state.Units.GetValueOrDefault(unitId) ?? throw ConditionFamilyMissing("formation_Army_owner");
        int physical = ConditionFamilyNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "formation_Army_receiver");
        int rawHex = ConditionFamilyNeed(current.ReadArmyUnitRawHex2A(new(physical)), "formation_Army2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw ConditionFamilyMissing("formation_Hex_pointer");
        int rawTerrain = ConditionFamilyNeed(current.ReadHexSurfaceCode(hex), "formation_Hex_surface10");
        var terrain = current.ReadSurfaceTypeTableReference(rawTerrain < 31 ? rawTerrain : 0)
            ?? throw ConditionFamilyMissing("formation_Terrain_pointer");
        bool naval = terrain.PhysicalSlot is int terrainId && terrainId != 0 &&
            (ConditionFamilyNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainId), "formation_Terrain_flags2C") & 2) != 0;
        int selected = ConditionFamilyNeed(naval ? ArmyConfigurationSourceAdapter.ReadNaval(state, unit)
            : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "formation_selected_byte");
        return selected == expected;
    }

    //0BE7E0's CTroops operand uses strict22E300, not the different
    //representative-Army query's fallback to the first registered member.
    internal int ReadOriginalAiConditionTroopsHexCurrent(GameState state, SourceWorldContext current,
        OriginalAiTroopsReference group)
    {
        if (!ReadOriginalAiTroopsValidityCurrent(current, group)) return 48400;
        var groups = state.ReadArmyLifecycle()?.ArmyGroups ?? throw ConditionFamilyMissing("Troops_owner");
        var leader = groups.GetLeader(group.GroupId);
        if (!leader.IsKnown) throw ConditionFamilyMissing("Troops_leader2C");
        int person = leader.Officer.Kind switch
        {
            ArmyGroupOfficerKind.Officer when leader.Officer.OfficerId is int id => ConditionFamilyPerson(current, id),
            ArmyGroupOfficerKind.DefaultOfficer => ConditionFamilyPerson(current, 0),
            ArmyGroupOfficerKind.NoOfficer => ConditionFamilyNeed(current.ReadTransferPersonTableReference(0)?.PhysicalSlot,
                "Troops_default_Person_pointer"),
            _ => throw ConditionFamilyMissing("Troops_leader_identity")
        };
        var position = PersonPositionRepair.DecodeSecondLocation(ConditionFamilyNeed(current.ReadPersonSecondLocation(person), "Troops_leaderE4"));
        int lookup = position.Kind == 27 && position.Index < 501 ? position.Index : 0;
        var army = current.ReadArmyUnitTableReference(lookup) ?? throw ConditionFamilyMissing("Troops_Army_pointer");
        if (ConditionFamilyNeed(current.ReadArmyUnitCommanderId(army), "Troops_Army_Commander12") !=
            ConditionFamilyNeed(current.ReadPersonIdentity(person), "Troops_leader_identity10"))
            army = current.ReadArmyUnitTableReference(0) ?? throw ConditionFamilyMissing("Troops_Army_placeholder");
        return ConditionFamilyNeed(current.ReadArmyUnitRawHex2A(army), "Troops_Army_hex2A");
    }

    internal int ReadOriginalAiConditionEntityHexCurrent(GameState state, SourceWorldContext current,
        OriginalAiEntity at, uint resolver)
    {
        switch (resolver)
        {
            case 0:
            {
                var force = current.ReadForceTableReference(at.Id < 52 ? at.Id : 0) ?? throw ConditionFamilyMissing("Force_pointer");
                if (!ConditionFamilyNeed(current.ReadForceNativeValidity(force), "Force_validity")) return 48400;
                int ruler = current.ReadForceRecord(ConditionFamilyNeed(force.PhysicalSlot, "Force_identity"))?.RawRuler10
                    ?? throw ConditionFamilyMissing("Force_ruler10");
                return Home(ConditionFamilyPerson(current, ruler));
            }
            case 1:
            {
                int district = ConditionFamilyNeed(current.ReadDistrictTableReference(at.Id < 52 ? at.Id : 0)?.PhysicalSlot, "District_pointer");
                if (!ConditionFamilyNeed(current.ReadDistrictNativeValidity(district), "District_validity")) return 48400;
                return Home(ConditionFamilyPerson(current, ConditionFamilyNeed(current.ReadDistrictLeader(district), "District_leader12")));
            }
            case 3:
            {
                var unit = state.Units.GetValueOrDefault(at.Id) ?? throw ConditionFamilyMissing("Army_owner");
                var army = new ArmyUnitReference(ConditionFamilyNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver"));
                return ConditionFamilyNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity")
                    ? ConditionFamilyNeed(current.ReadArmyUnitRawHex2A(army), "Army2A") : 48400;
            }
            case 4:
            {
                int person = ConditionFamilyPerson(current, at.Id);
                if (!ConditionFamilyNeed(current.ReadPersonNativeValidity(person), "Person_validity")) return 48400;
                var hex = PersonReturnCommands.ResolvePersonPosition(state, current, person,
                    () => state.NativeMapSeason?.ThirdDate ?? throw ConditionFamilyMissing("Person_journey_date"));
                //A known null Person position is not Hex0. The native
                //pointer-subtraction helper has no domain Hex ordinal for it.
                return hex.PhysicalSlot ?? throw ConditionFamilyMissing("Person_null_position_Hex_ordinal");
            }
            case 5 or 8 or 9:
            {
                var home = BaseLookup.Find(at.Id, current);
                return ConditionFamilyNeed(current.ReadBaseNativeValidity(home), "Base_validity") ? current.ReadBaseCurrentHexIndex(home) : 48400;
            }
            case 6:
            {
                var area = current.ReadAreaTableReference(at.Id < 501 ? at.Id : 0) ?? throw ConditionFamilyMissing("Area_pointer");
                return ConditionFamilyNeed(current.ReadAreaNativeValidity(area), "Area_validity")
                    ? ConditionFamilyNeed(current.ReadAreaHexIndex(area), "Area3A") : 48400;
            }
            case 10:
            {
                var obj = current.ReadObjectTableReference(at.Id < 3001 ? at.Id : 0) ?? throw ConditionFamilyMissing("Object_pointer");
                if (!ConditionFamilyNeed(current.ReadObjectNativeValidity(obj), "Object_validity")) return 48400;
                return current.ReadPhysicalObject(ConditionFamilyNeed(obj.PhysicalSlot, "Object_identity"))?.RawField10
                    ?? throw ConditionFamilyMissing("Object_hex10");
            }
            case 18:
            {
                var scenery = current.ReadSceneryTableReference(at.Id < 101 ? at.Id : 0) ?? throw ConditionFamilyMissing("Scenery_pointer");
                return ConditionFamilyNeed(current.ReadSceneryNativeValidity(scenery), "Scenery_validity")
                    ? ConditionFamilyNeed(current.ReadSceneryHexIndex(scenery), "Scenery44") : 48400;
            }
            default: return 48400;
        }
        int Home(int person) => current.ReadBaseCurrentHexIndex(BaseLookup.Find(
            ConditionFamilyNeed(current.ReadPersonFirstLocation(person), "leader_homeE2"), current));
    }

    private static int ConditionFamilyPerson(SourceWorldContext current, int identity) => ConditionFamilyNeed(
        (identity is >= 0 and <= 6000 ? current.ReadPersonLookupReference(identity)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup_pointer");
    private static T ConditionFamilyNeed<T>(T? value, string field) where T : struct => value ?? throw ConditionFamilyMissing(field);
    private static InvalidDataException ConditionFamilyMissing(string field) => new("original-ai.condition-family." + field);
}
