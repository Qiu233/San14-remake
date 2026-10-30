using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal TroopsInformationValue ReadOriginalAiHexInformationCurrent(GameState state,
        SourceWorldContext current, HexReference hex, int mode)
    {
        int physical = HexFormationNeed(hex.PhysicalSlot, "Hex_identity");
        TroopsInformationValue Entity(WorldEntityKind kind, int? id) => id is int known
            ? new TroopsInformationEntity(new(kind, known)) : new HexInformationNull();
        switch (mode)
        {
            case 0: return new TroopsInformationInteger(physical);
            case 1:
            {
                int raw = HexFormationNeed(current.ReadHexAreaSlot(hex), "Hex_Area12");
                var at = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw HexFormationMissing("Hex_Area_table");
                return Entity(WorldEntityKind.Area, at.PhysicalSlot);
            }
            case 2:
            {
                int raw = HexFormationNeed(current.ReadHexForceSlot(hex), "Hex_Force14");
                var at = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw HexFormationMissing("Hex_Force_table");
                return Entity(WorldEntityKind.Force, at.PhysicalSlot);
            }
            case 3: return new TroopsInformationInteger(HexFormationNeed(current.ReadCaptureHexWord16(physical), "Hex_fire_duration16"));
            case 5:
            {
                BaseReference? selected = null; int minimum = int.MaxValue;
                foreach (var entry in current.ReadRegisteredBases() ?? throw HexFormationMissing("Hex_nearest_Base_registry80"))
                {
                    var at = entry ?? throw HexFormationMissing("Hex_nearest_Base_payload");
                    //22D880 uses the invalid-Object sentinel48400. It is not
                    //the22D640 terrain getter's different Hex0 fallback.
                    int distance = ArmyRouteSearch.Distance(physical, current.ReadBaseCurrentHexIndex(at));
                    if (distance < minimum) { selected = at; minimum = distance; }
                }
                if (selected is null || !HexFormationNeed(current.ReadBaseNativeValidity(selected), "Hex_nearest_Base_validity"))
                    return new TroopsInformationUnchanged();
                return new TroopsInformationEntity(AiForceInformationBaseEntity(current, selected));
            }
            case 6:
            {
                int raw = HexFormationNeed(current.ReadHexObjectLink(hex), "Hex_Object1A");
                var at = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw HexFormationMissing("Hex_Object_table");
                //The source returns the actual object without a validity gate.
                return Entity(WorldEntityKind.MapObject, at.PhysicalSlot);
            }
            default: throw HexFormationMissing("Hex_selector_" + mode);
        }
    }

    internal int ReadOriginalAiFormationInformationCurrent(GameState state, int formation, int? force, int mode)
    {
        if (mode == 5)
            return HexFormationNeed(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A), "Formation_minimum_troops9A");
        if (mode is < 0 or > 4) throw HexFormationMissing("Formation_selector_" + mode);
        int actualForce = force ?? throw HexFormationMissing("Formation_actual_Force");
        byte flags = ForeignTradeCurrentSource.ReadFlags(state, actualForce);
        int bytes = HexFormationNeed(TacticRemakeCurrentFormations.ReadVectorByteCount(state, formation), "Formation_parameter_vector");
        if (mode == 0)
        {
            int raw = HexFormationNeed(TacticRemakeCurrentFormations.ReadParameter(state, formation, 0x7F), "Formation_mobility7F");
            int value = (bytes & ~3) == 32 ? Math.Clamp(raw, 1, 30) : raw;
            if ((flags & 2) != 0 && formation == 5)
                value = unchecked(value + Parameter(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus));
            return value;
        }
        var kind = mode switch { 1 => BattleUnitArmyStatKind.FieldAttack, 2 => BattleUnitArmyStatKind.SiegeTroopAttack,
            3 => BattleUnitArmyStatKind.WallAttack, _ => BattleUnitArmyStatKind.Defense };
        int offset = mode switch { 1 => 0x83, 2 => 0x84, 3 => 0x85, _ => 0x86 };
        bool encodedBranch = (bytes & ~3) == 32;
        //The pure kernel ignores its static argument on the vector branch;
        //do not require an unconsumed legacy static byte merely to pass it.
        byte rawValue = encodedBranch ? (byte)0 : HexFormationNeed(
            TacticRemakeCurrentFormations.ReadRawByte(state, formation, offset), "Formation_static_coefficient");
        byte? encoded = encodedBranch ? TacticRemakeCurrentFormations.ReadParameter(state, formation, offset) : null;
        int result = BattleUnitSourceMath.FormationCoefficient(kind, bytes, rawValue, encoded);
        if ((flags & 2) != 0 && formation == 5 && mode is 2 or 3)
            result = unchecked(result + Parameter(mode == 2 ? SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus :
                SimulationParameterKeys.BattleAttributes.WallAbilityBonus));
        if ((flags & 0x20) != 0 && formation == 7 && mode == 4)
            result = unchecked(result + Parameter(SimulationParameterKeys.BattleAttributes.Formation7ForceFlagBonus));
        return result;

        int Parameter(string key) => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
            ? value : throw HexFormationMissing("parameter." + key);
    }

    private static T HexFormationNeed<T>(T? value, string field) where T : struct => value ?? throw HexFormationMissing(field);
    private static InvalidDataException HexFormationMissing(string field) => new("original_ai.hex_formation_information." + field);
}
