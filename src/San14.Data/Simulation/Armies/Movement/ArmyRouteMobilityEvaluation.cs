using San14.Core;

namespace San14.Data;

/// <summary>Semantic motion inputs for shared 2B0660 math. EvaluateConditions
/// describes the unregistered reset-draft (1,1,null) call. Ordinary and
/// transport retain their distinct statuses; live movement has its own producer.</summary>
internal sealed record ArmyRouteMobilityInputs(int CommanderPhysicalSlot, int ForcePhysicalSlot,
    byte Status, int CurrentHex, int LandFormation, int NavalFormation, int Troops,
    bool EvaluateConditions, Func<byte?> ReadControl, int? NativeArmySlot = null);

internal static class ArmyRouteMobilityEvaluation
{
    internal static TransportArmyRouteMobilityResult Read(GameState state, SourceWorldContext current,
        ArmyRouteMobilityInputs input, int formationPhysical, byte? explicitHexSurface10,
        Func<byte?>? readRawLandFormation = null)
    {
        try
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("parameter_bank");
            var tutorial = TutorialArmyMobilitySource.Read(state, parameters,
                () => current.ReadPersonIdentity(input.CommanderPhysicalSlot),
                () => readRawLandFormation is not null ? readRawLandFormation() :
                    input.LandFormation is >= 0 and <= byte.MaxValue ? (byte)input.LandFormation : null);
            if (tutorial.Missing is { } tutorialMissing) throw Missing(tutorialMissing);
            if (tutorial.Value is > 0) return new(tutorial.Value, null, null);
            if (formationPhysical is < 1 or > 20) throw Missing("branch.nonordinary_Formation");
            int commander = input.CommanderPhysicalSlot;
            byte status = input.Status;
            var force = current.ReadForceTableReference(input.ForcePhysicalSlot) ?? throw Missing("Force_table");
            int forcePhysical = force.PhysicalSlot ?? throw Missing("Force_pointer");
            var currentHex = current.ReadHexTableReference(input.CurrentHex < 48400 ? input.CurrentHex : 0) ?? throw Missing("Hex_table");
            byte currentSurface = K(current.ReadHexSurfaceCode(currentHex), "current_Hex10");
            byte selectedSurface = explicitHexSurface10 ?? currentSurface;
            int policyFormation = formationPhysical;
            if (explicitHexSurface10 is null)
            {
                int terrain = currentSurface <= 30 ? currentSurface : 0;
                byte terrainFlags = K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrain), "current_Terrain2C");
                policyFormation = (terrainFlags & 2) != 0 ? input.NavalFormation : input.LandFormation;
            }
            byte flags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forcePhysical)?.Flags193
                ?? throw Missing("Force193");
            int size = K(TacticRemakeCurrentFormations.ReadVectorByteCount(state, formationPhysical), "Formation_A8_size");
            byte rawCoefficient = K(TacticRemakeCurrentFormations.ReadParameter(state, formationPhysical, 0x7F), "Formation7F");
            int coefficient = (size & ~3) == 32 ? Math.Clamp((int)rawCoefficient, 1, 30) : rawCoefficient;
            if ((flags & 2) != 0 && formationPhysical == 5) coefficient = unchecked(coefficient + P(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus));

            int percent = 100;
            ForcePolicyScratchReceipt? policyQuery = null;
            if (K(current.ReadForceNativeValidity(force), "Force_validity") && forcePhysical is < 46 && policyFormation is not (>= 14 and <= 16))
            {
                int effect = policyFormation <= 7 ? policyFormation : policyFormation <= 10 ? 8 : policyFormation <= 13 ? 9 : 12;
                policyQuery = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true);
                int scale = P(BattleRemakeCurrentParameterSeed.FormationEffectKey(effect)!);
                percent = unchecked(100 + unchecked(policyQuery.Value * scale));
            }
            DeploymentPreviewTraitEffects? preview = input.EvaluateConditions
                ? DeploymentPreviewTraits.Read(state, current, new(commander, input.Troops, input.LandFormation,
                    input.CurrentHex, forcePhysical, status)) : null;
            IReadOnlyList<int> traits = preview?.Effects ?? ReadUnconditionalCommander(current, commander);
            int trait = traits[94];
            if (status == 1)
            {
                // The age query has its own 26A740 call with evaluateConditions=0.
                // ETA's dynamic self-condition results cannot supply that call.
                int command = input.NativeArmySlot is int slot
                    ? ArmyCommandAgeEffectSource.ReadReceiver(state, current, slot, commander, traits)
                    : ArmyCommandAgeEffectSource.ReadResetPreview(state, current, commander,
                        commanderOnly: !input.EvaluateConditions, unconditionalEffects: traits);
                trait = unchecked(traits[4] + traits[19] + command);
                if (traits[116] != 0)
                {
                    byte control = K(input.ReadControl(), "Army140");
                    if (control != 0) trait = unchecked(trait + traits[116]);
                }
            }
            int limit = P(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit), step = P(SimulationParameterKeys.ArmyMobility.TraitMobilityStep), lower = unchecked(-limit);
            int capped = trait < lower ? lower : trait > limit ? limit : trait;
            float traitFactor = (float)unchecked(step * capped) / 100f + 1f;
            int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "EnvironmentE58");
            var baseline = current.ReadSurfaceTypeTableReference(1) ?? throw Missing("Terrain_table1");
            float baselineFactor = 1f;
            if (baseline.PhysicalSlot is int terrainPhysical && terrainPhysical != 0)
            {
                int cost = K(current.ReadSurfaceTypeRawField2A(baseline), "Terrain1_word2A");
                if (selector == 0 && (terrainPhysical is >= 3 and <= 14 or 24))
                    cost = Math.Clamp(unchecked(K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26), "advanced26") * cost) / 100, 225, 999);
                if (cost > 0) baselineFactor = 240f / (float)cost;
            }
            float formationTerrain = 1f;
            if (formationPhysical is 6 or 14 && selectedSurface is >= 5 and < 12 && selectedSurface != 7)
                formationTerrain = (float)P(formationPhysical == 6 ? SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent : SimulationParameterKeys.ArmyMobility.FormationTerrainPercent) / 100f;
            float forceTerrain = status == 2 && (flags & 8) != 0 && currentSurface is >= 8 and <= 11 ? (float)P(SimulationParameterKeys.ArmyMobility.ForceTerrainPercent) / 100f : 1f;
            float environment = 1f;
            if (status == 2 && selector != 0 && K(BattleRemakeCurrentSettings.ReadInt32(state, 0x104), "Environment104") == 1)
                environment = (float)P(SimulationParameterKeys.ArmyMobility.EnvironmentTerrainPercent) / 100f;
            float value = (float)unchecked(percent * coefficient) / 100f;
            value = traitFactor * value;
            value *= 1f; // Search flags0 and reset draft ETA byte4A both give exactly1.
            value *= baselineFactor; value *= formationTerrain; value *= forceTerrain; value *= environment;
            int? policy = null; int policyValue = 0;
            if (K(current.ReadForceNativeValidity(force), "Force_validity"))
            {
                byte formationFlags = K(TacticRemakeCurrentFormations.ReadParameter(state, formationPhysical, 0xA6), "FormationA6");
                policy = (formationFlags & 2) != 0 ? 59 : currentSurface is >= 8 and <= 11 ? 60 : null;
                if (policy is int effect)
                {
                    _ = state.NativeArmySimulation.Forces[forcePhysical];
                    ForcePolicyEffects? effects = state.Forces.GetValueOrDefault(forcePhysical)?.PolicyEffects;
                    bool active = effects?.ReadActivation(effect) ?? throw Missing("Force1CB");
                    if (active)
                    {
                        bool suppressed = effects!.ReadSuppression(effect) ?? throw Missing("Force1D5");
                        if (!suppressed)
                        {
                            if (!parameters.Int32Values.TryGetValue(OwnedBaseCapacityInputs.PanelKey(effect), out policyValue) ||
                                policyValue is < short.MinValue or > short.MaxValue) throw Missing($"Reform{effect}_word36");
                        }
                    }
                    value *= (float)policyValue / 100f + 1f;
                }
            }
            bool advanced = status == 2 && selector == 0;
            if (advanced) value = (float)K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 14), "advanced14") * value / 100f;
            return new(value, explicitHexSurface10.HasValue ? new(value, formationPhysical, forcePhysical, commander, currentSurface, explicitHexSurface10.Value,
                coefficient, percent, trait, traitFactor, baselineFactor, formationTerrain, forceTerrain,
                environment, policy, policyValue, advanced, policyQuery) : null, null);

            int P(string key) => parameters.Int32Values.TryGetValue(key, out int result)
                ? result : throw Missing($"parameter_{key}");
        }
        catch (MissingOwner missing) { return new(null, null, missing.Message); }
        catch (InvalidDataException missing) when (missing.Message.StartsWith("current.PolicyScratch_", StringComparison.Ordinal) ||
            missing.Message.StartsWith("deployment.figures.preview_traits.", StringComparison.Ordinal) ||
            missing.Message.StartsWith("army.command-age.", StringComparison.Ordinal) ||
            missing.Message.StartsWith("person.current-age.", StringComparison.Ordinal))
        { return new(null, null, missing.Message); }
    }

    private static int[] ReadUnconditionalCommander(SourceWorldContext current, int commander)
    {
        var traits = new int[127];
        for (int index = 0; index < 9; index++)
        {
            var personality = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, commander, index)
                ?? throw Missing("230730_Personality_pointer");
            if (!K(current.ReadSkillPersonalityValidity(personality), "Personality_validity")) continue;
            if (K(current.ReadPersonalityWord(personality, 0xB4), "PersonalityB4") != ushort.MaxValue) continue;
            for (int pair = 0; pair < 2; pair++)
            {
                byte effect = K(current.ReadPersonalityByte(personality, pair == 0 ? 0xB6 : 0xBA), "Personality_effect");
                short raw = unchecked((short)K(current.ReadPersonalityWord(personality, pair == 0 ? 0xB8 : 0xBC), "Personality_value"));
                if (effect < traits.Length) traits[effect] = unchecked(traits[effect] + Math.Clamp((int)raw, -100, 2000));
            }
        }
        return traits;
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new("current.army_route_mobility." + field);
    private sealed class MissingOwner(string message) : Exception(message);
}
