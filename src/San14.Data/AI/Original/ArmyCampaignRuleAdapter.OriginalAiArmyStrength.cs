using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual Army side of the shared common-attribute calculation:
    /// no opponent, divisor1, modifiers disabled. Used by the normal force-army
    /// weighted estimate; no damage descriptor or opposing Army is fabricated.</summary>
    internal float ReadOriginalAiArmyCommonCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference source)
    {
        if (!AiArmyStrengthKnown(current.ReadArmyUnitNativeValidity(source), "Army_validity")) return 0f;
        int unitId = ArmyDestructionQueue.ResolveUnitId(state, source.PhysicalSlot)
            ?? throw AiArmyStrengthMissing("Army_domain_identity");
        var unit = state.Units.GetValueOrDefault(unitId)
            ?? throw AiArmyStrengthMissing("Army_current_owner");
        int commanderId = AiArmyStrengthKnown(current.ReadArmyUnitCommanderId(source), "Army12");
        var commander = commanderId <= 6000 ? current.ReadPersonLookupReference(commanderId)
            : current.ReadTransferPersonTableReference(0);
        int person = commander?.PhysicalSlot ?? throw AiArmyStrengthMissing("Commander_pointer");
        bool personValid = AiArmyStrengthKnown(current.ReadPersonNativeValidity(person), "Commander_validity");
        int leadership = personValid
            ? ReadOriginalAiCityDutyAbility(state, current, AiArmyStrengthKnown(current.ReadPersonIdentity(person), "Commander10"), 0)
            : 0;
        var resources = current.ReadArmyReturnResourceFields(source)
            ?? throw AiArmyStrengthMissing("Army_resources");
        int troops = AiArmyStrengthKnown(resources.Ready16, "Army16");
        int morale = AiArmyStrengthKnown(resources.Morale1A, "Army1A");
        var force = current.ReadArmyUnitForceReference(source) ?? throw AiArmyStrengthMissing("Army_Force_pointer");
        _baseCapacityInputs.BindCurrent(state, current);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var rules = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain,
            _usage, Revision, abilities, new AiCrisisTacticQueries(this));
        int cap = AiArmyStrengthKnown(current.ReadForceNativeValidity(force), "Force_validity")
            ? AiArmyStrengthKnown(rules.ForceMoraleMaximum(state,
                AiArmyStrengthKnown(force.PhysicalSlot, "Force_identity")), "morale_maximum")
            : Parameter(SimulationParameterKeys.Morale.Maximum);
        morale = Math.Min(morale, cap);

        int rawHex = AiArmyStrengthKnown(current.ReadArmyUnitRawHex2A(source), "Army2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw AiArmyStrengthMissing("Hex_pointer");
        byte terrain = AiArmyStrengthKnown(current.ReadHexSurfaceCode(hex), "Hex10");
        int selected = AiArmyStrengthKnown((_terrain.Catalog.Types[terrain <= 30 ? terrain : 0].RawField2C & 2) != 0
            ? ArmyConfigurationSourceAdapter.ReadNaval(state, unit)
            : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "Army_formation");
        float terrainFactor = selected is 6 or 14 && terrain is 5 or 6 or >= 8 and <= 11
            ? (float)Parameter(selected == 6 ? SimulationParameterKeys.BattleAttributes.Formation6TerrainPercent
                : SimulationParameterKeys.BattleAttributes.Formation14TerrainPercent) / _battleSeed.PercentDenominator : 1f;
        var coefficients = AiArmyStrengthKnown(BattleRemakeCurrentSettings.ReadCommonMoraleCoefficients(state), "morale_settings");
        return BattleUnitSourceMath.CommonFactor(new(leadership, troops, 1, morale, false, false,
            coefficients.Offset, coefficients.Divisor, 1f, terrainFactor, 1f),
            new(_battleSeed.CommonPowerExponent,
                Parameter(SimulationParameterKeys.BattleAttributes.CommonRootDivisor),
                Parameter(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale),
                Parameter(SimulationParameterKeys.BattleAttributes.CommonAdditive),
                _battleSeed.FlagNumerator, _battleSeed.FlagDenominator,
                Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator),
                Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset)));

        int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } parameters &&
            parameters.Int32Values.TryGetValue(key, out int value) ? value : throw AiArmyStrengthMissing("parameter." + key);
    }

    private static T AiArmyStrengthKnown<T>(T? value, string field) where T : struct =>
        value ?? throw AiArmyStrengthMissing(field);
    private static InvalidDataException AiArmyStrengthMissing(string field) =>
        new("original_ai.army_strength." + field);
}
