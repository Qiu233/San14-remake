using San14.Core;

namespace San14.Data;

public sealed record OriginalTransportArmyRouteMobilityReceipt(float Value, int FormationPhysical,
    int ForcePhysical, int CommanderPhysical, byte CurrentHexSurface10, byte ExplicitHexSurface10,
    int Coefficient, int FormationPercent, int UnconditionalTrait94, float TraitFactor,
    float BaselineTerrainFactor, float FormationTerrainFactor, float ForceTerrainFactor,
    float EnvironmentFactor, int? PolicyIndex, int PolicyValue, bool AdvancedScalingApplied,
    OriginalForcePolicyScratchReceipt? FormationPolicyQuery);

public sealed record OriginalTransportArmyRouteMobilityResult(float? Value,
    OriginalTransportArmyRouteMobilityReceipt? Receipt, string? UnresolvedOwner)
{ public bool Complete => Value.HasValue; }

/// <summary>TC2B0660(Army,explicitFormation,0,0,CHex*) for ordinary type2.
/// Initial route heuristic supplies the source-created scratch surface2;
/// subsequent steps supply their actual candidate Hex10. Current Army Hex
/// still supplies the independent Force/policy water gates. The caller owns
/// this writable context: unknown after a policy query requires discarding
/// its operation draft, including any reached primary-pool changes.</summary>
public static class OriginalTransportArmyRouteMobilitySource
{
    public static OriginalTransportArmyRouteMobilityResult Read(GameState state,
        OriginalEventInitializationContext current, UnitState army, int formationPhysical,
        byte explicitHexSurface10)
    {
        try
        {
            int slot = K(army.NativeArmyOrigin?.NativeSlotIndex, "Army_physical_slot");
            var receiver = new OriginalArmyUnitReference(slot);
            if (!K(current.ReadArmyUnitNativeValidity(receiver), "Army_validity")) return new(0, null, null);
            if (K(current.ReadArmyReturnUnitStatus10(receiver), "Army10") != 2)
                throw Missing("branch.non_type2");
            if (formationPhysical is < 1 or > 20) throw Missing("branch.nonordinary_Formation");
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("parameter_bank");
            if (parameters.Int32Values.GetValueOrDefault(OriginalTacticRemakeCurrentBattleControl.VersionKey) != 1)
                throw Missing("CTutorial_owner");
            if (!parameters.Int32Values.TryGetValue(OriginalTacticRemakeCurrentBattleControl.HeaderKey, out int tutorial))
                throw Missing("CTutorial_header0");
            if (tutorial != 0) throw Missing("active_tutorial.4C6ED0");
            int commanderId = K(current.ReadArmyUnitCommanderId(receiver), "Army12");
            int commander = (current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("Person_table")).PhysicalSlot ?? throw Missing("Person_pointer");
            if (!K(current.ReadPersonNativeValidity(commander), "Person_validity"))
                throw Missing("branch.invalid_commander");
            byte rawForce = K(current.ReadPersonOldForceSlot(commander), "22CD80_Force");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("Force_table");
            int forcePhysical = force.PhysicalSlot ?? throw Missing("Force_pointer");
            byte flags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forcePhysical)?.Flags193
                ?? throw Missing("Force193");
            int size = K(OriginalTacticRemakeCurrentFormations.ReadVectorByteCount(state, formationPhysical), "Formation_A8_size");
            byte rawCoefficient = K(OriginalTacticRemakeCurrentFormations.ReadParameter(state, formationPhysical, 0x7F), "Formation7F");
            int coefficient = (size & ~3) == 32 ? Math.Clamp((int)rawCoefficient, 1, 30) : rawCoefficient;
            if ((flags & 2) != 0 && formationPhysical == 5) coefficient = unchecked(coefficient + P(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus));

            int percent = 100;
            OriginalForcePolicyScratchReceipt? policyQuery = null;
            if (K(current.ReadForceNativeValidity(force), "Force_validity") && forcePhysical is < 46 && formationPhysical is not (>= 14 and <= 16))
            {
                int effect = formationPhysical <= 7 ? formationPhysical : formationPhysical <= 10 ? 8 : formationPhysical <= 13 ? 9 : 12;
                policyQuery = OriginalForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true);
                int scale = P(OriginalBattleRemakeCurrentParameterSeed.FormationEffectKey(effect)!);
                percent = unchecked(100 + unchecked(policyQuery.Value * scale));
            }
            int trait = 0;
            for (int index = 0; index < 9; index++)
            {
                var personality = OriginalTacticRemakeCurrentTraits.ResolveCommanderPersonality(current, commander, index)
                    ?? throw Missing("230730_Personality_pointer");
                if (!K(current.ReadSkillPersonalityValidity(personality), "Personality_validity")) continue;
                if (K(current.ReadPersonalityWord(personality, 0xB4), "PersonalityB4") != ushort.MaxValue) continue;
                for (int pair = 0; pair < 2; pair++)
                {
                    if (K(current.ReadPersonalityByte(personality, pair == 0 ? 0xB6 : 0xBA), "Personality_effect") != 94) continue;
                    short raw = unchecked((short)K(current.ReadPersonalityWord(personality, pair == 0 ? 0xB8 : 0xBC), "Personality_value"));
                    trait = unchecked(trait + Math.Clamp((int)raw, -100, 2000));
                }
            }
            int limit = P(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit), step = P(SimulationParameterKeys.ArmyMobility.TraitMobilityStep), lower = unchecked(-limit);
            int capped = trait < lower ? lower : trait > limit ? limit : trait;
            float traitFactor = (float)unchecked(step * capped) / 100f + 1f;
            int selector = K(OriginalBattleRemakeCurrentSettings.ReadSelectorE58(state), "EnvironmentE58");
            var baseline = current.ReadSurfaceTypeTableReference(1) ?? throw Missing("Terrain_table1");
            float baselineFactor = 1f;
            if (baseline.PhysicalSlot is int terrainPhysical && terrainPhysical != 0)
            {
                int cost = K(current.ReadSurfaceTypeRawField2A(baseline), "Terrain1_word2A");
                if (selector == 0 && (terrainPhysical is >= 3 and <= 14 or 24))
                    cost = Math.Clamp(unchecked(K(OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26), "advanced26") * cost) / 100, 225, 999);
                if (cost > 0) baselineFactor = 240f / (float)cost;
            }
            float formationTerrain = 1f;
            if (formationPhysical is 6 or 14 && explicitHexSurface10 is >= 5 and < 12 && explicitHexSurface10 != 7)
                formationTerrain = (float)P(formationPhysical == 6 ? SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent : SimulationParameterKeys.ArmyMobility.FormationTerrainPercent) / 100f;
            ushort hexIndex = K(current.ReadArmyUnitRawHex2A(receiver), "Army2A");
            var currentHex = current.ReadHexTableReference(hexIndex < 48400 ? hexIndex : 0) ?? throw Missing("Hex_table");
            byte currentSurface = K(current.ReadHexSurfaceCode(currentHex), "current_Hex10");
            float forceTerrain = (flags & 8) != 0 && currentSurface is >= 8 and <= 11 ? (float)P(SimulationParameterKeys.ArmyMobility.ForceTerrainPercent) / 100f : 1f;
            float environment = 1f;
            if (selector != 0 && K(OriginalBattleRemakeCurrentSettings.ReadInt32(state, 0x104), "Environment104") == 1)
                environment = (float)P(SimulationParameterKeys.ArmyMobility.EnvironmentTerrainPercent) / 100f;
            float value = (float)unchecked(percent * coefficient) / 100f;
            value = traitFactor * value;
            value *= 1f; //flags0: source modifierFactor is exactly1, no Army4A read.
            value *= baselineFactor; value *= formationTerrain; value *= forceTerrain; value *= environment;
            int? policy = null; int policyValue = 0;
            if (K(current.ReadForceNativeValidity(force), "Force_validity"))
            {
                byte formationFlags = K(OriginalTacticRemakeCurrentFormations.ReadParameter(state, formationPhysical, 0xA6), "FormationA6");
                policy = (formationFlags & 2) != 0 ? 59 : currentSurface is >= 8 and <= 11 ? 60 : null;
                if (policy is int effect)
                {
                    var ownedForce = state.NativeArmySimulation.Forces[forcePhysical];
                    byte[] active = ownedForce.ActivePolicyBitmap1CB is { Length: 10 } a ? a : throw Missing("Force1CB");
                    int ordinal = effect - 1, mask = 1 << (ordinal & 7);
                    if ((active[ordinal >> 3] & mask) != 0)
                    {
                        byte[] suppressed = ownedForce.SuppressedPolicyBitmap1D5 is { Length: 10 } s ? s : throw Missing("Force1D5");
                        if ((suppressed[ordinal >> 3] & mask) == 0)
                        {
                            if (!parameters.Int32Values.TryGetValue(OriginalOwnedBaseCapacityInputs.PanelKey(effect), out policyValue) ||
                                policyValue is < short.MinValue or > short.MaxValue) throw Missing($"Reform{effect}_word36");
                        }
                    }
                    value *= (float)policyValue / 100f + 1f;
                }
            }
            bool advanced = selector == 0;
            if (advanced) value = (float)K(OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(state, 14), "advanced14") * value / 100f;
            return new(value, new(value, formationPhysical, forcePhysical, commander, currentSurface, explicitHexSurface10,
                coefficient, percent, trait, traitFactor, baselineFactor, formationTerrain, forceTerrain,
                environment, policy, policyValue, advanced, policyQuery), null);

            int P(string key) => parameters.Int32Values.TryGetValue(key, out int result)
                ? result : throw Missing($"parameter_{key}");
        }
        catch (MissingOwner missing) { return new(null, null, missing.Message); }
        catch (InvalidDataException missing) when (missing.Message.StartsWith("current.PolicyScratch_", StringComparison.Ordinal))
        { return new(null, null, missing.Message); }
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new("current.transport_route_mobility." + field);
    private sealed class MissingOwner(string message) : Exception(message);
}
