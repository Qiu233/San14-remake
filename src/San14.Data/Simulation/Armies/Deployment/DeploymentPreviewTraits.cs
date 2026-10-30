using San14.Core;

namespace San14.Data;

public sealed record DeploymentPreviewTraitInputs(int CommanderPhysicalSlot, int Troops,
    int LandFormationId, int HexId, int NativeForceSlot, byte Status = 1);
public sealed record DeploymentPreviewTraitEffects(IReadOnlyList<int> Effects);

/// <summary>CE26A740's preview receiver: reset cache36, condition evaluation
/// enabled and a temporary, unregistered commander receiver. The caller keeps
/// its actual reset status (ordinary1 or transport2).
/// This query never reads the commander's saved activation mask.</summary>
public static class DeploymentPreviewTraits
{
    public static DeploymentPreviewTraitEffects Read(GameState state, SourceWorldContext current,
        DeploymentPreviewTraitInputs input)
    {
        var values = new int[127];
        var zero = current.ReadPersonalityTableReference(0) ?? throw Missing("cache_zero_pointer");
        if (Need(current.ReadSkillPersonalityValidity(zero), "cache_zero_validity"))
            for (int index = 0; index < 36; index++) Add(zero);
        for (int index = 0; index < 9; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, input.CommanderPhysicalSlot, index)
                ?? throw Missing("commander_personality_pointer");
            if (!Need(current.ReadSkillPersonalityValidity(trait), "commander_personality_validity")) continue;
            ushort condition = Need(current.ReadPersonalityWord(trait, 0xB4), "condition");
            if (condition == ushort.MaxValue || AcceptsSelf(trait)) Add(trait);
        }
        return new(Array.AsReadOnly(values));

        void Add(PersonalityReference trait)
        {
            for (int pair = 0; pair < 2; pair++)
            {
                byte code = Need(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "effect");
                int amount = Math.Clamp((int)unchecked((short)Need(current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "amount")), -100, 2000);
                if (code < values.Length) values[code] = unchecked(values[code] + amount);
            }
        }
        bool AcceptsSelf(PersonalityReference trait)
        {
            byte category = Need(current.ReadPersonalityByte(trait, 0xA4), "condition_category");
            switch (category)
            {
                case 1: return input.Status == 1;
                case 11: case 36: return true;
                case 13: return input.Status == 2;
                //Forced26.bit2, reset abnormal counters and reset37=0.
                case 4: case 5: case 6: case 7: case 8: case 25: case 41: return false;
                //27E620 adds only manager-table targets. This unregistered
                //preview receiver cannot occur in the final target list.
                case 2: case 3: case 9: case 10: case 21: case 22: case 23: case 24: return false;
                case 20:
                    uint parameter = 0;
                    for (int index = 0; index < 4; index++)
                        parameter |= (uint)Need(current.ReadPersonalityByte(trait, 0xA8 + index), "condition_parameter") << (index * 8);
                    return input.Troops <= unchecked((int)parameter);
                case 12: return (FormationFlags() & 2) != 0;
                case 14: return TerrainCategory() == 4;
                case 15: return TerrainCategory() == 3;
                case 16: return TerrainCategory() == 2;
                case 17: return TerrainCategory() is 3 or 5;
                case 18: return TerrainCategory() == 6;
                case 19: return TerrainCategory() != 4;
                case 28: return SurfaceCode() is >= 15 and <= 18;
                case 34:
                    var hex = Hex();
                    ushort raw = Need(current.ReadHexAreaSlot(hex), "condition_area");
                    var area = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("condition_area_pointer");
                    return Need(current.ReadAreaCurrentForceSlot(area), "condition_area_force") == input.NativeForceSlot;
                case 37: return input.Status == 1 && TerrainCategory() != 4 && (FormationFlags() & 2) == 0;
                case 26: case 31: case 32: case 38: throw Missing("nearby_condition_" + category);
                default: return false; //The original101-slot dispatch is null here.
            }
        }
        HexReference Hex() => current.ReadHexTableReference(input.HexId) ?? throw Missing("condition_hex_pointer");
        byte SurfaceCode() => Need(current.ReadHexSurfaceCode(Hex()), "condition_terrain_code");
        byte TerrainCategory()
        {
            byte code = SurfaceCode();
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("condition_terrain_pointer");
            return Need(current.ReadSurfaceTypeWoundedCategory42(terrain), "condition_terrain_category");
        }
        byte FormationFlags() => Need(TacticRemakeCurrentFormations.ReadParameter(state, input.LandFormationId, 0xA6), "condition_formation_flags");
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("deployment.figures.preview_traits." + field);
}
