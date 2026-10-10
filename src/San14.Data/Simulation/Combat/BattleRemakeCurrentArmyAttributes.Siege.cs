using San14.Core;

namespace San14.Data;

public sealed record BattleRemakeCurrentArmySiegeReceipt(
    float Common, float SiegeTroopAttack, float WallAttack,
    int ArmyId, int NativeArmySlot, int NativeForceSlot, int SelectedFormation,
    int FullLeadership, int Troops, byte ForceFlags193, int FormationQueryPercent,
    BattleRemakeCurrentFormationCoefficient SiegeCoefficientSource,
    BattleRemakeCurrentFormationCoefficient WallCoefficientSource,
    int SiegeCoefficient, int WallCoefficient, int SiegeBonus, int WallBonus,
    int CommandEffect, int SiegeTrait6, int WallTrait8, int SharedTrait19,
    sbyte SiegeModifier4C, sbyte WallModifier4E,
    BattleRemakeCurrentCommonQueries CommonQueries, string Revision);

public sealed partial class BattleRemakeCurrentArmyAttributes
{
    /// <summary>TC2B2810 siege troop attack and2AA720 wall attack for an actual
    /// ordinary type1 Army and valid commander/formation. The supplied Common
    /// receipt is after source descriptor14 resolution; this does not scan an
    /// Army-only opponent list. Wall is independent of Common/morale/region.
    /// Provocation and transport are outside this finite producer.</summary>
    public BattleRemakeCurrentArmySiegeReceipt? ResolveSiegeOffense(GameState state,
        UnitState army, BattleRemakeCurrentCommonQueries reached)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.RuleVersion != source.Source.RuleVersion || state.Units.GetValueOrDefault(army.Id) != army ||
            army.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500 || queries.ReadArmyValidity(state, army) != true ||
            army.NativeMorale1A is not byte morale ||
            army.NativeTacticRuntime is not { Status10: 1, Abnormal25: 0, StatModifiers4A.Length: 6 } native ||
            army.NativeArmyConfiguration is not { } configuration ||
            !state.Officers.ContainsKey(army.OfficerId) ||
            definitions.ResolveOfficerSource(army.OfficerId) is not int commanderPhysical ||
            definitions.ReadPersonNativeValidity(commanderPhysical) != true ||
            abilities.Resolve(state, army.OfficerId) is not { } full ||
            queries.ReadSelectedFormationPhysicalSlot(state, army) is not int formation || formation is < 1 or > 20 ||
            queries.ReadArmyForcePhysicalSlot(state, army) is not int force || force is < 1 or > 45 ||
            state.NativeArmySimulation.Forces.GetValueOrDefault(force)?.Flags193 is not byte flags ||
            string.IsNullOrWhiteSpace(reached.Revision) || army.Troops is < 0 or > ushort.MaxValue ||
            reached.FinalDescriptorDivisor14 == 0 || reached.CappedNativeMorale is < 0 or > byte.MaxValue ||
            reached.CappedNativeMorale > morale || !float.IsFinite(reached.RegionFactor) ||
            !float.IsFinite(reached.NeighborFactor)) return null;

        BattleUnitCommonParameters? commonParameters = CommonParameters(state);
        int? moraleOffset = Parameter(state, reached.AlternateMoraleGlobals ? SimulationParameterKeys.BattleAttributes.AlternateMoraleOffset : SimulationParameterKeys.BattleAttributes.MoraleOffset),
            moraleDivisor = Parameter(state, reached.AlternateMoraleGlobals ? SimulationParameterKeys.BattleAttributes.AlternateMoraleDivisor : SimulationParameterKeys.BattleAttributes.MoraleDivisor);
        float? terrainFactor = TerrainFactor(state, formation, reached.NativeTerrainCode);
        var siegeSource = queries.ReadFormationCoefficient(state, army, formation, BattleUnitArmyStatKind.SiegeTroopAttack);
        var wallSource = queries.ReadFormationCoefficient(state, army, formation, BattleUnitArmyStatKind.WallAttack);
        //22B2D0's raw selected1B/1C is also this valid1..20 formation. Thus its
        //19/20 hard100 branch and41D5D0's other valid branches match QueryPercent.
        int? percent = QueryPercent(state, army, formation);
        bool bonusActive = formation == 5 && (flags & 2) != 0;
        int? siegeBonus = bonusActive ? Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus) : 0,
            wallBonus = bonusActive ? Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityBonus) : 0;
        int? siegeLimit = Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityLimit), siegeStep = Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityStep),
            scale = Parameter(state, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale), wallLimit = Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityLimit),
            wallStep = Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityStep), leadDenominator = Parameter(state, SimulationParameterKeys.BattleAttributes.WallLeadershipDenominator),
            troopOffset = Parameter(state, SimulationParameterKeys.BattleAttributes.WallTroopOffset), troopDenominator = Parameter(state, SimulationParameterKeys.BattleAttributes.WallTroopDenominator);
        if (commonParameters is null || moraleOffset is null || moraleDivisor is not > 0 || terrainFactor is null ||
            siegeSource is null || wallSource is null || percent is null || siegeBonus is null || wallBonus is null ||
            siegeLimit is null || siegeStep is null || scale is null || wallLimit is null || wallStep is null ||
            leadDenominator is null || troopOffset is null || troopDenominator is null) return null;
        int[]? traits = TacticRemakeCurrentTraits.ArmyContributions(definitions, commanderPhysical,
            ArmyTraitSourceAdapter.Encode(state, army));
        if (traits is not { Length: 127 }) return null;
        int? command = traits[31] == 0 && traits[97] == 0 ? 0 : queries.ReadCommandEffect(state, army);
        if (command is null) return null;
        float common = BattleUnitSourceMath.CommonFactor(new(full.Leadership, army.Troops,
            reached.FinalDescriptorDivisor14, reached.CappedNativeMorale, true, native.Abnormal23 != 0,
            moraleOffset.Value, moraleDivisor.Value, reached.RegionFactor, terrainFactor.Value, reached.NeighborFactor),
            commonParameters);
        int siegeCoefficient = BattleUnitSourceMath.FormationCoefficient(BattleUnitArmyStatKind.SiegeTroopAttack,
            siegeSource.NativeContainerByteCount, siegeSource.StaticField, siegeSource.EncodedQueryResult);
        int wallCoefficient = BattleUnitSourceMath.FormationCoefficient(BattleUnitArmyStatKind.WallAttack,
            wallSource.NativeContainerByteCount, wallSource.StaticField, wallSource.EncodedQueryResult);
        float siege = BattleUnitSourceMath.ArmyStat(BattleUnitArmyStatKind.SiegeTroopAttack,
            new(common, siegeCoefficient, siegeBonus.Value, percent.Value, true, command.Value, traits[6], traits[19],
                true, native.StatModifiers4A[2], false, 0),
            new(siegeLimit.Value, siegeStep.Value, siegeStep.Value, scale.Value, source.PercentDenominator, source.One));
        //The shared pure Wall helper does not read Effects.ScalePercent: the
        //actual wall source has no shared5% scale operand.
        float wall = BattleUnitSourceMath.ArmyWallAttack(new(full.Leadership, army.Troops,
            wallCoefficient, wallBonus.Value, percent.Value, true, command.Value, traits[8], traits[19],
            true, native.StatModifiers4A[4]),
            new(troopOffset.Value, troopDenominator.Value, leadDenominator.Value,
                new(wallLimit.Value, wallStep.Value, wallStep.Value, 0, source.PercentDenominator, source.One)));
        return new(common, siege, wall, army.Id, army.NativeArmyOrigin.NativeSlotIndex.Value, force, formation,
            full.Leadership, army.Troops, flags, percent.Value, siegeSource, wallSource, siegeCoefficient, wallCoefficient,
            siegeBonus.Value, wallBonus.Value, command.Value, traits[6], traits[8], traits[19], native.StatModifiers4A[2],
            native.StatModifiers4A[4], reached, $"{Revision}:{parameters.Revision}");
    }
}
