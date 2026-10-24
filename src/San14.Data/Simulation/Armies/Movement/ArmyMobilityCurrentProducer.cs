using San14.Core;

namespace San14.Data;

public sealed record ArmyMobilityCurrentReceipt(float Value, int FormationPhysicalSlot,
    int FormationCoefficient, int ForceFormationPercent, byte ArmyStatus10, int TraitContribution, float TraitFactor,
    float ModifierFactor, float BaselineTerrainFactor, float FormationTerrainFactor,
    float ForceTerrainFactor, float EnvironmentFactor, int? AppliedPolicyIndex, int AppliedPolicyValue,
    bool AdvancedScalingApplied, string SourceProvenance);
public sealed record ArmyMobilityCurrentResolution(ArmyMobilityCurrentReceipt? Receipt, string? UnresolvedOwner)
{ public bool Complete => Receipt is not null; }

/// <summary>Ordinary TC2B0660 call as reached by
///2695D0(flags1,0,null). Uses current owned formations, Person traits, Force
///bank/bitmaps, Tutorial header, Settings and captured Terrain definitions.
/// Status1 consumes the current saved-command control for trait116; Status2
/// uses trait94. Other types, active tutorial overrides and command-age effects
/// remain explicit unclosed branches; no displayed mobility fills them.</summary>
public sealed class ArmyMobilityCurrentProducer
{
    public const string Revision = "san14:ce-ordinary-current-army-mobility-producer-20261003";
    private readonly ArmyCampaignRuleAdapter package;
    private readonly ArmyMobilityParameterSeed source;
    private readonly LoadedMap map;
    private readonly LoadedTerrainTypes terrain;
    private readonly TacticSourceCatalog catalog;
    internal ArmyMobilityCurrentProducer(ArmyCampaignRuleAdapter ownedPackage,
        ArmyMobilityParameterSeed nativeSource, LoadedMap ownedMap,
        LoadedTerrainTypes ownedTerrain, TacticSourceCatalog ownedCatalog)
    {
        package = ownedPackage; source = nativeSource; map = ownedMap;
        terrain = ownedTerrain; catalog = ownedCatalog;
    }
    public ArmyMobilityCurrentProducer(GameResourceRepository repository, ArmyCampaignRuleAdapter ownedPackage,
        LoadedMap ownedMap, ArmyMobilityParameterSeed nativeSource)
    {
        package = ownedPackage; source = nativeSource; map = ownedMap;
        if (package.Seed.Language != source.Language || package.Seed.Source.RuleVersion != source.Source.RuleVersion)
            throw new InvalidDataException("Mobility and current Army package belong to different pinned source profiles.");
        var skills = repository.LoadSkillDefinitions($"0010_{package.Seed.Language}/fixdataexce.s14");
        terrain = repository.LoadTerrainTypes($"0010_{package.Seed.Language}/fixdataexce.s14");
        catalog = TacticSourceParameters.Read(skills.Formations, skills.Source);
    }

    public ArmyMobilityCurrentResolution Resolve(GameState state, UnitState army)
    {
        var definitions = package.CaptureCurrentDefinitions(state);
        if (definitions is null) return Missing("current.Base_Hex_Person_definition_capture");
        return ResolveCurrent(state, army, definitions);
    }

    /// <summary>Borrows the caller's current operation view; the caller keeps
    /// its saved owners and current fields synchronized after reached writes.</summary>
    public ArmyMobilityCurrentResolution Resolve(GameState state, UnitState army, SourceWorldContext current)
        => ResolveCurrent(state, army, current);

    internal ArmyMobilityCurrentResolution ResolveCurrent(GameState state, UnitState army, SourceWorldContext definitions)
    {
        var context = new TacticRemakeCurrentContext(definitions, catalog, map, terrain,
            new(package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language)), package.Revision);
        if (context.ArmyValid(state, army) != true) return Missing("current.Army_validity");
        if (army.NativeTacticRuntime is not { Status10: 1 or 2, StatModifiers4A.Length: 6 } runtime)
            return Missing("Status10_ordinary_Army1_or2_branch");
        var parameters = state.NativeArmySimulation!.Parameters;
        if (parameters.Language != source.Language || parameters.RuleVersion != source.Source.RuleVersion)
            return Missing("current.native_parameter_generation");
        if (!parameters.Int32Values.TryGetValue(TacticRemakeCurrentBattleControl.VersionKey, out int tutorialVersion) || tutorialVersion != 1 ||
            !parameters.Int32Values.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int tutorialHeader))
            return Missing("current.CTutorial_header0");
        if (tutorialHeader != 0) return Missing("active_tutorial.4C6ED0_override");
        if (context.EffectiveFormation(state, army) is not int formation || formation is < 1 or > 20 ||
            TacticRemakeCurrentFormations.ReadVectorByteCount(state, formation) is not int vectorBytes ||
            TacticRemakeCurrentFormations.ReadParameter(state, formation, 0x7F) is not byte rawCoefficient ||
            TacticRemakeCurrentFormations.ReadParameter(state, formation, 0xA6) is not byte flagsA6)
            return Missing("current.CFormation_identity_A8_7F_A6");
        int coefficient = (vectorBytes & ~3) == 32 ? Math.Clamp((int)rawCoefficient, 1, 30) : rawCoefficient;
        if (state.NativeArmySimulation.Forces.GetValueOrDefault(army.ForceId)?.Flags193 is not byte forceFlags ||
            !state.Forces.TryGetValue(army.ForceId, out var force)) return Missing("current.Force193_identity");
        if ((forceFlags & 2) != 0 && formation == 5)
        {
            if (Parameter(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus) is not int bonus) return Missing("current.mobility_formation5_bonus1A4AB98");
            coefficient = unchecked(coefficient + bonus);
        }
        int? percent = ForceFormationPercent(formation);
        if (percent is null) return Missing("current.Force_formation_effect41D5D0");
        if (!state.Officers.ContainsKey(army.OfficerId) ||
            definitions.ResolveOfficerSource(army.OfficerId) is not int commanderPhysical ||
            TacticRemakeCurrentTraits.ArmyContributions(definitions, commanderPhysical,
                ArmyTraitSourceAdapter.Encode(state, army)) is not { Length: 127 } traits)
            return Missing("current.Person_and_Army_traits26A740");
        if (Parameter(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit) is not int limit || Parameter(SimulationParameterKeys.ArmyMobility.TraitMobilityStep) is not int step)
            return Missing("current.mobility_trait_limit1A49C40_step1A4A8F4");
        int trait;
        if (runtime.Status10 == 1)
        {
            trait = unchecked(traits[19] + traits[4]);
            if (traits[116] != 0)
            {
                //2B0660 reads the receiver's current saved-command control.
                //Fresh2269E0 and later replaced-command writers already own
                //this byte; an absent old-save owner remains unknown.
                if (ArmySavedCommandKnowledge.ReadControl(state, army) is not byte control)
                    return Missing("current.Army_byte140_for_trait116");
                if (control != 0) trait = unchecked(trait + traits[116]);
            }
            // Every reached2A63B0 return is zero for both source effects31/97
            // zero. This does not invent an age, World mode or flag140 value.
            if (traits[31] != 0 || traits[97] != 0) return Missing("current.command_age229FC0_and_effect2A63B0");
        }
        else trait = traits[94];
        int lower = unchecked(-limit);
        int capped = trait < lower ? lower : trait > limit ? limit : trait;
        float traitFactor = (float)unchecked(step * capped) / source.Percent + source.One;
        float modifierFactor = (float)runtime.StatModifiers4A[0] * (float)step / source.Percent + source.One;
        if (BattleRemakeCurrentSettings.ReadSelectorE58(state) is not int settingsSelector)
            return Missing("current.CEnvironmentE58");
        var baselineTerrain = definitions.ReadSurfaceTypeTableReference(1);
        if (baselineTerrain is null) return Missing("current.Terrain_table1_pointer");
        float baselineFactor = source.One;
        if (baselineTerrain.PhysicalSlot is not null)
        {
            int? baselineCost = TerrainCost(baselineTerrain, settingsSelector);
            if (baselineCost is null) return Missing("current.Terrain1_cost_BDDD0");
            if (baselineCost > 0) baselineFactor = source.BaselineTerrainNormalization / (float)baselineCost.Value;
        }
        var hex = definitions.ReadHexTableReference(army.HexId <= 48399 ? army.HexId : 0);
        if (hex?.PhysicalSlot is null || definitions.ReadHexSurfaceCode(hex) is not byte terrainCode)
            return Missing("current.Hex2A_Terrain10");
        float formationTerrain = source.One;
        if (formation is 6 or 14 && terrainCode is >= 5 and < 12 && terrainCode != 7)
        {
            if (Parameter(formation == 6 ? SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent : SimulationParameterKeys.ArmyMobility.FormationTerrainPercent) is not int terrainPercent)
                return Missing("current.formation6_or14_Terrain_percent");
            formationTerrain = (float)terrainPercent / source.Percent;
        }
        float forceTerrain = source.One;
        if (runtime.Status10 == 2 && (forceFlags & 8) != 0 && terrainCode is >= 8 and <= 11)
        {
            if (Parameter(SimulationParameterKeys.ArmyMobility.ForceTerrainPercent) is not int terrainPercent) return Missing("current.Force193_bit8_Terrain_percent1A49C58");
            forceTerrain = (float)terrainPercent / source.Percent;
        }
        float environment = source.One;
        if (settingsSelector != 0)
        {
            if (BattleRemakeCurrentSettings.ReadInt32(state, 0x104) is not int mode)
                return Missing("current.CEnvironment104");
            if (runtime.Status10 == 2 && mode == 1)
            {
                if (Parameter(SimulationParameterKeys.ArmyMobility.EnvironmentTerrainPercent) is not int environmentPercent) return Missing("current.Environment104_percent1A49C64");
                environment = (float)environmentPercent / source.Percent;
            }
        }
        float result = (float)unchecked(percent.Value * coefficient) / source.Percent;
        result = traitFactor * result;
        result *= modifierFactor; result *= baselineFactor; result *= formationTerrain; result *= forceTerrain; result *= environment;
        int? policy = (flagsA6 & 2) != 0 ? 59 : terrainCode is >= 8 and <= 11 ? 60 : null;
        int policyValue = 0;
        if (policy is int requested)
        {
            _ = state.NativeArmySimulation.Forces[army.ForceId];
            ForcePolicyEffects? effects = state.Forces.GetValueOrDefault(army.ForceId)?.PolicyEffects;
            if (effects?.ReadActivation(requested) is not bool active) return Missing("current.Force_active_policy_bitmap1CB");
            if (active)
            {
                if (effects.ReadSuppression(requested) is not bool suppressed) return Missing("current.Force_suppressed_policy_bitmap1D5");
                if (!suppressed) return Missing($"active_unsuppressed_policy{requested}.22C250_contribution");
            }
            result *= (float)policyValue / source.Percent + source.One;
        }
        bool advanced = settingsSelector == 0 && runtime.Status10 == 2;
        if (advanced)
        {
            if (BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 14) is not int advancedPercent)
                return Missing("current.CEnvironment_advanced14");
            result = (float)advancedPercent * result / source.Percent;
        }
        if (!float.IsFinite(result)) return Missing("nonfinite_current_mobility_branch");
        return new(new(result, formation, coefficient, percent.Value, runtime.Status10, trait, traitFactor, modifierFactor, baselineFactor,
            formationTerrain, forceTerrain, environment, policy, policyValue, advanced,
            $"{Revision};{source.Source.ResourceUri}@{source.Source.RuleVersion};{parameters.Revision};owned-clock={state.NativeClock!.UpdateOrdinal}"), null);

        int? Parameter(string key) => parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
        int? ForceFormationPercent(int physical)
        {
            if (army.ForceId is >= 46 and <= 51 || physical is >= 14 and <= 16) return 100;
            int effect = physical is <= 7 ? physical : physical <= 10 ? 8 : physical <= 13 ? 9 : 12;
            string? key = BattleRemakeCurrentParameterSeed.FormationEffectKey(effect);
            int? contribution = BattleRemakeCurrentForcePolicies.ReadForceEffect(state, force, effect), scale = key is null ? null : Parameter(key);
            return contribution is null || scale is null ? null : unchecked(100 + unchecked(contribution.Value * scale.Value));
        }
        int? TerrainCost(SurfaceTypeReference pointer, int selector)
        {
            if (pointer.PhysicalSlot is not int physical || definitions.ReadSurfaceTypeRawField2A(pointer) is not ushort raw) return null;
            if (selector != 0 || physical is not (>= 3 and <= 14 or 24)) return raw;
            int? advancedPercent = BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26);
            return advancedPercent is null ? null : Math.Clamp(unchecked(advancedPercent.Value * raw) / 100, 225, 999);
        }
    }
    private static ArmyMobilityCurrentResolution Missing(string owner) => new(null, owner);
}
